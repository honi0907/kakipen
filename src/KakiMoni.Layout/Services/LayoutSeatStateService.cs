using KakiMoni.Core.Models;
using KakiMoni_Layout.Models;

namespace KakiMoni_Layout.Services;

public sealed class LayoutSeatStateService
{
    private readonly Dictionary<int, SeatDisplayModel> _seats = new();

    public LayoutSeatStateService()
    {
        for (var i = 1; i <= 10; i++)
            _seats[i] = new SeatDisplayModel { SeatId = i };
    }

    public IReadOnlyDictionary<int, SeatDisplayModel> All => _seats;

    public void ApplyFullState(IReadOnlyList<SeatClientState> states)
    {
        // 親機と同様、既存セルが購読している同一モデルを更新するだけにする。
        // ここで BindSeats→RebuildCells すると描画クリアや判定画像のタイミングが壊れる。
        foreach (var seat in states)
        {
            if (_seats.TryGetValue(seat.SeatId, out var model))
                model.ApplyState(seat);
        }
    }

    public void BeginStroke(int seatId, StrokeData stroke)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.BeginStroke(stroke);
    }

    public void AddPoint(int seatId, StrokePoint point)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.AddPoint(point);
    }

    public void EndStroke(int seatId)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.EndStroke();
    }

    public void ClearStrokes(int seatId)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.ClearStrokes();
    }

    public void LockSeat(int seatId)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.IsLocked = true;
    }

    public void UnlockSeat(int seatId)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.IsLocked = false;
    }

    public void LockAll()
    {
        foreach (var model in _seats.Values)
            model.IsLocked = true;
    }

    public void UnlockAll()
    {
        foreach (var model in _seats.Values)
            model.IsLocked = false;
    }

    public void DisconnectSeat(int seatId)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.ApplyState(new SeatClientState { SeatId = seatId });
    }

    public void MarkConnected(int seatId)
    {
        // FullState follows
    }

    public void SetChoice(string url)
    {
        var value = string.IsNullOrWhiteSpace(url) ? null : url;
        foreach (var model in _seats.Values)
            model.ChoiceImageUrl = value;
    }

    public void SetRevealed(int seatId, bool revealed)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.Revealed = revealed;
    }

    public void SetOverlay(int seatId, string url)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.OverlayImageUrl = string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public void SetWritingBlackout(int seatId, bool enabled)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.WritingBlackout = enabled;
    }
}
