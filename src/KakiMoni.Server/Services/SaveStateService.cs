using System.Text.Json;
using KakiMoni.Core.Models;
using KakiMoni.Core.Paths;

namespace KakiMoni.Server.Services;

public sealed class SaveStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _savesDir;
    private readonly string _statePath;

    public SaveStateService(string contentRoot)
    {
        _ = contentRoot;
        _savesDir = ContentRootResolver.SavesPath;
        _statePath = Path.Combine(_savesDir, ".state.json");
        Directory.CreateDirectory(_savesDir);
        EnsureSeatFolders();
        Load();
    }

    public int Session { get; private set; } = 1;
    public int Counter { get; private set; }

    public SaveStateDto GetState() => new() { Session = Session, Counter = Counter };

    public SaveStateDto NextCounter()
    {
        Counter++;
        Persist();
        return GetState();
    }

    public SaveStateDto SetSession(int session)
    {
        Session = Math.Clamp(session, 1, 99);
        Counter = 0;
        Persist();
        return GetState();
    }

    public SaveStateDto SetCounter(int counter)
    {
        Counter = Math.Clamp(counter, 0, 9999);
        Persist();
        return GetState();
    }

    public string SaveSnapshot(int seatId, int session, int counter, string type, byte[] pngBytes)
    {
        if (seatId is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(seatId));

        var seatLabel = seatId.ToString("D2");
        var seatDir = Path.Combine(_savesDir, $"ID{seatLabel}");
        Directory.CreateDirectory(seatDir);

        var safeType = string.IsNullOrWhiteSpace(type) ? "SAVE" : type.Trim().ToUpperInvariant();
        var fileName = BuildFileName(session, seatId, counter, safeType);
        File.WriteAllBytes(Path.Combine(seatDir, fileName), pngBytes);
        return fileName;
    }

    public static string BuildFileName(int session, int seatId, int counter, string type) =>
        $"S{session}_ID{seatId}_{counter:D3}_{type}.png";

    private void EnsureSeatFolders()
    {
        for (var i = 1; i <= 10; i++)
            Directory.CreateDirectory(Path.Combine(_savesDir, $"ID{i:D2}"));
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_statePath)) return;
            var dto = JsonSerializer.Deserialize<SaveStateDto>(File.ReadAllText(_statePath));
            if (dto is null) return;
            Session = Math.Clamp(dto.Session, 1, 99);
            Counter = Math.Clamp(dto.Counter, 0, 9999);
        }
        catch { }
    }

    private void Persist()
    {
        File.WriteAllText(_statePath, JsonSerializer.Serialize(GetState(), JsonOptions));
    }
}
