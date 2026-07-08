using KakiMoni.Core.Display;
using KakiMoni.Core.Models;
using KakiMoni_Layout.Controls;
using KakiMoni_Layout.Models;
using KakiMoni_Layout.Drawing;
using KakiMoni_Layout.Services;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KakiMoni_Layout.Display;

public sealed partial class LayoutDisplayCellView : UserControl
{
    private readonly LayoutImageLoader _images = new();
    private bool _canvasReady;
    private string? _loadedBgUrl;
    private string? _loadedChoiceUrl;
    private string? _loadedOverlayUrl;
    private CancellationTokenSource? _bgCts;
    private CancellationTokenSource? _overlayCts;
    private int _bgDecodeWidth;
    private int _choiceDecodeWidth;
    private int _overlayDecodeWidth;

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(SeatDisplayModel), typeof(LayoutDisplayCellView),
            new PropertyMetadata(null, OnModelChanged));

    public static readonly DependencyProperty FillColorArgbProperty =
        DependencyProperty.Register(nameof(FillColorArgb), typeof(uint), typeof(LayoutDisplayCellView),
            new PropertyMetadata(HostDisplayPanelColors.EmptySeatColor, OnFillColorChanged));

    public SeatDisplayModel? Model
    {
        get => (SeatDisplayModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public uint FillColorArgb
    {
        get => (uint)GetValue(FillColorArgbProperty);
        set => SetValue(FillColorArgbProperty, value);
    }

    public LayoutDisplayCellView()
    {
        InitializeComponent();
        SizeChanged += OnCellSizeChanged;
        Loaded += (_, _) =>
        {
            _canvasReady = true;
            ApplyFillColor();
            PreviewCanvas.Invalidate();
            _ = UpdateBackgroundAsync();
            _ = UpdateChoiceOverlayAsync();
            _ = UpdateJudgeOverlayAsync();
        };
        Unloaded += (_, _) =>
        {
            _canvasReady = false;
            _bgCts?.Cancel();
            _overlayCts?.Cancel();
        };
    }

    private static void OnFillColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LayoutDisplayCellView view)
            view.ApplyFillColor();
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LayoutDisplayCellView view) return;
        if (e.OldValue is SeatDisplayModel oldModel)
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
        if (e.NewValue is SeatDisplayModel newModel)
            newModel.PropertyChanged += view.OnModelPropertyChanged;
        view.RefreshUi();
        _ = view.UpdateBackgroundAsync();
        _ = view.UpdateChoiceOverlayAsync();
        _ = view.UpdateJudgeOverlayAsync();
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SeatDisplayModel.Strokes)
            or nameof(SeatDisplayModel.CurrentStroke)
            or nameof(SeatDisplayModel.IsConnected)
            or nameof(SeatDisplayModel.RawSeatName))
            RequestPreviewRefresh();

        if (e.PropertyName is nameof(SeatDisplayModel.BgImageUrl))
            _ = UpdateBackgroundAsync();
        if (e.PropertyName is nameof(SeatDisplayModel.ChoiceImageUrl))
            _ = UpdateChoiceOverlayAsync();
        if (e.PropertyName is nameof(SeatDisplayModel.OverlayImageUrl))
            _ = UpdateJudgeOverlayAsync();
        if (e.PropertyName is nameof(SeatDisplayModel.RawSeatName))
            UpdateSeatNameOverlay();
    }

    private void ApplyFillColor()
    {
        var argb = FillColorArgb;
        RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(
            255,
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF)));
    }

    public void RefreshUi()
    {
        if (Model is null)
        {
            DisconnectedOverlay.Visibility = Visibility.Visible;
            return;
        }

        DisconnectedOverlay.Visibility = Model.IsConnected ? Visibility.Collapsed : Visibility.Visible;
        UpdateSeatNameOverlay();
        RequestPreviewRefresh();
    }

    private void UpdateSeatNameOverlay()
    {
        var seatId = Model?.SeatId ?? 1;
        SeatNameOverlayUi.Apply(
            SeatNameOverlay,
            SeatNameOverlayText,
            SeatNameOverlayUi.GetStyle(seatId),
            Model?.RawSeatName,
            ActualWidth,
            ActualHeight);
    }

    private void RequestPreviewRefresh()
    {
        if (_canvasReady)
            PreviewCanvas.Invalidate();
    }

    private int GetDecodeWidth() =>
        DisplayCellImageDecode.ResolveDecodeWidth(
            ActualWidth,
            ActualHeight,
            XamlRoot?.RasterizationScale ?? 1.0);

    private void OnCellSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSeatNameOverlay();
        if (!_canvasReady)
            return;

        var target = GetDecodeWidth();
        if (target <= _bgDecodeWidth && target <= _choiceDecodeWidth && target <= _overlayDecodeWidth)
            return;

        _ = UpdateBackgroundAsync();
        _ = UpdateChoiceOverlayAsync();
        _ = UpdateJudgeOverlayAsync();
    }

    private async Task UpdateBackgroundAsync()
    {
        var url = Model?.BgImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedBgUrl = null;
            _bgDecodeWidth = 0;
            BackgroundImage.Source = null;
            return;
        }

        var decodeWidth = GetDecodeWidth();
        if (string.Equals(_loadedBgUrl, url, StringComparison.OrdinalIgnoreCase)
            && BackgroundImage.Source is not null
            && _bgDecodeWidth >= decodeWidth)
            return;

        _bgCts?.Cancel();
        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;
        var thumb = await _images.LoadThumbnailAsync(url, decodeWidth);
        if (token.IsCancellationRequested || Model?.BgImageUrl != url) return;
        _loadedBgUrl = url;
        _bgDecodeWidth = decodeWidth;
        BackgroundImage.Source = thumb;
    }

    private async Task UpdateChoiceOverlayAsync()
    {
        var url = Model?.ChoiceImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedChoiceUrl = null;
            _choiceDecodeWidth = 0;
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            return;
        }

        var decodeWidth = GetDecodeWidth();
        if (string.Equals(_loadedChoiceUrl, url, StringComparison.OrdinalIgnoreCase)
            && ChoiceOverlayImage.Source is not null
            && _choiceDecodeWidth >= decodeWidth)
            return;

        var thumb = await _images.LoadThumbnailAsync(url, decodeWidth);
        if (Model?.ChoiceImageUrl != url) return;
        _loadedChoiceUrl = url;
        _choiceDecodeWidth = decodeWidth;
        ChoiceOverlayImage.Source = thumb;
        ChoiceOverlayImage.Visibility = thumb is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task UpdateJudgeOverlayAsync()
    {
        var url = Model?.OverlayImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedOverlayUrl = null;
            _overlayDecodeWidth = 0;
            _overlayCts?.Cancel();
            FillJudgeOverlayImage.Source = null;
            FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            JudgeOverlayImage.Source = null;
            JudgeOverlayImage.Visibility = Visibility.Collapsed;
            return;
        }

        var decodeWidth = GetDecodeWidth();
        if (string.Equals(_loadedOverlayUrl, url, StringComparison.OrdinalIgnoreCase)
            && (FillJudgeOverlayImage.Source is not null || JudgeOverlayImage.Source is not null)
            && _overlayDecodeWidth >= decodeWidth)
            return;

        _overlayCts?.Cancel();
        _overlayCts = new CancellationTokenSource();
        var token = _overlayCts.Token;

        if (SeatPreviewRenderer.IsFillOverlayUrl(url))
        {
            var thumb = await _images.LoadThumbnailAsync(url, decodeWidth);
            if (token.IsCancellationRequested || Model?.OverlayImageUrl != url) return;

            _loadedOverlayUrl = url;
            _overlayDecodeWidth = decodeWidth;
            JudgeOverlayImage.Source = null;
            JudgeOverlayImage.Visibility = Visibility.Collapsed;
            if (thumb is null)
            {
                FillJudgeOverlayImage.Source = null;
                FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                FillJudgeOverlayImage.Source = thumb;
                FillJudgeOverlayImage.Visibility = Visibility.Visible;
            }

            RequestPreviewRefresh();
            return;
        }

        var judgeThumb = await _images.LoadThumbnailAsync(url, decodeWidth);
        if (token.IsCancellationRequested || Model?.OverlayImageUrl != url) return;

        _loadedOverlayUrl = url;
        _overlayDecodeWidth = decodeWidth;
        FillJudgeOverlayImage.Source = null;
        FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
        if (judgeThumb is null)
        {
            JudgeOverlayImage.Source = null;
            JudgeOverlayImage.Visibility = Visibility.Collapsed;
        }
        else
        {
            JudgeOverlayImage.Source = judgeThumb;
            JudgeOverlayImage.Visibility = Visibility.Visible;
        }

        RequestPreviewRefresh();
    }

    private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var w = (float)sender.ActualWidth;
        var h = (float)sender.ActualHeight;
        args.DrawingSession.Clear(Microsoft.UI.Colors.Transparent);
        if (Model is null) return;
        SeatPreviewRenderer.DrawStrokes(args.DrawingSession, Model, w, h);
    }
}
