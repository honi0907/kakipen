using System.Text.Json;
using KakiMoni.Core.Models;

namespace KakiMoni_Layout.Services;

public static class LayoutLauncherSettingsStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KakiMoni",
        "layout-launcher.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static LayoutLauncherSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var data = JsonSerializer.Deserialize<LayoutLauncherSettings>(File.ReadAllText(Path), JsonOptions);
                if (data is not null)
                    return data;
            }
        }
        catch { }

        return new LayoutLauncherSettings();
    }

    public static void Save(LayoutLauncherSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}

public sealed class LayoutLauncherSettings
{
    public string ServerUrl { get; set; } = "http://localhost:3000";
    public int Display0Monitor { get; set; }
    public int Display1Monitor { get; set; } = 1;
    public int Display2Monitor { get; set; } = 2;
}
