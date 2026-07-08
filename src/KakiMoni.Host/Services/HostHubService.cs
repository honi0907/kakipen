using KakiMoni.Core.Models;
using KakiMoni.Core.Protocol;
using Microsoft.AspNetCore.SignalR.Client;

namespace KakiMoni_Host.Services;

public sealed class HostHubService : IAsyncDisposable
{
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<IReadOnlyList<SeatClientState>>? FullStateReceived;
    public event Action<int, StrokeData>? StrokeStartReceived;
    public event Action<int, StrokePoint>? StrokePointReceived;
    public event Action<int>? StrokeEndReceived;
    public event Action<int>? SeatLockedReceived;
    public event Action<int>? SeatUnlockedReceived;
    public event Action? AllLockedReceived;
    public event Action? AllUnlockedReceived;
    public event Action<int>? ClientDisconnectedReceived;
    public event Action<int>? ClientRegisteredReceived;
    public event Action<int>? CanvasClearedReceived;
    public event Action<string>? ChoiceChangedReceived;
    public event Action<int, string>? SeatRevealedReceived;
    public event Action<int>? SeatHiddenReceived;
    public event Action<int, string>? JudgeResultReceived;
    public event Action<int, bool>? SeatWritingBlackoutReceived;
    public event Action<bool, bool>? ConnectionChanged;
    public event Action<long>? AssetsCatalogChangedReceived;

    public async Task ConnectAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl.TrimEnd('/')}/hub")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<IReadOnlyList<SeatClientState>>(HostCallbacks.FullState, seats =>
            FullStateReceived?.Invoke(seats));

        _connection.On<int, StrokeData>(HostCallbacks.StrokeStart, (seatId, stroke) =>
            StrokeStartReceived?.Invoke(seatId, stroke));

        _connection.On<int, StrokePoint>(HostCallbacks.StrokePoint, (seatId, point) =>
            StrokePointReceived?.Invoke(seatId, point));

        _connection.On<int>(HostCallbacks.StrokeEnd, seatId =>
            StrokeEndReceived?.Invoke(seatId));

        _connection.On<int>(HostCallbacks.SeatLocked, seatId =>
            SeatLockedReceived?.Invoke(seatId));

        _connection.On<int>(HostCallbacks.SeatUnlocked, seatId =>
            SeatUnlockedReceived?.Invoke(seatId));

        _connection.On(HostCallbacks.AllLocked, () =>
            AllLockedReceived?.Invoke());

        _connection.On(HostCallbacks.AllUnlocked, () =>
            AllUnlockedReceived?.Invoke());

        _connection.On<int>(HostCallbacks.ClientDisconnected, seatId =>
            ClientDisconnectedReceived?.Invoke(seatId));

        _connection.On<int>(HostCallbacks.ClientRegistered, seatId =>
            ClientRegisteredReceived?.Invoke(seatId));

        _connection.On<int>(HostCallbacks.CanvasCleared, seatId =>
            CanvasClearedReceived?.Invoke(seatId));

        _connection.On<string>(HostCallbacks.ChoiceChanged, url =>
            ChoiceChangedReceived?.Invoke(url));

        _connection.On<int, string>(HostCallbacks.SeatRevealed, (seatId, anim) =>
            SeatRevealedReceived?.Invoke(seatId, anim));

        _connection.On<int>(HostCallbacks.SeatHidden, seatId =>
            SeatHiddenReceived?.Invoke(seatId));

        _connection.On<int, string>(HostCallbacks.JudgeResult, (seatId, url) =>
            JudgeResultReceived?.Invoke(seatId, url));

        _connection.On<int, bool>(HostCallbacks.SeatWritingBlackout, (seatId, enabled) =>
            SeatWritingBlackoutReceived?.Invoke(seatId, enabled));

        _connection.On<long>(HostCallbacks.AssetsCatalogChanged, revision =>
            AssetsCatalogChangedReceived?.Invoke(revision));

        _connection.Reconnecting += _ =>
        {
            RaiseConnectionChanged();
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            RaiseConnectionChanged();
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            RaiseConnectionChanged();
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("RegisterHost", cancellationToken);
        RaiseConnectionChanged();
    }

    public Task HostLockAsync(int seatId) =>
        InvokeWhenConnected("HostLock", seatId);

    public Task HostUnlockAsync(int seatId) =>
        InvokeWhenConnected("HostUnlock", seatId);

    public Task HostLockAllAsync() =>
        InvokeWhenConnected("HostLockAll");

    public Task HostUnlockAllAsync() =>
        InvokeWhenConnected("HostUnlockAll");

    public Task HostClearStrokesOnlyAsync(int seatId) =>
        InvokeWhenConnected("HostClearStrokesOnly", seatId);

    public Task HostShowChoiceAsync(string choiceImageUrl) =>
        InvokeWhenConnected("HostShowChoice", choiceImageUrl);

    public Task HostClearChoiceAsync() =>
        InvokeWhenConnected("HostClearChoice");

    public Task HostRevealAsync(int seatId, string animType = "cut") =>
        InvokeWhenConnected("HostReveal", seatId, animType);

    public Task HostHideAsync(int seatId) =>
        InvokeWhenConnected("HostHide", seatId);

    public Task HostJudgeAsync(int seatId, string kind) =>
        InvokeWhenConnected("HostJudge", seatId, kind, (string?)null);

    public Task HostClearOverlayAsync() =>
        InvokeWhenConnected("HostClearOverlay");

    public Task HostStandbyAsync() =>
        InvokeWhenConnected("HostStandby");

    public Task HostSetWritingBlackoutAsync(int seatId, bool enabled) =>
        InvokeWhenConnected("HostSetWritingBlackout", seatId, enabled);

    public Task HostSetJudgeColorModeAsync(bool enabled) =>
        InvokeWhenConnected("HostSetJudgeColorMode", enabled);

    public Task HostSetLockOverlayOpacityAsync(int percent)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync("HostSetLockOverlayOpacity", percent);
    }

    public Task HostSetUseSeatNameFileAsync(bool enabled) =>
        InvokeWhenConnected("HostSetUseSeatNameFile", enabled);

    public Task HostSetSeatNameOverlayAsync(SeatNameOverlayConfig config)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync("HostSetSeatNameOverlay", config);
    }

    private Task InvokeWhenConnected(string method, bool arg)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method, arg);
    }

    private Task InvokeWhenConnected(string method, int seatId)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method, seatId);
    }

    private Task InvokeWhenConnected(string method, int seatId, string arg)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method, seatId, arg);
    }

    private Task InvokeWhenConnected(string method, int seatId, string arg1, string? arg2)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method, seatId, arg1, arg2);
    }

    private Task InvokeWhenConnected(string method, int seatId, bool arg)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method, seatId, arg);
    }

    private Task InvokeWhenConnected(string method, string arg)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method, arg);
    }

    private Task InvokeWhenConnected(string method)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync(method);
    }

    private void RaiseConnectionChanged()
    {
        var state = _connection?.State ?? HubConnectionState.Disconnected;
        ConnectionChanged?.Invoke(
            state == HubConnectionState.Connected,
            state == HubConnectionState.Reconnecting);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        await _connection.DisposeAsync();
        _connection = null;
        RaiseConnectionChanged();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
