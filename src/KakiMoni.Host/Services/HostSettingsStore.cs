using System.Text.Json;

namespace KakiMoni_Host.Services;

public static class HostSettingsStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KakiMoni",
        "host-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static HostSettings Load() => ReadFromDisk();

    public static void Save(HostSettings settings)
    {
        settings.LockOverlayOpacityPercent = Math.Clamp(settings.LockOverlayOpacityPercent, 0, 100);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static HostSettings ReadFromDisk()
    {
        try
        {
            if (File.Exists(Path))
            {
                var data = JsonSerializer.Deserialize<HostSettings>(File.ReadAllText(Path));
                if (data is not null)
                {
                    data.LockOverlayOpacityPercent = Math.Clamp(data.LockOverlayOpacityPercent, 0, 100);
                    return data;
                }
            }
        }
        catch { }

        return new HostSettings();
    }
}
