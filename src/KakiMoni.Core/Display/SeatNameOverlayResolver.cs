using KakiMoni.Core.Models;

namespace KakiMoni.Core.Display;

public static class SeatNameOverlayResolver
{
    public static SeatNameOverlayStyle Resolve(SeatNameOverlayConfig? config, int seatId)
    {
        config ??= new SeatNameOverlayConfig();
        config.Normalize();

        if (!config.UsePerSeatColors
            || !config.PerSeatColors.TryGetValue(seatId, out var colors)
            || colors is null
            || !colors.HasAny)
        {
            return config.Base.Clone();
        }

        var style = config.Base.Clone();
        if (!string.IsNullOrWhiteSpace(colors.TextColor))
            style.TextColor = colors.TextColor!;
        if (!string.IsNullOrWhiteSpace(colors.TextStrokeColor))
            style.TextStrokeColor = colors.TextStrokeColor!;
        if (!string.IsNullOrWhiteSpace(colors.BorderColor))
            style.BorderColor = colors.BorderColor!;
        if (!string.IsNullOrWhiteSpace(colors.BackgroundColor))
            style.BackgroundColor = colors.BackgroundColor!;

        style.Normalize();
        return style;
    }
}
