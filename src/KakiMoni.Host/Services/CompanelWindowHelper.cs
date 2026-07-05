using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace KakiMoni_Host.Services;

public static class CompanelWindowHelper
{
    private const int LauncherWidth = 920;
    private const int LauncherHeight = 600;

    private static SizeInt32? _savedWindowedSize;
    private static PointInt32? _savedWindowedPosition;
    private static bool _companelFullscreenActive;

    public static void EnsureCompanelWindowSize(Window? window) =>
        ApplyCompanelWindowMode(window);

    public static void ApplyCompanelWindowMode(Window? window)
    {
        if (window is null)
            return;

        if (HostSettingsStore.Load().CompanelFullscreen)
            SetCompanelFullscreen(window);
        else
            SetCompanelWindowed(window);
    }

    public static void EnsureLauncherWindowSize(Window? window)
    {
        if (window is null)
            return;

        RestoreFromCompanelFullscreen(window);
        window.AppWindow.Resize(new SizeInt32(LauncherWidth, LauncherHeight));
        HostDisplayWindowLayout.CenterOnPrimaryWorkArea(window.AppWindow, LauncherWidth, LauncherHeight);
    }

    private static void SetCompanelFullscreen(Window window)
    {
        if (_companelFullscreenActive)
            return;

        var appWindow = window.AppWindow;
        _savedWindowedSize = appWindow.Size;
        _savedWindowedPosition = appWindow.Position;

        if (window is MainWindow mainWindow)
            mainWindow.ApplyCompanelFullscreenChrome(true);

        try
        {
            HostDisplayWindowLayout.ApplyCompanelFullscreen(window, appWindow);
            _companelFullscreenActive = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CompanelWindowHelper] FullScreen failed: {ex.Message}");
            RestoreFromCompanelFullscreen(window);
            SetCompanelWindowed(window);
        }
    }

    private static void SetCompanelWindowed(Window window)
    {
        if (_companelFullscreenActive)
            RestoreFromCompanelFullscreen(window);

        var appWindow = window.AppWindow;
        var size = _savedWindowedSize ?? new SizeInt32(
            CompanelLayoutMetrics.DesignWidth,
            CompanelLayoutMetrics.DesignHeight);
        if (size.Width < CompanelLayoutMetrics.DesignWidth
            || size.Height < CompanelLayoutMetrics.DesignHeight)
        {
            size = new SizeInt32(
                CompanelLayoutMetrics.DesignWidth,
                CompanelLayoutMetrics.DesignHeight);
        }

        appWindow.Resize(size);
        if (_savedWindowedPosition is { } position)
        {
            try
            {
                appWindow.Move(position);
            }
            catch
            {
                // 位置復元は任意
            }
        }
    }

    private static void RestoreFromCompanelFullscreen(Window window)
    {
        HostDisplayWindowLayout.ConfigureNormalPresenter(window.AppWindow);

        if (window is MainWindow mainWindow)
            mainWindow.RestoreLauncherPresentation();

        _companelFullscreenActive = false;
    }
}
