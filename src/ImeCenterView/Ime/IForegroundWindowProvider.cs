namespace ImeCenterView.Ime;

/// <summary>
/// フォアグラウンドウィンドウを取得する。
/// </summary>
public interface IForegroundWindowProvider
{
    /// <summary>
    /// 現在のフォアグラウンドウィンドウのハンドルを取得する。
    /// 返るハンドルは借用であり、解放は不要。取得できない場合は <see cref="IntPtr.Zero"/> を返す。
    /// </summary>
    /// <returns>フォアグラウンドウィンドウのハンドル。</returns>
    IntPtr GetForegroundWindow();
}
