using System.Text;

namespace KakiMoni.Server.Services;

public sealed class SeatNameFileService
{
    private readonly string _filePath;

    public string ContentRoot { get; }

    public SeatNameFileService(string contentRoot)
    {
        ContentRoot = contentRoot;
        _filePath = Path.Combine(contentRoot, "assets", "seat-names.txt");
    }

    public string GetNameForSeat(int seatId)
    {
        if (seatId is < 1 or > 10)
            return string.Empty;

        return LoadNames()[seatId - 1];
    }

    public string[] LoadNames()
    {
        var result = new string[10];
        if (!File.Exists(_filePath))
            return result;

        try
        {
            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            for (var i = 0; i < 10 && i < lines.Length; i++)
                result[i] = lines[i].Trim();
        }
        catch
        {
            // 壊れたファイルは全席名前なし扱い
        }

        return result;
    }
}
