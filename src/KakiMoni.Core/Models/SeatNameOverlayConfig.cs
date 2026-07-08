using System.Text.Json.Serialization;

namespace KakiMoni.Core.Models;

[JsonConverter(typeof(SeatNameOverlayConfigJsonConverter))]
public sealed class SeatNameOverlayConfig
{
    public SeatNameOverlayStyle Base { get; set; } = new();

    public bool UsePerSeatColors { get; set; }

    public Dictionary<int, SeatNameOverlayColorOverride> PerSeatColors { get; set; } = new();

    public SeatNameOverlayConfig Clone() => new()
    {
        Base = Base.Clone(),
        UsePerSeatColors = UsePerSeatColors,
        PerSeatColors = PerSeatColors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone())
    };

    public void Normalize()
    {
        Base ??= new SeatNameOverlayStyle();
        Base.Normalize();
        PerSeatColors ??= new Dictionary<int, SeatNameOverlayColorOverride>();

        var normalized = new Dictionary<int, SeatNameOverlayColorOverride>();
        foreach (var (seatId, colors) in PerSeatColors)
        {
            if (seatId is < 1 or > 10 || colors is null)
                continue;

            colors.Normalize();
            if (colors.HasAny)
                normalized[seatId] = colors;
        }

        PerSeatColors = normalized;
    }
}
