using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The map window art: the big minimap frame (UIMap.img/MiniMap/MaxMap),
// the world map window (UIMap.img/WorldMap) and the world map itself
// (Map/WorldMap/WorldMap.img). The file names must match the client
// (MushTown.Client/Panels/HudMap.cs).
public static partial class Export
{
    public static bool ExportMapUi(WzRoot ui, WzRoot map, WzRoot str, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");

        // The big minimap frame: nine slices. The white fill (c) comes
        // first: the client draws it under the map, the rest over it.
        string[] frame = { "c", "nw", "n", "ne", "w", "e", "sw", "s", "se" };
        var frameFiles = new List<string>();
        foreach (string piece in frame)
        {
            string file = $"mm_{piece}.png";
            if (!UiPng.Save(ui, "UI", $"UIMap.img/MiniMap/MaxMap/{piece}", Path.Combine(dir, file))) return false;
            frameFiles.Add(file);
        }

        // The X button (both windows use it): the normal, hover and
        // pressed states, the names the Image widget wants.
        foreach (var (state, name) in new[] { ("normal", "_n"), ("mouseOver", "_h"), ("pressed", "_p") })
            if (!UiPng.Save(ui, "UI", $"UIMap.img/WorldMap/button:close/{state}/0", Path.Combine(dir, $"map_x{name}.png"))) return false;

        // The world map window frame.
        if (!UiPng.Save(ui, "UI", "UIMap.img/WorldMap/backgrnd", Path.Combine(dir, "wm_frame.png"))) return false;

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
        foreach (int id in ExportList.Load("maps"))
        {
            string name = "", street = "";
            foreach (Wz_Node world2 in mapImg.Nodes)
            {
                var entry = world2.Nodes[id.ToString()];
                if (entry == null) continue;
                name = entry.Nodes["mapName"]?.Value as string ?? "";
                street = entry.Nodes["streetName"]?.Value as string ?? "";
                break;
            }
            names.Add(new { Id = id, Name = name, Street = street });
        }

        WriteJson(Path.Combine(dir, "mapui.json"), new
        {
            Frame = frameFiles,
            World = new { Image = "ui/wm_world.png", Regions = regions },
            Maps = names,
        });
        Console.WriteLine($"map ui: {frameFiles.Count} frame pieces, {regions.Count} regions, {names.Count} map names");
        return true;
    }
}
