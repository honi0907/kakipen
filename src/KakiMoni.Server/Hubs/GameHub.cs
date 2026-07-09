using KakiMoni.Core.Models;
using KakiMoni.Core.Protocol;
using KakiMoni.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace KakiMoni.Server.Hubs;

public sealed class GameHub : Hub
{
    private const string HostsGroup = "hosts";
    private const string LayoutControllersGroup = "layout-controllers";
    private readonly SeatStateManager _seats;
    private readonly DisplayConnectionManager _displays;
    private readonly BackgroundFileService _backgrounds;
    private readonly OverlayFileService _overlays;
    private readonly GameSessionState _session;
    private readonly SeatNameFileService _seatNames;
    private readonly LayoutDisplayLayoutManager _layoutLayouts;

    public GameHub(
        SeatStateManager seats,
        DisplayConnectionManager displays,
        BackgroundFileService backgrounds,
        OverlayFileService overlays,
        GameSessionState session,
        SeatNameFileService seatNames,
        LayoutDisplayLayoutManager layoutLayouts)
    {
        _seats = seats;
        _displays = displays;
        _backgrounds = backgrounds;
        _overlays = overlays;
        _session = session;
        _seatNames = seatNames;
        _layoutLayouts = layoutLayouts;
    }

    private IClientProxy HostAndLayoutClients => Clients.Groups(HostsGroup, LayoutControllersGroup);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_displays.GetSeatId(Context.ConnectionId) is not null)
        {
            _displays.OnDisconnected(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
            return;
        }

        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        _seats.OnDisconnected(Context.ConnectionId);
        if (seatId is not null)
        {
            await HostAndLayoutClients.SendAsync(HostCallbacks.ClientDisconnected, seatId.Value);
            await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterHost()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HostsGroup);
        await Clients.Caller.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
        await Clients.Caller.SendAsync(HostCallbacks.ChoiceChanged, _session.CurrentChoiceUrl ?? string.Empty);
        await Clients.Caller.SendAsync(ClientCallbacks.SeatNameOverlay, _session.SeatNameOverlay);
    }

    public async Task RegisterLayoutController()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LayoutControllersGroup);
        await Clients.Caller.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
        await Clients.Caller.SendAsync(HostCallbacks.ChoiceChanged, _session.CurrentChoiceUrl ?? string.Empty);
        await Clients.Caller.SendAsync(ClientCallbacks.SeatNameOverlay, _session.SeatNameOverlay);
        foreach (var slot in LayoutDisplaySlots.All)
        {
            if (_layoutLayouts.Get(slot) is { } layout)
                await Clients.Caller.SendAsync(LayoutCallbacks.DisplayLayoutChanged, slot, layout);
        }
    }

    public async Task PushDisplayLayout(string group, HostDisplayLayout layout)
    {
        if (!LayoutDisplaySlots.IsValid(group))
            return;

        var groupId = group.Trim();
        _layoutLayouts.Set(groupId, layout);
        await Clients.Group(LayoutControllersGroup).SendAsync(LayoutCallbacks.DisplayLayoutChanged, groupId, layout);
    }

    public async Task RegisterClient(int seatId, string? bgImageUrl)
    {
        var result = _seats.RegisterClient(seatId, Context.ConnectionId, bgImageUrl);
        if (result == RegisterClientResult.DuplicateSeat)
        {
            await Clients.Caller.SendAsync(ClientCallbacks.DuplicateSeat, seatId);
            return;
        }

        if (result == RegisterClientResult.InvalidSeat)
            return;

        var autoBg = _backgrounds.TryResolveSeatBackground(seatId);
        if (autoBg is not null)
            _seats.SetBackground(seatId, autoBg);
        else if (!string.IsNullOrWhiteSpace(bgImageUrl))
            _seats.SetBackground(seatId, bgImageUrl);

        var seat = _seats.GetSeat(seatId)!;
        await Clients.Caller.SendAsync(ClientCallbacks.RestoreStrokes, seat.Strokes.ToList());
        if (seat.Locked)
            await Clients.Caller.SendAsync(ClientCallbacks.Lock);
        if (!string.IsNullOrWhiteSpace(seat.BgImageUrl))
            await Clients.Caller.SendAsync(ClientCallbacks.BackgroundChanged, seat.BgImageUrl);
        await Clients.Caller.SendAsync(ClientCallbacks.NameAssigned, seat.Name ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(_session.CurrentChoiceUrl))
            await Clients.Caller.SendAsync(ClientCallbacks.ShowChoice, _session.CurrentChoiceUrl);
        if (!string.IsNullOrWhiteSpace(seat.OverlayImageUrl))
            await Clients.Caller.SendAsync(ClientCallbacks.ShowOverlay, seat.OverlayImageUrl);
        if (seat.Revealed)
            await Clients.Caller.SendAsync(ClientCallbacks.Reveal, seat.AnimType ?? "cut");
        else
            await Clients.Caller.SendAsync(ClientCallbacks.Hide);
        await Clients.Caller.SendAsync(ClientCallbacks.WritingBlackout, seat.WritingBlackout);
        await Clients.Caller.SendAsync(ClientCallbacks.JudgeColorMode, _session.JudgeColorMode);
        await Clients.Caller.SendAsync(ClientCallbacks.LockOverlayOpacity, _session.LockOverlayOpacityPercent);
        await Clients.Caller.SendAsync(ClientCallbacks.SeatNameOverlay, _session.SeatNameOverlay);

        await HostAndLayoutClients.SendAsync(HostCallbacks.ClientRegistered, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task RegisterClientDisplay(int seatId)
    {
        if (seatId is < 1 or > 10)
            return;

        _displays.RegisterDisplay(seatId, Context.ConnectionId);

        if (_seats.GetSeat(seatId) is null)
        {
            await Clients.Caller.SendAsync(ClientCallbacks.RestoreStrokes, Array.Empty<StrokeData>());
            if (!string.IsNullOrWhiteSpace(_session.CurrentChoiceUrl))
                await Clients.Caller.SendAsync(ClientCallbacks.ShowChoice, _session.CurrentChoiceUrl);
            await Clients.Caller.SendAsync(ClientCallbacks.Hide);
            await Clients.Caller.SendAsync(ClientCallbacks.JudgeColorMode, _session.JudgeColorMode);
            await Clients.Caller.SendAsync(ClientCallbacks.LockOverlayOpacity, _session.LockOverlayOpacityPercent);
            await Clients.Caller.SendAsync(ClientCallbacks.SeatNameOverlay, _session.SeatNameOverlay);
            return;
        }

        var seat = _seats.GetSeat(seatId)!;
        await Clients.Caller.SendAsync(ClientCallbacks.RestoreStrokes, seat.Strokes.ToList());
        if (!string.IsNullOrWhiteSpace(seat.BgImageUrl))
            await Clients.Caller.SendAsync(ClientCallbacks.BackgroundChanged, seat.BgImageUrl);
        if (!string.IsNullOrWhiteSpace(_session.CurrentChoiceUrl))
            await Clients.Caller.SendAsync(ClientCallbacks.ShowChoice, _session.CurrentChoiceUrl);
        if (!string.IsNullOrWhiteSpace(seat.OverlayImageUrl))
            await Clients.Caller.SendAsync(ClientCallbacks.ShowOverlay, seat.OverlayImageUrl);
        if (seat.Revealed)
            await Clients.Caller.SendAsync(ClientCallbacks.Reveal, seat.AnimType ?? "cut");
        else
            await Clients.Caller.SendAsync(ClientCallbacks.Hide);
        await Clients.Caller.SendAsync(ClientCallbacks.JudgeColorMode, _session.JudgeColorMode);
        await Clients.Caller.SendAsync(ClientCallbacks.LockOverlayOpacity, _session.LockOverlayOpacityPercent);
        await Clients.Caller.SendAsync(ClientCallbacks.SeatNameOverlay, _session.SeatNameOverlay);
    }

    public async Task HostSetJudgeColorMode(bool enabled)
    {
        _session.JudgeColorMode = enabled;
        await Clients.All.SendAsync(ClientCallbacks.JudgeColorMode, enabled);
    }

    public async Task HostSetLockOverlayOpacity(int percent)
    {
        _session.LockOverlayOpacityPercent = Math.Clamp(percent, 0, 100);
        await Clients.All.SendAsync(ClientCallbacks.LockOverlayOpacity, _session.LockOverlayOpacityPercent);
    }

    public async Task HostSetSeatNameOverlay(SeatNameOverlayConfig config)
    {
        _session.SeatNameOverlay = config ?? new SeatNameOverlayConfig();
        _session.SeatNameOverlay.Normalize();
        await Clients.All.SendAsync(ClientCallbacks.SeatNameOverlay, _session.SeatNameOverlay);
    }

    public async Task HostSetUseSeatNameFile(bool enabled)
    {
        _session.UseSeatNameFile = enabled;
        _seats.RefreshSeatNames(enabled, _seatNames);

        foreach (var seat in _seats.GetConnectedSeats())
        {
            if (string.IsNullOrEmpty(seat.ConnectionId))
                continue;

            await Clients.Client(seat.ConnectionId)
                .SendAsync(ClientCallbacks.NameAssigned, seat.Name ?? string.Empty);
        }

        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostShowChoice(string choiceImageUrl)
    {
        _session.CurrentChoiceUrl = string.IsNullOrWhiteSpace(choiceImageUrl) ? null : choiceImageUrl.Trim();
        await BroadcastChoiceAsync();
    }

    public async Task HostClearChoice()
    {
        _session.CurrentChoiceUrl = null;
        await BroadcastChoiceAsync();
    }

    private async Task BroadcastChoiceAsync()
    {
        var url = _session.CurrentChoiceUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            foreach (var seat in _seats.GetConnectedSeats())
                await Clients.Client(seat.ConnectionId!).SendAsync(ClientCallbacks.ClearChoice);
            foreach (var seatId in GetConnectedDisplaySeatIds())
                await SendToDisplayAsync(seatId, ClientCallbacks.ClearChoice);
            await HostAndLayoutClients.SendAsync(HostCallbacks.ChoiceChanged, string.Empty);
            return;
        }

        foreach (var seat in _seats.GetConnectedSeats())
            await Clients.Client(seat.ConnectionId!).SendAsync(ClientCallbacks.ShowChoice, url);
        foreach (var seatId in GetConnectedDisplaySeatIds())
            await SendToDisplayAsync(seatId, ClientCallbacks.ShowChoice, url);
        await HostAndLayoutClients.SendAsync(HostCallbacks.ChoiceChanged, url);
    }

    public async Task HostReveal(int seatId, string? animType)
    {
        var anim = string.IsNullOrWhiteSpace(animType) ? "cut" : animType.Trim();
        _seats.SetRevealed(seatId, true, anim);
        await SendToDisplayAsync(seatId, ClientCallbacks.Reveal, anim);
        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Reveal, anim);
        await HostAndLayoutClients.SendAsync(HostCallbacks.SeatRevealed, seatId, anim);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostHide(int seatId)
    {
        _seats.SetRevealed(seatId, false);
        await SendToDisplayAsync(seatId, ClientCallbacks.Hide);
        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Hide);
        await HostAndLayoutClients.SendAsync(HostCallbacks.SeatHidden, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostJudge(int seatId, string kind, string? imageUrl)
    {
        var resolved = _overlays.ResolveJudgeUrl(kind, imageUrl);
        _seats.SetOverlay(seatId, resolved);

        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.ShowOverlay, resolved);
        await SendToDisplayAsync(seatId, ClientCallbacks.ShowOverlay, resolved);
        await HostAndLayoutClients.SendAsync(HostCallbacks.JudgeResult, seatId, resolved);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostClearOverlay()
    {
        for (var seatId = 1; seatId <= 10; seatId++)
        {
            _seats.SetOverlay(seatId, null);
            var seat = _seats.GetSeat(seatId);
            if (!string.IsNullOrEmpty(seat?.ConnectionId))
                await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.ClearOverlay);
            await SendToDisplayAsync(seatId, ClientCallbacks.ClearOverlay);
        }

        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    /// <summary>描画・判定・選択肢を全席一括クリア（スタンバイ）。外部出力はロゴ表示に戻す。</summary>
    public async Task HostStandby()
    {
        _session.CurrentChoiceUrl = null;

        for (var seatId = 1; seatId <= 10; seatId++)
        {
            _seats.ClearStrokes(seatId);
            _seats.SetOverlay(seatId, null);
            _seats.SetRevealed(seatId, false);

            var seat = _seats.GetSeat(seatId);
            if (!string.IsNullOrEmpty(seat?.ConnectionId))
            {
                await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Clear);
                await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.ClearOverlay);
                await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.ClearChoice);
                await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Hide);
            }

            await SendToDisplayAsync(seatId, ClientCallbacks.ClearStrokesOnly);
            await SendToDisplayAsync(seatId, ClientCallbacks.ClearOverlay);
            await SendToDisplayAsync(seatId, ClientCallbacks.ClearChoice);
            await SendToDisplayAsync(seatId, ClientCallbacks.Hide);
        }

        await HostAndLayoutClients.SendAsync(HostCallbacks.ChoiceChanged, string.Empty);
        // 親機・レイアウト専用機が FullState 前に描画を即クリアできるように送る
        for (var seatId = 1; seatId <= 10; seatId++)
            await HostAndLayoutClients.SendAsync(HostCallbacks.CanvasCleared, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostSetWritingBlackout(int seatId, bool enabled)
    {
        _seats.SetWritingBlackout(seatId, enabled);

        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.WritingBlackout, enabled);

        await HostAndLayoutClients.SendAsync(HostCallbacks.SeatWritingBlackout, seatId, enabled);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task ClientBackgroundChanged(string bgImageUrl)
    {
        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        if (seatId is null) return;

        _seats.SetBackground(seatId.Value, bgImageUrl);
        await SendToDisplayAsync(seatId.Value, ClientCallbacks.BackgroundChanged, bgImageUrl);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task StrokeStart(StrokeData stroke)
    {
        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        if (seatId is null) return;
        if (!_seats.TryBeginStroke(seatId.Value, stroke, out _))
            return;

        await HostAndLayoutClients.SendAsync(HostCallbacks.StrokeStart, seatId.Value, stroke);
        await SendToDisplayAsync(seatId.Value, ClientCallbacks.StrokeStart, stroke);
    }

    public async Task StrokePoint(StrokePoint point)
    {
        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        if (seatId is null) return;
        if (!_seats.TryAddPoint(seatId.Value, point))
            return;

        await HostAndLayoutClients.SendAsync(HostCallbacks.StrokePoint, seatId.Value, point);
        await SendToDisplayAsync(seatId.Value, ClientCallbacks.StrokePoint, point);
    }

    public async Task StrokeEnd()
    {
        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        if (seatId is null) return;
        if (!_seats.TryEndStroke(seatId.Value, out _))
            return;

        await HostAndLayoutClients.SendAsync(HostCallbacks.StrokeEnd, seatId.Value);
        await SendToDisplayAsync(seatId.Value, ClientCallbacks.StrokeEnd);
        var seat = _seats.GetSeat(seatId.Value);
        if (seat is not null)
            await HostAndLayoutClients.SendAsync(HostCallbacks.SeatStrokesUpdated, seatId.Value, seat.Strokes.ToList());
    }

    public async Task HostLock(int seatId)
    {
        _seats.SetLocked(seatId, true);
        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Lock);
        await HostAndLayoutClients.SendAsync(HostCallbacks.SeatLocked, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostUnlock(int seatId)
    {
        _seats.SetLocked(seatId, false);
        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Unlock);
        await HostAndLayoutClients.SendAsync(HostCallbacks.SeatUnlocked, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostLockAll()
    {
        _seats.SetAllLocked(true);
        foreach (var seat in _seats.GetConnectedSeats())
            await Clients.Client(seat.ConnectionId!).SendAsync(ClientCallbacks.Lock);
        await HostAndLayoutClients.SendAsync(HostCallbacks.AllLocked);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostUnlockAll()
    {
        _seats.SetAllLocked(false);
        foreach (var seat in _seats.GetConnectedSeats())
            await Clients.Client(seat.ConnectionId!).SendAsync(ClientCallbacks.Unlock);
        await HostAndLayoutClients.SendAsync(HostCallbacks.AllUnlocked);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task ClientConfirm()
    {
        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        if (seatId is null) return;

        var seat = _seats.GetSeat(seatId.Value);
        if (seat is null || seat.Locked) return;

        _seats.SetLocked(seatId.Value, true);
        await HostAndLayoutClients.SendAsync(HostCallbacks.SeatLocked, seatId.Value);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostClear(int seatId)
    {
        _seats.ClearStrokes(seatId);
        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.Clear);
        await SendToDisplayAsync(seatId, ClientCallbacks.ClearStrokesOnly);
        await HostAndLayoutClients.SendAsync(HostCallbacks.CanvasCleared, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task HostClearStrokesOnly(int seatId)
    {
        _seats.ClearStrokes(seatId);
        var seat = _seats.GetSeat(seatId);
        if (!string.IsNullOrEmpty(seat?.ConnectionId))
            await Clients.Client(seat.ConnectionId).SendAsync(ClientCallbacks.ClearStrokesOnly);
        await SendToDisplayAsync(seatId, ClientCallbacks.ClearStrokesOnly);
        await HostAndLayoutClients.SendAsync(HostCallbacks.CanvasCleared, seatId);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    public async Task ClearCanvas()
    {
        var seatId = _seats.GetSeatIdByConnection(Context.ConnectionId);
        if (seatId is null) return;
        var seat = _seats.GetSeat(seatId.Value);
        if (seat is null || seat.Locked) return;

        _seats.ClearStrokes(seatId.Value);
        await Clients.Caller.SendAsync(ClientCallbacks.ClearStrokesOnly);
        await SendToDisplayAsync(seatId.Value, ClientCallbacks.ClearStrokesOnly);
        await HostAndLayoutClients.SendAsync(HostCallbacks.CanvasCleared, seatId.Value);
        await HostAndLayoutClients.SendAsync(HostCallbacks.FullState, _seats.GetAllSeats());
    }

    private IEnumerable<int> GetConnectedDisplaySeatIds()
    {
        for (var seatId = 1; seatId <= 10; seatId++)
        {
            if (!string.IsNullOrEmpty(_displays.GetConnectionId(seatId)))
                yield return seatId;
        }
    }

    private Task SendToDisplayAsync(int seatId, string method, object? arg = null)
    {
        var connectionId = _displays.GetConnectionId(seatId);
        if (string.IsNullOrEmpty(connectionId))
            return Task.CompletedTask;

        return arg is null
            ? Clients.Client(connectionId).SendAsync(method)
            : Clients.Client(connectionId).SendAsync(method, arg);
    }
}
