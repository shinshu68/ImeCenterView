using System.Drawing;
using System.Windows.Forms;

namespace ImeCenterView.Tray;

/// <summary>
/// タスクトレイのアイコンと右クリックメニュー。
/// </summary>
/// <remarks>
/// アイコン・メニュー・メニュー項目は初期化時に 1 回だけ生成し、状態が変わったときは表示だけを書き換える。
/// <see cref="NotifyIcon"/> は破棄しないとトレイに残骸が残るため、所有者（<c>App</c>）が終了時に必ず <see cref="Dispose"/> を呼ぶ。
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    private const string AppName = "ImeCenterView";
    private const string IconResourceName = "ImeCenterView.Resources.tray.ico";

    private readonly Icon _icon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _exitItem;
    private bool _disposed;

    /// <summary>「一時停止」／「再開」が選ばれたときに発生する。</summary>
    public event EventHandler? PauseToggleRequested;

    /// <summary>「設定」が選ばれたときに発生する。</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>「スタートアップに登録」が選ばれたときに発生する。</summary>
    public event EventHandler? StartupToggleRequested;

    /// <summary>「終了」が選ばれたときに発生する。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// トレイアイコンとメニューを生成し、トレイに表示する。
    /// </summary>
    public TrayIcon()
    {
        _icon = LoadIcon();

        _pauseItem = new ToolStripMenuItem("一時停止");
        _settingsItem = new ToolStripMenuItem("設定");
        _startupItem = new ToolStripMenuItem("スタートアップに登録");
        _exitItem = new ToolStripMenuItem("終了");

        // ハンドラは初期化時に 1 回だけ登録し、Dispose で解除する
        _pauseItem.Click += OnPauseClick;
        _settingsItem.Click += OnSettingsClick;
        _startupItem.Click += OnStartupClick;
        _exitItem.Click += OnExitClick;

        // メニューに追加した項目はメニューが所有し、メニューの Dispose でまとめて破棄される
        _menu = new ContextMenuStrip();
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = AppName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    /// <summary>
    /// 一時停止中かどうかを、メニューの文言とツールチップに反映する。
    /// </summary>
    /// <param name="paused">一時停止中なら <see langword="true"/>。</param>
    public void SetPaused(bool paused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // どちらも定数の文字列で、切り替えのたびに新しい文字列は作らない
        _pauseItem.Text = paused ? "再開" : "一時停止";
        _notifyIcon.Text = paused ? AppName + "（一時停止中）" : AppName;
    }

    /// <summary>
    /// スタートアップに登録されているかどうかを、メニューのチェックに反映する。
    /// </summary>
    /// <param name="registered">登録されていれば <see langword="true"/>。</param>
    public void SetStartupRegistered(bool registered)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _startupItem.Checked = registered;
    }

    /// <summary>
    /// トレイからアイコンを消し、イベントの購読を解除して、アイコン・メニューを破棄する。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 先にトレイから消してから破棄する（残骸を残さないため）
        _notifyIcon.Visible = false;

        _pauseItem.Click -= OnPauseClick;
        _settingsItem.Click -= OnSettingsClick;
        _startupItem.Click -= OnStartupClick;
        _exitItem.Click -= OnExitClick;
        PauseToggleRequested = null;
        SettingsRequested = null;
        StartupToggleRequested = null;
        ExitRequested = null;

        // NotifyIcon は Icon・ContextMenuStrip を破棄しないため、それぞれ自分で破棄する
        _notifyIcon.ContextMenuStrip = null;
        _notifyIcon.Icon = null;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    /// <summary>
    /// 埋め込みリソースの .ico から、トレイ用の大きさのアイコンを読み込む。
    /// </summary>
    /// <remarks>
    /// <see cref="Icon"/> はストリームの内容を自分で保持するため、読み込み後にストリームを閉じてよい。
    /// 読み込んだ <see cref="Icon"/> は <see cref="Dispose"/> で破棄する。
    /// </remarks>
    private static Icon LoadIcon()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"埋め込みリソース {IconResourceName} が見つかりません。");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    private void OnPauseClick(object? sender, EventArgs e) => PauseToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsClick(object? sender, EventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnStartupClick(object? sender, EventArgs e) => StartupToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object? sender, EventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
