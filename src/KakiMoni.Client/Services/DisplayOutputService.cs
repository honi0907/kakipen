using KakiMoni.Core.Display;
using KakiMoni.Core.Models;
using KakiMoni_Client.Drawing;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;

namespace KakiMoni_Client.Services;

public sealed class DisplayOutputService
{
    private const float MirrorDpi = 96f;

    private DisplayWindow? _window;
    private DispatcherQueue? _uiQueue;
    private CanvasImageSource? _mirrorSource;
    private float _mirrorWidth;
    private float _mirrorHeight;
    private BitmapImage? _logoBitmapImage;
    private bool _hasLogo;
    private bool _coverRevealed;
    private bool _mirrorQueued;
    private bool _displayAttachComplete;
    private bool? _pendingCoverRevealed;

    /// <summary>WritingPage が DrawCanvas.Invalidate() を呼ぶために使う。</summary>
    public Action? RequestRedraw { get; set; }

    public bool IsOpen => _window is not null;

    public async Task TryOpenAsync(int seatId, string serverUrl, DispatcherQueue uiQueue)
    {
        if (!AppState.ExternalOutputEnabled)
            return;

        _uiQueue = uiQueue;
        _displayAttachComplete = false;
        _coverRevealed = false;

        // 前ウィンドウを閉じる
        var prevWindow = _window;
        _window = null;
        if (prevWindow is not null)
            uiQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => prevWindow.Close());

        if (AppServices.DisplayHub is not null)
        {
            await AppServices.DisplayHub.DisposeAsync();
            AppServices.DisplayHub = null;
        }

        _pendingCoverRevealed = null;
        _logoBitmapImage = null;
        _hasLogo = false;
        _mirrorSource = null;

        // ロゴ URL があれば cover フラグを立てる（bitmap は後で読む）
        var relative = AppState.LogoImageUrl;
        if (string.IsNullOrWhiteSpace(relative))
        {
            try { relative = await ClientApiService.GetLogoAsync(serverUrl); }
            catch { }
        }

        _hasLogo = !string.IsNullOrWhiteSpace(relative);
        if (_hasLogo) AppState.LogoImageUrl = relative;

        // ロゴはウィンドウ表示前に読み込む（表示後に白いミラーが一瞬見えるのを防ぐ）
        if (_hasLogo)
        {
            try
            {
                var absoluteUrl = BackgroundImageService.ToAbsoluteUrl(serverUrl, relative!);
                _logoBitmapImage = await AppServices.BackgroundImages.LoadBitmapImageAsync(absoluteUrl);
                System.Diagnostics.Debug.WriteLine($"[Display] Logo BitmapImage loaded: {_logoBitmapImage is not null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Display] Logo load failed: {ex.Message}");
            }
        }

        // pending の GO があれば反映
        if (_pendingCoverRevealed == true)
            _coverRevealed = true;

        var showCover = _hasLogo && !_coverRevealed;
        var logoForUi = _logoBitmapImage;

        // cover 状態を整えてから Show（ロゴあり時はミラーを出さない）
        var windowTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        uiQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            try
            {
                _window = new DisplayWindow();
                _window.SetCoverLogo(logoForUi);
                _window.SetCoverVisible(showCover);
                _window.ShowOnDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Display] Window creation failed: {ex}");
            }
            finally
            {
                windowTcs.TrySetResult();
            }
        });
        await windowTcs.Task;

        _displayAttachComplete = true;

        // 初期描画トリガー
        RequestRedraw?.Invoke();
    }

    public void PublishMirror(
        double canvasWidth,
        double canvasHeight,
        CanvasBitmap? bgBitmap,
        CanvasBitmap? fillOverlayBitmap,
        IReadOnlyList<StrokeData> strokes,
        StrokeData? currentStroke,
        CanvasBitmap? choiceBitmap = null,
        CanvasBitmap? judgeBitmap = null)
    {
        if (_window is null || _uiQueue is null) return;
        if (_mirrorQueued) return;

        _mirrorQueued = true;
        if (!_uiQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _mirrorQueued = false;
            try
            {
                PublishMirrorCore(canvasWidth, canvasHeight, bgBitmap, fillOverlayBitmap, strokes, currentStroke, choiceBitmap, judgeBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Display] PublishMirror failed: {ex.Message}");
            }
        }))
        {
            _mirrorQueued = false;
        }
    }

    public void SetCoverRevealed(bool revealed)
    {
        _coverRevealed = revealed;

        if (!_displayAttachComplete)
        {
            _pendingCoverRevealed = revealed;
            return;
        }

        ApplyCoverUiState();
        RequestRedraw?.Invoke();
    }

    public void SyncSnapshotFromWriting() { }
    public void MirrorRestoreStrokes(IReadOnlyList<StrokeData> strokes) { }
    public void MirrorStrokeStart(StrokeData stroke) { }
    public void MirrorStrokePoint(StrokePoint point) { }
    public void MirrorStrokeEnd() { }
    public void MirrorBackgroundChanged(string url) { }

    public async Task CloseAsync()
    {
        _displayAttachComplete = false;
        _pendingCoverRevealed = null;
        _mirrorQueued = false;
        _mirrorSource = null;
        _mirrorWidth = 0;
        _mirrorHeight = 0;
        _hasLogo = false;
        _coverRevealed = false;
        _logoBitmapImage = null;

        var window = _window;
        _window = null;
        if (window is not null && _uiQueue is not null)
            _uiQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => window.Close());

        if (AppServices.DisplayHub is not null)
        {
            await AppServices.DisplayHub.DisposeAsync();
            AppServices.DisplayHub = null;
        }
    }

    private void PublishMirrorCore(
        double canvasWidth,
        double canvasHeight,
        CanvasBitmap? bgBitmap,
        CanvasBitmap? fillOverlayBitmap,
        IReadOnlyList<StrokeData> strokes,
        StrokeData? currentStroke,
        CanvasBitmap? choiceBitmap = null,
        CanvasBitmap? judgeBitmap = null)
    {
        if (_window is null) return;

        var w = (float)canvasWidth;
        var h = (float)canvasHeight;
        if (w <= 1f || h <= 1f) return;

        // キャンバスサイズが変わったら CanvasImageSource を再生成
        if (_mirrorSource is null || Math.Abs(_mirrorWidth - w) > 0.5f || Math.Abs(_mirrorHeight - h) > 0.5f)
        {
            _mirrorWidth = w;
            _mirrorHeight = h;
            _mirrorSource = new CanvasImageSource(CanvasDevice.GetSharedDevice(), w, h, MirrorDpi);
        }

        if (_hasLogo && !_coverRevealed)
        {
            return;
        }

        using var session = _mirrorSource.CreateDrawingSession(Microsoft.UI.Colors.Transparent);
        DisplaySurfaceRenderer.Draw(
            session,
            w,
            h,
            bgBitmap,
            fillOverlayBitmap,
            strokes,
            currentStroke,
            AppState.ParseStrokeColor,
            choiceBitmap,
            judgeBitmap,
            SeatNameOverlayResolver.Resolve(AppState.SeatNameOverlay, AppState.SeatId),
            AppState.PlayerName);

        _window.SetMirrorImage(_mirrorSource);
    }

    private void ApplyCoverUiState()
    {
        if (_window is null || _uiQueue is null)
            return;

        var showCover = _hasLogo && !_coverRevealed;
        if (!_uiQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            if (_window is null)
                return;

            _window.SetCoverLogo(_logoBitmapImage);
            _window.SetCoverVisible(showCover);
        }))
        {
            if (_window is not null)
            {
                _window.SetCoverLogo(_logoBitmapImage);
                _window.SetCoverVisible(showCover);
            }
        }
    }
}
