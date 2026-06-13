using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace KakiMoni_Host.Services;

public sealed class HostImageLoader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<BitmapImage?> LoadThumbnailAsync(string urlOrRelative, int decodeWidth = 192)
    {
        if (HostAssetPathResolver.TryResolveLocalPath(urlOrRelative, out var localPath))
            return await LoadThumbnailFromFileAsync(localPath, decodeWidth);

        if (!Uri.TryCreate(urlOrRelative, UriKind.Absolute, out _))
            return null;

        return await LoadThumbnailFromHttpAsync(urlOrRelative, decodeWidth);
    }

    public async Task<CanvasBitmap?> LoadCanvasBitmapAsync(
        string urlOrRelative,
        CancellationToken cancellationToken = default)
    {
        if (HostAssetPathResolver.TryResolveLocalPath(urlOrRelative, out var localPath))
            return await LoadCanvasBitmapFromFileAsync(localPath, cancellationToken);

        if (!Uri.TryCreate(urlOrRelative, UriKind.Absolute, out _))
            return null;

        return await LoadCanvasBitmapFromHttpAsync(urlOrRelative, cancellationToken);
    }

    private static async Task<BitmapImage?> LoadThumbnailFromFileAsync(string localPath, int decodeWidth)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(localPath);
            using var stream = await file.OpenReadAsync();
            var bitmap = new BitmapImage { DecodePixelWidth = decodeWidth };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch
        {
            return null;
        }
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

    private static async Task<CanvasBitmap?> LoadCanvasBitmapFromFileAsync(
        string localPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(localPath);
            using var stream = await file.OpenReadAsync();
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
