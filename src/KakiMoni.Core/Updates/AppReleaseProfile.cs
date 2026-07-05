namespace KakiMoni.Core.Updates;

public sealed record AppReleaseProfile(
    AppUpdateKind Kind,
    string GitHubRepo,
    string ReleaseTagPrefix,
    string AssetNamePattern,
    string DisplayName)
{
    public const string DefaultRepo = "honi0907/kakipen";

    public static AppReleaseProfile For(AppUpdateKind kind) => kind switch
    {
        AppUpdateKind.Host => new(
            AppUpdateKind.Host,
            DefaultRepo,
            "host-v",
            "kakimoni_host",
            "親機"),
        AppUpdateKind.Client => new(
            AppUpdateKind.Client,
            DefaultRepo,
            "client-v",
            "kakimoni_client",
            "子機"),
        AppUpdateKind.Layout => new(
            AppUpdateKind.Layout,
            DefaultRepo,
            "layout-v",
            "kakimoni_layout",
            "レイアウト専用機"),
        AppUpdateKind.SaveViewer => new(
            AppUpdateKind.SaveViewer,
            DefaultRepo,
            "saveviewer-v",
            "kakimoni_saveviewer",
            "保存一覧"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
