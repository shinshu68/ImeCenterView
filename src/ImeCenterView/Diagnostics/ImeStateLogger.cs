#if DEBUG
using System.Diagnostics;
using System.Windows.Threading;
using ImeCenterView.Ime;
using ImeCenterView.Native;

namespace ImeCenterView.Diagnostics;

/// <summary>
/// IME 状態の取得を確認するための仮のロガー（Debug ビルド専用）。
/// 100ms ごとに IME 状態を取得し、フォアグラウンドウィンドウか状態が変わったときだけ <c>Debug.WriteLine</c> に出力する。
/// </summary>
/// <remarks>
/// フェーズ 2 で <c>ImeMonitor</c> に置き換える。
/// </remarks>
internal sealed class ImeStateLogger : IDisposable
{
    private readonly IImeStateReader _reader;
    private readonly DispatcherTimer _timer;
    private IntPtr _lastWindow;
    private ImeState _lastState;
    private bool _hasLast;
    private bool _disposed;

    /// <summary>
    /// ロガーを初期化する。<see cref="Start"/> を呼ぶまで取得は始まらない。
    /// </summary>
    /// <param name="reader">IME 状態の取得に使うリーダー。</param>
    public ImeStateLogger(IImeStateReader reader)
    {
        _reader = reader;
        // タイマーとハンドラは初期化時に 1 回だけ生成・登録する
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// 取得を開始する。
    /// </summary>
    public void Start() => _timer.Start();

    /// <summary>
    /// タイマーを停止し、イベントの購読を解除する。
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
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var window = NativeMethods.GetForegroundWindow();
        var state = _reader.Read(window);

        // 変化がなければ何もしない（毎周期の文字列生成を避ける）
        if (_hasLast && window == _lastWindow && state == _lastState)
        {
            return;
        }

        var reason = !_hasLast ? "初回" : window != _lastWindow ? "ウィンドウ変更" : "状態変更";
        Debug.WriteLine($"[ImeStateLogger] {DateTime.Now:HH:mm:ss.fff} hwnd=0x{window:X8} state={state} ({reason})");

        _lastWindow = window;
        _lastState = state;
        _hasLast = true;
    }
}
#endif
