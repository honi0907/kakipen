using System.Text.Json;

namespace KakiMoni_Host.Services;

public sealed class SaveViewerSettings
{
    public string ServerUrl { get; set; } = "http://localhost:3000";
    public bool LiveUpdateEnabled { get; set; } = true;
    public bool AutoRefreshEnabled { get; set; }
    public int AutoRefreshIntervalSec { get; set; } = 15;
    public bool ShowFileName { get; set; } = true;
    public bool ShowUpdated { get; set; } = true;
    public bool HideDisconnected { get; set; }
    public bool ToolbarVisible { get; set; }
    public int MaxPerSeat { get; set; } = 120;
    public int ThumbSize { get; set; } = 96;
    public string LayoutTop { get; set; } = "1,2,3,4,5";
    public string LayoutBottom { get; set; } = "6,7,8,9,10";
    public List<int> SelectedSeatIds { get; set; } = new(Enumerable.Range(1, 10));
}

public static class SaveViewerSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KakiMoni",
            "save-viewer-settings.json");

    public static SaveViewerSettings Load()
    {
        var defaults = new SaveViewerSettings();
        try
        {
            if (!File.Exists(SettingsPath))
                return defaults;

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<SaveViewerSettings>(json);
            if (settings is null)
                return defaults;

            ApplyDefaults(settings, defaults);
            return settings;
        }
        catch
        {
            return defaults;
        }
    }

    private static void ApplyDefaults(SaveViewerSettings settings, SaveViewerSettings defaults)
    {
        if (string.IsNullOrWhiteSpace(settings.ServerUrl))
            settings.ServerUrl = defaults.ServerUrl;

        if (settings.SelectedSeatIds is null || settings.SelectedSeatIds.Count == 0)
            settings.SelectedSeatIds = new List<int>(defaults.SelectedSeatIds);
    }

    public static void Save(SaveViewerSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch { }
    }
}
