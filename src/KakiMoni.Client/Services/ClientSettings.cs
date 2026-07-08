namespace KakiMoni_Client.Services;

public sealed class ClientSettings
{
    public static readonly string[] DefaultPalette = ["#000000", "#ef4444", "#3b82f6", "#22c55e"];
    public const string StartupPenColor = "#000000";

    public string? ServerUrl { get; set; }

    /// <summary>ランチャーで選べる保存済みサーバー URL（新しい順）。</summary>
    public List<string> SavedServerUrls { get; set; } = [];
    public int SeatId { get; set; } = 1;
    public string? BgImageUrl { get; set; }
    public string PenColor { get; set; } = DefaultPalette[0];
    public double PenSize { get; set; } = 8;
    public double EraserSize { get; set; } = 24;
    public List<string> Palette { get; set; } = DefaultPalette.ToList();

    /// <summary>書き画面をフルスクリーンで開く。</summary>
    public bool WritingFullscreen { get; set; }

    /// <summary>外部出力ウィンドウを使う。</summary>
    public bool ExternalOutputEnabled { get; set; }

    /// <summary>外部出力を拡張ディスプレイへ自動配置する。</summary>
    public bool ExternalAutoPlacement { get; set; }

    /// <summary>外部出力をフルスクリーンにする。</summary>
    public bool ExternalFullscreen { get; set; }

    /// <summary>書き画面に決定ボタンを表示する。</summary>
    public bool ShowConfirmButton { get; set; } = true;

    /// <summary>書き画面にクリアボタンを表示する。</summary>
    public bool ShowClearButton { get; set; } = true;

    /// <summary>書き画面に消しゴムツールを表示する。</summary>
    public bool ShowEraserTool { get; set; } = true;

    /// <summary>消しゴム使用後、ペンに自動で戻るまでの秒数。</summary>
    public int EraserAutoPenSeconds { get; set; } = 5;
}
