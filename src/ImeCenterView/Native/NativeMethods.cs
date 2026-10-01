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

    /// <summary><see cref="WM_IME_CONTROL"/> で IME の入力モード（<c>IME_CMODE_*</c> の組み合わせ）を取得するコマンド。</summary>
    internal const nint IMC_GETCONVERSIONMODE = 0x0001;

    /// <summary>入力モードのフラグ。日本語（ひらがな・カタカナ）を入力するモード。立っていなければ英数。</summary>
    internal const nint IME_CMODE_NATIVE = 0x0001;

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

    /// <summary><see cref="GetWindowLong"/> / <see cref="SetWindowLong"/> で拡張ウィンドウスタイルを指定するインデックス。</summary>
    internal const int GWL_EXSTYLE = -20;

    /// <summary>マウス入力を下のウィンドウへ透過させる拡張スタイル（レイヤードウィンドウと組み合わせて使う）。</summary>
    internal const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>タスクバーや Alt+Tab に表示しないツールウィンドウにする拡張スタイル。</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>表示やクリックでアクティブにならないようにする拡張スタイル。</summary>
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// ウィンドウの属性（拡張スタイルなど）を取得する。失敗した場合は 0 を返す。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    /// <remarks>
    /// 拡張スタイルは 32 ビット値のため、64 ビット環境でも <c>GetWindowLongW</c> で扱える。
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    internal static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// ウィンドウの属性（拡張スタイルなど）を設定する。変更前の値を返し、失敗した場合は 0 を返す。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    internal static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary><see cref="MonitorFromWindow"/> などで、該当するモニターがなければプライマリモニターを返すフラグ。</summary>
    internal const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    /// <summary><see cref="MonitorFromWindow"/> で、ウィンドウがどのモニターにも重ならなければ最も近いモニターを返すフラグ。</summary>
    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    /// <summary><see cref="GetDpiForMonitor"/> で、拡大率の設定を反映した実効 DPI を取得する種別。</summary>
    internal const int MDT_EFFECTIVE_DPI = 0;

    /// <summary><see cref="SetWindowPos"/> で、ウィンドウを最前面ウィンドウの一番上に置く指定。</summary>
    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    /// <summary><see cref="SetWindowPos"/> のフラグ。ウィンドウをアクティブにしない。</summary>
    internal const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>
    /// ウィンドウが最も大きく重なっているモニターのハンドルを取得する。
    /// HMONITOR は解放の必要がない（解放する API もない）。取得できない場合は <see cref="IntPtr.Zero"/>。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    /// <summary>
    /// 指定した点を含むモニターのハンドルを取得する。
    /// HMONITOR は解放の必要がない（解放する API もない）。取得できない場合は <see cref="IntPtr.Zero"/>。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>
    /// モニターの矩形（物理ピクセル）を取得する。呼び出し前に <see cref="MONITORINFO.cbSize"/> を設定する必要がある。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// モニターの DPI を取得する。成功した場合は S_OK（0）を返す。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>
    /// ウィンドウの位置・大きさ・Z オーダーを変更する（座標は物理ピクセル）。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>
    /// ウィンドウの矩形（物理ピクセル）を取得する。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary><see cref="SHQueryUserNotificationState"/> の結果。フルスクリーンのアプリが実行中、またはプレゼンテーション設定が有効。</summary>
    internal const int QUNS_BUSY = 2;

    /// <summary><see cref="SHQueryUserNotificationState"/> の結果。Direct3D の排他フルスクリーンのアプリが実行中。</summary>
    internal const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;

    /// <summary>
    /// 通知を出してよい状態かどうか（フルスクリーンのアプリが実行中かなど）を取得する。成功した場合は S_OK（0）を返す。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("shell32.dll")]
    internal static partial int SHQueryUserNotificationState(out int pquns);

    /// <summary><see cref="GetWindowLong"/> でウィンドウスタイルを指定するインデックス。</summary>
    internal const int GWL_STYLE = -16;

    /// <summary>タイトルバーを持つウィンドウスタイル（<c>WS_BORDER | WS_DLGFRAME</c>）。</summary>
    internal const int WS_CAPTION = 0x00C00000;

    /// <summary>
    /// シェルのデスクトップウィンドウ（Progman）のハンドルを取得する。
    /// 返る HWND は借用であり、解放は不要。シェルが動いていない場合は <see cref="IntPtr.Zero"/>。
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetShellWindow();

    /// <summary>
    /// ウィンドウのクラス名を呼び出し側のバッファーへ書き込み、書き込んだ文字数を返す。失敗した場合は 0 を返す。
    /// 解放が必要なリソースは生成しない。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    internal static unsafe partial int GetClassName(IntPtr hWnd, char* lpClassName, int nMaxCount);

    /// <summary>
    /// <see cref="GetMonitorInfo"/> で取得するモニターの情報。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// 点を表す Win32 の構造体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

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
