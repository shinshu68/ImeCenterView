using System.Runtime.InteropServices;

namespace ImeCenterView.Native;

/// <summary>
/// Win32 API の P/Invoke 定義を集約するクラス。
/// </summary>
internal static partial class NativeMethods
{
    /// <summary><see cref="GetGuiResources"/> で GDI オブジェクト数を取得するフラグ。</summary>
    internal const uint GR_GDIOBJECTS = 0;

    /// <summary><see cref="GetGuiResources"/> で USER オブジェクト数を取得するフラグ。</summary>
    internal const uint GR_USEROBJECTS = 1;

    /// <summary>
    /// 現在のプロセスの擬似ハンドルを取得する。
    /// 擬似ハンドルは実体を持たないため、CloseHandle による解放は不要。
    /// </summary>
    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetCurrentProcess();

    /// <summary>
    /// 指定したプロセスが使用している GDI / USER オブジェクトの数を取得する。
    /// 取得に失敗した場合は 0 を返す。解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial uint GetGuiResources(IntPtr hProcess, uint uiFlags);

    /// <summary>IME ウィンドウへ IME の制御を要求するメッセージ。</summary>
    internal const uint WM_IME_CONTROL = 0x0283;

    /// <summary><see cref="WM_IME_CONTROL"/> で IME のオン／オフ状態を取得するコマンド。</summary>
    internal const nint IMC_GETOPENSTATUS = 0x0005;

    /// <summary>
    /// <see cref="SendMessageTimeout"/> のフラグ。相手のスレッドが応答なし（ハング）と判定されていれば待たずに戻る。
    /// </summary>
    internal const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>
    /// フォアグラウンドウィンドウのハンドルを取得する。
    /// 返る HWND は借用であり、解放は不要（解放してはいけない）。取得できない場合は <see cref="IntPtr.Zero"/>。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    /// <summary>
    /// ウィンドウを作成したスレッドの ID を取得する。失敗した場合は 0 を返す。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 指定したスレッドの GUI 情報（フォーカスを持つウィンドウなど）を取得する。
    /// 呼び出し前に <see cref="GUITHREADINFO.cbSize"/> を設定する必要がある。
    /// 結果に含まれる HWND はすべて借用であり、解放は不要。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO pgui);

    /// <summary>
    /// 指定したウィンドウに対応する既定の IME ウィンドウのハンドルを取得する。
    /// 返る HWND は借用であり、解放は不要（解放してはいけない）。取得できない場合は <see cref="IntPtr.Zero"/>。
    /// </summary>
    [LibraryImport("imm32.dll")]
    internal static partial IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    /// <summary>
    /// タイムアウト付きでウィンドウにメッセージを送る。
    /// 失敗またはタイムアウトした場合は 0 を返す。解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static partial IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    /// <summary>
    /// <see cref="GetGUIThreadInfo"/> で取得するスレッドの GUI 情報。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    /// <summary>
    /// 矩形を表す Win32 の構造体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
