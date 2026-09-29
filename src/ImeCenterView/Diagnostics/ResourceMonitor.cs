#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ImeCenterView.Native;

namespace ImeCenterView.Diagnostics;

/// <summary>
/// リソースリーク検証用に、プロセスのハンドル数・GDI オブジェクト数・USER オブジェクト数・メモリを
/// 定期的にログ出力する（Debug ビルド専用）。
/// 出力先は <c>Debug.WriteLine</c> と <c>%APPDATA%\ImeCenterView\logs\</c> の CSV ファイル。
/// </summary>
internal sealed class ResourceMonitor : IDisposable
{
    private const string CsvHeader = "timestamp,handles,gdi,user,private_mb,managed_mb";
    private const string LogFilePattern = "resource-*.csv";

    /// <summary>残しておくログファイルの数（今回の起動分を含む）。</summary>
    private const int MaxLogFiles = 10;

    private readonly TimeSpan _interval;
    private readonly string _logFilePath;
    private readonly Process _process;
    private readonly Timer _timer;
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// 監視を初期化する。<see cref="Start"/> を呼ぶまで計測は始まらない。
    /// </summary>
    /// <param name="interval">計測の間隔。</param>
    public ResourceMonitor(TimeSpan interval)
    {
        _interval = interval;

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeCenterView",
            "logs");
        Directory.CreateDirectory(logDirectory);
        // これから作る 1 ファイル分の枠を空けて、古いログを削除する
        DeleteOldLogFiles(logDirectory, MaxLogFiles - 1);
        _logFilePath = Path.Combine(
            logDirectory,
            $"resource-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

        // Process は計測のたびに取得せず、1 つを使い回して Refresh する
        _process = Process.GetCurrentProcess();
        // UI スレッドを止めないよう、スレッドプールで動くタイマーを使う
        _timer = new Timer(OnTick);
    }

    /// <summary>
    /// 計測を開始する。開始直後に 1 回計測し、以降は指定した間隔で計測する。
    /// </summary>
    public void Start()
    {
        WriteLine(CsvHeader);
        _timer.Change(TimeSpan.Zero, _interval);
    }

    /// <summary>
    /// 計測を停止し、タイマーとプロセス情報を破棄する。
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            _process.Dispose();
        }
    }

    private void OnTick(object? state)
    {
        // Dispose と同時に呼ばれても破棄済みのオブジェクトに触れないよう、ロック内で計測する
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _process.Refresh();

            // GetCurrentProcess の擬似ハンドルは解放不要
            var processHandle = NativeMethods.GetCurrentProcess();
            var gdi = NativeMethods.GetGuiResources(processHandle, NativeMethods.GR_GDIOBJECTS);
            var user = NativeMethods.GetGuiResources(processHandle, NativeMethods.GR_USEROBJECTS);

            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{_process.HandleCount},{gdi},{user}," +
                $"{_process.PrivateMemorySize64 / 1024.0 / 1024.0:F1},{GC.GetTotalMemory(false) / 1024.0 / 1024.0:F1}");
            WriteLine(line);
        }
    }

    private static void DeleteOldLogFiles(string logDirectory, int keepCount)
    {
        // ファイル名に起動日時が入っているため、名前の降順が新しい順になる
        var oldFiles = Directory.GetFiles(logDirectory, LogFilePattern)
            .OrderDescending(StringComparer.Ordinal)
            .Skip(keepCount);

        foreach (var file in oldFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[ResourceMonitor] 古いログの削除に失敗しました: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[ResourceMonitor] 古いログの削除に失敗しました: {ex.Message}");
            }
        }
    }

    private void WriteLine(string line)
    {
        Debug.WriteLine($"[ResourceMonitor] {line}");

        try
        {
            // ファイルを開きっぱなしにするとハンドル数に影響するため、書き込みのたびに開いて閉じる
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[ResourceMonitor] ログの書き込みに失敗しました: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[ResourceMonitor] ログの書き込みに失敗しました: {ex.Message}");
        }
    }
}
#endif
