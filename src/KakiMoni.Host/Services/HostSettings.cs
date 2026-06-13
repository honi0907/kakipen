namespace KakiMoni_Host.Services;

public sealed class HostSettings
{
    /// <summary>スタンバイ実行時に全席ロックも解除する。</summary>
    public bool StandbyUnlockAll { get; set; }

    /// <summary>判定 GO（fill 型）表示中にパレット色を反転して描画する。</summary>
    public bool JudgeColorMode { get; set; }

    /// <summary>子機ロック時オーバーレイの暗さ（0–100%、100 が最も暗い）。</summary>
    public int LockOverlayOpacityPercent { get; set; } = 80;
}
