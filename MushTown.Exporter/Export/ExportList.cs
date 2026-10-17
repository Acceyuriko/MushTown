using System.Text.Json;

namespace MushTown.Exporter;

// Loads the export ID lists (tracked data: Export/lists/*.json). A missing
// or bad file fails loud: a silent default list would export the wrong game.
public static class ExportList
{
    public static int[] Load(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Export", "lists", $"{name}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing export list: {path}", path);
        return JsonSerializer.Deserialize<int[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"export list {path} holds no int array");
    }
}
