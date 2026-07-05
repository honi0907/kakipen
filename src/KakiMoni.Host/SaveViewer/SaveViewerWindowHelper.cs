using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KakiMoni_Host.SaveViewer;

public static class SaveViewerWindowHelper
{
    private static SaveViewerWindow? _window;    private static bool _standaloneMode;

    public static void ShowOrActivate()
    {
        _standaloneMode = false;
        EnqueueShow();
    }

    public static void ShowStandalone()
    {
        _standaloneMode = true;
        EnqueueShow();
    }

    /// <summary>--save-viewer 起動。OnLaunched 上で MainWindow なしで直接開く。</summary>
    public static void LaunchStandalone()
    {
        _standaloneMode = true;
        ShowOrActivateCore();
    }

    private static void EnqueueShow()
    {
        var queue = App.HostWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (!queue.TryEnqueue(DispatcherQueuePriority.Normal, ShowOrActivateCore))
            ShowOrActivateCore();
    }

    private static void ShowOrActivateCore()
    {
        try
        {
            if (_window is not null)
            {
                _window.Activate();
                return;
            }

            _window = new SaveViewerWindow();
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveViewerWindowHelper] failed: {ex}");
            _window = null;
            if (_standaloneMode)
                ShowStandaloneError(ex);
            else
                _ = ShowOpenErrorAsync(ex.Message);
        }
    }

    public static void ShowStandaloneErrorMessage(string message)
    {
        try
        {
            MessageBoxW(
                IntPtr.Zero,
                message,
                "KakiMoni 保存データ一覧",
                0x00000010);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static void ShowStandaloneError(Exception ex)
    {
        try
        {
            MessageBoxW(
                IntPtr.Zero,
                $"保存データ一覧を開けませんでした。\n\n{ex.Message}",
                "KakiMoni 保存データ一覧",
                0x00000010);
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            Application.Current.Exit();
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
        if (_standaloneMode)
            Application.Current.Exit();
    }

    private static async Task ShowOpenErrorAsync(string message)
    {
        try
        {
            FrameworkElement? root = null;
            if (_window?.Content is FrameworkElement saveRoot)
                root = saveRoot;
            else if (App.HostWindow?.Content is FrameworkElement hostRoot)
                root = hostRoot;

            if (root?.XamlRoot is null)
                return;

            var dialog = new ContentDialog
            {
                Title = "保存データ一覧",
                Content = $"ウィンドウを開けませんでした。\n{message}",
                CloseButtonText = "OK",
                XamlRoot = root.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { }
    }
}
