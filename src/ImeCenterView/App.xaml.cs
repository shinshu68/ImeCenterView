using System.Windows;
#if DEBUG
using ImeCenterView.Diagnostics;
#endif

namespace ImeCenterView;

/// <summary>
/// アプリケーションのエントリポイント。メインウィンドウを持たずに常駐する。
/// </summary>
public partial class App : Application
{
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
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
#if DEBUG
        _resourceMonitor?.Dispose();
        _resourceMonitor = null;
#endif

        base.OnExit(e);
    }
}
