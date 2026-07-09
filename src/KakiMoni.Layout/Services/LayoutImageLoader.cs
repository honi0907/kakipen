using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.DirectX;
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
        if (!TryResolveAbsoluteUrl(urlOrRelative, out var absoluteUrl))
            return null;

        if (!IsTiffUrl(absoluteUrl))
        {
            var thumb = await LoadThumbnailFromHttpAsync(absoluteUrl, decodeWidth);
            if (thumb is not null)
                return thumb;
        }

        return await LoadThumbnailViaCanvasAsync(absoluteUrl, decodeWidth);
    }

    public async Task<CanvasBitmap?> LoadCanvasBitmapAsync(
        string urlOrRelative,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveAbsoluteUrl(urlOrRelative, out var absoluteUrl))
            return null;

        return await LoadCanvasBitmapFromHttpAsync(absoluteUrl, cancellationToken);
    }

    private bool TryResolveAbsoluteUrl(string urlOrRelative, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(urlOrRelative))
            return false;

        if (Uri.TryCreate(urlOrRelative, UriKind.Absolute, out _))
        {
            absoluteUrl = urlOrRelative;
            return true;
        }

        return TryBuildServerAbsoluteUrl(urlOrRelative, out absoluteUrl);
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

    private static bool IsTiffUrl(string absoluteUrl)
    {
        var path = absoluteUrl;
        var q = path.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
            path = path[..q];
        return path.EndsWith(".tif", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<BitmapImage?> LoadThumbnailViaCanvasAsync(string absoluteUrl, int decodeWidth)
    {
        using var canvasBitmap = await LoadCanvasBitmapFromHttpAsync(absoluteUrl, CancellationToken.None);
        if (canvasBitmap is null)
            return null;

        try
        {
            return await CreateThumbnailFromCanvasBitmapAsync(canvasBitmap, decodeWidth);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<BitmapImage?> CreateThumbnailFromCanvasBitmapAsync(
        CanvasBitmap source,
        int decodeWidth)
    {
        var srcW = (int)source.SizeInPixels.Width;
        var srcH = (int)source.SizeInPixels.Height;
        if (srcW <= 0 || srcH <= 0)
            return null;

        var targetW = decodeWidth > 0 && srcW > decodeWidth ? decodeWidth : srcW;
        var targetH = Math.Max(1, (int)Math.Round((double)srcH * targetW / srcW));

        using var device = CanvasDevice.GetSharedDevice();
        using var target = new CanvasRenderTarget(
            device,
            targetW,
            targetH,
            96,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            CanvasAlphaMode.Premultiplied);
        using (var ds = target.CreateDrawingSession())
        {
            ds.Clear(Microsoft.UI.Colors.Transparent);
            ds.DrawImage(source, new Windows.Foundation.Rect(0, 0, targetW, targetH));
        }

        using var pngStream = new InMemoryRandomAccessStream();
        await target.SaveAsync(pngStream, CanvasBitmapFileFormat.Png);
        pngStream.Seek(0);

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(pngStream);
        return bitmap;
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
