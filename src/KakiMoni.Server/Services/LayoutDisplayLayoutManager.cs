using System.Text.Json;
using KakiMoni.Core.Models;

namespace KakiMoni.Server.Services;

public sealed class LayoutDisplayLayoutManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Dictionary<string, HostDisplayLayout> _layouts = new(StringComparer.Ordinal);

    public LayoutDisplayLayoutManager()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KakiMoni");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "layout-display-layouts.json");
        Load();
    }

    public IReadOnlyDictionary<string, HostDisplayLayout> GetAll() => _layouts;

    public HostDisplayLayout? Get(string group) =>
        _layouts.TryGetValue(group, out var layout) ? layout : null;

    public void Set(string group, HostDisplayLayout layout)
    {
        _layouts[group] = layout;
        Save();
    }

    private void Load()
    {
        _layouts.Clear();
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, HostDisplayLayout>>(json, JsonOptions);
            if (raw is null)
                return;

            foreach (var pair in raw)
            {
                if (LayoutDisplaySlots.IsValid(pair.Key))
                    _layouts[pair.Key] = pair.Value ?? new HostDisplayLayout();
            }
        }
        catch
        {
            // ignore corrupt file
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_layouts, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // ignore persistence errors
        }
    }
}
