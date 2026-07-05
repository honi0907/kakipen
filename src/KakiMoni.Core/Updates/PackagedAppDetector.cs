namespace KakiMoni.Core.Updates;

public static class PackagedAppDetector
{
    /// <summary>
    /// 開発ビルド（src/.../bin）では自己更新を適用しない。dist/ や Program Files などは許可。
    /// </summary>
    public static bool CanApplyOnlineUpdate()
    {
        var dir = AppContext.BaseDirectory;
        if (dir.Contains(@"\src\KakiMoni.", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
