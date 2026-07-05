using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace KakiMoni_Host.Services;

public static class CompanelWindowHelper
{
    private const int LauncherWidth = 920;
    private const int LauncherHeight = 600;

    public static void EnsureCompanelWindowSize(Window? window)
    {
        if (window is null) return;

        var appWindow = window.AppWindow;
        var size = appWindow.Size;
        if (size.Width < CompanelLayoutMetrics.DesignWidth || size.Height < CompanelLayoutMetrics.DesignHeight)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(
                CompanelLayoutMetrics.DesignWidth,
                CompanelLayoutMetrics.DesignHeight));
        }
    }

    public static void EnsureLauncherWindowSize(Window? window)
    {
        if (window is null) return;

        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(LauncherWidth, LauncherHeight));
    }
}
