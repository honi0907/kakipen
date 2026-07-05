namespace KakiMoni.Server.Services;

using KakiMoni.Core.Paths;

public sealed class OverlayFileService
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif", ".tif", ".tiff"];

    private readonly string _overlaysDir;

    public OverlayFileService(string contentRoot)
    {
        _overlaysDir = Path.Combine(contentRoot, "assets", "overlays");
        AppInstallPaths.SafeCreateDirectory(Path.Combine(_overlaysDir, "correct"));
        AppInstallPaths.SafeCreateDirectory(Path.Combine(_overlaysDir, "incorrect"));
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ListAll()
    {
        return new Dictionary<string, IReadOnlyList<string>>
        {
            ["correct"] = ListFolder("correct"),
            ["incorrect"] = ListFolder("incorrect")
        };
    }

    public string ResolveJudgeUrl(string kind, string? imageUrl)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
            return NormalizeRelativeUrl(imageUrl);

        var folder = kind.Equals("correct", StringComparison.OrdinalIgnoreCase) ? "correct" : "incorrect";
        var preferredBase = kind.Equals("correct", StringComparison.OrdinalIgnoreCase) ? "aka_fill" : "ao_fill";
        return FindOverlayFile(folder, preferredBase)
               ?? $"/overlays/{folder}/{preferredBase}.png";
    }

    private string? FindOverlayFile(string folder, string preferredBase)
    {
        var dir = Path.Combine(_overlaysDir, folder);
        if (!Directory.Exists(dir)) return null;

        foreach (var ext in SupportedExtensions)
        {
            var fileName = preferredBase + ext;
            if (File.Exists(Path.Combine(dir, fileName)))
                return $"/overlays/{folder}/{fileName}";
        }

        return Directory
            .EnumerateFiles(dir)
            .Select(Path.GetFileName)
            .Where(f => f is not null && SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => f!.Contains("fill", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => $"/overlays/{folder}/{f}")
            .FirstOrDefault();
    }

    private IReadOnlyList<string> ListFolder(string kind)
    {
        var dir = Path.Combine(_overlaysDir, kind);
        if (!Directory.Exists(dir))
            return Array.Empty<string>();

        return Directory
            .EnumerateFiles(dir)
            .Select(Path.GetFileName)
            .Where(f => f is not null && SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Cast<string>()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeRelativeUrl(string url)
    {
        url = url.Trim();
        if (url.StartsWith('/'))
            return url;
        return "/" + url;
    }
}
