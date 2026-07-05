namespace KakiMoni.Server.Services;

public sealed class SaveGalleryLiveService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private bool _started;

    public long Revision { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public event Action? Changed;

    public void Start(string contentRoot)
    {
        lock (_gate)
        {
            if (_started)
                return;

            var savesDir = Path.Combine(contentRoot, "saves");
            Directory.CreateDirectory(savesDir);
            WatchDirectory(savesDir);

            for (var seatId = 1; seatId <= 10; seatId++)
            {
                var seatDir = Path.Combine(savesDir, $"ID{seatId:D2}");
                Directory.CreateDirectory(seatDir);
                WatchDirectory(seatDir);
            }

            _started = true;
        }
    }

    public void NotifyChanged()
    {
        Revision = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Changed?.Invoke();
    }

    private void WatchDirectory(string dirPath)
    {
        var watcher = new FileSystemWatcher(dirPath)
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        watcher.Created += OnFsEvent;
        watcher.Changed += OnFsEvent;
        watcher.Deleted += OnFsEvent;
        watcher.Renamed += OnFsRenamed;
        watcher.Error += (_, _) => NotifyChanged();

        _watchers.Add(watcher);
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Name) || !e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return;

        NotifyChanged();
    }

    private void OnFsRenamed(object sender, RenamedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Name) && string.IsNullOrEmpty(e.OldName))
            return;

        NotifyChanged();
    }

    public void Dispose()
    {
        lock (_gate)
        {
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
