namespace KakiMoni.Core.Updates;

public static class AppVersionDisplay
{
    public static string Label => $"v{AppVersionReader.GetCurrentVersion()}";
}
