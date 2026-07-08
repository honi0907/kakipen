namespace KakiMoni.Server.Services;

using KakiMoni.Core.Models;

public sealed class GameSessionState
{
    public string? CurrentChoiceUrl { get; set; }

    /// <summary>判定 GO（fill 型）表示中に描画色を反転する。</summary>
    public bool JudgeColorMode { get; set; }

    /// <summary>子機ロックオーバーレイの不透明度（0–100）。</summary>
    public int LockOverlayOpacityPercent { get; set; } = 80;

    /// <summary>assets/seat-names.txt から席名を読み込む。</summary>
    public bool UseSeatNameFile { get; set; }

    /// <summary>キャンバス上の席名オーバーレイ表示スタイル。</summary>
    public SeatNameOverlayConfig SeatNameOverlay { get; set; } = new();
}
