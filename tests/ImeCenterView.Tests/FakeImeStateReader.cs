using ImeCenterView.Ime;

namespace ImeCenterView.Tests;

/// <summary>
/// テスト用に、設定した状態をそのまま返す <see cref="IImeStateReader"/>。
/// </summary>
internal sealed class FakeImeStateReader : IImeStateReader
{
    /// <summary>
    /// <see cref="Read"/> が返す状態。
    /// </summary>
    public ImeState State { get; set; }

    /// <inheritdoc />
    public ImeState Read(IntPtr foregroundWindow) => State;
}
