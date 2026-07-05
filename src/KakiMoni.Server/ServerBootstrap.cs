using System.Net.Sockets;
using KakiMoni.Core.Models;
using KakiMoni.Core.Paths;
using KakiMoni.Server.Hubs;
using KakiMoni.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KakiMoni.Server;

public sealed class ServerBootstrap : IAsyncDisposable
{
    private WebApplication? _app;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; }

    public event Action? Stopped;

    public async Task StartAsync(string contentRoot, int port, bool useSeatNameFile = false, CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;

        Port = port;
        Directory.CreateDirectory(AppInstallPaths.SavesPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            Args = Array.Empty<string>()
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.Host.ConfigureServices(services =>
        {
            services.AddSingleton<IHostLifetime, WinUiEmbeddedHostLifetime>();
        });
        builder.Services.AddSingleton(new BackgroundFileService(contentRoot));
        builder.Services.AddSingleton(new ChoiceFileService(contentRoot));
        builder.Services.AddSingleton(new LogoFileService(contentRoot));
        builder.Services.AddSingleton(new OverlayFileService(contentRoot));
        builder.Services.AddSingleton(new SeatNameFileService(contentRoot));
        builder.Services.AddSingleton(new SaveStateService(contentRoot));
        builder.Services.AddSingleton(new SaveGalleryService(contentRoot));
        builder.Services.AddSingleton<SaveGalleryLiveService>();
        builder.Services.AddSingleton<DisplayConnectionManager>();
        builder.Services.AddSingleton<GameSessionState>();
        builder.Services.AddSingleton<SeatStateManager>();
        builder.Services.AddSingleton<LayoutDisplayLayoutManager>();
        builder.Services.AddSignalR();

        var app = builder.Build();

        var session = app.Services.GetRequiredService<GameSessionState>();
        session.UseSeatNameFile = useSeatNameFile;
        app.Services.GetRequiredService<SeatStateManager>()
            .RefreshSeatNames(useSeatNameFile, app.Services.GetRequiredService<SeatNameFileService>());

        MapStatic(app, contentRoot);
        MapWeb(app, contentRoot);
        MapApi(app);
        app.MapHub<GameHub>("/hub");

        app.Services.GetRequiredService<SaveGalleryLiveService>().Start(contentRoot);

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopped.Register(OnHostStopped);

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex) when (IsPortInUse(ex))
        {
            await app.DisposeAsync();
            throw new InvalidOperationException(
                $"ポート {port} は既に使用中です。他の KakiMoni 親機を終了するか、ポート番号を変えてください。",
                ex);
        }

        _app = app;
    }

    private void OnHostStopped()
    {
        if (_app is null)
            return;

        _app = null;
        Stopped?.Invoke();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
            return;

        var app = _app;
        _app = null;
        await app.StopAsync(cancellationToken);
        await app.DisposeAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static void MapStatic(WebApplication app, string contentRoot)
    {
        var assets = Path.Combine(contentRoot, "assets");
        MapFolder(app, Path.Combine(assets, "backgrounds"), "/backgrounds");
        MapFolder(app, Path.Combine(assets, "choices"), "/choices");
        MapFolder(app, Path.Combine(assets, "overlays"), "/overlays");
        MapFolder(app, Path.Combine(assets, "logo"), "/logo");
    }

    private static void MapFolder(WebApplication app, string physicalPath, string requestPath)
    {
        AppInstallPaths.SafeCreateDirectory(physicalPath);
        if (!Directory.Exists(physicalPath))
            return;

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(physicalPath),
            RequestPath = requestPath
        });
    }

    private static void MapWeb(WebApplication app, string contentRoot)
    {
        var displayHtml = Path.Combine(contentRoot, "www", "client-display", "index.html");
        app.MapGet("/client-display", () =>
        {
            if (!File.Exists(displayHtml))
                return Results.NotFound("client-display page is missing");
            return Results.File(displayHtml, "text/html; charset=utf-8");
        });
    }

    private static void MapApi(WebApplication app)
    {
        app.MapGet("/api/backgrounds", (BackgroundFileService backgrounds) =>
            Results.Json(backgrounds.ListBackgroundEntries()));

        app.MapGet("/api/backgrounds/seat/{seatId:int}", (int seatId, BackgroundFileService backgrounds) =>
        {
            if (seatId is < 1 or > 10)
                return Results.BadRequest(new { error = "seatId must be 1-10" });

            var match = backgrounds.FindSeatBackground(seatId);
            return match is null
                ? Results.NotFound(new { seatId, expected = $"BG_ID{seatId} or BG_ID{seatId:D2}" })
                : Results.Json(new { fileName = match.Value.FileName, relativeUrl = match.Value.RelativeUrl });
        });

        app.MapGet("/api/choices", (ChoiceFileService choices) =>
            Results.Json(choices.ListChoices()));

        app.MapGet("/api/logo", (LogoFileService logos) =>
            Results.Json(new { url = logos.GetDefaultRelativeUrl() }));

        app.MapGet("/api/overlays", (OverlayFileService overlays) =>
            Results.Json(overlays.ListAll()));

        app.MapGet("/health", (BackgroundFileService backgrounds, ChoiceFileService choices, LogoFileService logos, SeatStateManager seats) =>
            Results.Json(new
            {
                status = "ok",
                contentRoot = backgrounds.ContentRoot,
                backgroundCount = backgrounds.ListBackgrounds().Count,
                choiceCount = choices.ListChoices().Count,
                hasLogo = logos.GetDefaultRelativeUrl() is not null,
                connectedSeats = seats.GetConnectedSeats().Count()
            }));

        app.MapGet("/api/seats", (SeatStateManager seats) =>
        {
            var list = Enumerable.Range(1, 10).Select(seatId =>
            {
                var seat = seats.GetSeat(seatId);
                return new
                {
                    seatId,
                    connected = !string.IsNullOrEmpty(seat?.ConnectionId),
                    name = SeatNameHelper.GetDisplayName(seatId, seat?.Name),
                    strokeCount = seat?.Strokes.Count ?? 0
                };
            });
            return Results.Json(list);
        });

        MapSaveApi(app);
    }

    private static void MapSaveApi(WebApplication app)
    {
        app.MapGet("/api/save-state", (SaveStateService saves) => Results.Json(saves.GetState()));

        app.MapPost("/api/save-next-counter", (SaveStateService saves) => Results.Json(saves.NextCounter()));

        app.MapPost("/api/save-set-session", async (HttpRequest request, SaveStateService saves) =>
        {
            var body = await request.ReadFromJsonAsync<SaveSessionRequest>();
            if (body?.Session is < 1 or > 99)
                return Results.BadRequest(new { error = "invalid session" });
            return Results.Json(saves.SetSession(body!.Session));
        });

        app.MapPost("/api/save-set-counter", async (HttpRequest request, SaveStateService saves) =>
        {
            var body = await request.ReadFromJsonAsync<SaveCounterRequest>();
            if (body?.Counter is < 0 or > 9999)
                return Results.BadRequest(new { error = "invalid counter" });
            return Results.Json(saves.SetCounter(body!.Counter));
        });

        app.MapPost("/api/save-snapshot", async (HttpRequest request, SaveStateService saves, SaveGalleryLiveService live) =>
        {
            var body = await request.ReadFromJsonAsync<SaveSnapshotRequest>();
            if (body is null || body.SeatId is < 1 or > 10)
                return Results.BadRequest(new { error = "invalid seatId" });
            if (body.Session is < 1 or > 99 || body.Counter is < 0 or > 9999)
                return Results.BadRequest(new { error = "invalid session or counter" });

            byte[] png;
            try
            {
                var base64 = (body.ImageData ?? string.Empty).Trim();
                if (base64.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = base64.IndexOf(',');
                    base64 = comma >= 0 ? base64[(comma + 1)..] : string.Empty;
                }

                png = Convert.FromBase64String(base64);
            }
            catch
            {
                return Results.BadRequest(new { error = "invalid imageData" });
            }

            var type = string.IsNullOrWhiteSpace(body.Type) ? "SAVE" : body.Type.Trim().ToUpperInvariant();
            var fileName = saves.SaveSnapshot(body.SeatId, body.Session, body.Counter, type, png);
            live.NotifyChanged();
            return Results.Json(new { ok = true, fileName });
        });

        app.MapGet("/api/save-gallery", (HttpRequest request, SaveGalleryService gallery) =>
        {
            try
            {
                var seatIds = SaveGalleryService.ParseSeatIds(request.Query["ids"]);
                var maxPerSeatRaw = int.TryParse(request.Query["maxPerSeat"], out var parsedMax) ? parsedMax : 120;
                var result = gallery.BuildGallery(seatIds, maxPerSeatRaw);
                return Results.Json(new
                {
                    ok = true,
                    seatIds = result.SeatIds,
                    maxPerSeat = result.MaxPerSeat,
                    total = result.Total,
                    bySeat = result.BySeat.ToDictionary(
                        pair => pair.Key.ToString(),
                        pair => new
                        {
                            seatId = pair.Value.SeatId,
                            totalCount = pair.Value.TotalCount,
                            items = pair.Value.Items.Select(item => new
                            {
                                item.SeatId,
                                item.Session,
                                item.Counter,
                                item.Type,
                                item.FileName,
                                item.Size,
                                updatedAt = item.UpdatedAt.ToString("O"),
                                updatedAtMs = item.UpdatedAtMs,
                                thumbnailUrl = $"/api/save-file/{item.SeatId}/{Uri.EscapeDataString(item.FileName)}"
                            })
                        })
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message });
            }
        });

        app.MapGet("/api/save-gallery/live", async (HttpContext context, SaveGalleryLiveService live, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-transform";
            context.Response.Headers.Connection = "keep-alive";

            await context.Response.WriteAsync("event: save-gallery-ready\n", cancellationToken);
            await context.Response.WriteAsync(
                $"data: {{\"revision\":{live.Revision},\"reason\":\"ready\"}}\n\n",
                cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            void OnChanged()
            {
                try
                {
                    var payload = $"data: {{\"revision\":{live.Revision},\"reason\":\"updated\"}}\n\n";
                    _ = context.Response.WriteAsync("event: save-gallery-updated\n", cancellationToken);
                    _ = context.Response.WriteAsync(payload, cancellationToken);
                    _ = context.Response.Body.FlushAsync(cancellationToken);
                }
                catch { }
            }

            live.Changed += OnChanged;
            try
            {
                while (!cancellationToken.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(25), cancellationToken);
                    await context.Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                live.Changed -= OnChanged;
            }
        });

        app.MapGet("/api/save-file/{seatId:int}/{fileName}", (int seatId, string fileName, SaveGalleryService gallery) =>
        {
            try
            {
                if (!gallery.TryResolveFilePath(seatId, fileName, out var filePath))
                    return Results.BadRequest(new { ok = false, error = "invalid file" });

                return Results.File(filePath, "image/png");
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message });
            }
        });
    }

    private sealed class SaveSessionRequest
    {
        public int Session { get; set; }
    }

    private sealed class SaveCounterRequest
    {
        public int Counter { get; set; }
    }

    private sealed class SaveSnapshotRequest
    {
        public int SeatId { get; set; }
        public int Session { get; set; }
        public int Counter { get; set; }
        public string? Type { get; set; }
        public string? ImageData { get; set; }
    }

    private static bool IsPortInUse(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
                return true;
        }

        return false;
    }
}

internal sealed class WinUiEmbeddedHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
