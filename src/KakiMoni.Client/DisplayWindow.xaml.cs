using KakiMoni_Client.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Windows.UI;

namespace KakiMoni_Client;

public sealed partial class DisplayWindow : Window
{
    public DisplayWindow()
    {
        InitializeComponent();
        Title = "KakiMoni 外部出力";
        ExtendsContentIntoTitleBar = false;
    }

    /// <summary>
    /// フルスクリーン時は Win32 のみで配置（MoveAndResize / OverlappedPresenter は DWM 枠を復活させるため使わない）。
    /// </summary>
    public void ShowOnDisplay()
    {
        var autoPlacement = AppState.ExternalAutoPlacement;
        var fullscreen = AppState.ExternalFullscreen;
        var useBorderless = fullscreen || autoPlacement;
        var hwnd = WindowNative.GetWindowHandle(this);
        var appWindow = ClientWindowLayout.GetAppWindow(this);

        if (!useBorderless)
            ClientWindowLayout.ConfigureDisplayNormalPresenter(appWindow);
        else if (fullscreen)
            ClientWindowLayout.PrepareDisplayWin32FullscreenChrome(this, appWindow);
        else
            ClientWindowLayout.ConfigureDisplayBorderlessPresenter(this, appWindow);

        appWindow.Show();

        if (autoPlacement)
        {
            var placementBounds = MonitorHelper.GetSecondaryMonitorBounds(fullscreen);
            if (placementBounds is not null)
            {
                if (fullscreen)
                {
                    var bounds = MonitorHelper.ExpandBounds(placementBounds.Value, overscanPixels: 2);
                    var win32Placed = MonitorHelper.ApplyWin32BorderlessAndBounds(hwnd, bounds, popupStyle: true);
                    System.Diagnostics.Debug.WriteLine(
                        $"[DisplayWindow] Win32 fullscreen placed={win32Placed} {MonitorHelper.DescribePlacement(hwnd, fullscreen)}");
                }
                else
                {
                    ClientWindowLayout.MoveToSecondaryMonitor(appWindow, useFullMonitorBounds: false);
                    MonitorHelper.ApplyWin32Borderless(hwnd, popupStyle: false);
                    MonitorHelper.SetWindowBounds(hwnd, placementBounds.Value);
                }
            }

            UpdatePlacementStatus();
        }
        else
        {
            Activate();
            if (useBorderless)
                MonitorHelper.ApplyWin32Borderless(hwnd, popupStyle: fullscreen);
        }
    }

    public void UpdatePlacementStatus()
    {
        var onSecondary = ClientWindowLayout.IsDisplayOnSecondary(this);
        Title = onSecondary
            ? "KakiMoni 外部出力"
            : ClientWindowLayout.GetDisplayMonitorCount() <= 1
                ? "KakiMoni 外部出力（ディスプレイ1台のみ）"
                : "KakiMoni 外部出力（拡張ディスプレイへ移動できませんでした）";
    }

    public void SetMirrorImage(ImageSource source)
    {
        MirrorImage.Source = source;
    }

    public void SetCoverLogo(ImageSource? source)
    {
        if (source is null)
        {
            CoverPanel.Background = new SolidColorBrush(Color.FromArgb(255, 0x0d, 0x1b, 0x2a));
            return;
        }

        CoverPanel.Background = new ImageBrush
        {
            ImageSource = source,
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
    }

    public void SetCoverVisible(bool visible)
    {
        CoverPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        MirrorHost.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
    }
}
