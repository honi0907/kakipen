namespace KakiMoni.Server.Services;

public sealed class LogoFileService
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif"];

    private readonly string _logoDir;

    public LogoFileService(string contentRoot)
    {
        _logoDir = Path.Combine(contentRoot, "assets", "logo");
        Directory.CreateDirectory(_logoDir);
    }

    public string? GetDefaultRelativeUrl()
    {
        if (!Directory.Exists(_logoDir))
            return null;

        var file = Directory
            .EnumerateFiles(_logoDir)
            .Select(Path.GetFileName)
            .Where(f => f is not null && SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return file is null ? null : "/logo/" + Uri.EscapeDataString(file);
    }
}
