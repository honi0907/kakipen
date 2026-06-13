using KakiMoni.Core.Models;

namespace KakiMoni.Server.Services;

public sealed class BackgroundFileService
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif", ".tif", ".tiff"];

    private readonly string _backgroundsDir;

    public string ContentRoot { get; }
    public string BackgroundsDirectory => _backgroundsDir;

    public BackgroundFileService(string contentRoot)
    {
        ContentRoot = contentRoot;
        _backgroundsDir = Path.Combine(contentRoot, "assets", "backgrounds");
        Directory.CreateDirectory(_backgroundsDir);
    }

    public IReadOnlyList<string> ListBackgrounds() =>
        ListBackgroundEntries().Select(e => e.FileName).ToList();

    public IReadOnlyList<BackgroundFileEntry> ListBackgroundEntries()
    {
        if (!Directory.Exists(_backgroundsDir))
            return Array.Empty<BackgroundFileEntry>();

        return Directory
            .EnumerateFiles(_backgroundsDir)
            .Select(path =>
            {
                var fileName = Path.GetFileName(path);
                if (fileName is null || !SupportedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
                    return null;

                var info = new FileInfo(path);
                return new BackgroundFileEntry
                {
                    FileName = fileName,
                    RelativeUrl = BuildRelativePath(fileName),
                    SizeBytes = info.Length,
                    LastModifiedUtcTicks = info.LastWriteTimeUtc.Ticks
                };
            })
            .Where(e => e is not null)
            .Cast<BackgroundFileEntry>()
            .OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 席 ID に対応する背景ファイル（例: BG_ID1.png）を探す。
    /// </summary>
    public (string FileName, string RelativeUrl)? FindSeatBackground(int seatId)
    {
        foreach (var fileName in ListBackgrounds())
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            if (IsSeatBackgroundName(baseName, seatId))
                return (fileName, BuildRelativePath(fileName));
        }

        return null;
    }

    public string? TryResolveSeatBackground(int seatId) =>
        FindSeatBackground(seatId)?.RelativeUrl;

    public static string BuildRelativePath(string fileName) =>
        "/backgrounds/" + Uri.EscapeDataString(fileName);

    private static bool IsSeatBackgroundName(string baseName, int seatId) =>
        baseName.Equals($"BG_ID{seatId}", StringComparison.OrdinalIgnoreCase)
        || baseName.Equals($"BG_ID{seatId:D2}", StringComparison.OrdinalIgnoreCase);
}
