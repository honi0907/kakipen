using KakiMoni_Host.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace KakiMoni_Host;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _stopInProgress;
    private bool _closingHooked;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        Title = "KakiMoni 親機";
        AppTitleBar.Title = "KakiMoni 親機";
        if (AppWindow is not null)
        {
            CompanelWindowHelper.EnsureLauncherWindowSize(this);
        }
        RootFrame.Navigate(typeof(MainPage));
        HookClosing();
        Activated += OnWindowActivated;
    }

    private void HookClosing()
    {
        AppWindow.Closing -= OnAppWindowClosing;
        AppWindow.Closing += OnAppWindowClosing;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_closingHooked)
            return;

        _closingHooked = true;
        HookClosing();
    }

    public async Task ShowBusyOverlayAsync(string message)
    {
        await EnqueueAsync(() => ShowOverlay(message));
        await WaitForOverlayRenderAsync();
    }

    public void HideBusyOverlay() =>
        DispatcherQueue.TryEnqueue(HideOverlay);

    public async Task StopServerWithOverlayAsync()
    {
        if (!AppHostContext.Server.IsRunning || _stopInProgress)
            return;

        await StopServerInternalAsync(closeAfter: false);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !AppHostContext.Server.IsRunning)
            return;

        args.Cancel = true;
        if (_stopInProgress)
            return;

        ShowOverlay("サーバー停止中...");
        _ = StopServerInternalAsync(closeAfter: true);
    }

    private async Task StopServerInternalAsync(bool closeAfter)
    {
        if (_stopInProgress)
        {
            if (closeAfter)
                await EnqueueAsync(CloseWindow);
            return;
        }

        _stopInProgress = true;
        if (StopOverlay.Visibility != Visibility.Visible)
            await EnqueueAsync(() => ShowOverlay("サーバー停止中..."));

        await WaitForOverlayRenderAsync();

        try
        {
            await AppHostContext.Server.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await EnqueueAsync(() =>
            {
                HideOverlay();
                _stopInProgress = false;
                if (closeAfter)
                    CloseWindow();
            });
        }
    }

    private async Task WaitForOverlayRenderAsync()
    {
        await EnqueueAsync(() => StopOverlay.UpdateLayout());
        await Task.Delay(50).ConfigureAwait(false);
    }

    private void CloseWindow()
    {
        _allowClose = true;
        Close();
    }

    private void ShowOverlay(string message)
    {
        OverlayMessageText.Text = message;
        StopProgressRing.IsActive = true;
        StopOverlay.Visibility = Visibility.Visible;
        StopOverlay.UpdateLayout();
    }

    private void HideOverlay()
    {
        StopProgressRing.IsActive = false;
        StopOverlay.Visibility = Visibility.Collapsed;
    }

    private Task EnqueueAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }))
        {
            tcs.TrySetException(new InvalidOperationException("UI dispatcher is unavailable."));
        }

        return tcs.Task;
    }
}
