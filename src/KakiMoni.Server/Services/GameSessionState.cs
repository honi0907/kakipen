namespace KakiMoni.Server.Services;

public sealed class GameSessionState
{
    public string? CurrentChoiceUrl { get; set; }

    /// <summary>判定 GO（fill 型）表示中に描画色を反転する。</summary>
    public bool JudgeColorMode { get; set; }

    /// <summary>子機ロックオーバーレイの不透明度（0–100）。</summary>
    public int LockOverlayOpacityPercent { get; set; } = 80;
}
