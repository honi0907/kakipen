using System.Text.Json;

namespace KakiMoni_Client.Services;

public static class ClientSettingsStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KakiMoni",
        "client-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static ClientSettings Load() => ReadFromDisk();

    public static string? GetServerUrl() => Load().ServerUrl;
    public static int GetSeatId() => Load().SeatId;
    public static string? GetBgImageUrl() => Load().BgImageUrl;

    public static void Save(ClientSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(Normalize(settings), JsonOptions));
    }

    public static void SaveConnection(string serverUrl, int seatId, string? bgImageUrl)
    {
        var settings = Load();
        settings.ServerUrl = serverUrl;
        settings.SeatId = seatId;
        settings.BgImageUrl = bgImageUrl;
        Save(settings);
    }

    private static ClientSettings ReadFromDisk()
    {
        try
        {
            if (File.Exists(Path))
            {
                var json = File.ReadAllText(Path);
                var data = JsonSerializer.Deserialize<ClientSettings>(json);
                if (data is not null)
                {
                    if (!json.Contains("ShowConfirmButton", StringComparison.Ordinal))
                    {
                        data.ShowConfirmButton = true;
                        data.ShowClearButton = true;
                        data.ShowEraserTool = true;
                    }

                    return Normalize(data);
                }
            }
        }
        catch { }

        return Normalize(new ClientSettings());
    }

    private static ClientSettings Normalize(ClientSettings settings)
    {
        settings.Palette = settings.Palette
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(NormalizeHex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (settings.Palette.Count == 0)
            settings.Palette = ClientSettings.DefaultPalette.ToList();

        settings.PenColor = settings.Palette[0];

        settings.PenSize = Math.Clamp(settings.PenSize, 2, 40);
        settings.EraserSize = Math.Clamp(settings.EraserSize, 4, 80);
        settings.EraserAutoPenSeconds = Math.Clamp(settings.EraserAutoPenSeconds, 1, 60);

        settings.ServerUrl = string.IsNullOrWhiteSpace(settings.ServerUrl)
            ? ClientApiService.DefaultServerUrl
            : ClientApiService.NormalizeServerUrl(settings.ServerUrl);

        settings.SavedServerUrls = (settings.SavedServerUrls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => ClientApiService.NormalizeServerUrl(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (settings.SavedServerUrls.Count == 0)
            settings.SavedServerUrls.Add(settings.ServerUrl);
        else if (!settings.SavedServerUrls.Contains(settings.ServerUrl, StringComparer.OrdinalIgnoreCase))
            settings.SavedServerUrls.Insert(0, settings.ServerUrl);

        if (settings.SavedServerUrls.Count > 10)
            settings.SavedServerUrls = settings.SavedServerUrls.Take(10).ToList();

        return settings;
    }

    private static string NormalizeHex(string hex)
    {
        hex = hex.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;
        return hex.Length == 7 ? hex.ToLowerInvariant() : "#000000";
    }
}
