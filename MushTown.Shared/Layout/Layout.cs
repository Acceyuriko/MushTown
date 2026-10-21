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
    public static (int X, int Y) MenuBtn = (266, 724);
    public static (int X, int Y) CharBtn = (304, 724);
    public static (int X, int Y) Qslot = (810, 691);
    public static (int X, int Y) Tracker = (1358, 44); // the quest lines are right aligned: X is the right edge

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        // All or nothing: a bad value throws before any field changes.
        var plate = GetPoint(root, "plate", Plate);
        var hpmp = GetPoint(root, "hpmp", HpMp);
        var menuBtn = GetPoint(root, "menuBtn", MenuBtn);
        var charBtn = GetPoint(root, "charBtn", CharBtn);
        var qslot = GetPoint(root, "qslot", Qslot);
        var tracker = GetPoint(root, "tracker", Tracker);
        Plate = plate; HpMp = hpmp; MenuBtn = menuBtn; CharBtn = charBtn; Qslot = qslot; Tracker = tracker;
    }
}
