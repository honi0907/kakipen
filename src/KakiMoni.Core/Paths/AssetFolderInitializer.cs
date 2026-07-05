namespace KakiMoni.Core.Paths;

/// <summary>
/// 画像アセット用フォルダと、退避用 <c>old</c> サブフォルダを用意する。
/// </summary>
public static class AssetFolderInitializer
{
    public static void EnsureDefaultFolders(string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
            return;

        foreach (var dir in DefaultDirectories(contentRoot))
            AppInstallPaths.SafeCreateDirectory(dir);
    }

    public static IEnumerable<string> DefaultDirectories(string contentRoot)
    {
        var assets = Path.Combine(contentRoot, "assets");
        yield return Path.Combine(assets, "backgrounds");
        yield return Path.Combine(assets, "backgrounds", "old");
        yield return Path.Combine(assets, "choices");
        yield return Path.Combine(assets, "choices", "old");
        yield return Path.Combine(assets, "logo");
        yield return Path.Combine(assets, "logo", "old");
        yield return Path.Combine(assets, "overlays", "correct");
        yield return Path.Combine(assets, "overlays", "correct", "old");
        yield return Path.Combine(assets, "overlays", "incorrect");
        yield return Path.Combine(assets, "overlays", "incorrect", "old");
    }
}
