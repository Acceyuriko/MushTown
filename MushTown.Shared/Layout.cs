using System.Text.Json;

namespace MushTown.Shared;

// hud-layout.json (repo root): HUD widget top-left positions in 1366x768
// space. Sizes stay in code (they must match the art). Missing keys keep
// their current values, so the file may list only what you tweak.
public static class Layout
{
    public static (int X, int Y) Inv = (1114, 45);
    public static (int X, int Y) Stat = (656, 130);
    public static (int X, int Y) Skill = (326, 130);
    public static (int X, int Y) Plate = (0, 723);
    public static (int X, int Y) HpMp = (584, 715);
    public static (int X, int Y) MenuBtn = (266, 724);
    public static (int X, int Y) CharBtn = (304, 724);
    public static (int X, int Y) Qslot = (810, 691);
    public static (int X, int Y) Coin = (24, 484); // inventory-panel-local
    public static (int X, int Y) Meso = (213, 484); // inventory-panel-local: number right end + top

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        // All or nothing: a bad value throws before any field changes.
        var inv = GetPoint(root, "inv", Inv);
        var stat = GetPoint(root, "stat", Stat);
        var skill = GetPoint(root, "skill", Skill);
        var plate = GetPoint(root, "plate", Plate);
        var hpmp = GetPoint(root, "hpmp", HpMp);
        var menuBtn = GetPoint(root, "menuBtn", MenuBtn);
        var charBtn = GetPoint(root, "charBtn", CharBtn);
        var qslot = GetPoint(root, "qslot", Qslot);
        var coin = GetPoint(root, "coin", Coin);
        var meso = GetPoint(root, "meso", Meso);
        Inv = inv; Stat = stat; Skill = skill; Plate = plate; HpMp = hpmp; MenuBtn = menuBtn;
        CharBtn = charBtn; Qslot = qslot; Coin = coin; Meso = meso;
    }

    private static (int X, int Y) GetPoint(JsonElement root, string key, (int X, int Y) def)
    {
        if (!root.TryGetProperty(key, out var el)) return def;
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() != 2
            || !el[0].TryGetInt32(out int x) || !el[1].TryGetInt32(out int y))
            throw new JsonException($"bad point \"{key}\"");
        return (x, y);
    }
}
