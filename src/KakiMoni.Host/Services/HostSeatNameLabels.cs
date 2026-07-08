using System.Text;
using KakiMoni.Core.Paths;

namespace KakiMoni_Host.Services;

internal static class HostSeatNameLabels
{
    public static string GetLabel(int seatId)
    {
        if (seatId is < 1 or > 10)
            return $"ID {seatId}";

        var name = LoadNames()[seatId - 1];
        return string.IsNullOrWhiteSpace(name) ? $"ID {seatId}" : $"席{seatId} {name}";
    }

    private static string[] LoadNames()
    {
        var result = new string[10];
        var path = Path.Combine(ContentRootResolver.Resolve(), "assets", "seat-names.txt");
        if (!File.Exists(path))
            return result;

        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            for (var i = 0; i < 10 && i < lines.Length; i++)
                result[i] = lines[i].Trim();
        }
        catch
        {
        }

        return result;
    }
}
