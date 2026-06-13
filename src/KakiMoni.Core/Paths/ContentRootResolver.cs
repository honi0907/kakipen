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

            if (HasAssetsBackgrounds(dir))

                return dir;



            if (File.Exists(Path.Combine(dir, "KakiMoni.WinUI.sln")) && HasAssetsDir(dir))

                return dir;



            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;

        }



        return AppContext.BaseDirectory;

    }



    public static string AssetsPath => Path.Combine(Resolve(), "assets");

    public static string SavesPath => Path.Combine(Resolve(), "saves");



    private static bool HasAssetsDir(string dir) => FindChildDir(dir, "assets") is not null;



    private static bool HasAssetsBackgrounds(string dir)

    {

        var assets = FindChildDir(dir, "assets");

        return assets is not null && FindChildDir(assets, "backgrounds") is not null;

    }



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


