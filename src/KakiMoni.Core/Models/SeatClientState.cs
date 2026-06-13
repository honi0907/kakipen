namespace KakiMoni.Core.Models;

public sealed class SeatClientState
{
    public int SeatId { get; init; }
    public string? ConnectionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<StrokeData> Strokes { get; set; } = new();
    public StrokeData? CurrentStroke { get; set; }
    public bool Locked { get; set; }
    public string BgImageUrl { get; set; } = string.Empty;
    public bool Revealed { get; set; }
    public string AnimType { get; set; } = "cut";
    public string OverlayImageUrl { get; set; } = string.Empty;
    public bool WritingBlackout { get; set; }
}
