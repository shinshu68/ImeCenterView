#if DEBUG
using System.Diagnostics;
#endif
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ImeCenterView.Ime;
using ImeCenterView.Native;
using ImeCenterView.Settings;

namespace ImeCenterView.Overlay;

/// <summary>
/// IME の切り替え時に画面中央へ「あ」／「A」を一瞬表示するウィンドウ。
/// </summary>
/// <remarks>
/// フォーカスを奪わず、クリックも透過する。
/// ウィンドウ・アニメーション・タイマー・ブラシ・字形は起動時に 1 回だけ生成し、表示のたびに使い回す。
/// 見た目は以前の Microsoft IME の表示（100% 表示で 162px 四方）を実測して合わせている。
/// </remarks>
public partial class OverlayWindow : Window
{
    /// <summary>字形を作るときの基準にする表示領域の一辺の長さ（DIP）。XAML の Canvas の Width / Height と合わせる。</summary>
    private const double BaseBoxSize = 162;

    /// <summary>表示領域の下端から、文字のインクの下端までの余白（DIP、基準の大きさのとき）。「あ」と「A」で共通。</summary>
    private const double GlyphBottomMargin = 27;

    private readonly Geometry _onGlyph;
    private readonly Geometry _offGlyph;
    private readonly DispatcherTimer _hideTimer;

    // 以下は設定から決まる値。表示のたびではなく、設定が変わったときだけ ApplySettings で作り直す
    private AppSettings? _settings;
    private DoubleAnimation _fadeOut = null!;
    private double _boxSize;
    private bool _closed;

    /// <summary>
    /// ウィンドウを初期化し、表示せずにウィンドウハンドルだけを作成する。
    /// </summary>
    /// <param name="settings">表示に使う設定。</param>
    public OverlayWindow(AppSettings settings)
    {
        InitializeComponent();

        // 以前の IME の実測値に合わせた色。背景はグレー 47 を、既定では約 85% の不透明度で重ねる（ApplySettings で設定する）。
        // 文字は以前の IME では少し透けていたが（白の上で 228、暗い灰色の上で 216）、背景と別の透け方を再現するのは難しいため、中間の値で不透明にする。
        // 使い回すブラシは Freeze して、変更監視のオーバーヘッドをなくす
        var foreground = new SolidColorBrush(Color.FromRgb(0xDE, 0xDE, 0xDE));
        foreground.Freeze();
        Glyph.Fill = foreground;

        // 以前の IME の表示と字形を比較して選んだフォントとサイズ。「A」は以前の IME では Light だが、細く見えるため Regular にしている
        _onGlyph = CreateGlyph("あ", "Yu Gothic UI", 400, 126);
        _offGlyph = CreateGlyph("A", "Yu Gothic", 400, 137);

        // 非表示にするタイミングはアニメーションの Completed ではなくタイマーで決める。
        // 表示中に再表示したとき、置き換えられた古いアニメーションの完了通知と区別する必要がなくなる
        _hideTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _hideTimer.Tick += OnHideTimerTick;

        ApplySettings(settings);

        // 最初の表示より前に拡張スタイルを付与しておくため、ここでハンドルを作成する（OnSourceInitialized が呼ばれる）
        new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>
    /// 設定（表示時間・フェード時間・大きさ・背景の不透明度）を反映する。次の表示から有効になる。
    /// 表示中に呼ばれた場合、表示中のものは消さず、古い設定のまま消えていく。
    /// </summary>
    /// <remarks>
    /// ブラシとアニメーションはここで作り直して Freeze し、表示のたびには生成しない。
    /// </remarks>
    /// <param name="settings">反映する設定。範囲外の値は許容範囲に収めて使う。</param>
    public void ApplySettings(AppSettings settings)
    {
        settings = settings.Normalize();
        if (_closed || settings == _settings)
        {
            return;
        }

        _settings = settings;

        var alpha = (byte)Math.Round(255 * settings.BackgroundOpacityPercent / 100.0);
        var background = new SolidColorBrush(Color.FromArgb(alpha, 0x2F, 0x2F, 0x2F));
        background.Freeze();
        Backdrop.Background = background;

        var hold = TimeSpan.FromMilliseconds(settings.HoldDurationMs);
        var fade = TimeSpan.FromMilliseconds(settings.FadeDurationMs);

        // 表示直後は不透明のまま保持し、hold 経過後に fade かけて消す
        var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(fade))
        {
            BeginTime = hold,
            FillBehavior = FillBehavior.HoldEnd,
        };
        fadeOut.Freeze();
        _fadeOut = fadeOut;
        _hideTimer.Interval = hold + fade;

        // 実際の位置と大きさは、表示のたびにモニターの DPI に合わせて SetWindowPos で設定する
        _boxSize = settings.Size;
        Width = _boxSize;
        Height = _boxSize;
    }

    /// <summary>
    /// IME の状態に応じて「あ」または「A」を、アクティブウィンドウがあるモニターの中央に表示する。
    /// 表示中に呼ばれた場合は文字を差し替え、表示時間とフェードアウトを最初からやり直す。
    /// </summary>
    /// <param name="state">表示する IME の状態。<see cref="ImeState.Unknown"/> の場合は何もしない。</param>
    public void Show(ImeState state)
    {
        if (_closed || state == ImeState.Unknown)
        {
            return;
        }

        Glyph.Data = state == ImeState.On ? _onGlyph : _offGlyph;

        _hideTimer.Stop();
        // 途中のアニメーションを外して不透明に戻してから、改めて開始する
        Backdrop.BeginAnimation(OpacityProperty, null);
        Backdrop.BeginAnimation(OpacityProperty, _fadeOut);

        MoveToActiveMonitorCenter();

        if (!IsVisible)
        {
            // ShowActivated="False" のため、アクティブにならずに表示される
            Show();
        }

        _hideTimer.Start();
    }

    /// <summary>
    /// 表示を打ち切って非表示にする。表示時間の経過を待たずに非表示にしたいとき（ストレステストなど）に使う。
    /// </summary>
    internal void HideNow()
    {
        _hideTimer.Stop();
        Hide();
        // 非表示にしてからアニメーションを外す（先に外すと一瞬不透明に戻って見えるため）
        Backdrop.BeginAnimation(OpacityProperty, null);
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // フォーカスを奪わない・クリックを透過する・タスクバーや Alt+Tab に出さない。
        // ここで扱う HWND は WPF が所有しているため、解放は不要
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _hideTimer.Stop();
        _hideTimer.Tick -= OnHideTimerTick;
        Backdrop.BeginAnimation(OpacityProperty, null);

        base.OnClosed(e);
    }

    /// <summary>
    /// 文字を、表示領域内の決まった位置に置いた字形（Freeze 済み）に変換する。
    /// </summary>
    /// <remarks>
    /// 横は文字送り幅で中央に置き、縦はインクの下端を <see cref="GlyphBottomMargin"/> の位置にそろえる。
    /// フォントごとの行の高さの違いに左右されないよう、テキストではなく字形として描画する。
    /// </remarks>
    private static Geometry CreateGlyph(string text, string fontFamily, int fontWeight, double fontSize)
    {
        var typeface = new Typeface(
            new FontFamily(fontFamily),
            FontStyles.Normal,
            FontWeight.FromOpenTypeWeight(fontWeight),
            FontStretches.Normal);
        // 字形はベクターのため、pixelsPerDip は結果に影響しない
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("ja-JP"),
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White,
            1.0);

        var ink = formatted.BuildGeometry(new Point(0, 0)).Bounds;
        var origin = new Point(
            (BaseBoxSize - formatted.WidthIncludingTrailingWhitespace) / 2,
            BaseBoxSize - GlyphBottomMargin - ink.Bottom);

        var geometry = formatted.BuildGeometry(origin);
        geometry.Freeze();
        return geometry;
    }

    private void OnHideTimerTick(object? sender, EventArgs e) => HideNow();

    /// <summary>
    /// アクティブウィンドウがあるモニター全体（タスクバーを含む）の中央へ移動し、最前面に置く。
    /// 以前の IME もモニター全体の中央に表示していた。
    /// </summary>
    /// <remarks>
    /// DPI の異なるモニターが混在していても正しく置けるよう、位置と大きさは物理ピクセルで計算して <c>SetWindowPos</c> で設定する。
    /// 大きさはモニターの DPI に合わせて、設定の大きさ（DIP）を拡大縮小した値にする（中身の拡大縮小は WPF が DPI の変化を受けて行う）。
    /// </remarks>
    private void MoveToActiveMonitorCenter()
    {
        // HWND・HMONITOR はどちらも借用であり、解放は不要
        var foreground = NativeMethods.GetForegroundWindow();
        var monitor = foreground != IntPtr.Zero
            ? NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;
        if (monitor == IntPtr.Zero)
        {
            monitor = NativeMethods.MonitorFromPoint(default, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        }

        var info = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            // モニターの情報が取れなければ、前回の位置のまま表示する
            return;
        }

        if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpi, out _) != 0 || dpi == 0)
        {
            dpi = 96;
        }

        var size = (int)Math.Round(_boxSize * dpi / 96);
        var rect = info.rcMonitor;
        var x = rect.left + (rect.right - rect.left - size) / 2;
        var y = rect.top + (rect.bottom - rect.top - size) / 2;

        var hwnd = new WindowInteropHelper(this).Handle;
        // DPI の異なるモニターへ移ると、WPF が WM_DPICHANGED を受けて推奨された位置へ動かすことがある。
        // その場合は 2 回目の呼び出しで（移動後のモニター上なので DPI の変化なしに）目的の位置へ置き直す
        for (var attempt = 0; attempt < 2; attempt++)
        {
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, size, size, NativeMethods.SWP_NOACTIVATE);
            if (NativeMethods.GetWindowRect(hwnd, out var actual)
                && actual.left == x && actual.top == y && actual.right == x + size && actual.bottom == y + size)
            {
                break;
            }
        }

#if DEBUG
        NativeMethods.GetWindowRect(hwnd, out var placed);
        Debug.WriteLine(
            $"[Overlay] モニター ({rect.left},{rect.top})-({rect.right},{rect.bottom}) DPI {dpi} / "
            + $"配置 ({placed.left},{placed.top}) {placed.right - placed.left}x{placed.bottom - placed.top} / "
            + $"WPF の DPI {VisualTreeHelper.GetDpi(this).PixelsPerInchX}");
#endif
    }
}
