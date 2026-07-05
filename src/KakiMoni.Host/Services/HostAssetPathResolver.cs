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

            var relativePart = path[prefix.Length..].TrimStart('/');
            if (string.IsNullOrWhiteSpace(relativePart))
                return false;

            var decodedRelative = Uri.UnescapeDataString(relativePart).Replace('\\', '/');
            if (decodedRelative.Contains("..", StringComparison.Ordinal))
                return false;

            var assetsRoot = Path.GetFullPath(Path.Combine(ContentRootResolver.AssetsPath, folder));
            var candidate = Path.GetFullPath(Path.Combine(
                assetsRoot,
                decodedRelative.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsUnderDirectory(candidate, assetsRoot))
                return false;

            if (!File.Exists(candidate))
                return false;

            localPath = candidate;
            return true;
        }

        return false;
    }

    private static bool IsUnderDirectory(string candidate, string root)
    {
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        if (candidate.Length == root.Length)
            return true;

        var next = candidate[root.Length];
        return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
    }
}
