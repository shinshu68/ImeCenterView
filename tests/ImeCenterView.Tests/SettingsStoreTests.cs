using ImeCenterView.Settings;

namespace ImeCenterView.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        // 実際の %APPDATA% には触れず、テストごとに別の一時フォルダーを使う
        _directory = Path.Combine(Path.GetTempPath(), "ImeCenterView.Tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "settings.json");
        _store = new SettingsStore(_filePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_filePath, content);
    }

    [Fact]
    public void ファイルがなければ既定値を返す()
    {
        Assert.Equal(AppSettings.Default, _store.Load());
    }

    [Fact]
    public void 保存した設定を読み込める()
    {
        var settings = new AppSettings
        {
            HoldDurationMs = 1500,
            FadeDurationMs = 0,
            Size = 200,
            BackgroundOpacityPercent = 60,
            PollingIntervalMs = 250,
        };

        Assert.True(_store.Save(settings));

        Assert.Equal(settings, _store.Load());
    }

    [Fact]
    public void フォルダーがなくても保存できる()
    {
        Assert.False(Directory.Exists(_directory));

        Assert.True(_store.Save(AppSettings.Default));

        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public void 上書き保存すると後の設定が残り一時ファイルは残らない()
    {
        _store.Save(AppSettings.Default with { Size = 200 });
        _store.Save(AppSettings.Default with { Size = 300 });

        Assert.Equal(300, _store.Load().Size);
        Assert.Equal([_filePath], Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"size\": ")]
    [InlineData("壊れた内容")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"size\": \"大きい\" }")]
    public void ファイルが壊れていれば既定値を返す(string content)
    {
        WriteFile(content);

        Assert.Equal(AppSettings.Default, _store.Load());
    }

    [Fact]
    public void ファイルにない項目は既定値になる()
    {
        WriteFile("{ \"size\": 200 }");

        Assert.Equal(AppSettings.Default with { Size = 200 }, _store.Load());
    }

    [Fact]
    public void 範囲外の値は許容範囲に収めて読み込む()
    {
        WriteFile("{ \"size\": 99999, \"pollingIntervalMs\": 1 }");

        var settings = _store.Load();

        Assert.Equal(AppSettings.MaxSize, settings.Size);
        Assert.Equal(AppSettings.MinPollingIntervalMs, settings.PollingIntervalMs);
    }

    [Fact]
    public void 知らない項目やコメントがあっても読み込める()
    {
        WriteFile("{ /* コメント */ \"size\": 200, \"unknown\": true, }");

        Assert.Equal(200, _store.Load().Size);
    }

    [Fact]
    public void 保存できない場所ではfalseを返す()
    {
        // フォルダーを作るべき場所に同名のファイルがあるため、フォルダーを作成できない
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "");
        var store = new SettingsStore(Path.Combine(blocker, "settings.json"));

        Assert.False(store.Save(AppSettings.Default));
    }
}
