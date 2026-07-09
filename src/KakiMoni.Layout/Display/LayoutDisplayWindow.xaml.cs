using KakiMoni.Core.Models;
using KakiMoni_Layout.Models;
using KakiMoni_Layout.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace KakiMoni_Layout.Display;

public sealed partial class LayoutDisplayWindow : Window
{
    private readonly LayoutImageLoader _images;
    private readonly int _slotNumber;
    private HostDisplayLayout? _layout;
    private IReadOnlyDictionary<int, SeatDisplayModel>? _seats;
    private string? _loadedBgUrl;

    public LayoutDisplayWindow(int slotNumber)
    {
        _slotNumber = slotNumber;
        _images = new LayoutImageLoader();
        InitializeComponent();
        Title = $"KakiMoni レイアウト出力 [{slotNumber}]";
        ExtendsContentIntoTitleBar = false;
        RootGrid.SizeChanged += (_, _) => RebuildCells();
    }

    public void ShowOnDisplay(int monitorIndex)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var appWindow = DisplayWindowLayout.GetAppWindow(this);
        DisplayWindowLayout.PrepareDisplayWin32FullscreenChrome(this, appWindow);
        appWindow.Show();

        var placementBounds = MonitorHelper.GetMonitorBoundsByIndex(monitorIndex, useFullMonitorBounds: true)
            ?? MonitorHelper.GetSecondaryMonitorBounds(useFullMonitorBounds: true);

        if (placementBounds is not null)
        {
            var bounds = MonitorHelper.ExpandBounds(placementBounds.Value, overscanPixels: 2);
            MonitorHelper.ApplyWin32BorderlessAndBounds(hwnd, bounds, popupStyle: true);
        }
        else
        {
            MonitorHelper.ApplyWin32Borderless(hwnd, popupStyle: true);
        }

        Title = $"KakiMoni レイアウト出力 [{_slotNumber}]";
    }

    public void BindSeats(IReadOnlyDictionary<int, SeatDisplayModel> seats)
    {
        _seats = seats;
        // 席モデルは常に同一インスタンス。セル未構築のときだけ生成する。
        if (CellsCanvas.Children.Count == 0)
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
            SeatDisplayModel? model = null;
            if (cell.SeatId is >= 1 and <= 10 && _seats is not null)
                _seats.TryGetValue(cell.SeatId.Value, out model);

            var fillColor = HostDisplayPanelColors.Resolve(cell.FillColorArgb, cell.SeatId);
            var view = new LayoutDisplayCellView
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

    public void RefreshSeatNameOverlays()
    {
        foreach (var child in CellsCanvas.Children)
        {
            if (child is LayoutDisplayCellView cell)
                cell.RefreshUi();
        }
    }
}
