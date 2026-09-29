using System.Runtime.InteropServices;

namespace ImeCenterView.Native;

/// <summary>
/// Win32 API の P/Invoke 定義を集約するクラス。
/// </summary>
internal static partial class NativeMethods
{
    /// <summary><see cref="GetGuiResources"/> で GDI オブジェクト数を取得するフラグ。</summary>
    internal const uint GR_GDIOBJECTS = 0;

    /// <summary><see cref="GetGuiResources"/> で USER オブジェクト数を取得するフラグ。</summary>
    internal const uint GR_USEROBJECTS = 1;

    /// <summary>
    /// 現在のプロセスの擬似ハンドルを取得する。
    /// 擬似ハンドルは実体を持たないため、CloseHandle による解放は不要。
    /// </summary>
    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetCurrentProcess();

    /// <summary>
    /// 指定したプロセスが使用している GDI / USER オブジェクトの数を取得する。
    /// 取得に失敗した場合は 0 を返す。解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial uint GetGuiResources(IntPtr hProcess, uint uiFlags);
}
