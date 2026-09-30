using System.Windows;
using ImeCenterView.Ime;
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

        _imeMonitor = new ImeMonitor(new ForegroundWindowProvider(), new ImeStateReader(), PollingInterval);
        // ハンドラは起動時に 1 回だけ登録する
        _imeMonitor.ImeStateChanged += OnImeStateChanged;
        _imeMonitor.Start();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        // タイマー停止 → イベント解除の順に行う
        if (_imeMonitor is not null)
        {
            _imeMonitor.Stop();
            _imeMonitor.ImeStateChanged -= OnImeStateChanged;
            _imeMonitor.Dispose();
            _imeMonitor = null;
        }

#if DEBUG
        _resourceMonitor?.Dispose();
        _resourceMonitor = null;
#endif

        base.OnExit(e);
    }

    private void OnImeStateChanged(object? sender, ImeState state)
    {
#if DEBUG
        // 表示処理はフェーズ 3 で接続する。それまでは確認用にデバッグ出力する
        Debug.WriteLine($"[ImeMonitor] {DateTime.Now:HH:mm:ss.fff} ImeStateChanged: {state}");
#endif
    }
}
