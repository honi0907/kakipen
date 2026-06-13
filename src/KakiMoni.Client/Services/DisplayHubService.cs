using KakiMoni.Core.Models;
using KakiMoni.Core.Protocol;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.UI.Dispatching;

namespace KakiMoni_Client.Services;

public sealed class DisplayHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private DispatcherQueue? _uiQueue;
    private List<StrokeData>? _restoredStrokes;
    private string? _restoredBackgroundUrl;
    private string? _restoredChoiceUrl;
    private string? _restoredOverlayUrl;
    private bool _restoredRevealed;
    private string _restoredAnimType = "cut";
    private int _seatId;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsRevealed => _restoredRevealed;
    public string? RestoredBackgroundUrl => _restoredBackgroundUrl;

    public event Action<IReadOnlyList<StrokeData>>? RestoreStrokes;
    public event Action<string>? BackgroundChanged;
    public event Action<string>? ShowChoice;
    public event Action? ClearChoice;
    public event Action<StrokeData>? StrokeStartReceived;
    public event Action<StrokePoint>? StrokePointReceived;
    public event Action? StrokeEndReceived;
    public event Action? ClearedStrokesOnly;
    public event Action<string>? Reveal;
    public event Action? Hide;
    public event Action<string>? ShowOverlay;
    public event Action? ClearOverlay;
    public event Action<bool>? JudgeColorModeChanged;
    public event Action<bool, bool>? ConnectionChanged;

    public async Task ConnectAsync(
        string serverUrl,
        int seatId,
        DispatcherQueue? uiQueue = null,
        CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        _seatId = seatId;
        _uiQueue = uiQueue;
        _connection = ClientHubConnectionFactory.Create(serverUrl);

        _connection.On<IReadOnlyList<StrokeData>>(ClientCallbacks.RestoreStrokes, strokes =>
        {
            _restoredStrokes = strokes.ToList();
            InvokeUi(() => RestoreStrokes?.Invoke(_restoredStrokes));
        });
        _connection.On<string>(ClientCallbacks.BackgroundChanged, url =>
        {
            _restoredBackgroundUrl = url;
            InvokeUi(() => BackgroundChanged?.Invoke(url));
        });
        _connection.On<string>(ClientCallbacks.ShowChoice, url =>
        {
            _restoredChoiceUrl = url;
            InvokeUi(() => ShowChoice?.Invoke(url));
        });
        _connection.On(ClientCallbacks.ClearChoice, () =>
        {
            _restoredChoiceUrl = null;
            InvokeUi(() => ClearChoice?.Invoke());
        });
        _connection.On<StrokeData>(ClientCallbacks.StrokeStart, stroke =>
            InvokeUi(() => StrokeStartReceived?.Invoke(stroke)));
        _connection.On<StrokePoint>(ClientCallbacks.StrokePoint, point =>
            InvokeUi(() => StrokePointReceived?.Invoke(point)));
        _connection.On(ClientCallbacks.StrokeEnd, () =>
            InvokeUi(() => StrokeEndReceived?.Invoke()));
        _connection.On(ClientCallbacks.ClearStrokesOnly, () =>
            InvokeUi(() => ClearedStrokesOnly?.Invoke()));
        _connection.On<string>(ClientCallbacks.Reveal, anim =>
        {
            _restoredRevealed = true;
            _restoredAnimType = anim;
            InvokeUi(() => Reveal?.Invoke(anim));
        });
        _connection.On(ClientCallbacks.Hide, () =>
        {
            _restoredRevealed = false;
            InvokeUi(() => Hide?.Invoke());
        });
        _connection.On<string>(ClientCallbacks.ShowOverlay, url =>
        {
            _restoredOverlayUrl = url;
            InvokeUi(() => ShowOverlay?.Invoke(url));
        });
        _connection.On(ClientCallbacks.ClearOverlay, () =>
        {
            _restoredOverlayUrl = null;
            InvokeUi(() => ClearOverlay?.Invoke());
        });
        _connection.On<bool>(ClientCallbacks.JudgeColorMode, enabled =>
            InvokeUi(() => JudgeColorModeChanged?.Invoke(enabled)));

        _connection.Reconnecting += _ =>
        {
            InvokeUi(() => RaiseConnectionChanged());
            return Task.CompletedTask;
        };
        _connection.Reconnected += async _ =>
        {
            InvokeUi(() => RaiseConnectionChanged());
            if (_connection is null) return;
            try
            {
                await _connection.InvokeAsync("RegisterClientDisplay", _seatId);
                InvokeUi(() =>
                {
                    ReplayDrawingState();
                    if (_restoredRevealed)
                        Reveal?.Invoke(_restoredAnimType);
                });
            }
            catch { }
        };
        _connection.Closed += _ =>
        {
            InvokeUi(() => RaiseConnectionChanged());
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("RegisterClientDisplay", seatId, cancellationToken);
        InvokeUi(() => RaiseConnectionChanged());
    }

    public void ReplayInitialState()
    {
        ReplayDrawingState();
        if (_restoredRevealed)
            Reveal?.Invoke(_restoredAnimType);
        else
            Hide?.Invoke();
    }

    public void ReplayDrawingState()
    {
        if (_restoredStrokes is not null)
            RestoreStrokes?.Invoke(_restoredStrokes);
        if (!string.IsNullOrWhiteSpace(_restoredBackgroundUrl))
            BackgroundChanged?.Invoke(_restoredBackgroundUrl);
        if (!string.IsNullOrWhiteSpace(_restoredChoiceUrl))
            ShowChoice?.Invoke(_restoredChoiceUrl);
        if (!string.IsNullOrWhiteSpace(_restoredOverlayUrl))
            ShowOverlay?.Invoke(_restoredOverlayUrl);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        await _connection.DisposeAsync();
        _connection = null;
        _uiQueue = null;
        _restoredStrokes = null;
        _restoredBackgroundUrl = null;
        _restoredChoiceUrl = null;
        _restoredOverlayUrl = null;
        _restoredRevealed = false;
        RaiseConnectionChanged();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    private void InvokeUi(Action action)
    {
        if (_uiQueue is null)
        {
            action();
            return;
        }

        if (_uiQueue.HasThreadAccess)
            action();
        else
            _uiQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }

    private void RaiseConnectionChanged()
    {
        var state = _connection?.State ?? HubConnectionState.Disconnected;
        ConnectionChanged?.Invoke(
            state == HubConnectionState.Connected,
            state == HubConnectionState.Reconnecting);
    }
}
