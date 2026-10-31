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
    // object, so a panel can keep it in a field. A file that does not parse
    // keeps the built-in defaults here: the game has to start. The reload
    // below collects the errors instead, so a typo is one line in the log and
    // the other files still reload.
    public static PanelLayout Load(string path)
    {
        if (Files.TryGetValue(path, out var hit)) return hit;
        var lay = new PanelLayout();
        try { Read(path, lay); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException) { } // the game has to start: a typo or a locked file keeps the defaults
        Files[path] = lay;
        return lay;
    }

    // Re-read every loaded file (the layout hot-reload). A file that does not
    // parse keeps the numbers it had and does not stop the others: its error
    // goes back to the caller, which logs it. One typo must not freeze every
    // panel at its old size.
    public static List<string> ReloadAll()
    {
        var errors = new List<string>();
        foreach (var path in Files.Keys)
            try { Read(path, Files[path]); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException) { errors.Add($"{path}: {ex.Message}"); }
        return errors;
    }

    // A missing file means the defaults; a file that does not parse throws.
    private static void Read(string path, PanelLayout lay)
    {
        var values = new Dictionary<string, int>();
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int v))
                    values[p.Name] = v;
        }
        lay._values = values;
    }
}
