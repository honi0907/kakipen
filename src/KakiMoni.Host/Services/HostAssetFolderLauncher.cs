using System.Diagnostics;
using KakiMoni.Core.Paths;

namespace KakiMoni_Host.Services;

public static class HostAssetFolderLauncher
{
    public static string ResolveAssetsPath()
    {
        var contentRoot = ContentRootResolver.Resolve();
        AssetFolderInitializer.EnsureDefaultFolders(contentRoot);
        return ContentRootResolver.AssetsPath;
    }

    public static bool TryOpen(out string? error)
    {
        error = null;
        try
        {
            var path = ResolveAssetsPath();
            Directory.CreateDirectory(path);

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
