using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ImeCenterView.Ime;
using ImeCenterView.Native;

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
    /// <summary>表示領域（正方形）の一辺の長さ（DIP）。XAML の Width / Height と合わせる。</summary>
    private const double BoxSize = 162;

    /// <summary>表示領域の下端から、文字のインクの下端までの余白（DIP）。「あ」と「A」で共通。</summary>
    private const double GlyphBottomMargin = 27;

    /// <summary>フェードアウトを始めるまで、不透明のまま表示しておく時間。</summary>
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(700);

    /// <summary>フェードアウトにかける時間。</summary>
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(300);

    private readonly Geometry _onGlyph;
    private readonly Geometry _offGlyph;
    private readonly DoubleAnimation _fadeOut;
    private readonly DispatcherTimer _hideTimer;
    private bool _closed;

    /// <summary>
    /// ウィンドウを初期化し、表示せずにウィンドウハンドルだけを作成する。
    /// </summary>
    public OverlayWindow()
    {
        InitializeComponent();

        // 以前の IME の実測値に合わせた色。背景はグレー 47 を約 85% の不透明度で重ねる（白の上で 78、暗い灰色 30 の上で 44）。
        // 文字は以前の IME では少し透けていたが（白の上で 228、暗い灰色の上で 216）、背景と別の透け方を再現するのは難しいため、中間の値で不透明にする。
        // 使い回すブラシは Freeze して、変更監視のオーバーヘッドをなくす
        var background = new SolidColorBrush(Color.FromArgb(0xD8, 0x2F, 0x2F, 0x2F));
        background.Freeze();
        Backdrop.Background = background;
        var foreground = new SolidColorBrush(Color.FromRgb(0xDE, 0xDE, 0xDE));
        foreground.Freeze();
        Glyph.Fill = foreground;

        // 以前の IME の表示と字形を比較して選んだフォントとサイズ。「A」は以前の IME では Light だが、細く見えるため Regular にしている
        _onGlyph = CreateGlyph("あ", "Yu Gothic UI", 400, 126);
        _offGlyph = CreateGlyph("A", "Yu Gothic", 400, 137);

        // 表示直後は不透明のまま保持し、HoldDuration 経過後に FadeDuration かけて消す
        _fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(FadeDuration))
        {
            BeginTime = HoldDuration,
            FillBehavior = FillBehavior.HoldEnd,
        };
        _fadeOut.Freeze();

        // 非表示にするタイミングはアニメーションの Completed ではなくタイマーで決める。
        // 表示中に再表示したとき、置き換えられた古いアニメーションの完了通知と区別する必要がなくなる
        _hideTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = HoldDuration + FadeDuration,
        };
        _hideTimer.Tick += OnHideTimerTick;

        // 最初の表示より前に拡張スタイルを付与しておくため、ここでハンドルを作成する（OnSourceInitialized が呼ばれる）
        new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>
    /// IME の状態に応じて「あ」または「A」をプライマリモニターの中央に表示する。
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

        MoveToPrimaryScreenCenter();

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
            (BoxSize - formatted.WidthIncludingTrailingWhitespace) / 2,
            BoxSize - GlyphBottomMargin - ink.Bottom);

        var geometry = formatted.BuildGeometry(origin);
        geometry.Freeze();
        return geometry;
    }

    private void OnHideTimerTick(object? sender, EventArgs e) => HideNow();

    /// <summary>
    /// プライマリモニター全体（タスクバーを含む）の中央へ移動する。以前の IME もモニター全体の中央に表示していた。
    /// </summary>
    /// <remarks>
    /// マルチモニター・DPI の混在への対応はフェーズ 4 で物理ピクセル基準の配置に置き換える。
    /// </remarks>
    private void MoveToPrimaryScreenCenter()
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
    }
}
