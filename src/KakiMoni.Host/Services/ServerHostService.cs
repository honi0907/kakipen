using KakiMoni.Core.Network;
using KakiMoni.Server;

namespace KakiMoni_Host.Services;

public sealed class ServerHostService
{
    private readonly ServerBootstrap _bootstrap = new();
    private string _localBaseUrl = "http://127.0.0.1:3000";
    private string _childBaseUrl = string.Empty;
    private string? _lastStopMessage;

    public ServerHostService()
    {
        _bootstrap.Stopped += OnBootstrapStopped;
    }

    public bool IsRunning => _bootstrap.IsRunning;

    /// <summary>親機自身の API / Hub 用（127.0.0.1）。</summary>
    public string LocalBaseUrl => _localBaseUrl;

    /// <summary>子機端末向け LAN URL。</summary>
    public string ChildBaseUrl => _childBaseUrl;

    /// <summary>互換: 内部呼び出しは LocalBaseUrl と同じ。</summary>
    public string BaseUrl => _localBaseUrl;

    public string? LastStopMessage => _lastStopMessage;

    public event EventHandler? StateChanged;

    public async Task StartAsync(
        string contentRoot,
        int port,
        HostSettings networkSettings,
        bool useSeatNameFile = false,
        CancellationToken cancellationToken = default)
    {
        _lastStopMessage = null;
        var bindAddress = HostNetworkService.ResolveBindAddress(networkSettings);
        await _bootstrap.StartAsync(contentRoot, port, bindAddress, useSeatNameFile, cancellationToken);
        _localBaseUrl = LanAddressResolver.BuildLocalBaseUrl(port);
        _childBaseUrl = HostNetworkService.BuildChildBaseUrl(port, networkSettings);
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
