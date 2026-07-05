using System.Text.Json;
using KakiMoni.Core.Models;

namespace KakiMoni_Host.Services;

public static class HostDisplayLayoutStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KakiMoni",
        "host-display-layout.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static HostDisplayLayout Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var data = JsonSerializer.Deserialize<HostDisplayLayout>(File.ReadAllText(Path), JsonOptions);
                if (data is not null)
                    return data;
            }
        }
        catch { }

        return new HostDisplayLayout();
    }

    public static void Save(HostDisplayLayout layout)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(layout, JsonOptions));
    }
}
