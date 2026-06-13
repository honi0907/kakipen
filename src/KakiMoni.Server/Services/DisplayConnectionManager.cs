namespace KakiMoni.Server.Services;

public sealed class DisplayConnectionManager
{
    private readonly object _gate = new();
    private readonly Dictionary<int, string> _seatToConnection = new();
    private readonly Dictionary<string, int> _connectionToSeat = new();

    public void RegisterDisplay(int seatId, string connectionId)
    {
        lock (_gate)
        {
            if (_seatToConnection.TryGetValue(seatId, out var oldId) && oldId != connectionId)
                _connectionToSeat.Remove(oldId);

            _seatToConnection[seatId] = connectionId;
            _connectionToSeat[connectionId] = seatId;
        }
    }

    public void OnDisconnected(string connectionId)
    {
        lock (_gate)
        {
            if (!_connectionToSeat.TryGetValue(connectionId, out var seatId))
                return;

            _connectionToSeat.Remove(connectionId);
            if (_seatToConnection.TryGetValue(seatId, out var current) && current == connectionId)
                _seatToConnection.Remove(seatId);
        }
    }

    public string? GetConnectionId(int seatId)
    {
        lock (_gate)
            return _seatToConnection.TryGetValue(seatId, out var id) ? id : null;
    }

    public int? GetSeatId(string connectionId)
    {
        lock (_gate)
            return _connectionToSeat.TryGetValue(connectionId, out var seatId) ? seatId : null;
    }
}
