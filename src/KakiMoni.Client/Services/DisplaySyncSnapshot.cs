using KakiMoni.Core.Models;

namespace KakiMoni_Client.Services;

public sealed class DisplaySyncSnapshot
{
    public required IReadOnlyList<StrokeData> Strokes { get; init; }
    public StrokeData? CurrentStroke { get; init; }
    public string? BgImageUrl { get; init; }
    public string? ChoiceImageUrl { get; init; }
}
