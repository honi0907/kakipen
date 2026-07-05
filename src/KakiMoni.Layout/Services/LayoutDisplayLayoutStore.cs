using System.Text.Json;
using KakiMoni.Core.Models;

namespace KakiMoni_Layout.Services;

public static class LayoutDisplayLayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string PathForSlot(string slot) => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KakiMoni",
        $"layout-slot-{slot}.json");

    public static HostDisplayLayout LoadForSlot(string slot)
    {
        try
        {
            var path = PathForSlot(slot);
            if (File.Exists(path))
            {
                var data = JsonSerializer.Deserialize<HostDisplayLayout>(File.ReadAllText(path), JsonOptions);
                if (data is not null)
                    return data;
            }
        }
        catch { }

        return new HostDisplayLayout();
    }

    public static void SaveForSlot(string slot, HostDisplayLayout layout)
    {
        var path = PathForSlot(slot);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(layout, JsonOptions));
    }
}
