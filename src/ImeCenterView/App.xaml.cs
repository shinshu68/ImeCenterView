using System.Windows;
using ImeCenterView.Ime;
using ImeCenterView.Overlay;
#if DEBUG
using System.Diagnostics;
using ImeCenterView.Diagnostics;
#endif

namespace ImeCenterView;

/// <summary>
/// アプリケーションのエントリポイント。メインウィンドウを持たずに常駐する。
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(100);

#if DEBUG
    /// <summary>ストレステストを実行するコマンドライン引数（Debug ビルド専用）。</summary>
    private const string StressTestArgument = "--stress";
#endif

    private OverlayWindow? _overlay;
    private ImeMonitor? _imeMonitor;
#if DEBUG
    private ResourceMonitor? _resourceMonitor;
#endif

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

#if DEBUG
        _resourceMonitor = new ResourceMonitor(TimeSpan.FromSeconds(10));
        _resourceMonitor.Start();
#endif

        // オーバーレイは起動時に 1 つだけ生成し、表示のたびに使い回す
        _overlay = new OverlayWindow();

        _imeMonitor = new ImeMonitor(new ForegroundWindowProvider(), new ImeStateReader(), PollingInterval);
        // ハンドラは起動時に 1 回だけ登録する
        _imeMonitor.ImeStateChanged += OnImeStateChanged;
        _imeMonitor.Start();

#if DEBUG
        if (e.Args.Contains(StressTestArgument))
        {
            _ = RunStressTestAsync(_overlay);
        }
#endif
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        // タイマー停止 → イベント解除 → ウィンドウ破棄の順に行う
        if (_imeMonitor is not null)
        {
            _imeMonitor.Stop();
            _imeMonitor.ImeStateChanged -= OnImeStateChanged;
            _imeMonitor.Dispose();
            _imeMonitor = null;
        }

        // Shutdown で閉じられていれば何もしない（閉じた後に Close を呼んでも例外にはならない）
        _overlay?.Close();
        _overlay = null;

#if DEBUG
        _resourceMonitor?.Dispose();
        _resourceMonitor = null;
#endif

        base.OnExit(e);
    }

    private void OnImeStateChanged(object? sender, ImeState state)
    {
#if DEBUG
        Debug.WriteLine($"[ImeMonitor] {DateTime.Now:HH:mm:ss.fff} ImeStateChanged: {state}");
#endif
        _overlay?.Show(state);
    }

#if DEBUG
    private static async Task RunStressTestAsync(OverlayWindow overlay)
    {
        try
        {
            await new StressTest(overlay).RunAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StressTest] 失敗しました: {ex}");
        }
    }
#endif
}
