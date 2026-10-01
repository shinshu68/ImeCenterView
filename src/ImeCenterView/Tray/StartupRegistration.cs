using System.IO;
using System.Security;
using Microsoft.Win32;

namespace ImeCenterView.Tray;

/// <summary>
/// サインイン時の自動起動（スタートアップ）の登録・解除を行う。
/// </summary>
/// <remarks>
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> を使うため、管理者権限は不要。
/// 開いた <see cref="RegistryKey"/> は毎回 <c>using</c> で閉じる。失敗は例外にせず、戻り値で返す。
/// </remarks>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ImeCenterView";
    private const string ExeFileName = "ImeCenterView.exe";

    /// <summary>
    /// 現在の実行ファイルがスタートアップに登録されているかどうかを調べる。
    /// </summary>
    /// <returns>
    /// 登録されていれば <see langword="true"/>。
    /// 別の場所の実行ファイルが登録されている場合（登録後に移動した場合など）や、取得に失敗した場合は <see langword="false"/>。
    /// </returns>
    public static bool IsRegistered()
    {
        var command = GetCommand();
        if (command is null)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value
                && string.Equals(value, command, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// 現在の実行ファイルをスタートアップに登録する。すでに登録があれば上書きする。
    /// </summary>
    /// <returns>登録できたら <see langword="true"/>。</returns>
    public static bool Register()
    {
        var command = GetCommand();
        if (command is null)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(ValueName, command, RegistryValueKind.String);
            return true;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// スタートアップの登録を解除する。登録がなければ何もしない。
    /// </summary>
    /// <returns>解除できたら（もともと登録がなかった場合を含む）<see langword="true"/>。</returns>
    public static bool Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// スタートアップに登録するコマンド（引用符で囲んだ実行ファイルのパス）を返す。パスが分からなければ <see langword="null"/>。
    /// </summary>
    private static string? GetCommand()
    {
        var path = Environment.ProcessPath;

        // VS Code のデバッグ実行などで dotnet.exe から起動された場合は、同じフォルダーにある本体の exe を登録する
        if (path is null || string.Equals(Path.GetFileName(path), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(AppContext.BaseDirectory, ExeFileName);
            if (!File.Exists(path))
            {
                return null;
            }
        }

        return $"\"{path}\"";
    }

    private static bool IsRegistryFailure(Exception ex) =>
        ex is SecurityException or UnauthorizedAccessException or IOException;
}
