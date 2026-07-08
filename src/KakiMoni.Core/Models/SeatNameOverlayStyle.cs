namespace KakiMoni.Core.Models;

public sealed class SeatNameOverlayStyle
{
    public const double ReferenceHeight = 1080.0;
    public const double MinFontSize = 8;
    public const double MaxFontSize = 144;
    public const double MaxTextStrokeThickness = 12;

    public static readonly string[] FontPresets =
    [
        "Yu Gothic UI",
        "Meiryo UI",
        "UD Digi Kyokasho N",
        "Segoe UI"
    ];

    public bool Enabled { get; set; } = true;

    public string FontFamily { get; set; } = FontPresets[0];

    /// <summary>1080p 基準のフォントサイズ（px）。</summary>
    public double FontSize { get; set; } = 32;

    public string TextColor { get; set; } = "#ffffff";

    public string TextStrokeColor { get; set; } = "#000000";

    public double TextStrokeThickness { get; set; } = 2;

    public bool TextStrokeEnabled { get; set; }

    public string BorderColor { get; set; } = "#000000";

    public string BackgroundColor { get; set; } = "#80000000";

    public double BorderThickness { get; set; } = 2;

    public bool BorderEnabled { get; set; } = true;

    public bool BackgroundEnabled { get; set; } = true;

    public SeatNameOverlayAnchor Anchor { get; set; } = SeatNameOverlayAnchor.BottomCenter;

    /// <summary>1080p 基準のマージン（px）。</summary>
    public double MarginX { get; set; } = 0;

    /// <summary>1080p 基準のマージン（px）。</summary>
    public double MarginY { get; set; } = 64;

    public SeatNameOverlayStyle Clone() => new()
    {
        Enabled = Enabled,
        FontFamily = FontFamily,
        FontSize = FontSize,
        TextColor = TextColor,
        TextStrokeColor = TextStrokeColor,
        TextStrokeThickness = TextStrokeThickness,
        TextStrokeEnabled = TextStrokeEnabled,
        BorderColor = BorderColor,
        BackgroundColor = BackgroundColor,
        BorderThickness = BorderThickness,
        BorderEnabled = BorderEnabled,
        BackgroundEnabled = BackgroundEnabled,
        Anchor = Anchor,
        MarginX = MarginX,
        MarginY = MarginY
    };

    public void Normalize()
    {
        FontSize = Math.Clamp(FontSize, MinFontSize, MaxFontSize);
        TextStrokeThickness = Math.Clamp(TextStrokeThickness, 0, MaxTextStrokeThickness);
        BorderThickness = Math.Clamp(BorderThickness, 0, 16);
        MarginX = Math.Clamp(MarginX, 0, 400);
        MarginY = Math.Clamp(MarginY, 0, 400);

        if (string.IsNullOrWhiteSpace(FontFamily)
            || !FontPresets.Contains(FontFamily, StringComparer.OrdinalIgnoreCase))
        {
            FontFamily = FontPresets[0];
        }

        TextColor = NormalizeColor(TextColor, "#ffffff");
        TextStrokeColor = NormalizeColor(TextStrokeColor, "#000000");
        BorderColor = NormalizeColor(BorderColor, "#000000");
        BackgroundColor = NormalizeColor(BackgroundColor, "#80000000");
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var hex = value.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;

        return hex.Length is 7 or 9 ? hex.ToLowerInvariant() : fallback;
    }
}
