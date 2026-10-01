using System.Windows;
using ImeCenterView.Ime;
using ImeCenterView.Overlay;
using ImeCenterView.Tray;
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

    /// <summary>二重起動防止に使う Mutex の名前。同じサインインセッション内で一意にする。</summary>
    private const string SingleInstanceMutexName = @"Local\ImeCenterView.SingleInstance";

#if DEBUG
    /// <summary>オーバーレイ表示のストレステストを実行するコマンドライン引数（Debug ビルド専用）。</summary>
    private const string StressTestArgument = "--stress";

    /// <summary>一時停止／再開のストレステストを実行するコマンドライン引数（Debug ビルド専用）。</summary>
    private const string PauseStressTestArgument = "--stress-pause";
#endif

    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private OverlayWindow? _overlay;
    private ImeMonitor? _imeMonitor;
    private TrayIcon? _trayIcon;
    private bool _paused;
#if DEBUG
    private ResourceMonitor? _resourceMonitor;
#endif

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // すでに起動していれば、何も生成せずに終了する（OnExit は呼ばれるが、破棄するものはない）
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

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

        _trayIcon = new TrayIcon();
        _trayIcon.PauseToggleRequested += OnPauseToggleRequested;
        _trayIcon.StartupToggleRequested += OnStartupToggleRequested;
        _trayIcon.ExitRequested += OnExitRequested;
        _trayIcon.SetStartupRegistered(StartupRegistration.IsRegistered());

#if DEBUG
        if (e.Args.Contains(StressTestArgument))
        {
            _ = RunStressTestAsync(new StressTest(_overlay).RunAsync());
        }
        else if (e.Args.Contains(PauseStressTestArgument))
        {
            _ = RunStressTestAsync(new StressTest(_overlay).RunPauseAsync(TogglePause));
        }
#endif
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        // タイマー停止 → イベント解除 → ウィンドウ破棄 → トレイアイコン破棄 → Mutex 解放の順に行う
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

        // 破棄しないとトレイにアイコンの残骸が残る。アイコン・メニューも TrayIcon が破棄する
        if (_trayIcon is not null)
        {
            _trayIcon.PauseToggleRequested -= OnPauseToggleRequested;
            _trayIcon.StartupToggleRequested -= OnStartupToggleRequested;
            _trayIcon.ExitRequested -= OnExitRequested;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

#if DEBUG
        _resourceMonitor?.Dispose();
        _resourceMonitor = null;
#endif

        if (_singleInstanceMutex is not null)
        {
            // 所有権は取得したスレッド（UI スレッド）からしか手放せない。OnExit は UI スレッドで呼ばれる
            if (_ownsMutex)
            {
                _singleInstanceMutex.ReleaseMutex();
                _ownsMutex = false;
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    private void OnImeStateChanged(object? sender, ImeState state)
    {
#if DEBUG
        Debug.WriteLine($"[ImeMonitor] {DateTime.Now:HH:mm:ss.fff} ImeStateChanged: {state}");
#endif
        _overlay?.Show(state);
    }

    private void OnPauseToggleRequested(object? sender, EventArgs e) => TogglePause();

    private void OnStartupToggleRequested(object? sender, EventArgs e)
    {
        if (StartupRegistration.IsRegistered())
        {
            StartupRegistration.Unregister();
        }
        else
        {
            StartupRegistration.Register();
        }

        // 登録・解除に失敗した場合でも実際の状態と食い違わないよう、読み直した結果を表示する
        _trayIcon?.SetStartupRegistered(StartupRegistration.IsRegistered());
    }

    private void OnExitRequested(object? sender, EventArgs e) => Shutdown();

    /// <summary>
    /// 一時停止と再開を切り替える。タイマーを止める／動かすだけで、オブジェクトの生成・破棄は行わない。
    /// </summary>
    private void TogglePause()
    {
        if (_imeMonitor is null)
        {
            return;
        }

        _paused = !_paused;
        if (_paused)
        {
            _imeMonitor.Stop();
        }
        else
        {
            // Stop で前回の状態を破棄しているため、再開直後の周期は記録だけで表示されない
            _imeMonitor.Start();
        }

        _trayIcon?.SetPaused(_paused);
    }

#if DEBUG
    private static async Task RunStressTestAsync(Task test)
    {
        try
        {
            await test;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StressTest] 失敗しました: {ex}");
        }
    }
#endif
}
