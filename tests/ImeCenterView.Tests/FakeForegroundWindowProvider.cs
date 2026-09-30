using ImeCenterView.Ime;

namespace ImeCenterView.Tests;

/// <summary>
/// テスト用に、設定したハンドルをそのまま返す <see cref="IForegroundWindowProvider"/>。
/// </summary>
internal sealed class FakeForegroundWindowProvider : IForegroundWindowProvider
{
    /// <summary>
    /// <see cref="GetForegroundWindow"/> が返すハンドル。
    /// </summary>
    public IntPtr Window { get; set; }

    /// <inheritdoc />
    public IntPtr GetForegroundWindow() => Window;
}
