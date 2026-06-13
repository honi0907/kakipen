using KakiMoni.Core.Drawing;
using KakiMoni.Core.Models;
using KakiMoni_Host.Drawing;
using KakiMoni_Host.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace KakiMoni_Host.Controls;

public sealed partial class SeatCardView : UserControl
{
    private const double AspectRatio = 16.0 / 9.0;

    private bool _canvasReady;
    private double _previewWidth;
    private double _previewHeight;
    private readonly HostImageLoader _images = new();
    private string? _loadedChoiceUrl;
    private string? _loadedBgUrl;
    private string? _loadedOverlayUrl;
    private CancellationTokenSource? _bgCts;
    private CancellationTokenSource? _overlayCts;

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(SeatCardModel), typeof(SeatCardView),
            new PropertyMetadata(null, OnModelChanged));

    public SeatCardModel? Model
    {
        get => (SeatCardModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public event EventHandler<SeatCardModel>? ClearClicked;

    public SeatCardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _canvasReady = true;
        UpdatePreviewLayout();
        _ = UpdateBackgroundAsync();
        RequestPreviewRefresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _canvasReady = false;
        _bgCts?.Cancel();
        _bgCts?.Dispose();
        _overlayCts?.Cancel();
        _overlayCts?.Dispose();
    }

    private void RequestPreviewRefresh()
    {
        if (!_canvasReady) return;
        PreviewCanvas.Invalidate();
    }

    private void OnCardSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdatePreviewLayout();

    private void UpdatePreviewLayout()
    {
        var padW = CardBorder.Padding.Left + CardBorder.Padding.Right;
        var availW = ActualWidth - padW;
        if (availW <= 0) return;

        // 幅いっぱい × 16:9（高さは幅から算出 — つぶさない）
        var w = availW;
        var h = w / AspectRatio;

        _previewWidth = w;
        _previewHeight = h;
        PreviewFrame.Width = w;
        PreviewFrame.Height = h;
        RequestPreviewRefresh();
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SeatCardView view) return;
        if (e.OldValue is SeatCardModel oldModel)
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
        if (e.NewValue is SeatCardModel newModel)
            newModel.PropertyChanged += view.OnModelPropertyChanged;
        view.BindLabels();
        if (view._canvasReady)
            _ = view.UpdateBackgroundAsync();
        view.RequestPreviewRefresh();
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        BindLabels();
        if (e.PropertyName is nameof(SeatCardModel.Strokes)
            or nameof(SeatCardModel.CurrentStroke)
            or nameof(SeatCardModel.IsLocked)
            or nameof(SeatCardModel.IsConnected)
            or nameof(SeatCardModel.IsReconnecting)
            or nameof(SeatCardModel.Status)
            or nameof(SeatCardModel.IsSelected)
            or nameof(SeatCardModel.Revealed))
            RequestPreviewRefresh();

        if (e.PropertyName is nameof(SeatCardModel.BgImageUrl))
            _ = UpdateBackgroundAsync();

        if (e.PropertyName is nameof(SeatCardModel.ChoiceImageUrl))
            _ = UpdateChoiceOverlayAsync();

        if (e.PropertyName is nameof(SeatCardModel.OverlayImageUrl))
            _ = UpdateJudgeOverlayAsync();
    }

    private async Task UpdateBackgroundAsync()
    {
        var url = Model?.BgImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _loadedBgUrl = null;
            _bgCts?.Cancel();
            BackgroundImage.Source = null;
            return;
        }

        if (string.Equals(_loadedBgUrl, url, StringComparison.OrdinalIgnoreCase)
            && BackgroundImage.Source is not null)
            return;

        _bgCts?.Cancel();
        _bgCts?.Dispose();
        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;

        var thumb = await _images.LoadThumbnailAsync(url, 320);
        if (token.IsCancellationRequested) return;
        if (Model?.BgImageUrl != url) return;

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

        if (string.Equals(_loadedChoiceUrl, url, StringComparison.OrdinalIgnoreCase)
            && ChoiceOverlayImage.Source is not null)
            return;

        var thumb = await _images.LoadThumbnailAsync(url, 320);
        if (Model?.ChoiceImageUrl != url) return;

        _loadedChoiceUrl = url;
        if (thumb is null)
        {
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            return;
        }

        ChoiceOverlayImage.Source = thumb;
        ChoiceOverlayImage.Visibility = Visibility.Visible;
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
        _overlayCts?.Dispose();
        _overlayCts = new CancellationTokenSource();
        var token = _overlayCts.Token;

        var fileName = Path.GetFileName(url.Trim('/').Split('/').LastOrDefault() ?? string.Empty);
        var isFill = fileName.Contains("fill", StringComparison.OrdinalIgnoreCase);

        if (isFill)
        {
            var thumb = await _images.LoadThumbnailAsync(url, 320);
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

        var judgeThumb = await _images.LoadThumbnailAsync(url, 320);
        if (Model?.OverlayImageUrl != url) return;

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

    public void InvalidatePreview() => RequestPreviewRefresh();

    private void BindLabels()
    {
        if (Model is null) return;

        SeatNumText.Text = Model.SeatId.ToString();
        if (Model.IsConnected)
        {
            StatusText.Text = Model.Status;
            SeatNumBadge.Background = new SolidColorBrush(Color.FromArgb(255, 34, 197, 94));
            PlaceholderText.Visibility = Visibility.Collapsed;
            PreviewFrame.Opacity = 1.0;
        }
        else if (Model.IsReconnecting)
        {
            StatusText.Text = "再接続中";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
            SeatNumBadge.Background = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
            PlaceholderText.Text = "再接続中...";
            PlaceholderText.Foreground = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
            PlaceholderText.Visibility = Visibility.Visible;
            PreviewFrame.Opacity = 0.55;
        }
        else
        {
            StatusText.Text = "未接続";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 156, 163, 175));
            SeatNumBadge.Background = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
            PlaceholderText.Text = "未接続";
            PlaceholderText.Foreground = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));
            PlaceholderText.Visibility = Visibility.Visible;
            PreviewFrame.Opacity = 0.35;
        }

        LockIcon.Visibility = Model.IsLocked ? Visibility.Visible : Visibility.Collapsed;

        if (Model.IsConnected)
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 156, 163, 175));

        if (Model.IsSelected)
        {
            CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 34, 211, 238));
            CardBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
            CardBorder.BorderThickness = new Thickness(1);
        }
    }

    private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var session = args.DrawingSession;
        var w = (float)(_previewWidth > 0 ? _previewWidth : sender.ActualWidth);
        var h = (float)(_previewHeight > 0 ? _previewHeight : sender.ActualHeight);
        if (w <= 1 || h <= 1) return;

        session.Clear(Colors.Transparent);
        if (Model is null || !Model.IsConnected) return;

        var reference = Model.Strokes.FirstOrDefault() ?? Model.CurrentStroke;
        var srcW = reference?.SrcW ?? 1600;
        var srcH = reference?.SrcH ?? 900;
        if (srcW <= 0) srcW = 1600;
        if (srcH <= 0) srcH = 900;

        var scale = Math.Min(w / (float)srcW, h / (float)srcH);
        var ox = (w - (float)srcW * scale) * 0.5f;
        var oy = (h - (float)srcH * scale) * 0.5f;

        foreach (var stroke in Model.Strokes)
            StrokeDrawHelper.DrawStroke(session, stroke, scale, ox, oy, ParseStrokeColor);
        if (Model.CurrentStroke is not null)
            StrokeDrawHelper.DrawStroke(session, Model.CurrentStroke, scale, ox, oy, ParseStrokeColor);
    }

    private Color ParseStrokeColor(string hex)
    {
        var invert = HostSettingsStore.Load().JudgeColorMode
            && ColorInvertHelper.IsFillOverlayUrl(Model?.OverlayImageUrl);
        if (invert)
            hex = ColorInvertHelper.InvertHex(hex);
        return ParseColor(hex);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return Color.FromArgb(255,
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
        }
        catch { }
        return Colors.Black;
    }

    private void OnPreviewDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (Model is null || !Model.IsConnected || Model.IsLocked) return;
        ClearClicked?.Invoke(this, Model);
    }
}
