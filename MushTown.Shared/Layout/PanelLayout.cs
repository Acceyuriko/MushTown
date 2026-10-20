using System.Text.Json;

namespace MushTown.Shared;

// layouts/panel-*.json: one file per game panel. Every number a panel
// needs lives in its own file: {"x": 1114, "y": 45, "w": 242}. A key the
// file does not have keeps the default the code passes in. A missing file
// means all defaults.
public sealed class PanelLayout
{
    private Dictionary<string, int> _values = new();

    // One number. A missing key keeps the default.
    public int Get(string key, int def) => _values.GetValueOrDefault(key, def);

    // The loaded files, by path.
    private static readonly Dictionary<string, PanelLayout> Files = new();

    // Load (or return) the layout file. The same path gives the same
    // object, so a panel can keep it in a field.
    public static PanelLayout Load(string path)
    {
        if (Files.TryGetValue(path, out var hit)) return hit;
        var lay = new PanelLayout();
        Read(path, lay);
        Files[path] = lay;
        return lay;
    }

    // Re-read every loaded file (the layout hot-reload).
    public static void ReloadAll()
    {
        foreach (var path in Files.Keys.ToList())
            Read(path, Files[path]);
    }

    private static void Read(string path, PanelLayout lay)
    {
        var values = new Dictionary<string, int>();
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int v))
                        values[p.Name] = v;
            }
            catch (JsonException) { return; } // a bad file keeps the values it had
        }
        lay._values = values;
    }
}
