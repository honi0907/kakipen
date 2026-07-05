using System.Text.RegularExpressions;
using KakiMoni.Core.Models;
using KakiMoni.Core.Paths;

namespace KakiMoni.Server.Services;

public sealed class SaveGalleryService
{
    private static readonly Regex FilePattern = new(
        @"^S(\d+)_ID(\d+)_(\d{3})_([^.]+)\.png$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _savesDir;

    public SaveGalleryService(string contentRoot)
    {
        _ = contentRoot;
        _savesDir = ContentRootResolver.SavesPath;
        Directory.CreateDirectory(_savesDir);
    }

    public string SavesDirectory => _savesDir;

    public static IReadOnlyList<int> ParseSeatIds(string? raw)
    {
        var source = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(source))
            return Enumerable.Range(1, 10).ToArray();

        var unique = new SortedSet<int>();
        foreach (var token in source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, out var seatId) && seatId is >= 1 and <= 10)
                unique.Add(seatId);
        }

        return unique.Count > 0 ? unique.ToArray() : Enumerable.Range(1, 10).ToArray();
    }

    public SaveGalleryResult BuildGallery(IReadOnlyList<int> seatIds, int maxPerSeat)
    {
        var clampedMax = Math.Clamp(maxPerSeat, 1, 500);
        var bySeat = new Dictionary<int, SaveGallerySeatBucket>();
        var total = 0;

        foreach (var seatId in seatIds)
        {
            var allEntries = ListSeatEntries(seatId);
            var items = allEntries.Take(clampedMax).ToArray();
            bySeat[seatId] = new SaveGallerySeatBucket
            {
                SeatId = seatId,
                TotalCount = allEntries.Count,
                Items = items
            };
            total += items.Length;
        }

        return new SaveGalleryResult
        {
            SeatIds = seatIds,
            MaxPerSeat = clampedMax,
            Total = total,
            BySeat = bySeat
        };
    }

    public IReadOnlyList<SaveGalleryEntry> ListSeatEntries(int seatId)
    {
        if (seatId is < 1 or > 10)
            return Array.Empty<SaveGalleryEntry>();

        var seatDir = Path.Combine(_savesDir, $"ID{seatId:D2}");
        if (!Directory.Exists(seatDir))
            return Array.Empty<SaveGalleryEntry>();

        var entries = new List<SaveGalleryEntry>();
        foreach (var filePath in Directory.EnumerateFiles(seatDir, "*.png"))
        {
            var fileName = Path.GetFileName(filePath);
            var match = FilePattern.Match(fileName);
            if (!match.Success)
                continue;

            var parsedSeatId = int.Parse(match.Groups[2].Value);
            if (parsedSeatId != seatId)
                continue;

            FileInfo stat;
            try
            {
                stat = new FileInfo(filePath);
            }
            catch
            {
                continue;
            }

            entries.Add(new SaveGalleryEntry
            {
                SeatId = seatId,
                Session = int.Parse(match.Groups[1].Value),
                Counter = int.Parse(match.Groups[3].Value),
                Type = match.Groups[4].Value.ToUpperInvariant(),
                FileName = fileName,
                FilePath = filePath,
                Size = stat.Length,
                UpdatedAt = stat.LastWriteTimeUtc,
                UpdatedAtMs = stat.LastWriteTimeUtc.Ticks
            });
        }

        entries.Sort((a, b) => b.UpdatedAtMs.CompareTo(a.UpdatedAtMs));
        return entries;
    }

    public bool TryResolveFilePath(int seatId, string fileName, out string filePath)
    {
        filePath = string.Empty;
        if (seatId is < 1 or > 10)
            return false;

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeFileName) || !string.Equals(safeFileName, fileName, StringComparison.Ordinal))
            return false;

        if (!FilePattern.IsMatch(safeFileName))
            return false;

        var seatLabel = seatId.ToString("D2");
        var parsedSeatId = int.Parse(safeFileName.Split('_')[1][2..]);
        if (parsedSeatId != seatId)
            return false;

        var candidate = Path.Combine(_savesDir, $"ID{seatLabel}", safeFileName);
        if (!File.Exists(candidate))
            return false;

        filePath = candidate;
        return true;
    }
}
