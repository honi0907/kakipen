using KakiMoni.Core.Models;
using KakiMoni_Layout.Display;
using KakiMoni_Layout.Models;
using Microsoft.UI.Dispatching;

namespace KakiMoni_Layout.Services;

public sealed class LayoutDisplayOutputService
{
    private readonly LayoutDisplayWindow?[] _windows = new LayoutDisplayWindow?[3];
    private readonly bool[] _open = new bool[3];
    private DispatcherQueue? _uiQueue;
    private IReadOnlyDictionary<int, SeatDisplayModel>? _seats;

    public bool IsSlotOpen(int slotIndex) => slotIndex is >= 0 and < 3 && _open[slotIndex];

    public void BindSeats(IReadOnlyDictionary<int, SeatDisplayModel> seats)
    {
        _seats = seats;
        _uiQueue?.TryEnqueue(() =>
        {
            for (var i = 0; i < 3; i++)
                _windows[i]?.BindSeats(seats);
        });
    }

    public void ApplyLayout(string slot, HostDisplayLayout layout)
    {
        var index = SlotToIndex(slot);
        if (index < 0)
            return;

        AppLayoutContext.SetSlotLayout(slot, layout);
        LayoutDisplayLayoutStore.SaveForSlot(slot, layout);
        _uiQueue?.TryEnqueue(() => _windows[index]?.ApplyLayout(layout));
    }

    public async Task ToggleSlotAsync(int slotIndex, int monitorIndex, DispatcherQueue uiQueue)
    {
        if (slotIndex is < 0 or > 2)
            return;

        _uiQueue = uiQueue;
        if (_open[slotIndex])
        {
            await CloseSlotAsync(slotIndex, uiQueue);
            return;
        }

        var slot = IndexToSlot(slotIndex);
        var layout = AppLayoutContext.GetSlotLayout(slot);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        uiQueue.TryEnqueue(() =>
        {
            try
            {
                var prev = _windows[slotIndex];
                _windows[slotIndex] = null;
                prev?.Close();

                var window = new LayoutDisplayWindow(slotIndex + 1);
                _windows[slotIndex] = window;
                if (_seats is not null)
                    window.BindSeats(_seats);
                window.ApplyLayout(layout.HasCells ? layout : null);
                window.Closed += (_, _) =>
                {
                    if (_windows[slotIndex] == window)
                    {
                        _windows[slotIndex] = null;
                        _open[slotIndex] = false;
                    }
                };
                window.ShowOnDisplay(monitorIndex);
                _open[slotIndex] = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayoutDisplayOutput] open slot {slotIndex}: {ex}");
            }
            finally
            {
                tcs.TrySetResult();
            }
        });
        await tcs.Task;
    }

    public async Task CloseSlotAsync(int slotIndex, DispatcherQueue uiQueue)
    {
        if (slotIndex is < 0 or > 2 || !_open[slotIndex])
            return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        uiQueue.TryEnqueue(() =>
        {
            try
            {
                _windows[slotIndex]?.Close();
                _windows[slotIndex] = null;
                _open[slotIndex] = false;
            }
            finally
            {
                tcs.TrySetResult();
            }
        });
        await tcs.Task;
    }

    private static int SlotToIndex(string slot) => slot switch
    {
        LayoutDisplaySlots.Slot1 => 0,
        LayoutDisplaySlots.Slot2 => 1,
        LayoutDisplaySlots.Slot3 => 2,
        _ => -1
    };

    private static string IndexToSlot(int index) => index switch
    {
        0 => LayoutDisplaySlots.Slot1,
        1 => LayoutDisplaySlots.Slot2,
        2 => LayoutDisplaySlots.Slot3,
        _ => LayoutDisplaySlots.Slot1
    };
}
