using System.Windows;
using System.Windows.Controls;

namespace ImeCenterView.Settings;

/// <summary>
/// 表示時間・大きさなどを変更する設定ウィンドウ。
/// </summary>
/// <remarks>
/// 値を変えるたびに <see cref="SettingsChanged"/> で通知し、反映と保存は所有者（<c>App</c>）が行う。
/// 開くたびに生成し、閉じたら破棄する（常駐中に持ち続けない）。所有者は閉じたときにイベントの購読を解除する。
/// </remarks>
public partial class SettingsWindow : Window
{
    /// <summary>画面の端からウィンドウまでの余白（DIP）。</summary>
    private const double ScreenMargin = 16;

    // スライダーに値を設定している間は true。InitializeComponent 中に通知しないよう、最初から true にしておく
    private bool _updating = true;

    /// <summary>
    /// 設定が変更されたときに発生する。引数は変更後の設定。
    /// </summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    /// <summary>
    /// 設定ウィンドウを初期化する。
    /// </summary>
    /// <param name="settings">最初に表示する設定。</param>
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        HoldSlider.Minimum = AppSettings.MinHoldDurationMs;
        HoldSlider.Maximum = AppSettings.MaxHoldDurationMs;
        FadeSlider.Minimum = AppSettings.MinFadeDurationMs;
        FadeSlider.Maximum = AppSettings.MaxFadeDurationMs;
        SizeSlider.Minimum = AppSettings.MinSize;
        SizeSlider.Maximum = AppSettings.MaxSize;
        OpacitySlider.Minimum = AppSettings.MinBackgroundOpacityPercent;
        OpacitySlider.Maximum = AppSettings.MaxBackgroundOpacityPercent;
        PollingSlider.Minimum = AppSettings.MinPollingIntervalMs;
        PollingSlider.Maximum = AppSettings.MaxPollingIntervalMs;

        SetValues(settings.Normalize());
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 画面中央にはプレビューのオーバーレイが出るため、重ならないよう作業領域の右端に寄せる。
        // 右下はトレイのアプリ一覧と重なるため、縦は中央にする。
        // SizeToContent で決まった大きさが必要なため、レイアウトが済んだ Loaded で行う
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - ActualWidth - ScreenMargin);
        Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - ActualHeight) / 2);
    }

    private void SetValues(AppSettings settings)
    {
        _updating = true;
        HoldSlider.Value = settings.HoldDurationMs;
        FadeSlider.Value = settings.FadeDurationMs;
        SizeSlider.Value = settings.Size;
        OpacitySlider.Value = settings.BackgroundOpacityPercent;
        PollingSlider.Value = settings.PollingIntervalMs;
        HideWhenFullScreenCheck.IsChecked = settings.HideWhenFullScreen;
        _updating = false;

        UpdateTexts();
    }

    private AppSettings GetValues() => new AppSettings
    {
        HoldDurationMs = (int)HoldSlider.Value,
        FadeDurationMs = (int)FadeSlider.Value,
        Size = (int)SizeSlider.Value,
        BackgroundOpacityPercent = (int)OpacitySlider.Value,
        PollingIntervalMs = (int)PollingSlider.Value,
        HideWhenFullScreen = HideWhenFullScreenCheck.IsChecked == true,
    }.Normalize();

    private void UpdateTexts()
    {
        HoldText.Text = $"{(int)HoldSlider.Value} ms";
        FadeText.Text = $"{(int)FadeSlider.Value} ms";
        SizeText.Text = $"{(int)SizeSlider.Value}";
        OpacityText.Text = $"{(int)OpacitySlider.Value} %";
        PollingText.Text = $"{(int)PollingSlider.Value} ms";
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating)
        {
            return;
        }

        UpdateTexts();
        SettingsChanged?.Invoke(this, GetValues());
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        SettingsChanged?.Invoke(this, GetValues());
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        SetValues(AppSettings.Default);
        SettingsChanged?.Invoke(this, GetValues());
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
