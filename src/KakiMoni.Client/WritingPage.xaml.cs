using KakiMoni.Core.Drawing;
using KakiMoni.Core.Display;
using KakiMoni.Core.Models;
using KakiMoni_Client.Drawing;
using KakiMoni_Client.Controls;
using KakiMoni_Client.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.UI;

namespace KakiMoni_Client;

public sealed partial class WritingPage : Page
{
    private const double AspectRatio = 16.0 / 9.0;

    private readonly List<StrokeData> _strokes = new();
    private readonly BackgroundImageService _images = AppServices.BackgroundImages;
    private StrokeData? _currentStroke;
    private bool _isLocked;
    private string _activeTool = "pen";
    private uint? _activePointerId;
    private double _canvasWidth;
    private double _canvasHeight;
    private CanvasBitmap? _bgBitmap;
    private CanvasBitmap? _fillOverlayBitmap;
    private CanvasBitmap? _choiceBitmap;
    private CanvasBitmap? _judgeBitmap;
    private CancellationTokenSource? _bgCts;
    private CancellationTokenSource? _choiceCts;
    private CancellationTokenSource? _judgeCts;
    private ClientHubService? _hub;
    private bool _surfaceInitialized;
    private bool _launchCompleted;
    private LaunchProgress? _launchProgress;
    private int _settingsTapCount;
    private DateTimeOffset _lastSettingsTapAt;
    private const int SettingsTapRequired = 5;
    private static readonly TimeSpan SettingsTapWindow = TimeSpan.FromSeconds(2);
    private Storyboard? _eraserReturnStoryboard;
    private DispatcherQueueTimer? _eraserReturnSwitchTimer;

    public WritingPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateCanvasLayout();
        CanvasHost.SizeChanged += (_, _) => UpdateCanvasLayout();
        UpdateToolButtons();
        ApplyLockOverlayOpacity(AppState.LockOverlayOpacityPercent);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _hub = AppServices.Hub;
        WireHub();
        UpdateHeader();
        AttachLaunchProgress();
        PrepareWritingSession();
        if (IsLoaded)
        {
            EnsureWritingSurface();
            ScheduleMainWindowLayout();
        }
    }

    private async Task OpenDisplayAsync()
    {
        // RequestRedraw は UI スレッドで Invalidate を呼ぶコールバック
        AppServices.DisplayOutput.RequestRedraw = () =>
            DispatcherQueue.TryEnqueue(() => DrawCanvas.Invalidate());

        try
        {
            await AppServices.DisplayOutput.TryOpenAsync(
                AppState.SeatId,
                AppState.ServerUrl,
                DispatcherQueue);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenDisplayAsync failed: {ex}");
        }
        finally
        {
            // ロゴ or カバー状態が決まったら必ず再描画
            DispatcherQueue.TryEnqueue(() => DrawCanvas.Invalidate());
        }
    }

    private void PrepareWritingSession()
    {
        AppState.ApplyStartupPen();
        _activeTool = "pen";
        ApplyWritingToolVisibility();
        BuildColorToolbar();
        UpdateToolButtons();
    }

    private void ApplyWritingToolVisibility()
    {
        ConfirmButton.Visibility = AppState.ShowConfirmButton ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = AppState.ShowClearButton ? Visibility.Visible : Visibility.Collapsed;
        EraserToolHost.Visibility = AppState.ShowEraserTool ? Visibility.Visible : Visibility.Collapsed;

        if (!AppState.ShowEraserTool && _activeTool == "eraser")
        {
            _activeTool = "pen";
            StopEraserReturnCountdown();
        }
    }

    private void ApplyMainWindowLayout()
    {
        if (App.MainWindowInstance is not { } main) return;
        ClientWindowLayout.ApplyWritingMainWindow(main, AppState.WritingFullscreen);
    }

    private void ScheduleMainWindowLayout()
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyMainWindowLayout);
    }

    private void RestoreMainWindowLayout()
    {
        if (App.MainWindowInstance is { } main)
        {
            ClientWindowLayout.RestoreMainWindow(main);
            main.ApplyLauncherWindowSize();
        }
    }

    private void AttachLaunchProgress()
    {
        if (AppServices.LaunchProgress is not LaunchProgress progress) return;

        _launchProgress = progress;
        progress.BindDispatcher(DispatcherQueue);
        progress.Changed += OnLaunchProgressChanged;
        LaunchProgressRing.IsActive = true;
        LaunchOverlay.Visibility = Visibility.Visible;
        UpdateLaunchOverlay(progress);
    }

    private void OnLaunchProgressChanged()
    {
        if (_launchProgress is null) return;
        UpdateLaunchOverlay(_launchProgress);
    }

    private void UpdateLaunchOverlay(LaunchProgress progress) =>
        LaunchStatusText.Text = progress.Message;

    private void DetachLaunchProgress()
    {
        if (_launchProgress is null) return;
        _launchProgress.Changed -= OnLaunchProgressChanged;
        _launchProgress = null;
        AppServices.LaunchProgress = null;
        LaunchOverlay.Visibility = Visibility.Collapsed;
        LaunchProgressRing.IsActive = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureWritingSurface();
        ScheduleMainWindowLayout();
    }

    private void EnsureWritingSurface()
    {
        if (_surfaceInitialized) return;
        _surfaceInitialized = true;

        _ = LoadBackgroundAsync();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            UpdateCanvasLayout();
            if (_canvasWidth <= 0)
                DispatcherQueue.TryEnqueue(UpdateCanvasLayout);
        });
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnwireHub();
        _bgCts?.Cancel();
        _bgCts?.Dispose();
        _choiceCts?.Cancel();
        _choiceCts?.Dispose();
        _judgeCts?.Cancel();
        _judgeCts?.Dispose();
        StopEraserReturnCountdown();
        _bgBitmap?.Dispose();
        _fillOverlayBitmap?.Dispose();
        _choiceBitmap?.Dispose();
        _judgeBitmap?.Dispose();
        if (_hub is not null && !_hub.DetachedFromDispose)
            await _hub.DisposeAsync();
    }

    private void WireHub()
    {
        if (_hub is null) return;
        UnwireHub();
        _hub.RestoreStrokes += OnRestoreStrokes;
        _hub.BackgroundChanged += OnBackgroundChanged;
        _hub.ShowChoice += OnShowChoice;
        _hub.ClearChoice += OnClearChoice;
        _hub.ShowOverlay += OnShowOverlay;
        _hub.ClearOverlay += OnClearOverlay;
        _hub.JudgeColorModeChanged += OnJudgeColorModeChanged;
        _hub.LockOverlayOpacityChanged += OnLockOverlayOpacityChanged;
        _hub.Locked += OnLocked;
        _hub.Unlocked += OnUnlocked;
        _hub.Cleared += OnClearAll;
        _hub.ClearedStrokesOnly += OnClearStrokesOnly;
        _hub.NameAssigned += OnNameAssigned;
        _hub.SeatNameOverlayChanged += OnSeatNameOverlayChanged;
        _hub.ConnectionChanged += OnConnectionChanged;
        _hub.Reveal += OnDisplayReveal;
        _hub.Hide += OnDisplayHide;
        _hub.WritingBlackout += OnWritingBlackout;
        _hub.ReplayRestoredStrokes();
        _hub.ReplayRestoredBackground();
        _hub.ReplayRestoredChoice();
        _hub.ReplayRestoredOverlay();
        _hub.ReplayRestoredWritingBlackout();
        _hub.ReplayRestoredLockOverlayOpacity();
        _hub.ReplayRestoredSeatNameOverlay();
        _hub.ReplayRestoredNameAssigned();
        // cover は外部出力 attach 時にロゴ有無で決める（Hub Reveal リプレイは使わない）
        ApplyRestoredBackground();
        ApplyRestoredChoice();
    }

    private void ApplyRestoredBackground()
    {
        var bgUrl = _hub?.GetRestoredBackgroundUrl();
        AppState.BgImageUrl = string.IsNullOrWhiteSpace(bgUrl) ? null : bgUrl;
        if (string.IsNullOrWhiteSpace(bgUrl))
        {
            _bgBitmap?.Dispose();
            _bgBitmap = null;
            BackgroundImage.Source = null;
            DrawCanvas.Invalidate();
            return;
        }

        _ = LoadBackgroundAsync();
    }

    private void ApplyRestoredChoice()
    {
        var choiceUrl = _hub?.GetRestoredChoiceUrl();
        AppState.ChoiceImageUrl = string.IsNullOrWhiteSpace(choiceUrl) ? null : choiceUrl;
        if (string.IsNullOrWhiteSpace(choiceUrl))
        {
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            return;
        }

        _ = LoadChoiceOverlayAsync();
    }

    private void UnwireHub()
    {
        if (_hub is null) return;
        _hub.RestoreStrokes -= OnRestoreStrokes;
        _hub.BackgroundChanged -= OnBackgroundChanged;
        _hub.ShowChoice -= OnShowChoice;
        _hub.ClearChoice -= OnClearChoice;
        _hub.ShowOverlay -= OnShowOverlay;
        _hub.ClearOverlay -= OnClearOverlay;
        _hub.JudgeColorModeChanged -= OnJudgeColorModeChanged;
        _hub.LockOverlayOpacityChanged -= OnLockOverlayOpacityChanged;
        _hub.Locked -= OnLocked;
        _hub.Unlocked -= OnUnlocked;
        _hub.Cleared -= OnClearAll;
        _hub.ClearedStrokesOnly -= OnClearStrokesOnly;
        _hub.NameAssigned -= OnNameAssigned;
        _hub.SeatNameOverlayChanged -= OnSeatNameOverlayChanged;
        _hub.ConnectionChanged -= OnConnectionChanged;
        _hub.Reveal -= OnDisplayReveal;
        _hub.Hide -= OnDisplayHide;
        _hub.WritingBlackout -= OnWritingBlackout;
    }

    private void OnWritingBlackout(bool enabled) =>
        DispatcherQueue.TryEnqueue(() => SetWritingBlackout(enabled));

    private void SetWritingBlackout(bool enabled)
    {
        WritingBlackoutOverlay.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (Resources.TryGetValue("BlackoutStoryboard", out var resource)
            && resource is Microsoft.UI.Xaml.Media.Animation.Storyboard storyboard)
        {
            if (enabled)
                storyboard.Begin();
            else
            {
                storyboard.Stop();
                BlackoutDotsText.Text = "。";
            }
        }
    }

    private void OnDisplayReveal(string anim)
    {
        AppServices.DisplayOutput.SetCoverRevealed(true);
        DrawCanvas.Invalidate();
    }

    private void OnDisplayHide() =>
        AppServices.DisplayOutput.SetCoverRevealed(false);

    private void UpdateHeader()
    {
        var name = string.IsNullOrWhiteSpace(AppState.PlayerName) ? string.Empty : $"　{AppState.PlayerName}";
        SeatLabel.Text = $"ID {AppState.SeatId}{name}";
        UpdateConnectionDot(_hub?.IsConnected == true, _hub?.IsReconnecting == true);
    }

    private void OnConnectionChanged(bool connected, bool reconnecting) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateConnectionDot(connected, reconnecting);
            if (connected)
            {
                if (HintText.Text is "サーバーとの接続が切れました" or "再接続中...")
                    HintText.Text = string.Empty;
            }
            else if (reconnecting)
            {
                HintText.Text = "再接続中...";
            }
            else if (string.IsNullOrEmpty(HintText.Text))
            {
                HintText.Text = "サーバーとの接続が切れました";
            }
        });

    private void UpdateConnectionDot(bool connected, bool reconnecting)
    {
        ConnDot.Fill = new SolidColorBrush(
            connected ? Color.FromArgb(255, 74, 222, 128)
            : reconnecting ? Color.FromArgb(255, 251, 191, 36)
            : Color.FromArgb(255, 100, 116, 139));
    }

    private void OnRestoreStrokes(IReadOnlyList<StrokeData> strokes)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _strokes.Clear();
            _strokes.AddRange(strokes.Select(CloneStroke));
            DrawCanvas.Invalidate();
        });
    }

    private void OnBackgroundChanged(string url)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.BgImageUrl = url;
            _ = LoadBackgroundAsync();
        });
    }

    private void OnShowChoice(string url) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.ChoiceImageUrl = url;
            _ = LoadChoiceOverlayAsync();
        });

    private void OnClearChoice() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.ChoiceImageUrl = null;
            _choiceCts?.Cancel();
            _choiceBitmap?.Dispose();
            _choiceBitmap = null;
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            DrawCanvas.Invalidate();
        });

    private void OnJudgeColorModeChanged(bool enabled) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.JudgeColorMode = enabled;
            if (AppState.JudgingFillOverlay)
            {
                DrawCanvas.Invalidate();
                RequestMirrorRefresh();
            }
        });

    private void OnLockOverlayOpacityChanged(int percent) =>
        DispatcherQueue.TryEnqueue(() => ApplyLockOverlayOpacity(percent));

    private void ApplyLockOverlayOpacity(int percent)
    {
        AppState.LockOverlayOpacityPercent = Math.Clamp(percent, 0, 100);
        var alpha = (byte)Math.Round(AppState.LockOverlayOpacityPercent / 100.0 * 255);
        LockOverlay.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
    }

    private void OnShowOverlay(string url) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadJudgeOverlayAsync(url));

    private void OnClearOverlay() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.OverlayImageUrl = null;
            AppState.JudgingFillOverlay = false;
            _judgeCts?.Cancel();
            _fillOverlayBitmap?.Dispose();
            _fillOverlayBitmap = null;
            _judgeBitmap?.Dispose();
            _judgeBitmap = null;
            FillJudgeOverlayImage.Source = null;
            FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            JudgeOverlayImage.Source = null;
            JudgeOverlayImage.Visibility = Visibility.Collapsed;
            RequestMirrorRefresh();
        });

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

        try
        {
            if (isFill)
            {
                var image = await _images.LoadBitmapImageAsync(url, token);
                if (token.IsCancellationRequested) return;

                FillJudgeOverlayImage.Source = image;
                FillJudgeOverlayImage.Visibility = image is not null ? Visibility.Visible : Visibility.Collapsed;
                _judgeBitmap?.Dispose();
                _judgeBitmap = null;
                JudgeOverlayImage.Source = null;
                JudgeOverlayImage.Visibility = Visibility.Collapsed;

                var bitmap = await _images.LoadCanvasBitmapAsync(DrawCanvas, url, token);
                if (token.IsCancellationRequested) return;
                _fillOverlayBitmap?.Dispose();
                _fillOverlayBitmap = bitmap;
                if (bitmap is null)
                    System.Diagnostics.Debug.WriteLine($"[Judge] Fill overlay load failed: {url}");
                RequestMirrorRefresh();
                if (AppState.JudgeColorMode)
                    DrawCanvas.Invalidate();
                return;
            }

            AppState.JudgingFillOverlay = false;
            FillJudgeOverlayImage.Source = null;
            FillJudgeOverlayImage.Visibility = Visibility.Collapsed;
            _fillOverlayBitmap?.Dispose();
            _fillOverlayBitmap = null;
            var judgeImage = await _images.LoadBitmapImageAsync(url, token);
            var canvasBitmap = await _images.LoadCanvasBitmapAsync(DrawCanvas, url, token);
            if (token.IsCancellationRequested) return;
            _judgeBitmap?.Dispose();
            _judgeBitmap = canvasBitmap;
            JudgeOverlayImage.Source = judgeImage;
            JudgeOverlayImage.Visibility = judgeImage is not null ? Visibility.Visible : Visibility.Collapsed;
            if (judgeImage is null)
                System.Diagnostics.Debug.WriteLine($"[Judge] Overlay load failed: {url}");
            RequestMirrorRefresh();
        }
        catch (OperationCanceledException) { }
    }

    private async Task LoadChoiceOverlayAsync()
    {
        _choiceCts?.Cancel();
        _choiceCts?.Dispose();
        _choiceCts = new CancellationTokenSource();
        var token = _choiceCts.Token;

        if (string.IsNullOrWhiteSpace(AppState.ChoiceImageUrl))
        {
            _choiceBitmap?.Dispose();
            _choiceBitmap = null;
            ChoiceOverlayImage.Source = null;
            ChoiceOverlayImage.Visibility = Visibility.Collapsed;
            DrawCanvas.Invalidate();
            return;
        }

        var url = BackgroundImageService.ToAbsoluteUrl(AppState.ServerUrl, AppState.ChoiceImageUrl);
        try
        {
            var bitmap = await _images.LoadBitmapImageAsync(url, token);
            var canvasBitmap = await _images.LoadCanvasBitmapAsync(DrawCanvas, url, token);
            if (token.IsCancellationRequested) return;
            if (bitmap is null)
            {
                _choiceBitmap?.Dispose();
                _choiceBitmap = null;
                ChoiceOverlayImage.Source = null;
                ChoiceOverlayImage.Visibility = Visibility.Collapsed;
                DrawCanvas.Invalidate();
                return;
            }

            _choiceBitmap?.Dispose();
            _choiceBitmap = canvasBitmap;
            ChoiceOverlayImage.Source = bitmap;
            ChoiceOverlayImage.Visibility = Visibility.Visible;
            RequestMirrorRefresh();
        }
        catch (OperationCanceledException) { }
    }

    private void OnLocked() => DispatcherQueue.TryEnqueue(() => SetLocked(true));
    private void OnUnlocked() => DispatcherQueue.TryEnqueue(() => SetLocked(false));
    private void OnClearAll() => DispatcherQueue.TryEnqueue(ClearStrokes);
    private void OnClearStrokesOnly() => DispatcherQueue.TryEnqueue(ClearStrokes);
    private void OnNameAssigned(string name)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.PlayerName = name;
            UpdateHeader();
            UpdateSeatNameOverlay();
        });
    }

    private void OnSeatNameOverlayChanged(SeatNameOverlayConfig config)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppState.SeatNameOverlay = config ?? new SeatNameOverlayConfig();
            AppState.SeatNameOverlay.Normalize();
            UpdateSeatNameOverlay();
            RequestMirrorRefresh();
        });
    }

    private void UpdateSeatNameOverlay()
    {
        var style = SeatNameOverlayResolver.Resolve(AppState.SeatNameOverlay, AppState.SeatId);
        SeatNameOverlayUi.Apply(
            SeatNameOverlay,
            SeatNameOverlayText,
            style,
            AppState.PlayerName,
            _canvasWidth,
            _canvasHeight);
    }

    private void SetLocked(bool locked)
    {
        _isLocked = locked;
        LockOverlay.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        if (locked)
        {
            _activeTool = "pen";
            StopEraserReturnCountdown();
        }
        UpdateToolButtons();
    }

    private void ClearStrokes()
    {
        _strokes.Clear();
        _currentStroke = null;
        DrawCanvas.Invalidate();
    }

    private async Task LoadBackgroundAsync()
    {
        _bgCts?.Cancel();
        _bgCts?.Dispose();
        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;

        if (string.IsNullOrWhiteSpace(AppState.BgImageUrl))
        {
            _bgBitmap?.Dispose();
            _bgBitmap = null;
            BackgroundImage.Source = null;
            RequestMirrorRefresh();
            return;
        }

        var url = BackgroundImageService.ToAbsoluteUrl(AppState.ServerUrl, AppState.BgImageUrl);
        HintText.Text = "背景読込中...";
        try
        {
            var image = await _images.LoadBitmapImageAsync(url, token);
            var bitmap = await _images.LoadCanvasBitmapAsync(DrawCanvas, url, token);
            if (token.IsCancellationRequested) return;
            if (bitmap is null)
            {
                HintText.Text = "背景読込失敗（画像をデコードできませんでした）";
                return;
            }

            _bgBitmap?.Dispose();
            _bgBitmap = bitmap;
            BackgroundImage.Source = image;
            HintText.Text = string.Empty;
            RequestMirrorRefresh();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HintText.Text = $"背景読込失敗: {ex.Message}";
        }
    }

    private void UpdateCanvasLayout()
    {
        var hostW = CanvasHost.ActualWidth;
        var hostH = CanvasHost.ActualHeight;
        if (hostW <= 0 || hostH <= 0) return;

        if (hostW / hostH > AspectRatio)
        {
            _canvasHeight = hostH;
            _canvasWidth = hostH * AspectRatio;
        }
        else
        {
            _canvasWidth = hostW;
            _canvasHeight = hostW / AspectRatio;
        }

        DrawCanvas.Width = _canvasWidth;
        DrawCanvas.Height = _canvasHeight;
        CanvasFrame.Width = _canvasWidth;
        CanvasFrame.Height = _canvasHeight;
        BackgroundImage.Width = _canvasWidth;
        BackgroundImage.Height = _canvasHeight;
        FillJudgeOverlayImage.Width = _canvasWidth;
        FillJudgeOverlayImage.Height = _canvasHeight;
        ChoiceOverlayImage.Width = _canvasWidth;
        ChoiceOverlayImage.Height = _canvasHeight;
        JudgeOverlayImage.Width = _canvasWidth;
        JudgeOverlayImage.Height = _canvasHeight;
        WritingBlackoutOverlay.Width = _canvasWidth;
        WritingBlackoutOverlay.Height = _canvasHeight;
        UpdateSeatNameOverlay();
        DrawCanvas.Invalidate();
        TryCompleteLaunch();
    }

    private void TryCompleteLaunch()
    {
        if (_launchCompleted || _canvasWidth <= 0 || _canvasHeight <= 0) return;
        _launchCompleted = true;
        AppServices.CompleteWritingPageLaunch();
        DetachLaunchProgress();
        _ = OpenDisplayAsync();
    }

    private void DrawCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var w = (float)_canvasWidth;
        var h = (float)_canvasHeight;
        DisplaySurfaceRenderer.DrawStrokesOnly(
            args.DrawingSession,
            w,
            h,
            _strokes,
            _currentStroke,
            AppState.ParseStrokeColor);

        AppServices.DisplayOutput.PublishMirror(
            _canvasWidth,
            _canvasHeight,
            _bgBitmap,
            _fillOverlayBitmap,
            _strokes,
            _currentStroke,
            _choiceBitmap,
            _judgeBitmap);

        TryCompleteLaunch();
    }

    private void RequestMirrorRefresh()
    {
        AppServices.DisplayOutput.PublishMirror(
            _canvasWidth,
            _canvasHeight,
            _bgBitmap,
            _fillOverlayBitmap,
            _strokes,
            _currentStroke,
            _choiceBitmap,
            _judgeBitmap);
    }

    private void BuildColorToolbar()
    {
        ColorPaletteUi.Render(
            ColorToolbar,
            AppState.Palette,
            AppState.PenColor,
            color =>
            {
                AppState.PenColor = color;
                _activeTool = "pen";
                StopEraserReturnCountdown();
                UpdateToolButtons();
                BuildColorToolbar();
            },
            highlightSelection: _activeTool != "eraser");
    }

    private void DrawCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isLocked || _hub is null) return;
        if (_activePointerId is not null) return;
        _activePointerId = e.Pointer.PointerId;
        DrawCanvas.CapturePointer(e.Pointer);

        var p = e.GetCurrentPoint(DrawCanvas).Position;
        var isEraser = _activeTool == "eraser";
        if (isEraser)
            StopEraserReturnCountdown();
        _currentStroke = new StrokeData
        {
            Tool = isEraser ? "eraser" : "pen",
            Color = isEraser ? "#000000" : AppState.PenColor,
            Size = isEraser ? AppState.EraserSize : AppState.PenSize,
            SrcW = _canvasWidth,
            SrcH = _canvasHeight,
            Points = { new StrokePoint { X = p.X, Y = p.Y } }
        };
        DrawCanvas.Invalidate();
        _ = _hub.SendStrokeStartAsync(CloneStroke(_currentStroke));
    }

    private void DrawCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isLocked || _hub is null || _currentStroke is null) return;
        if (_activePointerId != e.Pointer.PointerId) return;
        var p = e.GetCurrentPoint(DrawCanvas).Position;
        if (_currentStroke.Points.Count > 0)
        {
            var last = _currentStroke.Points[^1];
            var dx = p.X - last.X;
            var dy = p.Y - last.Y;
            if (dx * dx + dy * dy < 4) return;
        }

        var point = new StrokePoint { X = p.X, Y = p.Y };
        _currentStroke.Points.Add(point);
        DrawCanvas.Invalidate();
        _ = _hub.SendStrokePointAsync(point);
    }

    private async void DrawCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointerId != e.Pointer.PointerId) return;
        _activePointerId = null;
        DrawCanvas.ReleasePointerCapture(e.Pointer);
        if (_currentStroke is null || _hub is null) return;

        var wasEraser = string.Equals(_currentStroke.Tool, "eraser", StringComparison.OrdinalIgnoreCase);
        var hadEraserUse = wasEraser && _currentStroke.Points.Count >= 1;
        if (_currentStroke.Points.Count >= 2)
            _strokes.Add(CloneStroke(_currentStroke));
        _currentStroke = null;
        DrawCanvas.Invalidate();
        await _hub.SendStrokeEndAsync();
        if (hadEraserUse)
            ResetEraserReturnCountdown();
    }

    private async void OnSeatIdAreaTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        var now = DateTimeOffset.UtcNow;
        if (now - _lastSettingsTapAt > SettingsTapWindow)
            _settingsTapCount = 0;

        _lastSettingsTapAt = now;
        _settingsTapCount++;
        if (_settingsTapCount < SettingsTapRequired)
            return;

        _settingsTapCount = 0;
        await PromptReturnToSettingsAsync();
    }

    private async Task PromptReturnToSettingsAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "設定に戻る",
            Content = "設定に戻りますか？",
            PrimaryButtonText = "戻る",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        await ReturnToSettingsAsync();
    }

    private async Task ReturnToSettingsAsync()
    {
        await AppServices.DisplayOutput.CloseAsync();

        if (_hub is not null)
            _hub.DetachedFromDispose = true;

        var settings = AppState.ToSettings();
        settings.PenColor = settings.Palette.Count > 0 ? settings.Palette[0] : settings.PenColor;
        ClientSettingsStore.Save(settings);
        RestoreMainWindowLayout();
        Frame?.Navigate(typeof(SetupPage));
    }

    private void OnEraserTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_isLocked) return;
        _activeTool = "eraser";
        StopEraserReturnCountdown();
        UpdateToolButtons();
        BuildColorToolbar();
        e.Handled = true;
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (_isLocked || _hub is null) return;

        SetLocked(true);
        try
        {
            await _hub.SendClientConfirmAsync();
        }
        catch
        {
            SetLocked(false);
        }
    }

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_isLocked || _hub is null) return;

        var dialog = new ContentDialog
        {
            Title = "クリア",
            Content = "描画をすべて消しますか？",
            PrimaryButtonText = "はい",
            CloseButtonText = "いいえ",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        ClearStrokes();
        await _hub.SendClearCanvasAsync();
    }

    private void UpdateToolButtons()
    {
        var enabled = !_isLocked;
        if (EraserToolHost.Visibility == Visibility.Visible)
        {
            EraserToolHost.IsHitTestVisible = enabled;
            EraserToolHost.Opacity = enabled ? 1.0 : 0.4;
        }

        if (ClearButton.Visibility == Visibility.Visible)
            ClearButton.IsEnabled = enabled;
        if (ConfirmButton.Visibility == Visibility.Visible)
            ConfirmButton.IsEnabled = enabled;

        HintText.Text = string.Empty;
        if (_activeTool != "eraser")
            StopEraserReturnCountdown();
        else if (EraserToolHost.Visibility == Visibility.Visible)
            UpdateEraserSelectionRing(EraserReturnProgress.Visibility == Visibility.Visible);
    }

    private void UpdateEraserSelectionRing(bool countdownActive)
    {
        if (_activeTool != "eraser" || countdownActive)
        {
            EraserToolRing.Width = 40;
            EraserToolRing.Height = 40;
            EraserToolRing.CornerRadius = new CornerRadius(20);
            EraserToolRing.BorderThickness = new Thickness(0);
            return;
        }

        EraserToolRing.Width = 46;
        EraserToolRing.Height = 46;
        EraserToolRing.CornerRadius = new CornerRadius(23);
        EraserToolRing.BorderThickness = new Thickness(3);
    }

    private void ResetEraserReturnCountdown()
    {
        if (_activeTool != "eraser" || _isLocked)
            return;

        StartEraserReturnAnimation();
    }

    private void StartEraserReturnAnimation()
    {
        if (_activeTool != "eraser" || _isLocked)
            return;

        var duration = TimeSpan.FromSeconds(AppState.EraserAutoPenSeconds);
        StopEraserReturnAnimationOnly();

        EraserReturnProgress.Value = 100;
        EraserReturnProgress.Visibility = Visibility.Visible;
        UpdateEraserSelectionRing(countdownActive: true);

        var animation = new DoubleAnimation
        {
            From = 100,
            To = 0,
            Duration = duration,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, EraserReturnProgress);
        Storyboard.SetTargetProperty(animation, "Value");

        _eraserReturnStoryboard = new Storyboard { Duration = duration };
        _eraserReturnStoryboard.Children.Add(animation);
        _eraserReturnStoryboard.Begin();

        _eraserReturnSwitchTimer ??= DispatcherQueue.CreateTimer();
        _eraserReturnSwitchTimer.IsRepeating = false;
        _eraserReturnSwitchTimer.Interval = duration;
        _eraserReturnSwitchTimer.Tick -= OnEraserReturnSwitchTimerTick;
        _eraserReturnSwitchTimer.Tick += OnEraserReturnSwitchTimerTick;
        _eraserReturnSwitchTimer.Start();
    }

    private void OnEraserReturnSwitchTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_activeTool == "eraser" && !_isLocked)
            SwitchToPenMode();
    }

    private void StopEraserReturnAnimationOnly()
    {
        _eraserReturnStoryboard?.Stop();
        _eraserReturnStoryboard = null;

        if (_eraserReturnSwitchTimer is not null)
        {
            _eraserReturnSwitchTimer.Tick -= OnEraserReturnSwitchTimerTick;
            _eraserReturnSwitchTimer.Stop();
        }
    }

    private void SwitchToPenMode()
    {
        StopEraserReturnCountdown();
        _activeTool = "pen";
        UpdateToolButtons();
        BuildColorToolbar();
    }

    private void StopEraserReturnCountdown()
    {
        StopEraserReturnAnimationOnly();
        EraserReturnProgress.Visibility = Visibility.Collapsed;
        UpdateEraserSelectionRing(countdownActive: false);
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
