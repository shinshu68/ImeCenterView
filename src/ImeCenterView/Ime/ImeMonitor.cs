using System.Windows.Threading;

namespace ImeCenterView.Ime;

/// <summary>
/// IME 状態を定期的にポーリングし、同じウィンドウのまま On ⇔ Off が切り替わったことを検知する。
/// </summary>
/// <remarks>
/// フォアグラウンドウィンドウが変わった周期は状態を記録するだけで通知しない（ウィンドウ切り替えだけで表示されるのを防ぐ）。
/// <see cref="ImeState.Unknown"/> が絡む変化も通知しない。
/// ポーリングは 100ms ごとに走るため、<see cref="Poll"/> の中ではヒープ確保を行わない。
/// </remarks>
public sealed class ImeMonitor : IDisposable
{
    private readonly IForegroundWindowProvider _windowProvider;
    private readonly IImeStateReader _reader;
    private readonly DispatcherTimer _timer;
    private IntPtr _lastWindow;
    private ImeState _lastState;
    private bool _hasLast;
    private bool _disposed;

    /// <summary>
    /// 同じウィンドウのまま IME が On ⇔ Off に切り替わったときに発生する。引数は切り替わった後の状態。
    /// </summary>
    public event EventHandler<ImeState>? ImeStateChanged;

    /// <summary>
    /// モニターを初期化する。<see cref="Start"/> を呼ぶまでポーリングは始まらない。
    /// </summary>
    /// <param name="windowProvider">フォアグラウンドウィンドウの取得に使うプロバイダー。</param>
    /// <param name="reader">IME 状態の取得に使うリーダー。</param>
    /// <param name="interval">ポーリング間隔。</param>
    public ImeMonitor(IForegroundWindowProvider windowProvider, IImeStateReader reader, TimeSpan interval)
    {
        _windowProvider = windowProvider;
        _reader = reader;
        // タイマーとハンドラは初期化時に 1 回だけ生成・登録する
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval,
        };
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// ポーリング間隔。動作中に変更してもよく、その場合は変更した時点から新しい間隔で数え直す。
    /// </summary>
    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    /// <summary>
    /// ポーリングを開始する。
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Start();
    }

    /// <summary>
    /// ポーリングを停止する。前回の状態は破棄し、再開後の最初の周期は記録だけ行う。
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
        _hasLast = false;
    }

    /// <summary>
    /// タイマーを停止し、タイマーとイベントの購読を解除する。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        ImeStateChanged = null;
    }

    /// <summary>
    /// 1 周期分の取得と変化判定を行う。タイマーから呼ばれるほか、テストから直接呼ぶ。
    /// </summary>
    internal void Poll()
    {
        var window = _windowProvider.GetForegroundWindow();
        var state = _reader.Read(window);

        var changed = _hasLast
            && window == _lastWindow
            && state != _lastState
            && state != ImeState.Unknown
            && _lastState != ImeState.Unknown;

        _lastWindow = window;
        _lastState = state;
        _hasLast = true;

        if (changed)
        {
            ImeStateChanged?.Invoke(this, state);
        }
    }

    private void OnTick(object? sender, EventArgs e) => Poll();
}
