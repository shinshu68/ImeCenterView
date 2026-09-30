#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using ImeCenterView.Ime;
using ImeCenterView.Overlay;

namespace ImeCenterView.Diagnostics;

/// <summary>
/// オーバーレイを連続で表示し、前後のリソース使用量を比較してリークを検証する（Debug ビルド専用）。
/// 結果は <c>Debug.WriteLine</c> と <c>%APPDATA%\ImeCenterView\logs\stress-*.log</c> に出力する。
/// </summary>
internal sealed class StressTest
{
    /// <summary>高速に表示を繰り返す回数。</summary>
    private const int FastIterations = 10_000;

    /// <summary>表示時間の経過とフェードアウトを待って、自然に消える流れを繰り返す回数。</summary>
    private const int NaturalIterations = 10;

    /// <summary>自然に消えるのを待つ時間（表示 700ms + フェード 300ms に余裕を持たせる）。</summary>
    private static readonly TimeSpan NaturalWait = TimeSpan.FromMilliseconds(1200);

    private readonly OverlayWindow _overlay;
    private readonly string _logFilePath;

    /// <summary>
    /// ストレステストを初期化する。
    /// </summary>
    /// <param name="overlay">検証するオーバーレイウィンドウ。</param>
    public StressTest(OverlayWindow overlay)
    {
        _overlay = overlay;

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeCenterView",
            "logs");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"stress-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    /// <summary>
    /// ストレステストを実行する。UI スレッドから呼び出し、表示のたびにディスパッチャーへ制御を返す。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public async Task RunAsync()
    {
        // 初回表示で生成されるもの（ウィンドウの描画リソースなど）を計測から除くため、先に 1 回表示しておく
        _overlay.Show(ImeState.On);
        await Task.Delay(NaturalWait);

        var before = ResourceSnapshot.CaptureAfterGc();
        Log($"開始 before: {before}");
        var stopwatch = Stopwatch.StartNew();

        // 表示中の差し替え（あ ⇔ A）と、非表示からの再表示を交互に繰り返す
        for (var i = 0; i < FastIterations; i++)
        {
            _overlay.Show(i % 2 == 0 ? ImeState.On : ImeState.Off);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            if (i % 2 == 1)
            {
                _overlay.HideNow();
            }

            if ((i + 1) % 1000 == 0)
            {
                Log($"高速表示 {i + 1}/{FastIterations}");
            }
        }

        // タイマーとフェードアウトによって自然に消える流れも確認する
        for (var i = 0; i < NaturalIterations; i++)
        {
            _overlay.Show(i % 2 == 0 ? ImeState.On : ImeState.Off);
            await Task.Delay(NaturalWait);
        }

        stopwatch.Stop();
        var after = ResourceSnapshot.CaptureAfterGc();
        Log($"終了 after:  {after}");
        Log(string.Create(
            CultureInfo.InvariantCulture,
            $"差分 handles={after.Handles - before.Handles:+#;-#;0} " +
            $"gdi={(long)after.GdiObjects - before.GdiObjects:+#;-#;0} " +
            $"user={(long)after.UserObjects - before.UserObjects:+#;-#;0} " +
            $"private={(after.PrivateBytes - before.PrivateBytes) / 1024.0 / 1024.0:+0.0;-0.0;0.0}MB " +
            $"managed={(after.ManagedBytes - before.ManagedBytes) / 1024.0 / 1024.0:+0.0;-0.0;0.0}MB " +
            $"（{stopwatch.Elapsed.TotalSeconds:F1} 秒）"));
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        Debug.WriteLine($"[StressTest] {line}");

        try
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[StressTest] ログの書き込みに失敗しました: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[StressTest] ログの書き込みに失敗しました: {ex.Message}");
        }
    }
}
#endif
