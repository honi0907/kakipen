using KakiMoni.Core.Network;

namespace KakiMoni_Host.Services;

public enum HostNetworkMode
{
    Auto,
    Manual
}

public sealed class HostSettings
{
    /// <summary>スタンバイ実行時に全席ロックも解除する。</summary>
    public bool StandbyUnlockAll { get; set; }

    /// <summary>判定 GO（fill 型）表示中にパレット色を反転して描画する。</summary>
    public bool JudgeColorMode { get; set; }

    /// <summary>子機ロック時オーバーレイの暗さ（0–100%、100 が最も暗い）。</summary>
    public int LockOverlayOpacityPercent { get; set; } = 80;

    /// <summary>assets/seat-names.txt から席名を読み込む。</summary>
    public bool UseSeatNameFile { get; set; }

    public HostNetworkMode NetworkMode { get; set; } = HostNetworkMode.Auto;

    public LanAddressPreference NetworkPreference { get; set; } = LanAddressPreference.FirstFound;

    public string? ManualNetworkAddress { get; set; }

    /// <summary>コンパネ表示時にメインウィンドウをフルスクリーンにする。</summary>
    public bool CompanelFullscreen { get; set; }
}
