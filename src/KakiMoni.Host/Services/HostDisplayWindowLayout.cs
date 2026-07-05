using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Graphics;

namespace KakiMoni_Host.Services;

public static class HostDisplayWindowLayout
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
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] PrepareDisplayWin32FullscreenChrome failed: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] ConfigureDisplayBorderlessPresenter failed: {ex}");
        }
    }

    public static void ConfigureNormalPresenter(AppWindow appWindow)
    {
        try
        {
            if (appWindow.Presenter is OverlappedPresenter current
                && current.State == OverlappedPresenterState.Maximized)
            {
                try { current.Restore(); } catch { }
            }

            var presenter = OverlappedPresenter.Create();
            presenter.SetBorderAndTitleBar(true, true);
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.IsResizable = true;
            appWindow.SetPresenter(presenter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] ConfigureNormalPresenter failed: {ex}");
        }
    }

    public static void ApplyPrimaryMonitorFullscreen(Window window, AppWindow appWindow)
    {
        PrepareDisplayWin32FullscreenChrome(window, appWindow);
        ConfigureDisplayBorderlessPresenter(window, appWindow);

        var hwnd = WindowNative.GetWindowHandle(window);
        var bounds = HostMonitorHelper.GetPrimaryMonitorBounds(useFullMonitorBounds: true);
        if (bounds is not null)
        {
            var expanded = HostMonitorHelper.ExpandBounds(bounds.Value, overscanPixels: 2);
            HostMonitorHelper.ApplyWin32BorderlessAndBounds(hwnd, expanded, popupStyle: true);
            return;
        }

        try
        {
            var area = DisplayArea.Primary;
            appWindow.MoveAndResize(area.OuterBounds, area);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] ApplyPrimaryMonitorFullscreen fallback failed: {ex}");
        }
    }

    /// <summary>
    /// コンパネ用フルスクリーン。MainWindow では Win32 を触らない（ランチャー復帰で白画面になるため）。
    /// </summary>
    public static void ApplyCompanelFullscreen(Window window, AppWindow appWindow)
    {
        PrepareDisplayWin32FullscreenChrome(window, appWindow);
        ConfigureDisplayBorderlessPresenter(window, appWindow);

        try
        {
            var area = DisplayArea.Primary;
            appWindow.MoveAndResize(area.OuterBounds, area);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] ApplyCompanelFullscreen failed: {ex}");
        }
    }

    public static void CenterOnPrimaryWorkArea(AppWindow appWindow, int width, int height)
    {
        if (HostMonitorHelper.GetPrimaryMonitorBounds(useFullMonitorBounds: false) is not { } bounds)
            return;

        var x = bounds.Left + Math.Max(0, (bounds.Width - width) / 2);
        var y = bounds.Top + Math.Max(0, (bounds.Height - height) / 2);
        try
        {
            appWindow.Move(new PointInt32(x, y));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] CenterOnPrimaryWorkArea failed: {ex}");
        }
    }

    public static bool MoveToSecondaryMonitor(AppWindow appWindow, bool useFullMonitorBounds)
    {
        var bounds = HostMonitorHelper.GetSecondaryMonitorBounds(useFullMonitorBounds);
        if (bounds is null)
        {
            HostMonitorHelper.LogMonitors("HostDisplayWindowLayout");
            return false;
        }

        PlaceDisplayAtBounds(appWindow, bounds.Value);
        return true;
    }

    public static bool IsDisplayOnSecondary(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return HostMonitorHelper.IsOnSecondaryMonitor(hwnd);
    }

    public static int GetDisplayMonitorCount() => HostMonitorHelper.MonitorCount;

    private static void PlaceDisplayAtBounds(AppWindow appWindow, HostMonitorHelper.MonitorBounds bounds)
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
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] PlaceDisplayAtBounds failed: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] ClearWindowDragRegions failed: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"[HostDisplayWindowLayout] CollapseDisplayTitleBar failed: {ex}");
        }
    }

    private static DisplayArea? FindDisplayAreaForBounds(HostMonitorHelper.MonitorBounds bounds)
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
