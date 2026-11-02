using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The map window art: the minimap window (UIMap.img/MiniMap) and the world
// map window (UIMap.img/WorldMap), the world map itself
// (Map/WorldMap/WorldMap.img), and the world name the MENU window's channel
// pill shows (read by MushTown.Client/Panels/HudMenu.cs). The file names
// must match the client (MushTown.Client/Panels/HudMap.cs and Ui/Minimap.cs).
public static partial class Export
{
    // The Base tree keeps the Map/ folder in front of every map path, the
    // category tree does not. Both map exporters need the same two strings.
    public static (string Cat, string Pre) MapTree(WzRoot map)
    {
        bool baseTree = map.Find("WorldMap/WorldMap.img/BaseImg/0") == null;
        return baseTree ? ("", "Map/") : ("Map", "");
    }

    // The entry of one map id in String.wz Map.img: the ids are grouped by world
    // folder, so the folder name is not part of the path.
    public static Wz_Node? MapEntry(Wz_Node mapImg, int id, out string world)
    {
        world = "";
        foreach (Wz_Node w in mapImg.Nodes)
        {
            var entry = w.Nodes[id.ToString()];
            if (entry == null) continue;
            world = w.Text;
            return entry;
        }
        return null;
    }

    public static bool ExportMapUi(WzRoot ui, WzRoot map, WzRoot str, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");

        // The minimap frame: the eight official slices (UIMap.img/MiniMap/MinMap).
        // The client draws the map picture in place of the centre slice.
        foreach (string piece in new[] { "nw", "n", "ne", "w", "e", "sw", "s", "se" })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/MiniMap/MinMap/{piece}", Path.Combine(dir, $"mini_frame_{piece}.png"))) return false;
        // The large layout frame (UIMap.img/MiniMap/MaxMap): the same slices,
        // with a taller top bar that carries the map icon and the two names.
        foreach (string piece in new[] { "nw", "n", "ne", "w", "e", "sw", "s", "se" })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/MiniMap/MaxMap/{piece}", Path.Combine(dir, $"mini_frame_big_{piece}.png"))) return false;

        // The small buttons of the minimap frame (UIMap.img/MiniMap): the row
        // at the top (the open book, the person, the funnel) and the minus/plus
        // pair that switches the layout.
        var miniButtons = new (string Node, string Name)[]
        {
            ("BtMap", "map"),
            ("BtNpc", "npc"),
            ("BtFilter", "filter"),
            ("button:min", "zoomout"),
            ("button:max", "zoomin"),
        };
        foreach (var (node, name) in miniButtons)
            foreach (var (state, suffix) in new[] { ("normal", "_n"), ("mouseOver", "_h"), ("pressed", "_p") })
                if (!UiPng.Save(ui, "UI", $"UIMap.img/MiniMap/{node}/{state}/0", Path.Combine(dir, $"mini_btn_{name}{suffix}.png"))) return false;

        // The dots on the map (MapHelper.img/minimap): the player, an ordinary
        // NPC, the quest givers (their own mark) and a portal.
        var dots = new (string Node, string Name)[]
        {
            ("user", "user"), ("npc", "npc"), ("startnpc", "npcstart"),
            ("endnpc", "npcend"), ("portal", "portal"),
        };
        var (mapCat, mapPre) = MapTree(map);
        foreach (var (node, name) in dots)
            if (!UiPng.Save(map, mapCat, $"{mapPre}MapHelper.img/minimap/{node}", Path.Combine(dir, $"mini_dot_{name}.png"))) return false;
        // The map marks of the big minimap header (Map/MapHelper.img/mark).
        var marks = map.Find($"{mapPre}MapHelper.img/mark");
        if (marks == null) { Console.WriteLine("MapHelper.img/mark not found"); return false; }
        foreach (var child in marks.Nodes)
            if (!UiPng.Save(map, mapCat, $"{mapPre}MapHelper.img/mark/{child.Text}", Path.Combine(dir, $"map_mark_{child.Text}.png"))) return false;

        // The filter window (UIMap.img/MiniMap/filter): the panel art, which
        // also carries the section titles and the small row icons.
        if (!UiPng.Save(ui, "UI", "UIMap.img/MiniMap/filter/backgrnd", Path.Combine(dir, "mini_filter_bg.png"))) return false;

        // The checkbox states (Basic.img/CheckBox/0..1): off and on.
        for (int i = 0; i < 2; i++)
            if (!UiPng.Save(ui, "UI", $"Basic.img/CheckBox/{i}", Path.Combine(dir, $"check_{i}.png"))) return false;

        // The filter rows: where the official window puts the box and the name,
        // and what the name says. Written as one json the client reads.
        var filterNode = ui.Find("UIMap.img/MiniMap/filter");
        if (filterNode == null) { Console.WriteLine("minimap filter not found"); return false; }
        var rows = new List<object>();
        foreach (var check in filterNode.Nodes)
        {
            string id = check.Text;
            if (!id.StartsWith("check:")) continue;
            string row = id["check:".Length..];
            var info = filterNode.Nodes["checkInfo"]?.Nodes[row];
            var lt = check.Nodes["lt"]?.Value as Wz_Vector;
            if (lt == null) { Console.WriteLine($"filter row {row}: no lt"); return false; }
            // The two section headers have no name of their own (the panel art
            // writes it), so their checkbox is all there is to place.
            var pos = (info?.Nodes["namePos"]?.Value as Wz_Vector) ?? lt;
            string rowName = info?.Nodes["name"]?.GetValueEx<string>("") ?? "";
            rows.Add(new { Row = row, Name = rowName, X = pos.X, Y = pos.Y, CheckX = lt.X, CheckY = lt.Y });
        }
        WriteJson(Path.Combine(dir, "mini_filter.json"), rows);

        // The NPC list window (UIMap.img/MiniMap/npcList): the panel and the dot
        // in front of every row.
        if (!UiPng.Save(ui, "UI", "UIMap.img/MiniMap/npcList/backgrnd", Path.Combine(dir, "npclist_bg.png"))) return false;
        if (!UiPng.Save(ui, "UI", "UIMap.img/MiniMap/npcList/icon/npc", Path.Combine(dir, "npclist_dot.png"))) return false;
        // The X button of the world map window and the world map window frame.
        foreach (var (state, name) in new[] { ("normal", "_n"), ("mouseOver", "_h"), ("pressed", "_p") })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/WorldMap/button:close/{state}/0", Path.Combine(dir, $"map_x{name}.png"))) return false;
        if (!UiPng.Save(ui, "UI", "UIMap.img/WorldMap/backgrnd", Path.Combine(dir, "wm_frame.png"))) return false;


        // The official map names (String.wz Map.img: {world}/{map id}).
        var mapImg = str.Find("Map.img") ?? str.Find("String/Map.img");
        if (mapImg == null)
        {
            Console.WriteLine("map names not found: String.wz Map.img");
            return false;
        }
        var names = new List<object>();
        string worldName = "";
        foreach (int id in ExportList.Load("maps"))
        {
            string name = "", street = "";
            var entry = MapEntry(mapImg, id, out string world);
            if (entry != null)
            {
                name = entry.Nodes["mapName"]?.Value as string ?? "";
                street = entry.Nodes["streetName"]?.Value as string ?? "";
                worldName = world; // the world folder our maps live in
            }
            names.Add(new { Id = id, Name = name, Street = street });
        }

        WriteJson(Path.Combine(dir, "mapui.json"), new
        {
            Channel = new { World = worldName },
            Maps = names,
        });
        Console.WriteLine($"map ui: {names.Count} map names");
        return true;
    }
}
