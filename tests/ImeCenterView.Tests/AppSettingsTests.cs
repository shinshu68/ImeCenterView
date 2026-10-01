using ImeCenterView.Settings;

namespace ImeCenterView.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void 既定値は表示仕様のとおりになっている()
    {
        var settings = AppSettings.Default;

        Assert.Equal(700, settings.HoldDurationMs);
        Assert.Equal(300, settings.FadeDurationMs);
        Assert.Equal(162, settings.Size);
        Assert.Equal(85, settings.BackgroundOpacityPercent);
        Assert.Equal(100, settings.PollingIntervalMs);
        Assert.False(settings.HideWhenFullScreen);
    }

    [Fact]
    public void 既定値はNormalizeしても変わらない()
    {
        Assert.Equal(AppSettings.Default, AppSettings.Default.Normalize());
    }

    [Fact]
    public void 範囲内の値はNormalizeしても変わらない()
    {
        var settings = new AppSettings
        {
            HoldDurationMs = 1500,
            FadeDurationMs = 0,
            Size = 200,
            BackgroundOpacityPercent = 100,
            PollingIntervalMs = 50,
            HideWhenFullScreen = true,
        };

        Assert.Equal(settings, settings.Normalize());
    }

    [Fact]
    public void 小さすぎる値はNormalizeで最小値になる()
    {
        var settings = new AppSettings
        {
            HoldDurationMs = -1,
            FadeDurationMs = -1,
            Size = 0,
            BackgroundOpacityPercent = 0,
            PollingIntervalMs = 0,
        }.Normalize();

        Assert.Equal(AppSettings.MinHoldDurationMs, settings.HoldDurationMs);
        Assert.Equal(AppSettings.MinFadeDurationMs, settings.FadeDurationMs);
        Assert.Equal(AppSettings.MinSize, settings.Size);
        Assert.Equal(AppSettings.MinBackgroundOpacityPercent, settings.BackgroundOpacityPercent);
        Assert.Equal(AppSettings.MinPollingIntervalMs, settings.PollingIntervalMs);
    }

    [Fact]
    public void 大きすぎる値はNormalizeで最大値になる()
    {
        var settings = new AppSettings
        {
            HoldDurationMs = int.MaxValue,
            FadeDurationMs = int.MaxValue,
            Size = int.MaxValue,
            BackgroundOpacityPercent = int.MaxValue,
            PollingIntervalMs = int.MaxValue,
        }.Normalize();

        Assert.Equal(AppSettings.MaxHoldDurationMs, settings.HoldDurationMs);
        Assert.Equal(AppSettings.MaxFadeDurationMs, settings.FadeDurationMs);
        Assert.Equal(AppSettings.MaxSize, settings.Size);
        Assert.Equal(AppSettings.MaxBackgroundOpacityPercent, settings.BackgroundOpacityPercent);
        Assert.Equal(AppSettings.MaxPollingIntervalMs, settings.PollingIntervalMs);
    }
}
