using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KakiMoni.Core.Models;
using KakiMoni.Server.Services;

namespace KakiMoni_Host.Services;

public static class HostSaveApiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static Task<SaveStateDto> GetStateAsync(string baseUrl, CancellationToken cancellationToken = default) =>
        Http.GetFromJsonAsync<SaveStateDto>($"{baseUrl.TrimEnd('/')}/api/save-state", cancellationToken)!;

    public static async Task<SaveStateDto> NextCounterAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        using var response = await Http.PostAsync($"{baseUrl.TrimEnd('/')}/api/save-next-counter", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaveStateDto>(cancellationToken: cancellationToken))!;
    }

    public static async Task<SaveStateDto> SetSessionAsync(
        string baseUrl,
        int session,
        CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(new { session });
        using var response = await Http.PostAsync($"{baseUrl.TrimEnd('/')}/api/save-set-session", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaveStateDto>(cancellationToken: cancellationToken))!;
    }

    public static async Task<SaveStateDto> SetCounterAsync(
        string baseUrl,
        int counter,
        CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(new { counter });
        using var response = await Http.PostAsync($"{baseUrl.TrimEnd('/')}/api/save-set-counter", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaveStateDto>(cancellationToken: cancellationToken))!;
    }

    public static async Task<string> SaveSnapshotAsync(
        string baseUrl,
        int seatId,
        int session,
        int counter,
        string type,
        byte[] pngBytes,
        CancellationToken cancellationToken = default)
    {
        var payload = new SaveSnapshotRequest
        {
            SeatId = seatId,
            Session = session,
            Counter = counter,
            Type = type,
            ImageData = Convert.ToBase64String(pngBytes)
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        using var response = await Http.PostAsync($"{baseUrl.TrimEnd('/')}/api/save-snapshot", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SaveSnapshotResponse>(cancellationToken: cancellationToken);
        return result?.FileName ?? SaveStateService.BuildFileName(session, seatId, counter, type);
    }

    private sealed class SaveSnapshotRequest
    {
        [JsonPropertyName("seatId")]
        public int SeatId { get; set; }

        [JsonPropertyName("session")]
        public int Session { get; set; }

        [JsonPropertyName("counter")]
        public int Counter { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("imageData")]
        public string ImageData { get; set; } = string.Empty;
    }

    private sealed class SaveSnapshotResponse
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }
    }
}
