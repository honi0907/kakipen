using System.Diagnostics;
using KakiMoni.Core.Paths;

namespace KakiMoni_Host.Services;

public static class HostSavesFolderLauncher
{
    public static string ResolveSavesPath()
    {
        var path = ContentRootResolver.SavesPath;
        Directory.CreateDirectory(path);
        for (var i = 1; i <= 10; i++)
            Directory.CreateDirectory(Path.Combine(path, $"ID{i:D2}"));
        return path;
    }

    public static bool TryOpen(out string? error)
    {
        error = null;
        try
        {
            var path = ResolveSavesPath();
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
