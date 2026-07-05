using System.Text;
using System.Text.Json;
using KakiMoni.Core.Models;

namespace KakiMoni_Host.Services;

public static class HostDisplayLayoutPresetStore
{
    private static readonly string PresetsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KakiMoni",
        "layout-presets");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<string> List()
    {
        try
        {
            if (!Directory.Exists(PresetsDir))
                return Array.Empty<string>();

            return Directory
                .EnumerateFiles(PresetsDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static bool Exists(string name) =>
        File.Exists(GetPresetPath(name));

    public static void Save(string name, HostDisplayLayout layout)
    {
        Directory.CreateDirectory(PresetsDir);
        var path = GetPresetPath(name);
        File.WriteAllText(path, JsonSerializer.Serialize(layout, JsonOptions));
    }

    public static HostDisplayLayout? Load(string name)
    {
        try
        {
            var path = GetPresetPath(name);
            if (!File.Exists(path))
                return null;

            return JsonSerializer.Deserialize<HostDisplayLayout>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static bool TryNormalizeName(string? rawName, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawName))
            return false;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(rawName.Length);
        foreach (var ch in rawName.Trim())
        {
            if (Array.IndexOf(invalid, ch) >= 0)
                continue;
            sb.Append(ch);
        }

        normalized = sb.ToString().Trim();
        return !string.IsNullOrWhiteSpace(normalized);
    }

    private static string GetPresetPath(string name)
    {
        if (!TryNormalizeName(name, out var normalized))
            throw new ArgumentException("Invalid preset name.", nameof(name));

        return Path.Combine(PresetsDir, normalized + ".json");
    }
}
