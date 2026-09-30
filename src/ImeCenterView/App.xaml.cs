using System.Windows;
#if DEBUG
using ImeCenterView.Diagnostics;
using ImeCenterView.Ime;
#endif

namespace ImeCenterView;

/// <summary>
/// アプリケーションのエントリポイント。メインウィンドウを持たずに常駐する。
/// </summary>
public partial class App : Application
{
#if DEBUG
    private ResourceMonitor? _resourceMonitor;
    private ImeStateLogger? _imeStateLogger;
#endif

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

#if DEBUG
        _resourceMonitor = new ResourceMonitor(TimeSpan.FromSeconds(10));
        _resourceMonitor.Start();

        _imeStateLogger = new ImeStateLogger(new ImeStateReader());
        _imeStateLogger.Start();
#endif
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
#if DEBUG
        // タイマー停止を先に行い、その後に診断ログを止める
        _imeStateLogger?.Dispose();
        _imeStateLogger = null;

        _resourceMonitor?.Dispose();
        _resourceMonitor = null;
#endif

        base.OnExit(e);
    }
}
