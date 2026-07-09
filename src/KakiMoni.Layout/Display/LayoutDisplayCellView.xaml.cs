using KakiMoni.Core.Display;
using KakiMoni.Core.Models;
using KakiMoni_Layout.Controls;
using KakiMoni_Layout.Models;
using KakiMoni_Layout.Drawing;
using KakiMoni_Layout.Services;
using Microsoft.Graphics.Canvas;
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
    private CanvasBitmap? _fillOverlayBitmap;
    private CanvasBitmap? _judgeOverlayBitmap;

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
            ClearJudgeOverlayBitmaps();
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
        if (target <= _bgDecodeWidth && target <= _choiceDecodeWidth)
        {
            if (_fillOverlayBitmap is not null || _judgeOverlayBitmap is not null)
                RequestPreviewRefresh();
            return;
        }

        _ = UpdateBackgroundAsync();
        _ = UpdateChoiceOverlayAsync();
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

    private void ClearJudgeOverlayBitmaps()
    {
        _fillOverlayBitmap?.Dispose();
        _judgeOverlayBitmap?.Dispose();
        _fillOverlayBitmap = null;
        _judgeOverlayBitmap = null;
        _loadedOverlayUrl = null;
        FillJudgeOverlayImage.Source = null;
        FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
        JudgeOverlayImage.Source = null;
        JudgeOverlayImage.Visibility = Visibility.Collapsed;
    }

    private async Task UpdateJudgeOverlayAsync()
    {
        var url = Model?.OverlayImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _overlayCts?.Cancel();
            ClearJudgeOverlayBitmaps();
            RequestPreviewRefresh();
            return;
        }

        if (string.Equals(_loadedOverlayUrl, url, StringComparison.OrdinalIgnoreCase)
            && (_fillOverlayBitmap is not null || _judgeOverlayBitmap is not null))
            return;

        _overlayCts?.Cancel();
        _overlayCts = new CancellationTokenSource();
        var token = _overlayCts.Token;

        var bitmap = await _images.LoadCanvasBitmapAsync(url, token);
        if (token.IsCancellationRequested || Model?.OverlayImageUrl != url)
        {
            bitmap?.Dispose();
            return;
        }

        ClearJudgeOverlayBitmaps();
        _loadedOverlayUrl = url;

        if (bitmap is null)
        {
            RequestPreviewRefresh();
            return;
        }

        if (SeatPreviewRenderer.IsFillOverlayUrl(url))
            _fillOverlayBitmap = bitmap;
        else
            _judgeOverlayBitmap = bitmap;

        RequestPreviewRefresh();
    }

    private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var w = (float)sender.ActualWidth;
        var h = (float)sender.ActualHeight;
        args.DrawingSession.Clear(Microsoft.UI.Colors.Transparent);
        if (Model is null || w <= 1 || h <= 1)
            return;

        if (_fillOverlayBitmap is not null)
            DrawCanvasImageUniformToFill(args.DrawingSession, _fillOverlayBitmap, w, h);

        SeatPreviewRenderer.DrawStrokes(args.DrawingSession, Model, w, h);

        if (_judgeOverlayBitmap is not null)
            DrawCanvasImageUniform(args.DrawingSession, _judgeOverlayBitmap, w, h);
    }

    private static void DrawCanvasImageUniformToFill(
        CanvasDrawingSession session,
        CanvasBitmap bitmap,
        float canvasW,
        float canvasH)
    {
        var iw = bitmap.SizeInPixels.Width;
        var ih = bitmap.SizeInPixels.Height;
        if (iw <= 0 || ih <= 0)
            return;

        var scale = Math.Max(canvasW / iw, canvasH / ih);
        var dw = iw * scale;
        var dh = ih * scale;
        var x = (canvasW - dw) * 0.5f;
        var y = (canvasH - dh) * 0.5f;
        session.DrawImage(bitmap, new Windows.Foundation.Rect(x, y, dw, dh));
    }

    private static void DrawCanvasImageUniform(
        CanvasDrawingSession session,
        CanvasBitmap bitmap,
        float canvasW,
        float canvasH)
    {
        var iw = bitmap.SizeInPixels.Width;
        var ih = bitmap.SizeInPixels.Height;
        if (iw <= 0 || ih <= 0)
            return;

        var scale = Math.Min(canvasW / iw, canvasH / ih);
        var dw = iw * scale;
        var dh = ih * scale;
        var x = (canvasW - dw) * 0.5f;
        var y = (canvasH - dh) * 0.5f;
        session.DrawImage(bitmap, new Windows.Foundation.Rect(x, y, dw, dh));
    }
}
