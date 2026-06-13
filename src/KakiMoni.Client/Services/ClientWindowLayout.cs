using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Graphics;

namespace KakiMoni_Client.Services;

public static class ClientWindowLayout
{
    public static AppWindow GetAppWindow(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void ApplyWritingMainWindow(Window window, bool fullscreen)
    {
        try
        {
            if (window is MainWindow mainWindow)
                mainWindow.ApplyWritingFullscreenChrome(fullscreen);

            var appWindow = GetAppWindow(window);
            if (fullscreen)
            {
                ClearWindowDragRegions(window, appWindow);
                ApplyBorderlessMaximized(appWindow);
                return;
            }

            RestoreMainWindow(appWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] ApplyWritingMainWindow failed: {ex}");
        }
    }

    public static void RestoreMainWindow(Window window)
    {
        try
        {
            if (window is MainWindow mainWindow)
                mainWindow.ApplyWritingFullscreenChrome(false);

            RestoreMainWindow(GetAppWindow(window));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] RestoreMainWindow failed: {ex}");
        }
    }

    /// <summary>フルスクリーン Win32 配置用。OverlappedPresenter は触らない（MoveAndResize も使わない）。</summary>
    public static void PrepareDisplayWin32FullscreenChrome(Window window, AppWindow appWindow)
    {
        try
        {
            ClearWindowDragRegions(window, appWindow);
            CollapseDisplayTitleBar(appWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] PrepareDisplayWin32FullscreenChrome failed: {ex}");
        }
    }

    /// <summary>外部出力ウィンドウの WinUI 側ボーダーレス設定（Win32 は呼び出し元で最後に1回）。</summary>
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
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] ConfigureDisplayBorderlessPresenter failed: {ex}");
        }
    }

    /// <summary>外部出力ウィンドウを通常の枠付きウィンドウに戻す。</summary>
    public static void ConfigureDisplayNormalPresenter(AppWindow appWindow)
    {
        try
        {
            var normal = OverlappedPresenter.Create();
            normal.SetBorderAndTitleBar(true, true);
            normal.IsMaximizable = true;
            normal.IsMinimizable = true;
            normal.IsResizable = true;
            appWindow.SetPresenter(normal);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] ConfigureDisplayNormalPresenter failed: {ex}");
        }
    }

    /// <summary>WinUI の MoveAndResize のみ。Win32 は呼び出し元で最後に適用する。</summary>
    public static bool MoveToSecondaryMonitor(AppWindow appWindow, bool useFullMonitorBounds)
    {
        var bounds = MonitorHelper.GetSecondaryMonitorBounds(useFullMonitorBounds);
        if (bounds is null)
        {
            MonitorHelper.LogMonitors("ClientWindowLayout");
            LogDisplayAreas();
            return false;
        }

        PlaceDisplayAtBounds(appWindow, bounds.Value);
        return true;
    }

    public static string DescribeDisplayPlacement(Window window, bool fullscreen)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return MonitorHelper.DescribePlacement(hwnd, fullscreen);
    }

    public static bool IsDisplayOnSecondary(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return MonitorHelper.IsOnSecondaryMonitor(hwnd);
    }

    public static int GetDisplayMonitorCount() => MonitorHelper.MonitorCount;

    private static void ApplyBorderlessMaximized(AppWindow appWindow)
    {
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        appWindow.SetPresenter(presenter);
        try { presenter.Maximize(); } catch { }
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
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] ClearWindowDragRegions failed: {ex}");
        }
    }

    private static void RestoreMainWindow(AppWindow appWindow)
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
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] PlaceDisplayAtBounds failed: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] CollapseDisplayTitleBar failed: {ex}");
        }
    }

    private static void LogDisplayAreas()
    {
        try
        {
            var areas = DisplayArea.FindAll();
            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                var bounds = area.OuterBounds;
                System.Diagnostics.Debug.WriteLine(
                    $"[ClientWindowLayout] DisplayArea {i}: primary={area.IsPrimary} id={area.DisplayId.Value} bounds={bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] LogDisplayAreas failed: {ex}");
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientWindowLayout] FindDisplayAreaForBounds failed: {ex}");
            return null;
        }
    }
}
