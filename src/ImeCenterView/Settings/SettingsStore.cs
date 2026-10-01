using System.IO;
using System.Text.Json;

namespace ImeCenterView.Settings;

/// <summary>
/// 設定を JSON ファイル（既定では <c>%APPDATA%\ImeCenterView\settings.json</c>）に保存・読み込みする。
/// </summary>
/// <remarks>
/// ファイルは読み書きのたびに開いて閉じ、ハンドルを保持しない。
/// 読み書きの失敗は例外にせず、読み込みは既定値、保存は戻り値で呼び出し側に返す。
/// </remarks>
public sealed class SettingsStore
{
    // JsonSerializerOptions は内部にキャッシュを持つため、1 つを使い回す
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        // 手で書き換えたときの些細な違いは許容する
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _filePath;

    /// <summary>
    /// 既定の場所（<c>%APPDATA%\ImeCenterView\settings.json</c>）を使うストアを初期化する。
    /// </summary>
    public SettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeCenterView",
            "settings.json"))
    {
    }

    /// <summary>
    /// 指定したファイルを使うストアを初期化する。
    /// </summary>
    /// <param name="filePath">設定ファイルのパス。</param>
    public SettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// 設定を読み込む。
    /// </summary>
    /// <returns>
    /// 読み込んだ設定（範囲外の値は許容範囲に収め、ファイルにない項目は既定値にする）。
    /// ファイルがない・壊れている・読めない場合は既定値。
    /// </returns>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return AppSettings.Default;
            }

            var json = File.ReadAllText(_filePath);
            // 中身が null リテラルの場合は null が返る
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings?.Normalize() ?? AppSettings.Default;
        }
        catch (Exception ex) when (ex is JsonException || IsFileFailure(ex))
        {
            return AppSettings.Default;
        }
    }

    /// <summary>
    /// 設定を保存する。フォルダーがなければ作成する。
    /// </summary>
    /// <param name="settings">保存する設定。範囲外の値は許容範囲に収めてから保存する。</param>
    /// <returns>保存できたら <see langword="true"/>。</returns>
    public bool Save(AppSettings settings)
    {
        // 書き込みの途中で終了しても元のファイルが壊れないよう、一時ファイルに書いてから置き換える
        var tempPath = _filePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings.Normalize(), JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            TryDelete(tempPath);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // 一時ファイルが残っても、次の保存で上書きされるため問題ない
        }
    }

    private static bool IsFileFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException;
}
