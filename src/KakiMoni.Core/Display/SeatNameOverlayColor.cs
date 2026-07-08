namespace KakiMoni.Core.Display;

public static class SeatNameOverlayColor
{
    public static (byte A, byte R, byte G, byte B) ParseArgb(string? hex, byte defaultA = 255)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return (defaultA, 255, 255, 255);

        var value = hex.Trim();
        if (!value.StartsWith('#'))
            value = "#" + value;

        try
        {
            if (value.Length == 9)
            {
                return (
                    Convert.ToByte(value[1..3], 16),
                    Convert.ToByte(value[3..5], 16),
                    Convert.ToByte(value[5..7], 16),
                    Convert.ToByte(value[7..9], 16));
            }

            if (value.Length == 7)
            {
                return (
                    defaultA,
                    Convert.ToByte(value[1..3], 16),
                    Convert.ToByte(value[3..5], 16),
                    Convert.ToByte(value[5..7], 16));
            }
        }
        catch
        {
        }

        return (defaultA, 255, 255, 255);
    }

    public static string ToHex(byte a, byte r, byte g, byte b, bool includeAlpha) =>
        includeAlpha
            ? $"#{a:x2}{r:x2}{g:x2}{b:x2}"
            : $"#{r:x2}{g:x2}{b:x2}";

    public static (byte A, byte R, byte G, byte B) ParseArgb(string? hex) =>
        ParseArgb(hex, 255);
}
