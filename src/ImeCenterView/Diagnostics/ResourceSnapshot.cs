#if DEBUG
using System.Diagnostics;
using System.Globalization;
using ImeCenterView.Native;

namespace ImeCenterView.Diagnostics;

/// <summary>
/// ある時点のプロセスのリソース使用量（Debug ビルド専用）。
/// </summary>
/// <param name="Handles">ハンドル数。</param>
/// <param name="GdiObjects">GDI オブジェクト数。</param>
/// <param name="UserObjects">USER オブジェクト数。</param>
/// <param name="PrivateBytes">プライベートメモリ（バイト）。</param>
/// <param name="ManagedBytes">マネージドヒープの使用量（バイト）。</param>
internal readonly record struct ResourceSnapshot(
    int Handles,
    uint GdiObjects,
    uint UserObjects,
    long PrivateBytes,
    long ManagedBytes)
{
    /// <summary>
    /// フル GC を行ってから、現在のリソース使用量を取得する。
    /// </summary>
    /// <returns>取得したリソース使用量。</returns>
    public static ResourceSnapshot CaptureAfterGc()
    {
        // 回収可能なオブジェクトとファイナライザー待ちのオブジェクトを片付けてから計測する
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        // GetCurrentProcess の擬似ハンドルは解放不要
        var processHandle = NativeMethods.GetCurrentProcess();
        return new ResourceSnapshot(
            process.HandleCount,
            NativeMethods.GetGuiResources(processHandle, NativeMethods.GR_GDIOBJECTS),
            NativeMethods.GetGuiResources(processHandle, NativeMethods.GR_USEROBJECTS),
            process.PrivateMemorySize64,
            GC.GetTotalMemory(false));
    }

    /// <summary>
    /// ログ出力用の文字列にする。
    /// </summary>
    /// <returns>各値を並べた文字列。</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"handles={Handles} gdi={GdiObjects} user={UserObjects} " +
        $"private={PrivateBytes / 1024.0 / 1024.0:F1}MB managed={ManagedBytes / 1024.0 / 1024.0:F1}MB");
}
#endif
