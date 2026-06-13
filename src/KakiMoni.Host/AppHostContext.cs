namespace KakiMoni_Host;

public static class AppHostContext
{
    public static Services.ServerHostService Server { get; } = new();
}
