using System.Net.Http.Json;
using KakiMoni.Core.Models;

namespace KakiMoni_Layout.Services;

public static class LayoutApiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<IReadOnlyList<BackgroundFileEntry>> GetBackgroundsAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/backgrounds";
        var entries = await Http.GetFromJsonAsync<List<BackgroundFileEntry>>(url, cancellationToken);
        return entries ?? new List<BackgroundFileEntry>();
    }
}
