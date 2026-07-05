namespace KakiMoni.Core.Paths;



public static class ContentRootResolver

{

    /// <summary>

    /// リポジトリルート（小文字の assets/backgrounds があるディレクトリ）を返す。

    /// WinUI 出力の <c>Assets</c> は Windows 上で <c>assets</c> と同一視されるため、

    /// フォルダ名の大文字小文字を厳密に見る。

    /// </summary>

    public static string Resolve()
    {
        var dir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(dir))
        {
            var hasSln = File.Exists(Path.Combine(dir, "KakiMoni.WinUI.sln"));
            if (hasSln && HasAssetsBackgrounds(dir))
                return dir;

            if (HasAssetsBackgrounds(dir) && !IsAppOutputDirectory(dir))
                return dir;

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return AppContext.BaseDirectory;
    }



    public static string AssetsPath => Path.Combine(Resolve(), "assets");

    /// <summary>
    /// 保存画像・セッション状態。開発時はリポジトリ直下の saves/、
    /// Program Files インストール時は %LocalAppData%\KakiMoni\saves（書き込み可能）。
    /// </summary>
    public static string SavesPath
    {
        get
        {
            var root = Resolve();
            if (!IsProtectedInstallDirectory(root))
                return Path.Combine(root, "saves");

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KakiMoni",
                "saves");
        }
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



    private static bool HasAssetsDir(string dir) => FindChildDir(dir, "assets") is not null;



    private static bool HasAssetsBackgrounds(string dir)

    {

        var assets = FindChildDir(dir, "assets");

        return assets is not null && FindChildDir(assets, "backgrounds") is not null;

    }

    private static bool IsAppOutputDirectory(string dir) =>
        File.Exists(Path.Combine(dir, "KakiMoni.Host.dll"))
        || File.Exists(Path.Combine(dir, "KakiMoni.Client.dll"));



    /// <summary>大文字小文字完全一致の直下フォルダのみ（Assets ≠ assets）。</summary>

    private static string? FindChildDir(string parent, string exactName)

    {

        if (!Directory.Exists(parent))

            return null;



        foreach (var path in Directory.EnumerateDirectories(parent))

        {

            if (string.Equals(Path.GetFileName(path), exactName, StringComparison.Ordinal))

                return path;

        }



        return null;

    }

}


