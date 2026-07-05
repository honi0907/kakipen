using KakiMoni.Core.Models;
using KakiMoni.Core.Models;
using KakiMoni_Host.Controls;
using KakiMoni_Host.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace KakiMoni_Host.Display;

public sealed partial class HostDisplayWindow : Window
{
    private readonly HostImageLoader _images = new();
    private HostDisplayLayout? _layout;
    private IReadOnlyDictionary<int, SeatCardModel>? _seats;
    private string? _loadedBgUrl;

    public HostDisplayWindow()
    {
        InitializeComponent();
        Title = "KakiMoni 親機外部出力";
        ExtendsContentIntoTitleBar = false;
        RootGrid.SizeChanged += (_, _) => RebuildCells();
    }

    public void ShowOnDisplay()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var appWindow = HostDisplayWindowLayout.GetAppWindow(this);
        HostDisplayWindowLayout.PrepareDisplayWin32FullscreenChrome(this, appWindow);
        appWindow.Show();

        var placementBounds = HostMonitorHelper.GetSecondaryMonitorBounds(useFullMonitorBounds: true);
        if (placementBounds is not null)
        {
            var bounds = HostMonitorHelper.ExpandBounds(placementBounds.Value, overscanPixels: 2);
            HostMonitorHelper.ApplyWin32BorderlessAndBounds(hwnd, bounds, popupStyle: true);
        }
        else
        {
            HostMonitorHelper.ApplyWin32Borderless(hwnd, popupStyle: true);
        }

        UpdatePlacementStatus();
    }

    public void UpdatePlacementStatus()
    {
        Title = HostDisplayWindowLayout.IsDisplayOnSecondary(this)
            ? "KakiMoni 親機外部出力"
            : HostDisplayWindowLayout.GetDisplayMonitorCount() <= 1
                ? "KakiMoni 親機外部出力（ディスプレイ1台のみ）"
                : "KakiMoni 親機外部出力（拡張ディスプレイへ移動できませんでした）";
    }

    public void BindSeats(IReadOnlyDictionary<int, SeatCardModel> seats)
    {
        _seats = seats;
        RebuildCells();
    }

    public void ApplyLayout(HostDisplayLayout? layout)
    {
        _layout = layout;
        WaitingPanel.Visibility = layout is { HasCells: true } ? Visibility.Collapsed : Visibility.Visible;
        CellsCanvas.Visibility = layout is { HasCells: true } ? Visibility.Visible : Visibility.Collapsed;
        _ = UpdateBackgroundAsync(layout?.BackgroundUrl);
        RebuildCells();
    }

    private async Task UpdateBackgroundAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedBgUrl = null;
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
            return;
        }

        if (string.Equals(_loadedBgUrl, url, StringComparison.OrdinalIgnoreCase) && BackgroundImage.Source is not null)
            return;

        var thumb = await _images.LoadThumbnailAsync(url, 1920);
        if (_layout?.BackgroundUrl != url) return;
        _loadedBgUrl = url;
        BackgroundImage.Source = thumb;
        BackgroundImage.Visibility = thumb is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RebuildCells()
    {
        CellsCanvas.Children.Clear();
        if (_layout is not { HasCells: true })
            return;

        var w = RootGrid.ActualWidth;
        var h = RootGrid.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        foreach (var cell in _layout.Cells.OrderBy(c => c.ZIndex))
        {
            SeatCardModel? model = null;
            if (cell.SeatId is >= 1 and <= 10 && _seats is not null)
                _seats.TryGetValue(cell.SeatId.Value, out model);

            var fillColor = HostDisplayPanelColors.Resolve(cell.FillColorArgb, cell.SeatId);
            var view = new HostDisplayCellView
            {
                Model = model,
                FillColorArgb = fillColor
            };
            var left = cell.X / 100.0 * w;
            var top = cell.Y / 100.0 * h;
            var width = Math.Max(1, cell.W / 100.0 * w);
            var height = Math.Max(1, cell.H / 100.0 * h);

            Canvas.SetLeft(view, left);
            Canvas.SetTop(view, top);
            Canvas.SetZIndex(view, cell.ZIndex);
            view.Width = width;
            view.Height = height;
            CellsCanvas.Children.Add(view);
        }
    }
}
