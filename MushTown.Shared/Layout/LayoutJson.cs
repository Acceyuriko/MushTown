using System.Text.Json;

namespace MushTown.Shared;

// Shared readers for the layouts/*.json files (Layout, MenuLayout,
// CharLayout). Missing keys keep their current values; bad values throw
// JsonException before the caller changes anything.
internal static class LayoutJson
{
    internal static int GetInt(JsonElement root, string key, int def)
    {
        if (!root.TryGetProperty(key, out var el)) return def;
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out int v)) throw new JsonException($"bad int \"{key}\"");
        return v;
    }

    internal static float GetFloat(JsonElement root, string key, float def)
    {
        if (!root.TryGetProperty(key, out var el)) return def;
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetDouble(out double v)) throw new JsonException($"bad number \"{key}\"");
        return (float)v;
    }

    internal static (int X, int Y) GetPoint(JsonElement root, string key, (int X, int Y) def)
    {
        if (!root.TryGetProperty(key, out var el)) return def;
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() != 2
            || el[0].ValueKind != JsonValueKind.Number || el[1].ValueKind != JsonValueKind.Number
            || !el[0].TryGetInt32(out int x) || !el[1].TryGetInt32(out int y))
            throw new JsonException($"bad point \"{key}\"");
        return (x, y);
    }

    internal static int[] GetInts(JsonElement root, string key, int[] def)
    {
        if (!root.TryGetProperty(key, out var el)) return def;
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() != def.Length)
            throw new JsonException($"bad array \"{key}\"");
        var arr = new int[def.Length];
        for (int i = 0; i < arr.Length; i++)
            if (el[i].ValueKind != JsonValueKind.Number || !el[i].TryGetInt32(out arr[i]))
                throw new JsonException($"bad array \"{key}\"");
        return arr;
    }

    // A [w,h] box (the drawn size).
    internal static (int W, int H) GetSize(JsonElement root, string key, (int W, int H) def)
    {
        var (w, h) = GetPoint(root, key, def);
        return (w, h);
    }

    // An [r,g,b] colour.
    internal static (int R, int G, int B) GetColor(JsonElement root, string key, (int R, int G, int B) def)
    {
        if (!root.TryGetProperty(key, out var el)) return def;
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() != 3
            || el[0].ValueKind != JsonValueKind.Number || el[1].ValueKind != JsonValueKind.Number
            || el[2].ValueKind != JsonValueKind.Number
            || !el[0].TryGetInt32(out int r) || !el[1].TryGetInt32(out int g) || !el[2].TryGetInt32(out int b))
            throw new JsonException($"bad color \"{key}\"");
        if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255) throw new JsonException($"bad color \"{key}\"");
        return (r, g, b);
    }
}
