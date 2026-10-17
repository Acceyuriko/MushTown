using System.Text.Json;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Shared export helpers: json writes, png dedupe, WZ value readers.
public static partial class Export
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ---------- helpers ----------

    private static void SaveOnce(Wz_Png png, string filePath, HashSet<string> saved)
    {
        if (!saved.Add(filePath)) return;
        WzNodeUtil.SavePng(png, filePath);
    }

    private static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    private static int Int(Wz_Node? node, string name, int defaultValue = 0) =>
        node?.Nodes[name]?.GetValueEx<int>(defaultValue) ?? defaultValue;

    private static string Str(Wz_Node? node, string name) =>
        node?.Nodes[name]?.GetValueEx<string>("") ?? "";
}
