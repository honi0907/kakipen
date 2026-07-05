using KakiMoni.Server;

namespace KakiMoni_Host.Services;

public sealed class ServerHostService
{
    private readonly ServerBootstrap _bootstrap = new();
    private string _baseUrl = "http://127.0.0.1:3000";
    private string? _lastStopMessage;

    public ServerHostService()
    {
        _bootstrap.Stopped += OnBootstrapStopped;
    }

    public bool IsRunning => _bootstrap.IsRunning;
    public string BaseUrl => _baseUrl;
    public string? LastStopMessage => _lastStopMessage;

    public event EventHandler? StateChanged;

    public async Task StartAsync(string contentRoot, int port, bool useSeatNameFile = false, CancellationToken cancellationToken = default)
    {
        _lastStopMessage = null;
        await _bootstrap.StartAsync(contentRoot, port, useSeatNameFile, cancellationToken);
        _baseUrl = $"http://127.0.0.1:{port}";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _bootstrap.StopAsync(cancellationToken);
        _lastStopMessage = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBootstrapStopped()
    {
        _lastStopMessage = "サーバーが予期せず停止しました。ポートの競合や権限エラーの可能性があります。";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
