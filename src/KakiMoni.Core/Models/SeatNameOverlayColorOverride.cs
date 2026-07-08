namespace KakiMoni.Core.Models;

public sealed class SeatNameOverlayColorOverride
{
    public string? TextColor { get; set; }

    public string? TextStrokeColor { get; set; }

    public string? BorderColor { get; set; }

    public string? BackgroundColor { get; set; }

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(TextColor)
        || !string.IsNullOrWhiteSpace(TextStrokeColor)
        || !string.IsNullOrWhiteSpace(BorderColor)
        || !string.IsNullOrWhiteSpace(BackgroundColor);

    public SeatNameOverlayColorOverride Clone() => new()
    {
        TextColor = TextColor,
        TextStrokeColor = TextStrokeColor,
        BorderColor = BorderColor,
        BackgroundColor = BackgroundColor
    };

    public void Normalize()
    {
        TextColor = NormalizeOptionalColor(TextColor);
        TextStrokeColor = NormalizeOptionalColor(TextStrokeColor);
        BorderColor = NormalizeOptionalColor(BorderColor);
        BackgroundColor = NormalizeOptionalColor(BackgroundColor);
    }

    private static string? NormalizeOptionalColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var hex = value.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;

        return hex.Length is 7 or 9 ? hex.ToLowerInvariant() : null;
    }
}
