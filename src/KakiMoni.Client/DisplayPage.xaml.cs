using KakiMoni.Core.Drawing;
using KakiMoni.Core.Models;
using KakiMoni_Client.Drawing;
using KakiMoni_Client.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace KakiMoni_Client;

public sealed partial class DisplayPage : UserControl
{
    private readonly List<StrokeData> _strokes = new();
    private readonly BackgroundImageService _images = AppServices.BackgroundImages;
    private DisplayHubService? _hub;
    private StrokeData? _currentStroke;
    private double _canvasWidth;
    private double _canvasHeight;
    private CanvasBitmap? _bgBitmap;
    private CanvasBitmap? _fillOverlayBitmap;
    private CancellationTokenSource? _bgCts;
    private CancellationTokenSource? _choiceCts;
    private CancellationTokenSource? _judgeCts;
    private CancellationTokenSource? _coverCts;
    private bool _coverVisible;
    private bool _hasLogo;
    private bool _coverLoaded;
    private bool _writingSurfaceVisible = true;
    private int _coverStateTicket;
    private int _coverStateApplied;
    private bool _attachComplete;
    private ClientHubService? _clientHub;
    private AppWindow? _appWindow;
    private double _windowWidth;
    private double _windowHeight;

    public DisplayPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateCanvasLayout();
        CanvasHost.SizeChanged += (_, _) => UpdateCanvasLayout();
        DrawCanvas.Loaded += OnDrawCanvasLoaded;
        DrawCanvas.CreateResources += (_, _) => DrawCanvas.Invalidate();
        DrawCanvas.SizeChanged += (_, _) => DrawCanvas.Invalidate();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureDisplaySurface();
        await LoadCoverAsync();
        if (_hub is null)
        {
            if (_hasLogo)
                ShowLogoCover();
            else
                ShowWritingSurface();
        }
    }

    internal void BindToWindow(AppWindow appWindow)
    {
        if (_appWindow is not null)
            _appWindow.Changed -= OnAppWindowChanged;

        _appWindow = appWindow;
        _appWindow.Changed += OnAppWindowChanged;
        ApplyWindowSize(appWindow.Size);
    }

    internal void ForceLayoutSize(double width, double height)
    {
        if (width <= 1 || height <= 1) return;

        _windowWidth = width;
        _windowHeight = height;
        Width = width;
        Height = height;
        MinWidth = width;
        MinHeight = height;
        RootGrid.Width = width;
        RootGrid.Height = height;
        RootGrid.MinWidth = width;
        RootGrid.MinHeight = height;
        CanvasHost.Width = width;
        CanvasHost.Height = height;
        UpdateCanvasLayout();
        RequestRedraw();
    }

    private void OnDrawCanvasLoaded(object sender, RoutedEventArgs e)
    {
        EnsureDisplaySurface();
        RequestRedraw();
    }

    private void RequestRedraw()
    {
        DrawCanvas.Invalidate();
        if (DrawCanvas.ReadyToDraw)
            return;

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            UpdateCanvasLayout();
            DrawCanvas.Invalidate();
        });
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange) return;
        RunOnUi(() => ApplyWindowSize(sender.Size));
    }

    private void ApplyWindowSize(SizeInt32 size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;

        _windowWidth = size.Width;
        _windowHeight = size.Height;
        RootGrid.Width = size.Width;
        RootGrid.Height = size.Height;
        UpdateCanvasLayout();
    }

    internal Task InvokeOnUiAsync(Func<Task> action)
    {
        if (DispatcherQueue.HasThreadAccess)
            return action();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }))
        {
            tcs.TrySetException(new InvalidOperationException("Display UI dispatcher is unavailable."));
        }

        return tcs.Task;
    }

    internal void WireToHub(DisplayHubService hub)
    {
        _hub = hub;
        WireHub();
    }

    internal void UnwireFromOutput()
    {
        UnwireHub();
    }

    internal void SetCoverRevealed(bool revealed) => ApplyCoverState(revealed);

    internal void ApplySyncSnapshot(DisplaySyncSnapshot snapshot)
    {
        MirrorRestoreStrokes(snapshot.Strokes);
        if (snapshot.CurrentStroke is not null)
            MirrorStrokeStart(snapshot.CurrentStroke);
        if (!string.IsNullOrWhiteSpace(snapshot.BgImageUrl))
            MirrorBackgroundChanged(snapshot.BgImageUrl);
    }

    internal void MirrorRestoreStrokes(IReadOnlyList<StrokeData> strokes) =>
        RunOnUi(() =>
        {
            _strokes.Clear();
            _strokes.AddRange(strokes.Select(CloneStroke));
            _currentStroke = null;
            UpdateCanvasLayout();
            DrawCanvas.Invalidate();
        });

    internal void MirrorStrokeStart(StrokeData stroke) =>
        RunOnUi(() =>
        {
            _currentStroke = CloneStroke(stroke);
            UpdateCanvasLayout();
            DrawCanvas.Invalidate();
        });

    internal void MirrorStrokePoint(StrokePoint point) =>
        RunOnUi(() =>
        {
            _currentStroke?.Points.Add(new StrokePoint { X = point.X, Y = point.Y });
            DrawCanvas.Invalidate();
        });

    internal void MirrorStrokeEnd() =>
        RunOnUi(() =>
        {
            if (_currentStroke is not null && _currentStroke.Points.Count >= 1)
                _strokes.Add(CloneStroke(_currentStroke));
            _currentStroke = null;
            DrawCanvas.Invalidate();
        });

    internal void MirrorBackgroundChanged(string url) =>
        RunOnUi(() =>
        {
            AppState.BgImageUrl = url;
            _ = LoadBackgroundAsync(url);
        });

    internal async Task CompleteHubAttachAsync(DisplayHubService hub, ClientHubService? clientHub)
    {
        _clientHub = clientHub;
        await WaitForLayoutAsync();

        if (!_coverLoaded)
            await LoadCoverAsync();

        hub.ReplayDrawingState();

        ApplyCoverState(hub.IsRevealed || (clientHub?.IsRevealed ?? false));

        var bgUrl = !string.IsNullOrWhiteSpace(AppState.BgImageUrl)
            ? AppState.BgImageUrl
            : hub.RestoredBackgroundUrl;
        if (!string.IsNullOrWhiteSpace(bgUrl))
            await LoadBackgroundAsync(bgUrl);

        EnsureDisplaySurface();
        _attachComplete = true;
    }

    internal async Task AttachHubAsync(DisplayHubService hub, ClientHubService? clientHub = null)
    {
        WireToHub(hub);
        await CompleteHubAttachAsync(hub, clientHub);
    }

    private async Task WaitForLayoutAsync()
    {
        for (var i = 0; i < 40; i++)
        {
            UpdateCanvasLayout();
            if (_canvasWidth > 1 && _canvasHeight > 1)
                return;
            await Task.Delay(50);
        }
    }

    private void RunOnUi(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => action()))
            System.Diagnostics.Debug.WriteLine("DisplayPage.RunOnUi: TryEnqueue failed.");
    }

    private void EnsureDisplaySurface()
    {
        UpdateCanvasLayout();
        if (_canvasWidth > 1 && _canvasHeight > 1)
        {
            DrawCanvas.Invalidate();
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            UpdateCanvasLayout();
            if (_canvasWidth <= 1)
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, EnsureDisplaySurface);
            else
                DrawCanvas.Invalidate();
        });
    }

    private void ApplyCoverVisibility()
    {
        CoverPanel.Visibility = _coverVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLogoToCover(ImageSource? source)
    {
        if (source is null)
        {
            _hasLogo = false;
            CoverPanel.Background = new SolidColorBrush(Color.FromArgb(255, 0x0d, 0x1b, 0x2a));
            if (_writingSurfaceVisible)
                ShowWritingSurface();
            return;
        }

        _hasLogo = true;
        CoverPanel.Background = new ImageBrush
        {
            ImageSource = source,
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_appWindow is not null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow = null;
        }

        UnwireHub();
        _bgCts?.Cancel();
        _choiceCts?.Cancel();
        _judgeCts?.Cancel();
        _coverCts?.Cancel();
        _fillOverlayBitmap?.Dispose();
        _bgBitmap?.Dispose();
    }

    private void WireHub()
    {
        if (_hub is null) return;
        UnwireHub();
        _hub.RestoreStrokes += OnRestoreStrokes;
        _hub.BackgroundChanged += OnBackgroundChanged;
        _hub.ShowChoice += OnShowChoice;
        _hub.ClearChoice += OnClearChoice;
        _hub.StrokeStartReceived += OnStrokeStart;
        _hub.StrokePointReceived += OnStrokePoint;
        _hub.StrokeEndReceived += OnStrokeEnd;
        _hub.ClearedStrokesOnly += OnClearStrokes;
        _hub.Reveal += OnHubReveal;
        _hub.Hide += OnHubHide;
        _hub.ShowOverlay += OnShowOverlay;
        _hub.ClearOverlay += OnClearOverlay;
        _hub.JudgeColorModeChanged += OnJudgeColorModeChanged;
    }

    private void UnwireHub()
    {
        if (_hub is null) return;
        _hub.RestoreStrokes -= OnRestoreStrokes;
        _hub.BackgroundChanged -= OnBackgroundChanged;
        _hub.ShowChoice -= OnShowChoice;
        _hub.ClearChoice -= OnClearChoice;
        _hub.StrokeStartReceived -= OnStrokeStart;
        _hub.StrokePointReceived -= OnStrokePoint;
        _hub.StrokeEndReceived -= OnStrokeEnd;
        _hub.ClearedStrokesOnly -= OnClearStrokes;
        _hub.Reveal -= OnHubReveal;
        _hub.Hide -= OnHubHide;
        _hub.ShowOverlay -= OnShowOverlay;
        _hub.ClearOverlay -= OnClearOverlay;
        _hub.JudgeColorModeChanged -= OnJudgeColorModeChanged;
    }

    private void OnJudgeColorModeChanged(bool enabled) =>
        RunOnUi(() =>
        {
            AppState.JudgeColorMode = enabled;
            if (AppState.JudgingFillOverlay)
                DrawCanvas.Invalidate();
        });

    private async Task LoadCoverAsync()
    {
        _coverCts?.Cancel();
        _coverCts = new CancellationTokenSource();
        var token = _coverCts.Token;

        try
        {
            var logo = AppState.LogoImageUrl;
            if (string.IsNullOrWhiteSpace(logo))
                logo = await ClientApiService.GetLogoAsync(AppState.ServerUrl, token);
            if (token.IsCancellationRequested) return;

            if (string.IsNullOrWhiteSpace(logo))
            {
                _coverLoaded = true;
                RunOnUi(() => ApplyLogoToCover(null));
                return;
            }

            var url = BackgroundImageService.ToAbsoluteUrl(AppState.ServerUrl, logo);
            BitmapImage? bitmap = null;
            await InvokeOnUiAsync(async () =>
            {
                bitmap = await _images.LoadBitmapImageAsync(url, token);
            });
            if (token.IsCancellationRequested) return;

            _coverLoaded = true;
            RunOnUi(() => ApplyLogoToCover(bitmap));
        }
        catch (OperationCanceledException) { }
        catch
        {
            _coverLoaded = true;
            RunOnUi(() => ApplyLogoToCover(null));
        }
    }

    private void OnHubReveal(string anim) => ApplyCoverState(true);

    private void OnHubHide()
    {
        if (!_attachComplete)
            return;
        ApplyCoverState(false);
    }

    private void ApplyCoverState(bool revealed)
    {
        var ticket = Interlocked.Increment(ref _coverStateTicket);
        RunOnUi(() =>
        {
            if (ticket <= _coverStateApplied)
                return;

            _coverStateApplied = ticket;
            if (revealed || !_hasLogo)
                ShowWritingSurface();
            else
                ShowLogoCover();
        });
    }

    private void ShowWritingSurface()
    {
        _writingSurfaceVisible = true;
        _coverVisible = false;
        CoverPanel.Visibility = Visibility.Collapsed;
        CoverPanel.IsHitTestVisible = false;
        CanvasHost.Visibility = Visibility.Visible;
        UpdateCanvasLayout();
        RequestRedraw();
    }

    private void ShowLogoCover()
    {
        if (!_hasLogo)
        {
            ShowWritingSurface();
            return;
        }

        _writingSurfaceVisible = false;
        _coverVisible = true;
        CoverPanel.IsHitTestVisible = true;
        CanvasHost.Visibility = Visibility.Collapsed;
        ApplyCoverVisibility();
    }

    private void OnRestoreStrokes(IReadOnlyList<StrokeData> strokes) =>
        RunOnUi(() =>
        {
            _strokes.Clear();
            _strokes.AddRange(strokes.Select(CloneStroke));
            _currentStroke = null;
            DrawCanvas.Invalidate();
            EnsureDisplaySurface();
        });

    private void OnBackgroundChanged(string url) =>
        RunOnUi(() =>
        {
            AppState.BgImageUrl = url;
            _ = LoadBackgroundAsync();
        });

    private void OnShowChoice(string url) =>
        RunOnUi(() =>
        {
            AppState.ChoiceImageUrl = url;
            _ = LoadChoiceOverlayAsync();
        });

    private void OnClearChoice() =>
        RunOnUi(() =>
        {
            AppState.ChoiceImageUrl = null;
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
        });

    private void OnStrokeStart(StrokeData stroke) =>
        RunOnUi(() =>
        {
            _currentStroke = CloneStroke(stroke);
            DrawCanvas.Invalidate();
        });

    private void OnStrokePoint(StrokePoint point) =>
        RunOnUi(() =>
        {
            _currentStroke?.Points.Add(new StrokePoint { X = point.X, Y = point.Y });
            DrawCanvas.Invalidate();
        });

    private void OnStrokeEnd() =>
        RunOnUi(() =>
        {
            if (_currentStroke is not null && _currentStroke.Points.Count >= 1)
                _strokes.Add(CloneStroke(_currentStroke));
            _currentStroke = null;
            DrawCanvas.Invalidate();
        });

    private void OnClearStrokes() =>
        RunOnUi(() =>
        {
            _strokes.Clear();
            _currentStroke = null;
            DrawCanvas.Invalidate();
        });

    private void OnShowOverlay(string url) =>
        RunOnUi(() => _ = LoadJudgeOverlayAsync(url));

    private void OnClearOverlay() =>
        RunOnUi(() =>
        {
            AppState.OverlayImageUrl = null;
            AppState.JudgingFillOverlay = false;
            _fillOverlayBitmap?.Dispose();
            _fillOverlayBitmap = null;
            FillJudgeOverlayImage.Source = null;
            FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            JudgeOverlayImage.Source = null;
            JudgeOverlayImage.Visibility = Visibility.Collapsed;
        });

    private async Task LoadBackgroundAsync(string? relativeUrl = null)
    {
        _bgCts?.Cancel();
        _bgCts?.Dispose();
        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;

        var bgUrl = relativeUrl ?? AppState.BgImageUrl;
        if (string.IsNullOrWhiteSpace(bgUrl))
        {
        RunOnUi(() =>
        {
            _bgBitmap?.Dispose();
            _bgBitmap = null;
            BgImage.Source = null;
            BgImage.Visibility = Visibility.Collapsed;
            RequestRedraw();
        });
            return;
        }

        AppState.BgImageUrl = bgUrl;
        var url = BackgroundImageService.ToAbsoluteUrl(AppState.ServerUrl, bgUrl);
        BitmapImage? image = null;
        CanvasBitmap? bitmap = null;
        await InvokeOnUiAsync(async () =>
        {
            image = await _images.LoadBitmapImageAsync(url, token);
            bitmap = await _images.LoadCanvasBitmapAsync(DrawCanvas, url, token);
        });
        if (token.IsCancellationRequested) return;

        RunOnUi(() =>
        {
            _bgBitmap?.Dispose();
            _bgBitmap = bitmap;
            BgImage.Source = image;
            BgImage.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
            RequestRedraw();
        });
    }

    private async Task LoadChoiceOverlayAsync()
    {
        _choiceCts?.Cancel();
        _choiceCts?.Dispose();
        _choiceCts = new CancellationTokenSource();
        var token = _choiceCts.Token;

        if (string.IsNullOrWhiteSpace(AppState.ChoiceImageUrl))
        {
            RunOnUi(() =>
            {
                ChoiceOverlayImage.Source = null;
                ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            });
            return;
        }

        var url = BackgroundImageService.ToAbsoluteUrl(AppState.ServerUrl, AppState.ChoiceImageUrl);
        BitmapImage? bitmap = null;
        await InvokeOnUiAsync(async () =>
        {
            bitmap = await _images.LoadBitmapImageAsync(url, token);
        });
        if (token.IsCancellationRequested) return;

        RunOnUi(() =>
        {
            ChoiceOverlayImage.Source = bitmap;
            ChoiceOverlayImage.Visibility = Visibility.Visible;
        });
    }

    private async Task LoadJudgeOverlayAsync(string relativeUrl)
    {
        _judgeCts?.Cancel();
        _judgeCts?.Dispose();
        _judgeCts = new CancellationTokenSource();
        var token = _judgeCts.Token;

        AppState.OverlayImageUrl = relativeUrl;
        var isFill = ColorInvertHelper.IsFillOverlayUrl(relativeUrl);
        AppState.JudgingFillOverlay = isFill;
        var url = BackgroundImageService.ToAbsoluteUrl(AppState.ServerUrl, relativeUrl);

        if (isFill)
        {
            BitmapImage? image = null;
            CanvasBitmap? bitmap = null;
            await InvokeOnUiAsync(async () =>
            {
                image = await _images.LoadBitmapImageAsync(url, token);
                bitmap = await _images.LoadCanvasBitmapAsync(DrawCanvas, url, token);
            });
            if (token.IsCancellationRequested) return;

            RunOnUi(() =>
            {
                _fillOverlayBitmap?.Dispose();
                _fillOverlayBitmap = bitmap;
                FillJudgeOverlayImage.Source = image;
                FillJudgeOverlayImage.Visibility = image is not null ? Visibility.Visible : Visibility.Collapsed;
                JudgeOverlayImage.Source = null;
                JudgeOverlayImage.Visibility = Visibility.Collapsed;
                if (AppState.JudgeColorMode)
                    DrawCanvas.Invalidate();
            });
            return;
        }

        BitmapImage? judgeImage = null;
        await InvokeOnUiAsync(async () =>
        {
            judgeImage = await _images.LoadBitmapImageAsync(url, token);
        });
        if (token.IsCancellationRequested) return;

        RunOnUi(() =>
        {
            _fillOverlayBitmap?.Dispose();
            _fillOverlayBitmap = null;
            AppState.JudgingFillOverlay = false;
            FillJudgeOverlayImage.Source = null;
            FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            JudgeOverlayImage.Source = judgeImage;
            JudgeOverlayImage.Visibility = judgeImage is not null ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void UpdateCanvasLayout()
    {
        var hostW = CanvasHost.ActualWidth;
        var hostH = CanvasHost.ActualHeight;
        if (hostW <= 1 || hostH <= 1)
        {
            hostW = RootGrid.ActualWidth;
            hostH = RootGrid.ActualHeight;
        }

        if (hostW <= 1 || hostH <= 1)
        {
            hostW = _windowWidth;
            hostH = _windowHeight;
        }

        if (hostW <= 1 || hostH <= 1) return;

        _canvasWidth = hostW;
        _canvasHeight = hostH;
    }

    private void DrawCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var session = args.DrawingSession;
        var w = (float)sender.ActualWidth;
        var h = (float)sender.ActualHeight;
        if (w <= 1 || h <= 1)
        {
            w = (float)(_canvasWidth > 1 ? _canvasWidth : 0);
            h = (float)(_canvasHeight > 1 ? _canvasHeight : 0);
        }

        if (w <= 1 || h <= 1) return;

        session.Clear(Colors.Transparent);

        var reference = _strokes.FirstOrDefault() ?? _currentStroke;
        var srcW = (float)(reference?.SrcW ?? _canvasWidth);
        var srcH = (float)(reference?.SrcH ?? _canvasHeight);
        if (srcW <= 0) srcW = w;
        if (srcH <= 0) srcH = h;

        var scale = Math.Min(w / srcW, h / srcH);
        var ox = (w - srcW * scale) * 0.5f;
        var oy = (h - srcH * scale) * 0.5f;

        foreach (var stroke in _strokes)
            StrokeDrawHelper.DrawStroke(session, stroke, scale, ox, oy, AppState.ParseStrokeColor);
        if (_currentStroke is not null)
            StrokeDrawHelper.DrawStroke(session, _currentStroke, scale, ox, oy, AppState.ParseStrokeColor);
    }

    private static StrokeData CloneStroke(StrokeData source) => new()
    {
        Tool = source.Tool,
        Color = source.Color,
        Size = source.Size,
        SrcW = source.SrcW,
        SrcH = source.SrcH,
        Points = source.Points.Select(p => new StrokePoint { X = p.X, Y = p.Y }).ToList()
    };
}
