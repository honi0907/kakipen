using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KakiMoni.Core.Models;

namespace KakiMoni_Host.Services;

public static class SaveGalleryApiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly HttpClient LiveHttp = new() { Timeout = Timeout.InfiniteTimeSpan };
    public static async Task<SaveGalleryResult> GetGalleryAsync(
        string baseUrl,
        IReadOnlyList<int> seatIds,
        int maxPerSeat,
        CancellationToken cancellationToken = default)
    {
        var root = baseUrl.TrimEnd('/');
        var ids = string.Join(',', seatIds);
        var url =
            $"{root}/api/save-gallery?ids={Uri.EscapeDataString(ids)}&maxPerSeat={maxPerSeat}";

        var payload = await Http.GetFromJsonAsync<GalleryApiResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("サーバーから空の応答が返りました");

        if (!payload.Ok)
            throw new InvalidOperationException(payload.Error ?? "一覧の取得に失敗しました");

        return MapResult(payload, root);
    }

    public static async Task RunLiveStreamAsync(
        string baseUrl,
        Action onUpdated,
        CancellationToken cancellationToken)
    {
        var root = baseUrl.TrimEnd('/');
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/api/save-gallery/live");
                using var response = await LiveHttp.SendAsync(                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line is null)
                        break;

                    if (line.StartsWith("event: save-gallery-updated", StringComparison.Ordinal))
                        onUpdated();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    public static string NormalizeServerUrl(string? raw)
    {
        var url = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url;

        return url.TrimEnd('/');
    }

    public static string ToAbsoluteUrl(string baseUrl, string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath))
            return string.Empty;

        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            return absolute.ToString();

        var root = baseUrl.TrimEnd('/');
        return urlOrPath.StartsWith('/')
            ? root + urlOrPath
            : root + "/" + urlOrPath;
    }

    private static SaveGalleryResult MapResult(GalleryApiResponse payload, string baseUrl)
    {
        var bySeat = new Dictionary<int, SaveGallerySeatBucket>();
        foreach (var pair in payload.BySeat)
        {
            var bucket = pair.Value;
            var items = bucket.Items
                .Select(item => new SaveGalleryEntry
                {
                    SeatId = item.SeatId,
                    Session = item.Session,
                    Counter = item.Counter,
                    Type = item.Type,
                    FileName = item.FileName,
                    FilePath = ToAbsoluteUrl(baseUrl, item.ThumbnailUrl),
                    Size = item.Size,
                    UpdatedAt = item.UpdatedAt,
                    UpdatedAtMs = item.UpdatedAtMs
                })
                .ToArray();

            bySeat[bucket.SeatId] = new SaveGallerySeatBucket
            {
                SeatId = bucket.SeatId,
                TotalCount = bucket.TotalCount,
                Items = items
            };
        }

        return new SaveGalleryResult
        {
            Ok = true,
            SeatIds = payload.SeatIds,
            MaxPerSeat = payload.MaxPerSeat,
            Total = payload.Total,
            BySeat = bySeat
        };
    }

    private sealed class GalleryApiResponse
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public List<int> SeatIds { get; set; } = new();
        public int MaxPerSeat { get; set; }
        public int Total { get; set; }
        public Dictionary<string, GallerySeatBucketDto> BySeat { get; set; } = new();
    }

    private sealed class GallerySeatBucketDto
    {
        public int SeatId { get; set; }
        public int TotalCount { get; set; }
        public List<GalleryItemDto> Items { get; set; } = new();
    }

    private sealed class GalleryItemDto
    {
        public int SeatId { get; set; }
        public int Session { get; set; }
        public int Counter { get; set; }
        public string Type { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public long UpdatedAtMs { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        public string ThumbnailUrl { get; set; } = string.Empty;
    }
}
