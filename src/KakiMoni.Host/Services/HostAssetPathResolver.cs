using KakiMoni.Core.Paths;

namespace KakiMoni_Host.Services;

/// <summary>
/// サーバーが配信する <c>/backgrounds/...</c> 等を、親機ローカルの <c>assets/</c> ファイルパスへ解決する。
/// </summary>
internal static class HostAssetPathResolver
{
    private static readonly (string UrlPrefix, string Folder)[] Mappings =
    [
        ("/backgrounds/", "backgrounds"),
        ("/choices/", "choices"),
        ("/overlays/", "overlays"),
        ("/logo/", "logo"),
    ];

    public static bool TryResolveLocalPath(string relativeOrAbsoluteUrl, out string localPath)
    {
        localPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
            return false;

        var path = relativeOrAbsoluteUrl;
        if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            path = absolute.AbsolutePath;

        if (!path.StartsWith('/'))
            path = "/" + path;

        foreach (var (prefix, folder) in Mappings)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var encodedName = path[prefix.Length..].TrimStart('/');
            if (string.IsNullOrWhiteSpace(encodedName))
                return false;

            var fileName = Path.GetFileName(Uri.UnescapeDataString(encodedName));
            if (string.IsNullOrWhiteSpace(fileName)
                || fileName.Contains("..", StringComparison.Ordinal))
                return false;

            localPath = Path.Combine(ContentRootResolver.AssetsPath, folder, fileName);
            return File.Exists(localPath);
        }

        return false;
    }
}
