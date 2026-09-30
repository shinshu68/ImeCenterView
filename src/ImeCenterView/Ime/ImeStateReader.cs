using System.Runtime.InteropServices;
using ImeCenterView.Native;

namespace ImeCenterView.Ime;

/// <summary>
/// Win32 API（<c>WM_IME_CONTROL</c> / <c>IMC_GETOPENSTATUS</c>）で IME 状態を取得する。
/// </summary>
/// <remarks>
/// 解放漏れの原因になるため <c>ImmGetContext</c> は使わない。
/// ここで扱う HWND はすべて借用であり、解放が必要なリソースは生成しない。
/// ポーリングで繰り返し呼ばれるため、処理中にヒープ確保を行わない。
/// </remarks>
public sealed class ImeStateReader : IImeStateReader
{
    /// <summary>
    /// IME ウィンドウの応答を待つ最大時間（ミリ秒）。UI スレッドから呼ばれるため短くする。
    /// </summary>
    private const uint SendMessageTimeoutMilliseconds = 50;

    private static readonly uint GuiThreadInfoSize = (uint)Marshal.SizeOf<NativeMethods.GUITHREADINFO>();

    /// <inheritdoc />
    public ImeState Read(IntPtr foregroundWindow)
    {
        if (foregroundWindow == IntPtr.Zero)
        {
            return ImeState.Unknown;
        }

        var targetWindow = GetFocusWindow(foregroundWindow);

        // 返る HWND は借用のため解放不要
        var imeWindow = NativeMethods.ImmGetDefaultIMEWnd(targetWindow);
        if (imeWindow == IntPtr.Zero)
        {
            return ImeState.Unknown;
        }

        var sent = NativeMethods.SendMessageTimeout(
            imeWindow,
            NativeMethods.WM_IME_CONTROL,
            NativeMethods.IMC_GETOPENSTATUS,
            IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG,
            SendMessageTimeoutMilliseconds,
            out var openStatus);
        if (sent == IntPtr.Zero)
        {
            // タイムアウト、応答なし、UIPI によるブロックなど
            return ImeState.Unknown;
        }

        return openStatus != IntPtr.Zero ? ImeState.On : ImeState.Off;
    }

    /// <summary>
    /// フォアグラウンドウィンドウのスレッドでフォーカスを持つウィンドウを取得する。
    /// 取得できなければフォアグラウンドウィンドウをそのまま返す。
    /// </summary>
    private static IntPtr GetFocusWindow(IntPtr foregroundWindow)
    {
        var threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
        if (threadId == 0)
        {
            return foregroundWindow;
        }

        // 構造体はスタック上に確保されるため、ヒープ確保は発生しない
        var info = new NativeMethods.GUITHREADINFO { cbSize = GuiThreadInfoSize };
        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero)
        {
            return foregroundWindow;
        }

        return info.hwndFocus;
    }
}
