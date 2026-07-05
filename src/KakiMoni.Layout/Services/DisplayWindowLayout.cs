using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Graphics;

namespace KakiMoni_Layout.Services;

public static class DisplayWindowLayout
{
    public static AppWindow GetAppWindow(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void PrepareDisplayWin32FullscreenChrome(Window window, AppWindow appWindow)
    {
        try
        {
            ClearWindowDragRegions(window, appWindow);
            CollapseDisplayTitleBar(appWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindowLayout] PrepareDisplayWin32FullscreenChrome failed: {ex}");
        }
    }

    public static void ConfigureDisplayBorderlessPresenter(Window window, AppWindow appWindow)
    {
        try
        {
            ClearWindowDragRegions(window, appWindow);
            CollapseDisplayTitleBar(appWindow);

            var presenter = OverlappedPresenter.Create();
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            appWindow.SetPresenter(presenter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindowLayout] ConfigureDisplayBorderlessPresenter failed: {ex}");
        }
    }

    public static bool MoveToSecondaryMonitor(AppWindow appWindow, bool useFullMonitorBounds)
    {
        var bounds = MonitorHelper.GetSecondaryMonitorBounds(useFullMonitorBounds);
        if (bounds is null)
        {
            MonitorHelper.LogMonitors("DisplayWindowLayout");
            return false;
        }

        PlaceDisplayAtBounds(appWindow, bounds.Value);
        return true;
    }

    public static bool IsDisplayOnSecondary(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return MonitorHelper.IsOnSecondaryMonitor(hwnd);
    }

    public static int GetDisplayMonitorCount() => MonitorHelper.MonitorCount;

    private static void PlaceDisplayAtBounds(AppWindow appWindow, MonitorHelper.MonitorBounds bounds)
    {
        try
        {
            var rect = new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            var targetArea = FindDisplayAreaForBounds(bounds);

            if (targetArea is not null)
                appWindow.MoveAndResize(rect, targetArea);
            else
                appWindow.MoveAndResize(rect);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindowLayout] PlaceDisplayAtBounds failed: {ex}");
        }
    }

    private static void ClearWindowDragRegions(Window window, AppWindow appWindow)
    {
        try
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = false;
            if (AppWindowTitleBar.IsCustomizationSupported())
                titleBar.SetDragRectangles(Array.Empty<RectInt32>());

            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            InputNonClientPointerSource
                .GetForWindowId(windowId)
                .SetRegionRects(NonClientRegionKind.Caption, Array.Empty<RectInt32>());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindowLayout] ClearWindowDragRegions failed: {ex}");
        }
    }

    private static void CollapseDisplayTitleBar(AppWindow appWindow)
    {
        try
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = false;
            if (AppWindowTitleBar.IsCustomizationSupported())
                titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindowLayout] CollapseDisplayTitleBar failed: {ex}");
        }
    }

    private static DisplayArea? FindDisplayAreaForBounds(MonitorHelper.MonitorBounds bounds)
    {
        try
        {
            var centerX = bounds.Left + bounds.Width / 2;
            var centerY = bounds.Top + bounds.Height / 2;
            IReadOnlyList<DisplayArea> areas = DisplayArea.FindAll();
            for (var i = 0; i < areas.Count; i++)
            {
                var areaBounds = areas[i].OuterBounds;
                if (centerX >= areaBounds.X
                    && centerX < areaBounds.X + areaBounds.Width
                    && centerY >= areaBounds.Y
                    && centerY < areaBounds.Y + areaBounds.Height)
                {
                    return areas[i];
                }
            }

            for (var i = 0; i < areas.Count; i++)
            {
                if (!areas[i].IsPrimary)
                    return areas[i];
            }

            return areas.Count > 1 ? areas[1] : null;
        }
        catch
        {
            return null;
        }
    }
}
