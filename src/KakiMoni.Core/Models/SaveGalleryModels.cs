namespace KakiMoni.Core.Models;

public sealed class SaveGalleryEntry
{
    public int SeatId { get; init; }
    public int Session { get; init; }
    public int Counter { get; init; }
    public string Type { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public long UpdatedAtMs { get; init; }
}

public sealed class SaveGallerySeatBucket
{
    public int SeatId { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<SaveGalleryEntry> Items { get; init; } = Array.Empty<SaveGalleryEntry>();
}

public sealed class SaveGalleryResult
{
    public bool Ok { get; init; } = true;
    public string? Error { get; init; }
    public IReadOnlyList<int> SeatIds { get; init; } = Array.Empty<int>();
    public int MaxPerSeat { get; init; }
    public int Total { get; init; }
    public IReadOnlyDictionary<int, SaveGallerySeatBucket> BySeat { get; init; }
        = new Dictionary<int, SaveGallerySeatBucket>();
}
