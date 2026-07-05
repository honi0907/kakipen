using KakiMoni.Core.Models;
using KakiMoni_Host.Controls;
using KakiMoni_Host.Display;
using Microsoft.UI.Dispatching;

namespace KakiMoni_Host.Services;

public sealed class HostDisplayOutputService
{
    private HostDisplayWindow? _window;
    private DispatcherQueue? _uiQueue;
    private HostDisplayLayout? _layout;
    private IReadOnlyDictionary<int, SeatCardModel>? _seats;

    public bool IsOpen => _window is not null;

    public string StatusText
    {
        get
        {
            if (!IsOpen)
                return "外部出力: オフ";
            if (_layout is not { HasCells: true })
                return "外部出力: オン（レイアウト未設定）";
            return "外部出力: オン";
        }
    }

    public void BindSeats(IReadOnlyDictionary<int, SeatCardModel> seats)
    {
        _seats = seats;
        _uiQueue?.TryEnqueue(() =>
        {
            _window?.BindSeats(seats);
        });
    }

    public void ApplyLayout(HostDisplayLayout layout)
    {
        _layout = layout;
        HostDisplayLayoutStore.Save(layout);
        _uiQueue?.TryEnqueue(() =>
        {
            _window?.ApplyLayout(layout);
        });
    }

    public HostDisplayLayout GetCurrentLayout() =>
        _layout ?? HostDisplayLayoutStore.Load();

    public async Task TryOpenAsync(DispatcherQueue uiQueue)
    {
        _uiQueue = uiQueue;
        _layout ??= HostDisplayLayoutStore.Load();

        var prev = _window;
        _window = null;
        if (prev is not null)
            uiQueue.TryEnqueue(() => prev.Close());

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        uiQueue.TryEnqueue(() =>
        {
            try
            {
                _window = new HostDisplayWindow();
                if (_seats is not null)
                    _window.BindSeats(_seats);
                _window.ApplyLayout(_layout);
                _window.ShowOnDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HostDisplayOutput] open failed: {ex}");
            }
            finally
            {
                tcs.TrySetResult();
            }
        });
        await tcs.Task;
    }

    public Task CloseAsync()
    {
        var window = _window;
        _window = null;
        if (window is null || _uiQueue is null)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiQueue.TryEnqueue(() =>
        {
            window.Close();
            tcs.TrySetResult();
        });
        return tcs.Task;
    }
}
