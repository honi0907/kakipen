using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.SignalR.Client;

namespace KakiMoni_Client.Services;

internal static class ClientHubConnectionFactory
{
    public static HubConnection Create(string serverUrl)
    {
        var baseUrl = ClientApiService.NormalizeServerUrl(serverUrl);
        return new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hub", options =>
            {
                options.HttpMessageHandlerFactory = _ => new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All
                };
            })
            .WithAutomaticReconnect()
            .WithServerTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }
}
