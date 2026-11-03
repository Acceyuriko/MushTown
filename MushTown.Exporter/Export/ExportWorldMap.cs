using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The world map (Map/WorldMap/*.img + MapHelper.img/worldMap + UIMap.img/WorldMap).
// The root map and its first level regions are exported whole; the deeper ones
// are only listed, because none of our maps live there. The client reads
// worldmap.json (MushTown.Client/Ui/WorldMap.cs) and loads the pictures by name.
public static partial class Export
{
    // The pictures of the window that are not part of the frame.
    private static readonly (string Node, string Name)[] WorldMapParts =
    {
        ("UIMap.img/WorldMap/mapInfo/layer:mapNameNormal", "wm_info_top"),
        ("UIMap.img/WorldMap/mapInfo/layer:mapInfoNormal", "wm_info_body"),
        ("Basic.img/sysPop/back/top", "wm_pop_top"),
        ("Basic.img/sysPop/back/mid", "wm_pop_mid"),
        ("Basic.img/sysPop/back/bottom", "wm_pop_bottom"),
        ("Basic.img/sysPop/button/BtYes/normal/0", "wm_pop_yes_n"),
        ("Basic.img/sysPop/button/BtYes/mouseOver/0", "wm_pop_yes_h"),
        ("Basic.img/sysPop/button/BtYes/pressed/0", "wm_pop_yes_p"),
        ("Basic.img/sysPop/button/BtNo/normal/0", "wm_pop_no_n"),
        ("Basic.img/sysPop/button/BtNo/mouseOver/0", "wm_pop_no_h"),
        ("Basic.img/sysPop/button/BtNo/pressed/0", "wm_pop_no_p"),
        ("UIMap.img/WorldMap/mapInfo/lifeNpc/icon", "wm_life_npc"),
        ("UIMap.img/WorldMap/mapInfo/lifeMob/icon", "wm_life_mob"),
        ("UIMap.img/WorldMap/mapInfo/button:premiumTeleport0/disabled/0", "wm_premium_d"),
        ("UIMap.img/WorldMap/mapInfo/button:teleport/disabled/0", "wm_teleport_d"),
        ("UIMap.img/WorldMap/mapInfo/button:navigationStart/disabled/0", "wm_navi_d"),
    };

    // upper, locate and search work; book mark and navigation are drawn
    // disabled, so only their disabled art is exported.
    private static readonly (string Node, string Name, bool Enabled)[] WorldMapButtons =
    {
        ("button:goToUpperWorldMap", "upper", true),
        ("button:goToCurrentMap", "locate", true),
        ("button:search", "search", true),
        ("button:bookMark", "bookmark", false),
        ("button:navigation", "navi", false),
    };

    public static bool ExportWorldMap(WzRoot ui, WzRoot map, WzRoot str, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        var (mapCat, mapPre) = MapTree(map);

        foreach (var (node, name) in WorldMapParts)
            if (!UiPng.Save(ui, "UI", node, Path.Combine(dir, $"{name}.png"))) return false;
        foreach (var (node, name, enabled) in WorldMapButtons)
        {
            string[] states = enabled ? new[] { "normal", "mouseOver", "pressed" } : new[] { "disabled" };
            string[] suffixes = enabled ? new[] { "_n", "_h", "_p" } : new[] { "_d" };
            for (int i = 0; i < states.Length; i++)
                if (!UiPng.Save(ui, "UI", $"UIMap.img/WorldMap/{node}/{states[i]}/0", Path.Combine(dir, $"wm_btn_{name}{suffixes[i]}.png"))) return false;
        }

        // The search window: its frame, its two buttons and the result row parts.
        const string search = "UIMap.img/WorldMap/WorldMapSearch";
        if (!UiPng.Save(ui, "UI", $"{search}/backgrnd", Path.Combine(dir, "wm_s_back.png"))) return false;
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, "UI", $"{search}/button:search/{state}/0", Path.Combine(dir, $"wm_s_go_{suffix}.png"))) return false;
        foreach (var (node, name) in new[]
        {
            ("resultMap/mouseOverBase", "row"), ("resultMap/icon", "map"),
            ("resultNpc/icon", "npc"), ("resultMob/icon", "mob"),
        })
            if (!UiPng.Save(ui, "UI", $"{search}/{node}", Path.Combine(dir, $"wm_s_{name}.png"))) return false;

        // The combo boxes of the top strip: three pieces per state, from the
        // shared Basic.img set the WZ points at.
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            for (int i = 0; i < 3; i++)
                if (!UiPng.Save(ui, "UI", $"Basic.img/ComboBox21/{state}/{i}", Path.Combine(dir, $"wm_combo_{suffix}{i}.png"))) return false;

        // The tooltip: the grey rule under the name and one bullet per kind.
        foreach (var (node, name) in new[] { ("Line", "line"), ("Mob", "mob"), ("Npc", "npc") })
            if (!UiPng.Save(ui, "UI", $"UIWindow2.img/ToolTip/WorldMap/{node}", Path.Combine(dir, $"wm_tip_{name}.png"))) return false;

        var root = map.Find($"{mapPre}WorldMap/WorldMap.img");
        if (root == null) { Console.WriteLine("world map not found: Map/WorldMap/WorldMap.img"); return false; }
        var helper = map.Find($"{mapPre}MapHelper.img/worldMap");
        if (helper == null) { Console.WriteLine("world map art not found: MapHelper.img/worldMap"); return false; }

        // The node icons, by the type number the MapList entries carry.
        var mapImage = helper.Nodes["mapImage"];
        if (mapImage == null) { Console.WriteLine("world map icons not found"); return false; }
        var icons = new List<object>();
        foreach (Wz_Node icon in mapImage.Nodes)
        {
            string iconArt = $"{mapPre}MapHelper.img/worldMap/mapImage/{icon.Text}";
            if (!UiPng.Save(map, mapCat, iconArt, Path.Combine(dir, $"wm_node_{icon.Text}.png"))) return false;
            if (!UiPng.TryRead(map, mapCat, iconArt, out Wz_Png? iconPng, out Wz_Vector? iconOrigin)) return false;
            // The origin is the point that sits on the map spot; it is not the
            // centre of the picture, so the client cannot just centre it.
            icons.Add(new { Type = int.Parse(icon.Text), W = iconPng!.Width, H = iconPng.Height, OX = iconOrigin?.X ?? 0, OY = iconOrigin?.Y ?? 0 });
        }
        // The pin and the arrow hang off the map point by their origin too.
        string pinArt = $"{mapPre}MapHelper.img/worldMap/curPos/0";
        if (!UiPng.Save(map, mapCat, pinArt, Path.Combine(dir, "wm_cur.png"))) return false;
        if (!UiPng.TryRead(map, mapCat, pinArt, out Wz_Png? pinPng, out Wz_Vector? pinOrigin)) return false;
        string selArt = $"{mapPre}MapHelper.img/worldMap/selectPos/0";
        if (!UiPng.Save(map, mapCat, selArt, Path.Combine(dir, "wm_select.png"))) return false;
        if (!UiPng.TryRead(map, mapCat, selArt, out Wz_Png? selPng, out Wz_Vector? selOrigin)) return false;

        // The name of every map a node stands for, so a tooltip can name a node
        // whose map we did not export.
        var mapImg = str.Find("Map.img") ?? str.Find("String/Map.img");
        if (mapImg == null) { Console.WriteLine("map names not found: String.wz Map.img"); return false; }
        var names = new Dictionary<int, string>();
        string MapName(int id)
        {
            if (names.TryGetValue(id, out string? hit)) return hit;
            names[id] = MapEntry(mapImg, id, out _)?.Nodes["mapName"]?.Value as string ?? "";
            return names[id];
        }

        // The root and the regions its links point at.
        var wanted = new List<string> { "WorldMap" };
        var rootLinks = root.Nodes["MapLink"];
        if (rootLinks != null)
            foreach (Wz_Node link in rootLinks.Nodes)
            {
                if (link.Nodes["link"]?.Nodes["linkMap"]?.Value is not string target) continue;
                if (!wanted.Contains(target)) wanted.Add(target);
            }

        var ours = new HashSet<int>(ExportList.Load("maps"));
        var maps = new List<object>();
        foreach (string name in wanted)
            if (!AddWorldMap(map, mapCat, mapPre, dir, name, ours, maps, MapName)) return false;

        WriteJson(Path.Combine(dir, "worldmap.json"), new
        {
            Maps = maps,
            Icons = icons,
            Pin = new { OX = pinOrigin?.X ?? 0, OY = pinOrigin?.Y ?? 0 },
            Sel = new { OX = selOrigin?.X ?? 0, OY = selOrigin?.Y ?? 0 },
            Names = names.Select(kv => new { Id = kv.Key, Name = kv.Value }).OrderBy(n => n.Id).ToList(),
        });
        Console.WriteLine($"world map: {maps.Count} maps, {names.Count} names");
        return true;
    }

    // One world map: the base picture, its nodes and its links.
    private static bool AddWorldMap(WzRoot map, string mapCat, string mapPre, string dir, string name,
        HashSet<int> ours, List<object> maps, Func<int, string> mapName)
    {
        var img = map.Find($"{mapPre}WorldMap/{name}.img");
        if (img == null) { Console.WriteLine($"world map {name}: not found"); return false; }
        string art = $"{mapPre}WorldMap/{name}.img/BaseImg/0";
        string file = $"ui/wm_{name}.png";
        if (!UiPng.TryRead(map, mapCat, art, out Wz_Png? artPng, out Wz_Vector? artOrigin)) return false;
        if (!UiPng.Save(map, mapCat, art, Path.Combine(dir, $"wm_{name}.png"))) return false;
        int ox = artOrigin?.X ?? 0, oy = artOrigin?.Y ?? 0;

        var nodes = new List<object>();
        var list = img.Nodes["MapList"];
        if (list != null)
            foreach (Wz_Node entry in list.Nodes)
            {
                if (entry.Nodes["spot"]?.Value is not Wz_Vector spot) continue;
                var mapNo = entry.Nodes["mapNo"];
                if (mapNo == null) continue;
                var ids = mapNo.Nodes.Select(n => n.GetValueEx<int>(0)).Where(id => id > 0).ToList();
                if (ids.Count == 0) continue;
                nodes.Add(new
                {
                    X = spot.X,
                    Y = spot.Y,
                    Type = entry.Nodes["type"]?.GetValueEx<int>(0) ?? 0,
                    Maps = ids,
                    Name = mapName(ids[0]),
                    // A node opens when it stands for one of our maps; the rest
                    // are drawn but do nothing.
                    Ours = ids.Any(ours.Contains),
                });
            }

        var links = new List<object>();
        var linkList = img.Nodes["MapLink"];
        if (linkList != null)
            foreach (Wz_Node entry in linkList.Nodes)
            {
                if (entry.Nodes["link"]?.Nodes["linkMap"]?.Value is not string target) continue;
                string linkArt = $"{mapPre}WorldMap/{name}.img/MapLink/{entry.Text}/link/linkImg";
                if (!UiPng.TryRead(map, mapCat, linkArt, out Wz_Png? png, out Wz_Vector? at))
                {
                    Console.WriteLine($"world map: link art missing: {linkArt}");
                    return false;
                }
                // The link picture is the highlight plate of that block: the base art
                // already carries the block, so the game draws this one only under
                // the mouse.
                string linkFile = $"wm_link_{name}_{entry.Text}.png";
                if (!UiPng.Save(map, mapCat, linkArt, Path.Combine(dir, linkFile))) return false;
                links.Add(new
                {
                    // The picture's origin point sits on the base art's centre.
                    X = ox - (at?.X ?? 0),
                    Y = oy - (at?.Y ?? 0),
                    W = png!.Width,
                    H = png.Height,
                    ToolTip = entry.Nodes["toolTip"]?.GetValueEx<string>("") ?? "",
                    Target = target,
                    Image = linkFile,
                });
            }

        maps.Add(new
        {
            Name = name,
            Image = file,
            W = artPng!.Width,
            H = artPng.Height,
            OriginX = ox,
            OriginY = oy,
            Parent = img.Nodes["info"]?.Nodes["parentMap"]?.GetValueEx<string>("") ?? "",
            Nodes = nodes,
            Links = links,
        });
        return true;
    }
}
