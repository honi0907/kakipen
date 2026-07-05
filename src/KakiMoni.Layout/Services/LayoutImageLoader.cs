using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace KakiMoni_Layout.Services;

public sealed class LayoutImageLoader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string? _serverBaseUrl;

    public LayoutImageLoader(string? serverBaseUrl = null)
    {
        _serverBaseUrl = serverBaseUrl ?? AppLayoutContext.ServerBaseUrl;
    }

    public async Task<BitmapImage?> LoadThumbnailAsync(string urlOrRelative, int decodeWidth = 192)
    {
        if (Uri.TryCreate(urlOrRelative, UriKind.Absolute, out _))
            return await LoadThumbnailFromHttpAsync(urlOrRelative, decodeWidth);

        if (TryBuildServerAbsoluteUrl(urlOrRelative, out var serverUrl))
            return await LoadThumbnailFromHttpAsync(serverUrl, decodeWidth);

        return null;
    }

    public async Task<CanvasBitmap?> LoadCanvasBitmapAsync(
        string urlOrRelative,
        CancellationToken cancellationToken = default)
    {
        if (Uri.TryCreate(urlOrRelative, UriKind.Absolute, out _))
            return await LoadCanvasBitmapFromHttpAsync(urlOrRelative, cancellationToken);

        if (TryBuildServerAbsoluteUrl(urlOrRelative, out var serverUrl))
            return await LoadCanvasBitmapFromHttpAsync(serverUrl, cancellationToken);

        return null;
    }

    private bool TryBuildServerAbsoluteUrl(string urlOrRelative, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(urlOrRelative) || !urlOrRelative.StartsWith('/'))
            return false;

        var baseUrl = _serverBaseUrl ?? AppLayoutContext.ServerBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;

        absoluteUrl = baseUrl.TrimEnd('/') + urlOrRelative;
        return true;
    }

    private static async Task<BitmapImage?> LoadThumbnailFromHttpAsync(string absoluteUrl, int decodeWidth)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(absoluteUrl);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = decodeWidth };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<CanvasBitmap?> LoadCanvasBitmapFromHttpAsync(
        string absoluteUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(absoluteUrl, cancellationToken);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            var device = CanvasDevice.GetSharedDevice();
            return await CanvasBitmap.LoadAsync(device, stream);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
