using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KakiMoni.Core.Models;

namespace KakiMoni_Client.Services;

public static class ClientApiService
{
    public const string DefaultServerUrl = "http://127.0.0.1:3000";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>
    /// Windows では localhost が IPv6 待ちで数秒かかることがあるため 127.0.0.1 に寄せる。
    /// </summary>
    public static string NormalizeServerUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        if (!uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return url;

        var builder = new UriBuilder(uri) { Host = "127.0.0.1" };
        return builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    public static async Task<bool> PingHealthAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = NormalizeServerUrl(serverUrl);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/health");
            request.Headers.ConnectionClose = true;
            using var response = await Http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<IReadOnlyList<BackgroundFileEntry>> GetBackgroundEntriesAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        var entries = await Http.GetFromJsonAsync<List<BackgroundFileEntry>>(
            $"{baseUrl}/api/backgrounds", cancellationToken);
        return entries ?? new List<BackgroundFileEntry>();
    }

    public static async Task<IReadOnlyList<string>> GetBackgroundsAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var entries = await GetBackgroundEntriesAsync(serverUrl, cancellationToken);
        return entries.Select(e => e.FileName).ToList();
    }

    public static async Task<IReadOnlyList<BackgroundFileEntry>> GetChoiceEntriesAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        var entries = await Http.GetFromJsonAsync<List<BackgroundFileEntry>>(
            $"{baseUrl}/api/choices", cancellationToken);
        return entries ?? new List<BackgroundFileEntry>();
    }

    public static async Task<string?> GetLogoAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        var dto = await Http.GetFromJsonAsync<LogoDto>($"{baseUrl}/api/logo", cancellationToken);
        return string.IsNullOrWhiteSpace(dto?.Url) ? null : dto.Url;
    }

    public static async Task<SeatBackgroundInfo?> GetSeatBackgroundAsync(
        string serverUrl,
        int seatId,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        using var response = await Http.GetAsync($"{baseUrl}/api/backgrounds/seat/{seatId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<SeatBackgroundDto>(cancellationToken: cancellationToken);
        return dto is null ? null : new SeatBackgroundInfo(dto.FileName, dto.RelativeUrl);
    }

    private sealed class LogoDto
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    private sealed class SeatBackgroundDto
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("relativeUrl")]
        public string RelativeUrl { get; set; } = string.Empty;
    }
}

public sealed record SeatBackgroundInfo(string FileName, string RelativeUrl);

public sealed record LogoWarmResult(string? FileName, string? RelativeUrl);
