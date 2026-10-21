using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The map window art: the minimap panel (Minimap.img/Mini), the world map
// window (UIMap.img/WorldMap), the world map itself (Map/WorldMap/WorldMap.img)
// and the channel bar (UIWindow.img/Channel). The file names must match the
// client (MushTown.Client/Panels/HudMap.cs).
public static partial class Export
{
    public static bool ExportMapUi(WzRoot ui, WzRoot map, WzRoot str, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");

        // The minimap panel: the dark frame the map sits in.
        if (!UiPng.Save(ui, "UI", "Minimap.img/Mini/Backgrnd/0", Path.Combine(dir, "mini_bg.png"))) return false;

        // The X button of the world map window and the world map window frame.
        foreach (var (state, name) in new[] { ("normal", "_n"), ("mouseOver", "_h"), ("pressed", "_p") })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/WorldMap/button:close/{state}/0", Path.Combine(dir, $"map_x{name}.png"))) return false;
        if (!UiPng.Save(ui, "UI", "UIMap.img/WorldMap/backgrnd", Path.Combine(dir, "wm_frame.png"))) return false;

        // The channel bar: the CHANGE CHANNEL panel.
        if (!UiPng.Save(ui, "UI", "UIWindow.img/Channel/t", Path.Combine(dir, "ch_t.png"))) return false;

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
        var regions = new List<object>();
        foreach (Wz_Node child in list.Nodes)
        {
            string title = child.Nodes["title"]?.Value as string ?? "";
            var spot = child.Nodes["spot"]?.Value as Wz_Vector;
            if (title == "" || spot == null) continue; // the towns and the links have no title
            var mapNo = child.Nodes["mapNo"];
            var ids = mapNo == null ? new List<int>() : mapNo.Nodes.Select(n => n.GetValueEx<int>(0)).ToList();
            regions.Add(new { Title = title, X = (origin?.X ?? 0) + spot.X, Y = (origin?.Y ?? 0) + spot.Y, Maps = ids });
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

        // The channel rows of the channel panel (CH1..CH30).
        var channelRows = ui.Find("UIWindow.img/Channel/ch");
        if (channelRows == null)
        {
            Console.WriteLine("channel list not found: UIWindow.img/Channel/ch");
            return false;
        }

        WriteJson(Path.Combine(dir, "mapui.json"), new
        {
            World = new { Image = "ui/wm_world.png", Regions = regions },
            Channel = new { Image = "ui/ch_t.png", World = worldName, Channels = channelRows.Nodes.Count },
            Maps = names,
        });
        Console.WriteLine($"map ui: {regions.Count} regions, {names.Count} map names, {channelRows.Nodes.Count} channels");
        return true;
    }
}
