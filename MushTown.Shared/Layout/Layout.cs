using System.Text.Json;
using static MushTown.Shared.LayoutJson;

namespace MushTown.Shared;

// layouts/hud-layout.json: HUD widget top-left positions in 1366x768
// space. The game windows (inventory, stat, skill, quest, settings) have
// their own file: layouts/panel-<name>.json. Sizes stay in code (they
// must match the art). Missing keys keep their current values, so the
// file may list only what you tweak.
public static class Layout
{
    public static (int X, int Y) Plate = (0, 723);
    public static (int X, int Y) HpMp = (584, 715);
    public static (int X, int Y) Qslot = (810, 691);
    public static (int X, int Y) QslotCap = (2, 3); // the key cap offset inside a 32x32 quick slot cell
    public static (int X, int Y) QslotPitch = (35, 35); // the step between two cells of the strip: across and down
    public static (int X, int Y) QslotSelect = (-2, -2); // where the 38x38 blue frame of a picked cell sits against the cell (the WZ origin is 2,2)

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        // All or nothing: a bad value throws before any field changes.
        var plate = GetPoint(root, "plate", Plate);
        var hpmp = GetPoint(root, "hpmp", HpMp);
        var qslot = GetPoint(root, "qslot", Qslot);
        var qslotCap = GetPoint(root, "qslotcap", QslotCap);
        var qslotPitch = GetPoint(root, "qslotpitch", QslotPitch);
        var qslotSelect = GetPoint(root, "qslotselect", QslotSelect);
        Plate = plate; HpMp = hpmp; Qslot = qslot; QslotCap = qslotCap; QslotPitch = qslotPitch; QslotSelect = qslotSelect;
    }
}
