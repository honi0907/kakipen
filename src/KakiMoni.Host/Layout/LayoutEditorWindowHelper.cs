using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KakiMoni_Host.Layout;

public static class LayoutEditorWindowHelper
{
    private static LayoutEditorWindow? _window;

    public static void ShowOrActivate()
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

            _window = new LayoutEditorWindow();
            _window.Closed += (_, _) => _window = null;
            _window.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LayoutEditorWindowHelper] failed: {ex}");
            _window = null;
            _ = ShowOpenErrorAsync(FormatOpenError(ex));
        }
    }

    private static string FormatOpenError(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
            parts.Add(current.Message);

        return string.Join("\n", parts.Distinct());
    }

    private static async Task ShowOpenErrorAsync(string message)
    {
        try
        {
            var host = App.HostWindow;
            if (host?.Content is not FrameworkElement root || root.XamlRoot is null)
                return;

            var dialog = new ContentDialog
            {
                Title = "レイアウト編集",
                Content = $"ウィンドウを開けませんでした。\n{message}",
                CloseButtonText = "OK",
                XamlRoot = root.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { }
    }
}
