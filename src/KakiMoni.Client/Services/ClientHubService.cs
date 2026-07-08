using KakiMoni.Core.Models;
using KakiMoni.Core.Protocol;
using Microsoft.AspNetCore.SignalR.Client;

namespace KakiMoni_Client.Services;

public sealed class ClientHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private TaskCompletionSource<bool>? _registerTcs;
    private List<StrokeData>? _restoredStrokes;
    private string? _restoredBackgroundUrl;
    private string? _restoredChoiceUrl;
    private string? _restoredOverlayUrl;
    private bool _restoredRevealed;
    private bool _restoredWritingBlackout;
    private int _restoredLockOverlayOpacity = 80;
    private SeatNameOverlayConfig _restoredSeatNameOverlay = new();
    private string _restoredPlayerName = string.Empty;
    private string _restoredAnimType = "cut";
    private string? _serverUrl;
    private int _seatId;
    private string? _bgImageUrl;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private bool _manualReconnecting;
    private volatile bool _isDisposingHub;
    private readonly SemaphoreSlim _registerGate = new(1, 1);

    /// <summary>書き画面アンロード時に Hub を破棄しない（AppServices 管理）。再接続は止めない。</summary>
    public bool DetachedFromDispose { get; set; }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsReconnecting =>
        _manualReconnecting || _connection?.State == HubConnectionState.Reconnecting;
    public bool IsRevealed => _restoredRevealed;

    public event Action? DuplicateSeat;
    public event Action<IReadOnlyList<StrokeData>>? RestoreStrokes;
    public event Action? Locked;
    public event Action? Unlocked;
    public event Action? Cleared;
    public event Action? ClearedStrokesOnly;
    public event Action<string>? BackgroundChanged;
    public event Action<string>? ShowChoice;
    public event Action? ClearChoice;
    public event Action<string>? ShowOverlay;
    public event Action? ClearOverlay;
    public event Action<string>? Reveal;
    public event Action? Hide;
    public event Action<bool>? WritingBlackout;
    public event Action<bool>? JudgeColorModeChanged;
    public event Action<int>? LockOverlayOpacityChanged;
    public event Action<SeatNameOverlayConfig>? SeatNameOverlayChanged;
    public event Action<string>? NameAssigned;
    public event Action? Disconnected;
    public event Action<bool, bool>? ConnectionChanged;

    public async Task ConnectAndRegisterAsync(
        string serverUrl,
        int seatId,
        string? bgImageUrl,
        LaunchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        StopReconnectLoop();
        await DisposeHubSilentlyAsync();

        _serverUrl = ClientApiService.NormalizeServerUrl(serverUrl);
        _seatId = seatId;
        _bgImageUrl = bgImageUrl;
        _manualReconnecting = false;

        _connection = ClientHubConnectionFactory.Create(_serverUrl);
        BindConnectionHandlers(_connection);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        progress?.Report("サーバーに接続中...");
        await _connection.StartAsync(timeout.Token);
        await RegisterClientOnServerAsync(timeout.Token);
        RaiseConnectionChanged();
    }

    private void BindConnectionHandlers(HubConnection connection)
    {
        connection.On<int>(ClientCallbacks.DuplicateSeat, _ =>
        {
            _registerTcs?.TrySetException(new InvalidOperationException("duplicate-seat"));
            DuplicateSeat?.Invoke();
        });

        connection.On<IReadOnlyList<StrokeData>>(ClientCallbacks.RestoreStrokes, strokes =>
        {
            _restoredStrokes = CloneStrokes(strokes);
            try
            {
                _registerTcs?.TrySetResult(true);
                RestoreStrokes?.Invoke(_restoredStrokes);
            }
            catch
            {
                _registerTcs?.TrySetResult(true);
            }
        });

        connection.On(ClientCallbacks.Lock, () => Locked?.Invoke());
        connection.On(ClientCallbacks.Unlock, () => Unlocked?.Invoke());
        connection.On(ClientCallbacks.Clear, () => Cleared?.Invoke());
        connection.On(ClientCallbacks.ClearStrokesOnly, () => ClearedStrokesOnly?.Invoke());
        connection.On<string>(ClientCallbacks.BackgroundChanged, url =>
        {
            _restoredBackgroundUrl = url;
            BackgroundChanged?.Invoke(url);
        });
        connection.On<string>(ClientCallbacks.ShowChoice, url =>
        {
            _restoredChoiceUrl = url;
            ShowChoice?.Invoke(url);
        });
        connection.On(ClientCallbacks.ClearChoice, () =>
        {
            _restoredChoiceUrl = null;
            ClearChoice?.Invoke();
        });
        connection.On<string>(ClientCallbacks.ShowOverlay, url =>
        {
            _restoredOverlayUrl = url;
            ShowOverlay?.Invoke(url);
        });
        connection.On(ClientCallbacks.ClearOverlay, () =>
        {
            _restoredOverlayUrl = null;
            ClearOverlay?.Invoke();
        });
        connection.On<string>(ClientCallbacks.Reveal, anim =>
        {
            _restoredRevealed = true;
            _restoredAnimType = string.IsNullOrWhiteSpace(anim) ? "cut" : anim;
            Reveal?.Invoke(_restoredAnimType);
        });
        connection.On(ClientCallbacks.Hide, () =>
        {
            _restoredRevealed = false;
            Hide?.Invoke();
        });
        connection.On<bool>(ClientCallbacks.WritingBlackout, enabled =>
        {
            _restoredWritingBlackout = enabled;
            WritingBlackout?.Invoke(enabled);
        });
        connection.On<bool>(ClientCallbacks.JudgeColorMode, enabled =>
            JudgeColorModeChanged?.Invoke(enabled));
        connection.On<int>(ClientCallbacks.LockOverlayOpacity, percent =>
        {
            _restoredLockOverlayOpacity = Math.Clamp(percent, 0, 100);
            LockOverlayOpacityChanged?.Invoke(_restoredLockOverlayOpacity);
        });
        connection.On<SeatNameOverlayConfig>(ClientCallbacks.SeatNameOverlay, config =>
        {
            _restoredSeatNameOverlay = config ?? new SeatNameOverlayConfig();
            _restoredSeatNameOverlay.Normalize();
            SeatNameOverlayChanged?.Invoke(_restoredSeatNameOverlay);
        });
        connection.On<string>(ClientCallbacks.NameAssigned, name =>
        {
            _restoredPlayerName = name ?? string.Empty;
            NameAssigned?.Invoke(_restoredPlayerName);
        });

        connection.Reconnecting += _ =>
        {
            _manualReconnecting = true;
            RaiseConnectionChanged();
            return Task.CompletedTask;
        };

        connection.Reconnected += async _ =>
        {
            try
            {
                await RegisterClientOnServerAsync();
                _manualReconnecting = false;
                RaiseConnectionChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClientHub] Re-register after reconnect failed: {ex}");
                ScheduleReconnectLoop();
            }
        };

        connection.Closed += _ =>
        {
            if (_isDisposingHub)
                return Task.CompletedTask;

            _manualReconnecting = true;
            RaiseConnectionChanged();
            Disconnected?.Invoke();
            ScheduleReconnectLoop();
            return Task.CompletedTask;
        };
    }

    private async Task RegisterClientOnServerAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Hub is not connected.");

        await _registerGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Hub is not connected.");

            _registerTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            await _connection.InvokeAsync("RegisterClient", _seatId, _bgImageUrl ?? string.Empty, timeout.Token);

            var registerTask = _registerTcs.Task;
            var completed = await Task.WhenAny(registerTask, Task.Delay(TimeSpan.FromSeconds(15), timeout.Token));
            if (completed != registerTask)
                throw new InvalidOperationException("サーバーからの登録応答がありません。");

            await registerTask;
        }
        finally
        {
            _registerGate.Release();
        }
    }

    private void ScheduleReconnectLoop()
    {
        if (string.IsNullOrWhiteSpace(_serverUrl))
            return;

        if (_reconnectTask is { IsCompleted: false })
            return;

        StopReconnectLoop();
        _reconnectCts = new CancellationTokenSource();
        _manualReconnecting = true;
        RaiseConnectionChanged();
        _reconnectTask = RunReconnectLoopAsync(_reconnectCts.Token);
    }

    private void StopReconnectLoop()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = null;
        _reconnectTask = null;
    }

    private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
    {
        var delaySeconds = 2;
        try
        {
            while (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(_serverUrl))
            {
                if (!await ClientApiService.PingHealthAsync(_serverUrl!, cancellationToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    delaySeconds = Math.Min(15, delaySeconds + 1);
                    continue;
                }

                if (_connection?.State == HubConnectionState.Connected)
                {
                    try
                    {
                        await RegisterClientOnServerAsync(cancellationToken);
                        _manualReconnecting = false;
                        RaiseConnectionChanged();
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ClientHub] Register during reconnect failed: {ex}");
                    }
                }

                try
                {
                    await DisposeHubSilentlyAsync();
                    _connection = ClientHubConnectionFactory.Create(_serverUrl!);
                    BindConnectionHandlers(_connection);
                    await _connection.StartAsync(cancellationToken);
                    await RegisterClientOnServerAsync(cancellationToken);
                    _manualReconnecting = false;
                    RaiseConnectionChanged();
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClientHub] Reconnect loop failed: {ex}");
                }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds = Math.Min(15, delaySeconds + 2);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested
                && !string.IsNullOrWhiteSpace(_serverUrl)
                && _connection?.State != HubConnectionState.Connected)
            {
                ScheduleReconnectLoop();
            }
        }
    }

    private async Task DisposeHubSilentlyAsync()
    {
        if (_connection is null)
            return;

        _isDisposingHub = true;
        try
        {
            var old = _connection;
            _connection = null;
            try
            {
                await old.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClientHub] StopAsync failed: {ex}");
            }

            try
            {
                await old.DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClientHub] DisposeAsync failed: {ex}");
            }
        }
        finally
        {
            _isDisposingHub = false;
        }
    }

    public void ReplayRestoredStrokes()
    {
        if (_restoredStrokes is null) return;
        RestoreStrokes?.Invoke(_restoredStrokes);
    }

    public void ReplayRestoredBackground()
    {
        if (string.IsNullOrWhiteSpace(_restoredBackgroundUrl)) return;
        BackgroundChanged?.Invoke(_restoredBackgroundUrl);
    }

    public void ReplayRestoredChoice()
    {
        if (string.IsNullOrWhiteSpace(_restoredChoiceUrl)) return;
        ShowChoice?.Invoke(_restoredChoiceUrl);
    }

    public void ReplayRestoredOverlay()
    {
        if (string.IsNullOrWhiteSpace(_restoredOverlayUrl)) return;
        ShowOverlay?.Invoke(_restoredOverlayUrl);
    }

    public void ReplayRestoredReveal()
    {
        if (_restoredRevealed)
            Reveal?.Invoke(_restoredAnimType);
    }

    public void ReplayRestoredWritingBlackout() =>
        WritingBlackout?.Invoke(_restoredWritingBlackout);

    public void ReplayRestoredLockOverlayOpacity() =>
        LockOverlayOpacityChanged?.Invoke(_restoredLockOverlayOpacity);

    public void ReplayRestoredSeatNameOverlay() =>
        SeatNameOverlayChanged?.Invoke(_restoredSeatNameOverlay);

    public void ReplayRestoredNameAssigned() =>
        NameAssigned?.Invoke(_restoredPlayerName);

    public string GetRestoredPlayerName() => _restoredPlayerName;

    public bool GetRestoredWritingBlackout() => _restoredWritingBlackout;

    public string? GetRestoredBackgroundUrl() => _restoredBackgroundUrl;

    public string? GetRestoredChoiceUrl() => _restoredChoiceUrl;

    private static List<StrokeData> CloneStrokes(IReadOnlyList<StrokeData> strokes) =>
        strokes.Select(CloneStroke).ToList();

    private static StrokeData CloneStroke(StrokeData source) => new()
    {
        Tool = source.Tool,
        Color = source.Color,
        Size = source.Size,
        SrcW = source.SrcW,
        SrcH = source.SrcH,
        Points = source.Points.Select(p => new StrokePoint { X = p.X, Y = p.Y }).ToList()
    };

    private void RaiseConnectionChanged()
    {
        var state = _connection?.State ?? HubConnectionState.Disconnected;
        var reconnecting = IsReconnecting;
        var connected = state == HubConnectionState.Connected && !reconnecting;
        ConnectionChanged?.Invoke(connected, reconnecting);
    }

    public Task SendBackgroundChangedAsync(string bgImageUrl) =>
        InvokeWhenConnected("ClientBackgroundChanged", bgImageUrl);

    public Task SendStrokeStartAsync(StrokeData stroke) =>
        InvokeWhenConnected("StrokeStart", stroke);

    public Task SendStrokePointAsync(StrokePoint point) =>
        InvokeWhenConnected("StrokePoint", point);

    public Task SendStrokeEndAsync() =>
        InvokeWhenConnected("StrokeEnd");

    public Task SendClearCanvasAsync() =>
        InvokeWhenConnected("ClearCanvas");

    public Task SendClientConfirmAsync() =>
        InvokeWhenConnected("ClientConfirm");

    private Task InvokeWhenConnected(string method, object? arg = null)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return arg is null
            ? _connection.InvokeAsync(method)
            : _connection.InvokeAsync(method, arg);
    }

    public async Task DisconnectAsync()
    {
        StopReconnectLoop();
        _manualReconnecting = false;
        _serverUrl = null;

        await DisposeHubSilentlyAsync();
        _restoredStrokes = null;
        _restoredBackgroundUrl = null;
        _restoredChoiceUrl = null;
        _restoredOverlayUrl = null;
        _restoredRevealed = false;
        _restoredWritingBlackout = false;
        RaiseConnectionChanged();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
