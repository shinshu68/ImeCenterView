using System.Runtime.InteropServices;
using ImeCenterView.Native;

namespace ImeCenterView.Overlay;

/// <summary>
/// フルスクリーンのアプリが実行中かどうかを判定する。
/// </summary>
/// <remarks>
/// アクティブウィンドウが、タイトルバーなしでモニター全体を覆っていればフルスクリーンとみなす
/// （ゲーム、ブラウザーや動画の全画面表示など）。
/// Windows による判定（<c>SHQueryUserNotificationState</c>）はブラウザーの全画面表示などを拾わないため、
/// Direct3D の排他フルスクリーンなどを補う目的で併用する。
/// </remarks>
public static class FullScreenDetector
{
    /// <summary>クラス名の取得に使うバッファーの長さ。Win32 のクラス名は最大 256 文字。</summary>
    private const int ClassNameBufferLength = 257;

    /// <summary>
    /// フルスクリーンのアプリが実行中かどうかを取得する。
    /// </summary>
    /// <returns>実行中なら <see langword="true"/>。取得に失敗した場合は <see langword="false"/>。</returns>
    public static bool IsFullScreenAppRunning()
    {
        if (NativeMethods.SHQueryUserNotificationState(out var state) == 0
            && state is NativeMethods.QUNS_BUSY or NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN)
        {
            return true;
        }

        return IsForegroundWindowFullScreen();
    }

    private static bool IsForegroundWindowFullScreen()
    {
        // HWND・HMONITOR はどちらも借用であり、解放は不要
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero || IsDesktop(window))
        {
            return false;
        }

        // 最大化したウィンドウも、タスクバーを自動的に隠す設定ではモニター全体を覆う。
        // 全画面表示ではタイトルバーのスタイルを外すのが通例のため、これで区別する
        var style = NativeMethods.GetWindowLong(window, NativeMethods.GWL_STYLE);
        if ((style & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(window, out var rect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(window, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var screen = info.rcMonitor;
        return rect.left <= screen.left
            && rect.top <= screen.top
            && rect.right >= screen.right
            && rect.bottom >= screen.bottom;
    }

    /// <summary>
    /// デスクトップ（壁紙とアイコンのウィンドウ）かどうかを判定する。モニター全体を覆うが、フルスクリーンのアプリではない。
    /// </summary>
    private static unsafe bool IsDesktop(IntPtr window)
    {
        if (window == NativeMethods.GetShellWindow())
        {
            return true;
        }

        // 壁紙の構成によっては、デスクトップのアイコンが Progman ではなく WorkerW の下に置かれる。
        // バッファーはスタックに確保するため、ヒープは使わない
        var buffer = stackalloc char[ClassNameBufferLength];
        var length = NativeMethods.GetClassName(window, buffer, ClassNameBufferLength);
        return new ReadOnlySpan<char>(buffer, length).SequenceEqual("WorkerW");
    }
}
