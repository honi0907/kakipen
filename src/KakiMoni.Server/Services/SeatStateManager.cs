using KakiMoni.Core.Models;

namespace KakiMoni.Server.Services;

public enum RegisterClientResult
{
    Success,
    DuplicateSeat,
    InvalidSeat
}

public sealed class SeatStateManager
{
    private readonly object _gate = new();
    private readonly Dictionary<int, SeatClientState> _seats = new();
    private readonly Dictionary<string, int> _connectionToSeat = new();

    public RegisterClientResult RegisterClient(int seatId, string connectionId, string? bgImageUrl)
    {
        if (seatId is < 1 or > 10)
            return RegisterClientResult.InvalidSeat;

        lock (_gate)
        {
            if (_seats.TryGetValue(seatId, out var existing) &&
                !string.IsNullOrEmpty(existing.ConnectionId) &&
                existing.ConnectionId != connectionId)
            {
                return RegisterClientResult.DuplicateSeat;
            }

            if (!_seats.TryGetValue(seatId, out var seat))
            {
                EnsureSeatLocked(seatId);
                seat = _seats[seatId];
            }

            if (!string.IsNullOrEmpty(seat.ConnectionId) && seat.ConnectionId != connectionId)
                _connectionToSeat.Remove(seat.ConnectionId);

            seat.ConnectionId = connectionId;
            if (!string.IsNullOrWhiteSpace(bgImageUrl))
                seat.BgImageUrl = bgImageUrl;

            _connectionToSeat[connectionId] = seatId;
            return RegisterClientResult.Success;
        }
    }

    public SeatClientState? GetSeat(int seatId)
    {
        lock (_gate)
            return _seats.TryGetValue(seatId, out var seat) ? seat : null;
    }

    public IReadOnlyList<SeatClientState> GetAllSeats()
    {
        lock (_gate)
            return _seats.Values.OrderBy(s => s.SeatId).ToList();
    }

    public int? GetSeatIdByConnection(string connectionId)
    {
        lock (_gate)
            return _connectionToSeat.TryGetValue(connectionId, out var seatId) ? seatId : null;
    }

    public void OnDisconnected(string connectionId)
    {
        lock (_gate)
        {
            if (!_connectionToSeat.TryGetValue(connectionId, out var seatId))
                return;

            _connectionToSeat.Remove(connectionId);
            if (_seats.TryGetValue(seatId, out var seat) && seat.ConnectionId == connectionId)
                seat.ConnectionId = null;
        }
    }

    public bool TryBeginStroke(int seatId, StrokeData stroke, out SeatClientState? seat)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out seat!) || seat.Locked)
                return false;

            seat.CurrentStroke = stroke;
            return true;
        }
    }

    public bool TryAddPoint(int seatId, StrokePoint point)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out var seat) || seat.Locked || seat.CurrentStroke is null)
                return false;

            seat.CurrentStroke.Points.Add(point);
            return true;
        }
    }

    public bool TryEndStroke(int seatId, out SeatClientState? seat)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out seat!) || seat.CurrentStroke is null)
                return false;

            seat.Strokes.Add(seat.CurrentStroke);
            seat.CurrentStroke = null;
            return true;
        }
    }

    public void SetBackground(int seatId, string bgImageUrl)
    {
        lock (_gate)
        {
            if (_seats.TryGetValue(seatId, out var seat))
                seat.BgImageUrl = bgImageUrl ?? string.Empty;
        }
    }

    public void SetLocked(int seatId, bool locked)
    {
        lock (_gate)
        {
            if (_seats.TryGetValue(seatId, out var seat))
                seat.Locked = locked;
        }
    }

    public void SetAllLocked(bool locked)
    {
        lock (_gate)
        {
            for (var seatId = 1; seatId <= 10; seatId++)
            {
                if (!_seats.TryGetValue(seatId, out var seat))
                {
                    EnsureSeatLocked(seatId);
                    seat = _seats[seatId];
                }

                seat.Locked = locked;
            }
        }
    }

    public IEnumerable<SeatClientState> GetConnectedSeats()
    {
        lock (_gate)
            return _seats.Values.Where(s => !string.IsNullOrEmpty(s.ConnectionId)).ToList();
    }

    public void ClearStrokes(int seatId)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out var seat))
                return;

            seat.Strokes.Clear();
            seat.CurrentStroke = null;
        }
    }

    public void SetRevealed(int seatId, bool revealed, string animType = "cut")
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out var seat))
                return;

            seat.Revealed = revealed;
            seat.AnimType = animType;
        }
    }

    public void SetOverlay(int seatId, string? overlayUrl)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out var seat))
                return;

            seat.OverlayImageUrl = overlayUrl ?? string.Empty;
        }
    }

    public void SetWritingBlackout(int seatId, bool enabled)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(seatId, out var seat))
                return;

            seat.WritingBlackout = enabled;
        }
    }

    public void RefreshSeatNames(bool useFile, SeatNameFileService nameFiles)
    {
        lock (_gate)
        {
            for (var seatId = 1; seatId <= 10; seatId++)
            {
                EnsureSeatLocked(seatId);
                _seats[seatId].Name = useFile ? nameFiles.GetNameForSeat(seatId) : string.Empty;
            }
        }
    }

    private void EnsureSeatLocked(int seatId)
    {
        if (!_seats.ContainsKey(seatId))
            _seats[seatId] = new SeatClientState { SeatId = seatId, Name = string.Empty };
    }
}
