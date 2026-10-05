namespace ImeCenterView.Ime;

/// <summary>
/// IME のオン／オフ状態。
/// </summary>
public enum ImeState
{
    /// <summary>状態を取得できなかった（IME ウィンドウがない、権限不足、応答なしなど）。</summary>
    Unknown,

    /// <summary>IME がオフ（直接入力）。</summary>
    Off,

    /// <summary>IME がオン。</summary>
    On,
}
