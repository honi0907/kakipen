using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace KakiMoni_Host.Services;

public static class CompanelWindowHelper
{
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
}
