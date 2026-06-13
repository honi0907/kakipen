using System.Collections.Concurrent;
using KakiMoni.Core.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace KakiMoni_Client.Services;

public sealed class BackgroundImageService
{
    public enum LoadState { Idle, Loading, Ready, Failed }

    private const int ThumbnailWidth = 192;
    private const int MaxCacheEntries = 20;
    private const int MaxConcurrent = 2;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly ConcurrentDictionary<string, string> _entryMeta = new();
    private readonly SemaphoreSlim _gate = new(MaxConcurrent, MaxConcurrent);
    private readonly LinkedList<string> _lru = new();
    private readonly object _lruGate = new();

    public static string BuildRelativePath(string fileName) =>
        "/backgrounds/" + Uri.EscapeDataString(fileName);

    public static bool IsTiffFile(string fileName) =>
        fileName.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase);

    public static string ToAbsoluteUrl(string serverUrl, string? relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute)) return string.Empty;
        var baseUri = new Uri(ClientApiService.NormalizeServerUrl(serverUrl) + "/");
        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var absolute))
            return absolute.AbsoluteUri;
        var relative = relativeOrAbsolute.StartsWith('/') ? relativeOrAbsolute[1..] : relativeOrAbsolute;
        return new Uri(baseUri, relative).AbsoluteUri;
    }

    /// <summary>
    /// サーバー上の背景をバイトキャッシュへ先読み。
    /// ファイル名が同じでもサイズ・更新日時が変われば再取得する。
    /// </summary>
    public async Task<int> WarmCacheFromServerAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var baseUrl = ClientApiService.NormalizeServerUrl(serverUrl);
        var entries = await ClientApiService.GetBackgroundEntriesAsync(baseUrl, cancellationToken);
        var liveUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var url = ToAbsoluteUrl(baseUrl, entry.RelativeUrl);
            liveUrls.Add(url);
            var meta = BuildMetaKey(entry);

            if (string.Equals(_entryMeta.GetValueOrDefault(url), meta, StringComparison.Ordinal)
                && _cache.ContainsKey(url))
            {
                continue;
            }

            Invalidate(url);
            await DownloadBytesAsync(url, cancellationToken);
            _entryMeta[url] = meta;
        }

        foreach (var url in _entryMeta.Keys.ToList())
        {
            if (liveUrls.Contains(url)) continue;
            Invalidate(url);
        }

        return entries.Count;
    }

    /// <summary>
    /// 背景一覧を差分同期し、指定席のファイルは毎回サーバーから取り直す。
    /// </summary>
    public async Task<int> RefreshBackgroundsForSeatAsync(
        string serverUrl,
        string? seatRelativeUrl,
        CancellationToken cancellationToken = default)
    {
        var count = await WarmCacheFromServerAsync(serverUrl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(seatRelativeUrl))
            InvalidateUrl(serverUrl, seatRelativeUrl);
        return count;
    }

    /// <summary>
    /// サーバー上の選択肢画像をバイトキャッシュへ先読み。
    /// </summary>
    public async Task<int> WarmChoicesFromServerAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var baseUrl = ClientApiService.NormalizeServerUrl(serverUrl);
        var entries = await ClientApiService.GetChoiceEntriesAsync(baseUrl, cancellationToken);
        var liveUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var url = ToAbsoluteUrl(baseUrl, entry.RelativeUrl);
            liveUrls.Add(url);
            var meta = BuildMetaKey(entry);

            if (string.Equals(_entryMeta.GetValueOrDefault(url), meta, StringComparison.Ordinal)
                && _cache.ContainsKey(url))
            {
                continue;
            }

            Invalidate(url);
            await DownloadBytesAsync(url, cancellationToken);
            _entryMeta[url] = meta;
        }

        foreach (var url in _entryMeta.Keys.ToList())
        {
            if (liveUrls.Contains(url)) continue;
            if (!url.Contains("/choices/", StringComparison.OrdinalIgnoreCase)) continue;
            Invalidate(url);
        }

        return entries.Count;
    }

    /// <summary>サーバー上のロゴ画像をバイトキャッシュへ先読み。</summary>
    public async Task<LogoWarmResult> WarmLogoFromServerAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        var logoUrl = await ClientApiService.GetLogoAsync(serverUrl, cancellationToken);
        if (string.IsNullOrWhiteSpace(logoUrl))
            return new LogoWarmResult(null, null);

        var absolute = ToAbsoluteUrl(serverUrl, logoUrl);
        await DownloadBytesAsync(absolute, cancellationToken);
        var fileName = Path.GetFileName(logoUrl.Trim('/').Split('/').LastOrDefault() ?? string.Empty);
        return new LogoWarmResult(fileName, logoUrl);
    }

    public void ClearCache()
    {
        _cache.Clear();
        _entryMeta.Clear();
        lock (_lruGate)
            _lru.Clear();
    }

    /// <summary>指定 URL のキャッシュのみ破棄。</summary>
    public void InvalidateUrl(string serverUrl, string relativeUrl)
    {
        var url = ToAbsoluteUrl(serverUrl, relativeUrl);
        Invalidate(url);
    }

    private static string BuildMetaKey(BackgroundFileEntry entry) =>
        $"{entry.SizeBytes}:{entry.LastModifiedUtcTicks}";

    private void Invalidate(string url)
    {
        _entryMeta.TryRemove(url, out _);
        _cache.TryRemove(url, out _);
        lock (_lruGate)
            _lru.Remove(url);
    }

    public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(url, out var cached))
        {
            Touch(url);
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(url, out cached))
            {
                Touch(url);
                return cached;
            }

            var bytes = await Http.GetByteArrayAsync(url, cancellationToken);
            Store(url, bytes);
            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BitmapImage?> CreateThumbnailAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await DownloadBytesAsync(url, cancellationToken);
            return await CreateBitmapAsync(bytes, ThumbnailWidth);
        }
        catch
        {
            return null;
        }
    }

    public async Task<BitmapImage?> LoadBitmapImageAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await DownloadBytesAsync(url, cancellationToken);
            return await CreateFullBitmapAsync(bytes);
        }
        catch
        {
            return null;
        }
    }

    public async Task<CanvasBitmap?> LoadCanvasBitmapAsync(CanvasControl? control, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await DownloadBytesAsync(url, cancellationToken);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            // CanvasControl 未 Loaded / 初回 Draw 前は LoadAsync(control) が失敗するため共有デバイスを使う
            var device = CanvasDevice.GetSharedDevice();
            return await CanvasBitmap.LoadAsync(device, stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadCanvasBitmapAsync failed ({url}): {ex}");
            return null;
        }
    }

    private async Task<BitmapImage> CreateBitmapAsync(byte[] bytes, int decodeWidth)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        var bitmap = new BitmapImage { DecodePixelWidth = decodeWidth };
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private async Task<BitmapImage> CreateFullBitmapAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private void Store(string url, byte[] bytes)
    {
        _cache[url] = bytes;
        Touch(url);
        lock (_lruGate)
        {
            while (_lru.Count > MaxCacheEntries)
            {
                var oldest = _lru.Last?.Value;
                _lru.RemoveLast();
                if (oldest is not null)
                {
                    _cache.TryRemove(oldest, out _);
                    _entryMeta.TryRemove(oldest, out _);
                }
            }
        }
    }

    private void Touch(string url)
    {
        lock (_lruGate)
        {
            _lru.Remove(url);
            _lru.AddFirst(url);
        }
    }
}
