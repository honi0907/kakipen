namespace KakiMoni_Client;

using KakiMoni_Client.Services;

public static class AppServices
{
    public static Services.ClientHubService? Hub { get; set; }
    public static Services.DisplayHubService? DisplayHub { get; set; }
    public static Services.DisplayOutputService DisplayOutput { get; } = new();
    public static Services.BackgroundImageService BackgroundImages { get; } = new();
    public static LaunchProgress? LaunchProgress { get; set; }

    /// <summary>外部出力起動時に書き画面の状態をコピーするためのスナップショット取得。</summary>
    public static Func<DisplaySyncSnapshot?>? GetDisplaySyncSnapshot { get; set; }

    private static TaskCompletionSource<bool>? _writingPageReady;

    public static void BeginWritingPageLaunch() =>
        _writingPageReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    public static Task WaitForWritingPageReadyAsync(CancellationToken cancellationToken = default)
    {
        var tcs = _writingPageReady ?? throw new InvalidOperationException("Writing page launch was not started.");
        return tcs.Task.WaitAsync(cancellationToken);
    }

    public static void CompleteWritingPageLaunch() =>
        _writingPageReady?.TrySetResult(true);
}
