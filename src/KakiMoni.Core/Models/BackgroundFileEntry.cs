namespace KakiMoni.Core.Models;

public sealed class BackgroundFileEntry
{
    public string FileName { get; init; } = string.Empty;
    public string RelativeUrl { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public long LastModifiedUtcTicks { get; init; }
}
