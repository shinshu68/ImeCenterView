using ImeCenterView.Ime;

namespace ImeCenterView.Tests;

public sealed class ImeMonitorTests : IDisposable
{
    private static readonly IntPtr WindowA = new(0x1000);
    private static readonly IntPtr WindowB = new(0x2000);

    private readonly FakeForegroundWindowProvider _windowProvider = new();
    private readonly FakeImeStateReader _reader = new();
    private readonly ImeMonitor _monitor;
    private readonly List<ImeState> _raised = [];

    public ImeMonitorTests()
    {
        // タイマーは開始せず、Poll を直接呼んで周期を進める
        _monitor = new ImeMonitor(_windowProvider, _reader, TimeSpan.FromMilliseconds(100));
        _monitor.ImeStateChanged += (_, state) => _raised.Add(state);
    }

    public void Dispose() => _monitor.Dispose();

    private void Poll(IntPtr window, ImeState state)
    {
        _windowProvider.Window = window;
        _reader.State = state;
        _monitor.Poll();
    }

    [Fact]
    public void 同じウィンドウでOffからOnに変わるとイベントが発生する()
    {
        Poll(WindowA, ImeState.Off);
        Poll(WindowA, ImeState.On);

        Assert.Equal([ImeState.On], _raised);
    }

    [Fact]
    public void 同じウィンドウでOnからOffに変わるとイベントが発生する()
    {
        Poll(WindowA, ImeState.On);
        Poll(WindowA, ImeState.Off);

        Assert.Equal([ImeState.Off], _raised);
    }

    [Fact]
    public void 同じウィンドウで状態が変わらなければイベントは発生しない()
    {
        Poll(WindowA, ImeState.On);
        Poll(WindowA, ImeState.On);
        Poll(WindowA, ImeState.On);

        Assert.Empty(_raised);
    }

    [Fact]
    public void 初回の取得ではイベントは発生しない()
    {
        Poll(WindowA, ImeState.On);

        Assert.Empty(_raised);
    }

    [Fact]
    public void ウィンドウが変わると同時に状態も変わった場合はイベントは発生しない()
    {
        Poll(WindowA, ImeState.Off);
        Poll(WindowB, ImeState.On);

        Assert.Empty(_raised);
    }

    [Fact]
    public void ウィンドウが変わった後に同じウィンドウで状態が変わるとイベントが発生する()
    {
        Poll(WindowA, ImeState.Off);
        Poll(WindowB, ImeState.On);
        Poll(WindowB, ImeState.Off);

        Assert.Equal([ImeState.Off], _raised);
    }

    [Theory]
    [InlineData(ImeState.Unknown, ImeState.On)]
    [InlineData(ImeState.Unknown, ImeState.Off)]
    [InlineData(ImeState.On, ImeState.Unknown)]
    [InlineData(ImeState.Off, ImeState.Unknown)]
    public void Unknownが絡む変化ではイベントは発生しない(ImeState before, ImeState after)
    {
        Poll(WindowA, before);
        Poll(WindowA, after);

        Assert.Empty(_raised);
    }

    [Fact]
    public void Unknownを挟んだ後は既知の状態同士の変化でイベントが発生する()
    {
        Poll(WindowA, ImeState.Unknown);
        Poll(WindowA, ImeState.Off);
        Poll(WindowA, ImeState.On);

        Assert.Equal([ImeState.On], _raised);
    }

    [Fact]
    public void Stop後の最初の取得ではイベントは発生しない()
    {
        Poll(WindowA, ImeState.Off);
        _monitor.Stop();
        Poll(WindowA, ImeState.On);

        Assert.Empty(_raised);
    }

    [Fact]
    public void Dispose後はイベントの購読が解除される()
    {
        Poll(WindowA, ImeState.Off);
        _monitor.Dispose();
        Poll(WindowA, ImeState.On);

        Assert.Empty(_raised);
    }
}
