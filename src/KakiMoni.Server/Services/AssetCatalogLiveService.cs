using KakiMoni.Core.Paths;
using KakiMoni.Core.Protocol;
using KakiMoni.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KakiMoni.Server.Services;

public sealed class AssetCatalogLiveService : IDisposable
{
    private const string HostsGroup = "hosts";
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif", ".tif", ".tiff", ".txt"];

    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private Timer? _debounceTimer;
    private IHubContext<GameHub>? _hub;
    private bool _started;

    public long Revision { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public event Action? Changed;

    public void Configure(IHubContext<GameHub> hub) => _hub = hub;

    public void Start(string contentRoot)
    {
        lock (_gate)
        {
            if (_started)
                return;

            AssetFolderInitializer.EnsureDefaultFolders(contentRoot);
            var assets = Path.Combine(contentRoot, "assets");

            WatchDirectory(Path.Combine(assets, "backgrounds"));
            WatchDirectory(Path.Combine(assets, "choices"));
            WatchDirectory(Path.Combine(assets, "logo"));
            WatchDirectory(Path.Combine(assets, "overlays", "correct"));
            WatchDirectory(Path.Combine(assets, "overlays", "incorrect"));
            WatchFile(assets, "seat-names.txt");

            _started = true;
        }
    }

    public void NotifyChanged()
    {
        Revision = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Changed?.Invoke();
        _ = BroadcastAsync();
    }

    private void WatchDirectory(string dirPath)
    {
        AppInstallPaths.SafeCreateDirectory(dirPath);

        var watcher = new FileSystemWatcher(dirPath)
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        watcher.Created += OnFsEvent;
        watcher.Changed += OnFsEvent;
        watcher.Deleted += OnFsEvent;
        watcher.Renamed += OnFsRenamed;
        watcher.Error += (_, _) => ScheduleNotify();

        _watchers.Add(watcher);
    }

    private void WatchFile(string dirPath, string fileName)
    {
        AppInstallPaths.SafeCreateDirectory(dirPath);

        var watcher = new FileSystemWatcher(dirPath, fileName)
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        watcher.Changed += OnFsEvent;
        watcher.Created += OnFsEvent;
        watcher.Deleted += OnFsEvent;
        watcher.Renamed += OnFsRenamed;
        watcher.Error += (_, _) => ScheduleNotify();

        _watchers.Add(watcher);
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (!ShouldNotify(e.Name, e.FullPath))
            return;

        ScheduleNotify();
    }

    private void OnFsRenamed(object sender, RenamedEventArgs e)
    {
        if (!ShouldNotify(e.Name, e.FullPath) && !ShouldNotify(e.OldName, e.OldFullPath))
            return;

        ScheduleNotify();
    }

    private static bool ShouldNotify(string? name, string? fullPath)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (name.Equals("old", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(fullPath)
            && fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("old", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (name.Equals("seat-names.txt", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(name);
        return !string.IsNullOrEmpty(ext)
               && SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private void ScheduleNotify()
    {
        lock (_gate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ => NotifyChanged(), null, 500, Timeout.Infinite);
        }
    }

    private async Task BroadcastAsync()
    {
        if (_hub is null)
            return;

        try
        {
            await _hub.Clients.Group(HostsGroup)
                .SendAsync(HostCallbacks.AssetsCatalogChanged, Revision);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AssetCatalogLiveService] Broadcast failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _started = false;
        }
    }
}
