using ImeCenterView.Native;

namespace ImeCenterView.Ime;

/// <summary>
/// Win32 API（<c>GetForegroundWindow</c>）でフォアグラウンドウィンドウを取得する。
/// </summary>
public sealed class ForegroundWindowProvider : IForegroundWindowProvider
{
    /// <inheritdoc />
    public IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();
}
