namespace ImeCenterView.Settings;

/// <summary>
/// ユーザーが変更できる設定値。既定値は以前の Microsoft IME の表示に合わせている。
/// </summary>
/// <remarks>
/// 不変の型で、変更するときは <c>with</c> 式で新しいインスタンスを作る。
/// 設定ファイルを手で書き換えた場合などに範囲外の値が入りうるため、使う前に <see cref="Normalize"/> で範囲内に収める。
/// </remarks>
public sealed record AppSettings
{
    /// <summary><see cref="HoldDurationMs"/> の最小値。</summary>
    public const int MinHoldDurationMs = 100;

    /// <summary><see cref="HoldDurationMs"/> の最大値。</summary>
    public const int MaxHoldDurationMs = 5000;

    /// <summary><see cref="FadeDurationMs"/> の最小値。0 はフェードアウトせずに消すことを表す。</summary>
    public const int MinFadeDurationMs = 0;

    /// <summary><see cref="FadeDurationMs"/> の最大値。</summary>
    public const int MaxFadeDurationMs = 3000;

    /// <summary><see cref="Size"/> の最小値。</summary>
    public const int MinSize = 50;

    /// <summary><see cref="Size"/> の最大値。</summary>
    public const int MaxSize = 500;

    /// <summary><see cref="BackgroundOpacityPercent"/> の最小値。</summary>
    public const int MinBackgroundOpacityPercent = 10;

    /// <summary><see cref="BackgroundOpacityPercent"/> の最大値。</summary>
    public const int MaxBackgroundOpacityPercent = 100;

    /// <summary><see cref="PollingIntervalMs"/> の最小値。</summary>
    public const int MinPollingIntervalMs = 50;

    /// <summary><see cref="PollingIntervalMs"/> の最大値。</summary>
    public const int MaxPollingIntervalMs = 1000;

    /// <summary>既定値の設定。</summary>
    public static AppSettings Default { get; } = new();

    /// <summary>フェードアウトを始めるまで、不透明のまま表示しておく時間（ミリ秒）。</summary>
    public int HoldDurationMs { get; init; } = 700;

    /// <summary>フェードアウトにかける時間（ミリ秒）。</summary>
    public int FadeDurationMs { get; init; } = 300;

    /// <summary>表示領域（正方形）の一辺の長さ（DIP）。文字もこれに比例して拡大縮小する。</summary>
    public int Size { get; init; } = 162;

    /// <summary>背景の不透明度（%）。文字は常に不透明。</summary>
    public int BackgroundOpacityPercent { get; init; } = 85;

    /// <summary>IME 状態を取得する間隔（ミリ秒）。</summary>
    public int PollingIntervalMs { get; init; } = 100;

    /// <summary>
    /// すべての値を許容範囲内に収めた設定を返す。
    /// </summary>
    /// <returns>範囲外の値を最小値または最大値に置き換えた設定。すべて範囲内なら同じ内容の設定。</returns>
    public AppSettings Normalize() => this with
    {
        HoldDurationMs = Math.Clamp(HoldDurationMs, MinHoldDurationMs, MaxHoldDurationMs),
        FadeDurationMs = Math.Clamp(FadeDurationMs, MinFadeDurationMs, MaxFadeDurationMs),
        Size = Math.Clamp(Size, MinSize, MaxSize),
        BackgroundOpacityPercent = Math.Clamp(
            BackgroundOpacityPercent,
            MinBackgroundOpacityPercent,
            MaxBackgroundOpacityPercent),
        PollingIntervalMs = Math.Clamp(PollingIntervalMs, MinPollingIntervalMs, MaxPollingIntervalMs),
    };
}
