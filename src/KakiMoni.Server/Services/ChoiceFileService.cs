using KakiMoni.Core.Models;

namespace KakiMoni.Server.Services;

public sealed class ChoiceFileService
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif", ".tif", ".tiff"];

    private readonly string _choicesDir;

    public string ChoicesDirectory => _choicesDir;

    public ChoiceFileService(string contentRoot)
    {
        _choicesDir = Path.Combine(contentRoot, "assets", "choices");
        Directory.CreateDirectory(_choicesDir);
    }

    public IReadOnlyList<BackgroundFileEntry> ListChoices()
    {
        if (!Directory.Exists(_choicesDir))
            return Array.Empty<BackgroundFileEntry>();

        return Directory
            .EnumerateFiles(_choicesDir)
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

    public static string BuildRelativePath(string fileName) =>
        "/choices/" + Uri.EscapeDataString(fileName);
}
