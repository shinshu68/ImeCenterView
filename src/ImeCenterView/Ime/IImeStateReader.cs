namespace ImeCenterView.Ime;

/// <summary>
/// ウィンドウの IME 状態を取得する。
/// </summary>
public interface IImeStateReader
{
    /// <summary>
    /// 指定したフォアグラウンドウィンドウの IME 状態を取得する。
    /// 取得に失敗した場合は例外を投げず、<see cref="ImeState.Unknown"/> を返す。
    /// </summary>
    /// <param name="foregroundWindow">フォアグラウンドウィンドウのハンドル。</param>
    /// <returns>IME の状態。</returns>
    ImeState Read(IntPtr foregroundWindow);
}
