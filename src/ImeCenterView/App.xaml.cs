using System.Windows;
using ImeCenterView.Ime;
using ImeCenterView.Overlay;
using ImeCenterView.Settings;
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
    private SettingsStore? _settingsStore;
    private AppSettings _settings = AppSettings.Default;
    private bool _settingsDirty;
    private SettingsWindow? _settingsWindow;
    private bool _paused;
#if DEBUG
    private ResourceMonitor? _resourceMonitor;
#endif

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // すでに起動していれば、何も生成せずに終了する（OnExit は呼ばれるが、破棄するものはない）
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out _ownsMutex);
        }
        catch (UnauthorizedAccessException)
        {
            // 管理者として起動したものが常駐していると、その Mutex に通常の権限ではアクセスできない。
            // これも「すでに起動している」として扱う
            _ownsMutex = false;
        }

        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

#if DEBUG
        _resourceMonitor = new ResourceMonitor(TimeSpan.FromSeconds(10));
        _resourceMonitor.Start();
#endif

        // 設定ファイルがない・壊れている場合は既定値が返る
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
#if DEBUG
        var stressTest = e.Args.Contains(StressTestArgument);
        var pauseStressTest = e.Args.Contains(PauseStressTestArgument);
        if (stressTest || pauseStressTest)
        {
            // ストレステストは既定の表示時間・ポーリング間隔を前提に待ち時間を決めている
            _settings = AppSettings.Default;
        }
#endif

        // オーバーレイは起動時に 1 つだけ生成し、表示のたびに使い回す
        _overlay = new OverlayWindow(_settings);

        _imeMonitor = new ImeMonitor(
            new ForegroundWindowProvider(),
            new ImeStateReader(),
            TimeSpan.FromMilliseconds(_settings.PollingIntervalMs));
        // ハンドラは起動時に 1 回だけ登録する
        _imeMonitor.ImeStateChanged += OnImeStateChanged;
        _imeMonitor.Start();

        _trayIcon = new TrayIcon();
        _trayIcon.PauseToggleRequested += OnPauseToggleRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.StartupToggleRequested += OnStartupToggleRequested;
        _trayIcon.ExitRequested += OnExitRequested;
        _trayIcon.SetStartupRegistered(StartupRegistration.IsRegistered());

#if DEBUG
        if (stressTest)
        {
            _ = RunStressTestAsync(new StressTest(_overlay).RunAsync());
        }
        else if (pauseStressTest)
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

        // Shutdown で閉じられていれば何もしない（閉じた後に Close を呼んでも例外にはならない）。
        // 設定ウィンドウは閉じたときに OnSettingsWindowClosed で購読を解除し、設定を保存する
        _settingsWindow?.Close();
        SaveSettingsIfDirty();
        _overlay?.Close();
        _overlay = null;

        // 破棄しないとトレイにアイコンの残骸が残る。アイコン・メニューも TrayIcon が破棄する
        if (_trayIcon is not null)
        {
            _trayIcon.PauseToggleRequested -= OnPauseToggleRequested;
            _trayIcon.SettingsRequested -= OnSettingsRequested;
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
        // 切り替わったときにだけ判定する（ポーリングのたびには呼ばない）
        if (_settings.HideWhenFullScreen && FullScreenDetector.IsFullScreenAppRunning())
        {
#if DEBUG
            Debug.WriteLine("[ImeMonitor] フルスクリーンのアプリが実行中のため表示しない");
#endif
            return;
        }

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

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        // すでに開いていれば、新しく作らずに前面へ出す
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // 開くたびに生成し、閉じたとき（OnSettingsWindowClosed）に購読を解除して手放す
        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.SettingsChanged += OnSettingsChanged;
        _settingsWindow.Closed += OnSettingsWindowClosed;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (settings == _settings)
        {
            return;
        }

        // 見た目に関わる項目が変わったときだけ、確認用に表示する
        var appearanceChanged = settings.HoldDurationMs != _settings.HoldDurationMs
            || settings.FadeDurationMs != _settings.FadeDurationMs
            || settings.Size != _settings.Size
            || settings.BackgroundOpacityPercent != _settings.BackgroundOpacityPercent;

        _settings = settings;
        // スライダーを動かしている間は何度も呼ばれるため、ファイルへの保存はウィンドウを閉じるときにまとめて行う
        _settingsDirty = true;

        _overlay?.ApplySettings(settings);
        if (_imeMonitor is not null)
        {
            _imeMonitor.Interval = TimeSpan.FromMilliseconds(settings.PollingIntervalMs);
        }

        if (appearanceChanged)
        {
            _overlay?.Show(ImeState.On);
        }
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.SettingsChanged -= OnSettingsChanged;
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow = null;
        }

        SaveSettingsIfDirty();
    }

    private void SaveSettingsIfDirty()
    {
        if (!_settingsDirty || _settingsStore is null)
        {
            return;
        }

        // 保存に失敗しても動作は続ける（今回の起動中は変更後の設定で動き、次に閉じるときにもう一度保存を試みる）
        if (_settingsStore.Save(_settings))
        {
            _settingsDirty = false;
        }
    }

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
