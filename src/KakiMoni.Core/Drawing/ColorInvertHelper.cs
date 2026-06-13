namespace KakiMoni.Core.Drawing;

public static class ColorInvertHelper
{
    /// <summary>パレット色の RGB 反転（#RRGGBB）。</summary>
    public static string InvertHex(string hex)
    {
        try
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length != 6)
                return hex;

            var r = 255 - Convert.ToByte(hex[..2], 16);
            var g = 255 - Convert.ToByte(hex[2..4], 16);
            var b = 255 - Convert.ToByte(hex[4..6], 16);
            return $"#{r:x2}{g:x2}{b:x2}";
        }
        catch
        {
            return hex;
        }
    }

    public static bool IsFillOverlayUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Path.GetFileName(url.Trim('/').Split('/').LastOrDefault() ?? string.Empty)
            .Contains("fill", StringComparison.OrdinalIgnoreCase);
}
