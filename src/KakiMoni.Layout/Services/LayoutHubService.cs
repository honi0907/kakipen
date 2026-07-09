using KakiMoni.Core.Models;
using KakiMoni.Core.Protocol;
using KakiMoni_Layout.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.UI.Dispatching;

namespace KakiMoni_Layout.Services;

public sealed class LayoutHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private string? _baseUrl;
    private DispatcherQueue? _uiQueue;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<bool, bool>? ConnectionChanged;
    public event Action<string, HostDisplayLayout>? DisplayLayoutChanged;

    public void SetUiQueue(DispatcherQueue uiQueue) => _uiQueue = uiQueue;

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
            OnUi(() => seats.ApplyFullState(list)));

        _connection.On<int, StrokeData>(HostCallbacks.StrokeStart, (id, stroke) =>
            OnUi(() => seats.BeginStroke(id, stroke)));
        _connection.On<int, StrokePoint>(HostCallbacks.StrokePoint, (id, point) =>
            OnUi(() => seats.AddPoint(id, point)));
        _connection.On<int>(HostCallbacks.StrokeEnd, id =>
            OnUi(() => seats.EndStroke(id)));
        _connection.On<int>(HostCallbacks.CanvasCleared, id =>
            OnUi(() => seats.ClearStrokes(id)));
        _connection.On<int>(HostCallbacks.SeatLocked, id =>
            OnUi(() => seats.LockSeat(id)));
        _connection.On<int>(HostCallbacks.SeatUnlocked, id =>
            OnUi(() => seats.UnlockSeat(id)));
        _connection.On(HostCallbacks.AllLocked, () =>
            OnUi(seats.LockAll));
        _connection.On(HostCallbacks.AllUnlocked, () =>
            OnUi(seats.UnlockAll));
        _connection.On<int>(HostCallbacks.ClientDisconnected, id =>
            OnUi(() => seats.DisconnectSeat(id)));
        _connection.On<int>(HostCallbacks.ClientRegistered, id =>
            OnUi(() => seats.MarkConnected(id)));

        _connection.On<string>(HostCallbacks.ChoiceChanged, url =>
            OnUi(() => seats.SetChoice(url)));

        _connection.On<int, string>(HostCallbacks.SeatRevealed, (id, _) =>
            OnUi(() => seats.SetRevealed(id, true)));
        _connection.On<int>(HostCallbacks.SeatHidden, id =>
            OnUi(() => seats.SetRevealed(id, false)));
        _connection.On<int, string>(HostCallbacks.JudgeResult, (id, url) =>
            OnUi(() => seats.SetOverlay(id, url)));
        _connection.On<int, bool>(HostCallbacks.SeatWritingBlackout, (id, enabled) =>
            OnUi(() => seats.SetWritingBlackout(id, enabled)));

        _connection.On<SeatNameOverlayConfig>(ClientCallbacks.SeatNameOverlay, config =>
        {
            OnUi(() =>
            {
                var normalized = config ?? new SeatNameOverlayConfig();
                normalized.Normalize();
                AppLayoutContext.SeatNameOverlay = normalized;
                AppLayoutContext.DisplayOutput.RefreshSeatNameOverlays();
            });
        });

        _connection.On<string, HostDisplayLayout>(LayoutCallbacks.DisplayLayoutChanged, (group, layout) =>
        {
            OnUi(() =>
            {
                AppLayoutContext.SetSlotLayout(group, layout);
                LayoutDisplayLayoutStore.SaveForSlot(group, layout);
                DisplayLayoutChanged?.Invoke(group, layout);
                AppLayoutContext.DisplayOutput.ApplyLayout(group, layout);
            });
        });
    }

    private void OnUi(Action action)
    {
        var queue = _uiQueue;
        if (queue is null || queue.HasThreadAccess)
        {
            action();
            return;
        }

        queue.TryEnqueue(() => action());
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
