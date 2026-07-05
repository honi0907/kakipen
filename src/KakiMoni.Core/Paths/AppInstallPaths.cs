namespace KakiMoni.Core.Paths;

public static class AppInstallPaths
{
    public static string BaseDirectory =>
        Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static bool IsInstalledInProgramFiles => IsProtectedInstallDirectory(BaseDirectory);

    public static string UserDataRoot
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KakiMoni");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string SavesPath =>
        IsInstalledInProgramFiles
            ? EnsureDirectory(Path.Combine(UserDataRoot, "saves"))
            : Path.Combine(ContentRootResolver.Resolve(), "saves");

    public static void SafeCreateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (UnauthorizedAccessException)
        {
            // Installed under Program Files: read-only asset folders are OK.
        }
        catch (IOException)
        {
        }
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool IsProtectedInstallDirectory(string dir)
    {
        var full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var baseDir in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrWhiteSpace(baseDir))
                continue;

            var baseFull = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Equals(baseFull, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
