using KakiMoni.Server;

namespace KakiMoni_Host.Services;

public sealed class ServerHostService
{
    private readonly ServerBootstrap _bootstrap = new();
    private string _baseUrl = "http://localhost:3000";

    public bool IsRunning => _bootstrap.IsRunning;
    public string BaseUrl => _baseUrl;

    public event EventHandler? StateChanged;

    public async Task StartAsync(string contentRoot, int port, bool useSeatNameFile = false, CancellationToken cancellationToken = default)
    {
        await _bootstrap.StartAsync(contentRoot, port, useSeatNameFile, cancellationToken);
        _baseUrl = $"http://localhost:{port}";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _bootstrap.StopAsync(cancellationToken);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
