using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The map window art: the minimap window (UIMap.img/MiniMap) and the world
// map window (UIMap.img/WorldMap), the world map itself
// (Map/WorldMap/WorldMap.img), and the world name the MENU window's channel
// pill shows (read by MushTown.Client/Panels/HudMenu.cs). The file names
// must match the client (MushTown.Client/Panels/HudMap.cs).
public static partial class Export
{
    public static bool ExportMapUi(WzRoot ui, WzRoot map, WzRoot str, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");

        // The minimap frame: the nine official slices (UIMap.img/MiniMap/MinMap).
        foreach (string piece in new[] { "nw", "n", "ne", "w", "e", "sw", "s", "se", "c" })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/MiniMap/MinMap/{piece}", Path.Combine(dir, $"mini_frame_{piece}.png"))) return false;

        // The small buttons of the minimap frame (UIMap.img/MiniMap): the row
        // at the top (the house, the open book, the pin, the person, the
        // funnel) and the zoom pair (the minus and the plus). The disabled
        // state is the art for the buttons that have no job yet.
        var miniButtons = new (string Node, string Name)[]
        {
            ("BtTown", "town"),
            ("BtMap", "map"),
            ("BtNavigation", "navi"),
            ("BtNpc", "npc"),
            ("BtFilter", "filter"),
            ("button:min", "zoomout"),
            ("button:max", "zoomin"),
        };
        foreach (var (node, name) in miniButtons)
            foreach (var (state, suffix) in new[] { ("normal", "_n"), ("mouseOver", "_h"), ("pressed", "_p"), ("disabled", "_d") })
                if (!UiPng.Save(ui, "UI", $"UIMap.img/MiniMap/{node}/{state}/0", Path.Combine(dir, $"mini_btn_{name}{suffix}.png"))) return false;

        // The X button of the world map window and the world map window frame.
        foreach (var (state, name) in new[] { ("normal", "_n"), ("mouseOver", "_h"), ("pressed", "_p") })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/WorldMap/button:close/{state}/0", Path.Combine(dir, $"map_x{name}.png"))) return false;
        if (!UiPng.Save(ui, "UI", "UIMap.img/WorldMap/backgrnd", Path.Combine(dir, "wm_frame.png"))) return false;

        // The scroll bar of the map list (the official VScr111: the track and
        // the cursor). The panel art has the "Cooldown" label baked in at the
        // bottom, so a long list needs the official scroll bar.
        if (!UiPng.Save(ui, "UI", "Basic.img/VScr111/enabled/base", Path.Combine(dir, "wm_scroll_base.png"))) return false;
        if (!UiPng.Save(ui, "UI", "Basic.img/VScr111/enabled/thumb0", Path.Combine(dir, "wm_scroll_thumb.png"))) return false;

        // The world map art and the region marks (Map/WorldMap/WorldMap.img).
        // The Base tree keeps the "Map/" folder in the path, a Map.wz root
        // does not; the link inside the node wants the same treatment.
        bool baseTree = map.Find("WorldMap/WorldMap.img/BaseImg/0") == null;
        string prefix = baseTree ? "Map/" : "";
        if (!UiPng.TryRead(map, baseTree ? "" : "Map", prefix + "WorldMap/WorldMap.img/BaseImg/0", out var world, out var origin))
        {
            Console.WriteLine("world map art not found: Map/WorldMap/WorldMap.img/BaseImg/0");
            return false;
        }
        WzNodeUtil.SavePng(world, Path.Combine(dir, "wm_world.png"));

        var list = map.Find(prefix + "WorldMap/WorldMap.img/MapList");
        if (list == null)
        {
            Console.WriteLine("world map list not found: Map/WorldMap/WorldMap.img/MapList");
            return false;
        }

        // The region map of every region (MapLink): the towns of the region
        // and the maps in every town. The world map list holds the old map
        // ids, so the town list is the source for the newer ones.
        var links = map.Find(prefix + "WorldMap/WorldMap.img/MapLink");
        if (links == null)
        {
            Console.WriteLine("world map links not found: Map/WorldMap/WorldMap.img/MapLink");
            return false;
        }
        var regionMaps = new Dictionary<string, string>();
        foreach (Wz_Node link in links.Nodes)
        {
            string tip = link.Nodes["toolTip"]?.Value as string ?? "";
            string linkMap = link.Nodes["link"]?.Nodes["linkMap"]?.Value as string ?? "";
            if (tip != "" && linkMap != "") regionMaps[tip] = linkMap;
        }

        var regions = new List<object>();
        foreach (Wz_Node child in list.Nodes)
        {
            string title = child.Nodes["title"]?.Value as string ?? "";
            var spot = child.Nodes["spot"]?.Value as Wz_Vector;
            if (title == "" || spot == null) continue; // the towns and the links have no title
            var ids = new List<int>();
            var mapNo = child.Nodes["mapNo"];
            if (mapNo != null) ids.AddRange(mapNo.Nodes.Select(n => n.GetValueEx<int>(0)));
            // the towns of the region map: their maps are in the region too
            if (!regionMaps.TryGetValue(title, out string? linkMap))
            {
                Console.WriteLine($"world map region '{title}' has no region map (MapLink)");
                return false;
            }
            var towns = map.Find(prefix + "WorldMap/" + linkMap + ".img/MapList");
            if (towns == null)
            {
                Console.WriteLine($"world map region map not found: Map/WorldMap/{linkMap}.img/MapList");
                return false;
            }
            foreach (Wz_Node town in towns.Nodes)
            {
                var townMaps = town.Nodes["mapNo"];
                if (townMaps == null) continue;
                ids.AddRange(townMaps.Nodes.Select(n => n.GetValueEx<int>(0)));
            }
            regions.Add(new { Title = title, X = (origin?.X ?? 0) + spot.X, Y = (origin?.Y ?? 0) + spot.Y, Maps = ids.Distinct().ToList() });
        }

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
            foreach (Wz_Node world2 in mapImg.Nodes)
            {
                var entry = world2.Nodes[id.ToString()];
                if (entry == null) continue;
                name = entry.Nodes["mapName"]?.Value as string ?? "";
                street = entry.Nodes["streetName"]?.Value as string ?? "";
                worldName = world2.Text; // the world folder our maps live in
                break;
            }
            names.Add(new { Id = id, Name = name, Street = street });
        }

        WriteJson(Path.Combine(dir, "mapui.json"), new
        {
            World = new { Image = "ui/wm_world.png", Regions = regions },
            Channel = new { World = worldName },
            Maps = names,
        });
        Console.WriteLine($"map ui: {regions.Count} regions, {names.Count} map names");
        return true;
    }
}
