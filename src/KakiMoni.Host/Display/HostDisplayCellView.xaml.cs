using KakiMoni.Core.Models;
using KakiMoni_Host.Controls;
using KakiMoni_Host.Drawing;
using KakiMoni_Host.Services;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KakiMoni_Host.Display;

public sealed partial class HostDisplayCellView : UserControl
{
    private readonly HostImageLoader _images = new();
    private bool _canvasReady;
    private string? _loadedBgUrl;
    private string? _loadedChoiceUrl;
    private string? _loadedOverlayUrl;
    private CancellationTokenSource? _bgCts;
    private CancellationTokenSource? _overlayCts;

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(SeatCardModel), typeof(HostDisplayCellView),
            new PropertyMetadata(null, OnModelChanged));

    public static readonly DependencyProperty FillColorArgbProperty =
        DependencyProperty.Register(nameof(FillColorArgb), typeof(uint), typeof(HostDisplayCellView),
            new PropertyMetadata(HostDisplayPanelColors.EmptySeatColor, OnFillColorChanged));

    public SeatCardModel? Model
    {
        get => (SeatCardModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public uint FillColorArgb
    {
        get => (uint)GetValue(FillColorArgbProperty);
        set => SetValue(FillColorArgbProperty, value);
    }

    public HostDisplayCellView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _canvasReady = true;
            ApplyFillColor();
            PreviewCanvas.Invalidate();
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
        if (d is HostDisplayCellView view)
            view.ApplyFillColor();
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HostDisplayCellView view) return;
        if (e.OldValue is SeatCardModel oldModel)
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
        if (e.NewValue is SeatCardModel newModel)
            newModel.PropertyChanged += view.OnModelPropertyChanged;
        view.RefreshUi();
        _ = view.UpdateBackgroundAsync();
        _ = view.UpdateChoiceOverlayAsync();
        _ = view.UpdateJudgeOverlayAsync();
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SeatCardModel.Strokes)
            or nameof(SeatCardModel.CurrentStroke)
            or nameof(SeatCardModel.IsConnected))
            RequestPreviewRefresh();

        if (e.PropertyName is nameof(SeatCardModel.BgImageUrl))
            _ = UpdateBackgroundAsync();
        if (e.PropertyName is nameof(SeatCardModel.ChoiceImageUrl))
            _ = UpdateChoiceOverlayAsync();
        if (e.PropertyName is nameof(SeatCardModel.OverlayImageUrl))
            _ = UpdateJudgeOverlayAsync();
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
        RequestPreviewRefresh();
    }

    private void RequestPreviewRefresh()
    {
        if (_canvasReady)
            PreviewCanvas.Invalidate();
    }

    private async Task UpdateBackgroundAsync()
    {
        var url = Model?.BgImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedBgUrl = null;
            BackgroundImage.Source = null;
            return;
        }

        if (string.Equals(_loadedBgUrl, url, StringComparison.OrdinalIgnoreCase) && BackgroundImage.Source is not null)
            return;

        _bgCts?.Cancel();
        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;
        var thumb = await _images.LoadThumbnailAsync(url, 640);
        if (token.IsCancellationRequested || Model?.BgImageUrl != url) return;
        _loadedBgUrl = url;
        BackgroundImage.Source = thumb;
    }

    private async Task UpdateChoiceOverlayAsync()
    {
        var url = Model?.ChoiceImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedChoiceUrl = null;
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            return;
        }

        if (string.Equals(_loadedChoiceUrl, url, StringComparison.OrdinalIgnoreCase) && ChoiceOverlayImage.Source is not null)
            return;

        var thumb = await _images.LoadThumbnailAsync(url, 640);
        if (Model?.ChoiceImageUrl != url) return;
        _loadedChoiceUrl = url;
        ChoiceOverlayImage.Source = thumb;
        ChoiceOverlayImage.Visibility = thumb is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task UpdateJudgeOverlayAsync()
    {
        var url = Model?.OverlayImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedOverlayUrl = null;
            _overlayCts?.Cancel();
            FillJudgeOverlayImage.Source = null;
            FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            JudgeOverlayImage.Source = null;
            JudgeOverlayImage.Visibility = Visibility.Collapsed;
            return;
        }

        if (string.Equals(_loadedOverlayUrl, url, StringComparison.OrdinalIgnoreCase)
            && (FillJudgeOverlayImage.Source is not null || JudgeOverlayImage.Source is not null))
            return;

        _overlayCts?.Cancel();
        _overlayCts = new CancellationTokenSource();
        var token = _overlayCts.Token;

        if (SeatPreviewRenderer.IsFillOverlayUrl(url))
        {
            var thumb = await _images.LoadThumbnailAsync(url, 640);
            if (token.IsCancellationRequested || Model?.OverlayImageUrl != url) return;

            _loadedOverlayUrl = url;
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

        var judgeThumb = await _images.LoadThumbnailAsync(url, 640);
        if (token.IsCancellationRequested || Model?.OverlayImageUrl != url) return;

        _loadedOverlayUrl = url;
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
