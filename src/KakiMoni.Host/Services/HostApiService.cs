using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KakiMoni.Core.Models;

namespace KakiMoni_Host.Services;

public static class HostApiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<IReadOnlyList<SeatStatusEntry>> GetSeatsStatusAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/seats";
        var entries = await Http.GetFromJsonAsync<List<SeatStatusEntry>>(url, cancellationToken);
        return entries ?? new List<SeatStatusEntry>();
    }

    public static async Task<IReadOnlyList<BackgroundFileEntry>> GetChoicesAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/choices";
        var entries = await Http.GetFromJsonAsync<List<BackgroundFileEntry>>(url, cancellationToken);
        return entries ?? new List<BackgroundFileEntry>();
    }
}

public sealed class SeatStatusEntry
{
    [JsonPropertyName("seatId")]
    public int SeatId { get; set; }

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("strokeCount")]
    public int StrokeCount { get; set; }
}
