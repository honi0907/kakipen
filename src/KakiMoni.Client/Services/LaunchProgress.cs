using Microsoft.UI.Dispatching;

namespace KakiMoni_Client.Services;

public sealed class LaunchProgress
{
    private DispatcherQueue? _dispatcher;

    public string Message { get; private set; } = "サーバーに接続中...";

    public event Action? Changed;

    public void BindDispatcher(DispatcherQueue dispatcher) => _dispatcher = dispatcher;

    public void Report(string message)
    {
        Message = message;
        if (_dispatcher is not null)
            _dispatcher.TryEnqueue(RaiseChanged);
        else
            RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke();
}
