using KakiMoni.Core.Models;
using KakiMoni.Core.Protocol;
using KakiMoni_Layout.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace KakiMoni_Layout.Services;

public sealed class LayoutHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private string? _baseUrl;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<bool, bool>? ConnectionChanged;
    public event Action<string, HostDisplayLayout>? DisplayLayoutChanged;

    public async Task ConnectAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        _baseUrl = baseUrl.TrimEnd('/');
        AppLayoutContext.ServerBaseUrl = _baseUrl;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hub")
            .WithAutomaticReconnect()
            .Build();

        WireCallbacks();

        _connection.Reconnecting += _ =>
        {
            RaiseConnectionChanged();
            return Task.CompletedTask;
        };

        _connection.Reconnected += async _ =>
        {
            RaiseConnectionChanged();
            try
            {
                if (_connection.State == HubConnectionState.Connected)
                    await _connection.InvokeAsync("RegisterLayoutController", cancellationToken);
            }
            catch { }
        };

        _connection.Closed += _ =>
        {
            RaiseConnectionChanged();
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("RegisterLayoutController", cancellationToken);
        RaiseConnectionChanged();
    }

    private void WireCallbacks()
    {
        if (_connection is null)
            return;

        var seats = AppLayoutContext.Seats;

        _connection.On<IReadOnlyList<SeatClientState>>(HostCallbacks.FullState, list =>
            seats.ApplyFullState(list));

        _connection.On<int, StrokeData>(HostCallbacks.StrokeStart, seats.BeginStroke);
        _connection.On<int, StrokePoint>(HostCallbacks.StrokePoint, seats.AddPoint);
        _connection.On<int>(HostCallbacks.StrokeEnd, seats.EndStroke);
        _connection.On<int>(HostCallbacks.CanvasCleared, seats.ClearStrokes);
        _connection.On<int>(HostCallbacks.SeatLocked, seats.LockSeat);
        _connection.On<int>(HostCallbacks.SeatUnlocked, seats.UnlockSeat);
        _connection.On(HostCallbacks.AllLocked, seats.LockAll);
        _connection.On(HostCallbacks.AllUnlocked, seats.UnlockAll);
        _connection.On<int>(HostCallbacks.ClientDisconnected, seats.DisconnectSeat);
        _connection.On<int>(HostCallbacks.ClientRegistered, id => seats.MarkConnected(id));

        _connection.On<string>(HostCallbacks.ChoiceChanged, url =>
            seats.SetChoice(url));

        _connection.On<int, string>(HostCallbacks.SeatRevealed, (id, _) => seats.SetRevealed(id, true));
        _connection.On<int>(HostCallbacks.SeatHidden, id => seats.SetRevealed(id, false));
        _connection.On<int, string>(HostCallbacks.JudgeResult, seats.SetOverlay);
        _connection.On<int, bool>(HostCallbacks.SeatWritingBlackout, seats.SetWritingBlackout);

        _connection.On<SeatNameOverlayConfig>(ClientCallbacks.SeatNameOverlay, config =>
        {
            var normalized = config ?? new SeatNameOverlayConfig();
            normalized.Normalize();
            AppLayoutContext.SeatNameOverlay = normalized;
            AppLayoutContext.DisplayOutput.RefreshSeatNameOverlays();
        });

        _connection.On<string, HostDisplayLayout>(LayoutCallbacks.DisplayLayoutChanged, (group, layout) =>
        {
            AppLayoutContext.SetSlotLayout(group, layout);
            LayoutDisplayLayoutStore.SaveForSlot(group, layout);
            DisplayLayoutChanged?.Invoke(group, layout);
            AppLayoutContext.DisplayOutput.ApplyLayout(group, layout);
        });
    }

    public Task PushDisplayLayoutAsync(string group, HostDisplayLayout layout)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
            return Task.CompletedTask;
        return _connection.InvokeAsync("PushDisplayLayout", group, layout);
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
        if (_connection is null)
            return;
        await _connection.DisposeAsync();
        _connection = null;
        RaiseConnectionChanged();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
