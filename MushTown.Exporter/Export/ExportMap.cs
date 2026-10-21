using System.Diagnostics.CodeAnalysis;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Map exports: the map list, link checks, and one map's full json + art.
public static partial class Export
{
    // W6 exports the Maple Island chain. Every map must resolve here: WZ dir
    // rule is not id/1e8, so FindMapImg searches Map0..Map9.
    private static readonly int[] MapIds = ExportList.Load("maps");

    // Every portal with a real target must land on an exported map and a
    // portal name that exists there ( Tn matches the target's Pn ).
    private static bool VerifyLinks(Dictionary<int, MapData> maps)
    {
        foreach (var map in maps.Values)
            foreach (var p in map.Portals)
            {
                if (p.Tm == PortalRules.NoTarget) continue;
                if (!maps.TryGetValue(p.Tm, out var target))
                {
                    Console.WriteLine($"map {map.MapId} portal {p.Pn}: target map {p.Tm} not exported");
                    return false;
                }
                // arrival lands on the ground at/below the target portal (top00/top0
                // platforms sit ~1px above their portal point, so look from y - ArrivalLookUp)
                var land = target.Portals.FirstOrDefault(q => q.Pn == p.Tn);
                if (land == null)
                {
                    Console.WriteLine($"map {map.MapId} portal {p.Pn}: target portal '{p.Tn}' missing on map {p.Tm}");
                    return false;
                }
                if (SceneMath.ArrivalGround(target.Footholds, land.X, land.Y) == null)
                {
                    Console.WriteLine($"map {map.MapId} portal {p.Pn}: no ground below '{p.Tn}' on map {p.Tm}");
                    return false;
                }
            }
        return true;
    }

    // ---------- map ----------

    // img file names are zero-padded (000010000.img); the MapN dir is not
    // derivable from the id, so search all ten.
    private static Wz_Node? FindMapImg(WzRoot root, int mapId)
    {
        for (int n = 0; n <= 9; n++)
        {
            var node = root.Find($@"Map\Map\Map{n}\{mapId:D9}.img");
            if (node != null) return node;
        }
        return null;
    }

    // Portal art, same source as the simulator (MapData.cs:840): game art by
    // type name (7 looks like 2), image 0 means "default". A portalContinue
    // node holds the loop, else the frames sit directly under the node (the
    // simulator's start/exit states are skipped: the loop always shows).
    private static bool ExportPortalArt(WzRoot root, int mapId, Wz_Node p, PortalItem item, string outDir, HashSet<string> saved)
    {
        int pt = Int(p, "pt");
        string? type = pt switch { 2 => "pv", 7 => "pv", 10 => "ph", _ => null };
        if (type == null)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: live type {pt} has no art rule");
            return false;
        }
        if (Int(p, "delay") != 0 || Int(p, "onlyOnce") != 0)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: delay/onlyOnce portals are not supported");
            return false;
        }
        int image = Int(p, "image");
        string img = image == 0 ? "default" : image.ToString();
        var node = root.Find($@"Map\MapHelper.img\portal\game\{type}\{img}");
        if (node == null)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} not found");
            return false;
        }
        var frames = node.Nodes["portalContinue"] ?? node;
        for (int i = 0; ; i++)
        {
            var f = frames.Nodes[i.ToString()];
            if (f == null) break;
            var (png, origin) = WzNodeUtil.ReadCanvas(root, f);
            if (png == null)
            {
                Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} frame {i} has no png");
                return false;
            }
            string path = $"portal/{type}/{img}/{i}.png";
            SaveOnce(png, Path.Combine(outDir, path), saved);
            int delay = Int(f, "delay", 100);
            if (delay <= 0)
            {
                Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} frame {i} bad delay {delay}");
                return false;
            }
            item.Frames.Add(new PortalFrame { Image = path, Dx = -(origin?.X ?? 0), Dy = -(origin?.Y ?? 0), Delay = delay });
        }
        if (item.Frames.Count == 0)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} has no frames");
            return false;
        }
        return true;
    }

    // The map's image and its info node. Prints the problem and returns
    // false when the map or the info node is missing.
    private static bool MapNodes(WzRoot root, int mapId, [NotNullWhen(true)] out Wz_Node? scene, [NotNullWhen(true)] out Wz_Node? info)
    {
        scene = FindMapImg(root, mapId);
        if (scene == null)
        {
            Console.WriteLine($"map {mapId} not found");
            info = null;
            return false;
        }
        info = scene.Nodes["info"];
        if (info == null)
        {
            Console.WriteLine($"map {mapId}: info node not found");
            return false;
        }
        return true;
    }

    // The map's track (info/bgm). Prints the problem and returns "" when the
    // string is missing.
    private static string MapBgm(Wz_Node info, int mapId)
    {
        string bgm = Str(info, "bgm");
        if (bgm == "")
        {
            Console.WriteLine($"map {mapId}: info/bgm missing");
            return "";
        }
        return bgm;
    }

    private static MapData? ExportMap(WzRoot root, int mapId, string outDir, HashSet<string> saved)
    {
        if (!MapNodes(root, mapId, out var scene, out var info)) return null;

        var map = new MapData { MapId = mapId };

        // info
        map.Info.VrLeft = Int(info, "VRLeft");
        map.Info.VrTop = Int(info, "VRTop");
        map.Info.VrRight = Int(info, "VRRight");
        map.Info.VrBottom = Int(info, "VRBottom");
        map.Info.Bgm = MapBgm(info, mapId);
        if (map.Info.Bgm == "") return null;

        // miniMap: the canvas plus the world rect it covers
        var mm = scene.Nodes["miniMap"];
        if (mm == null)
        {
            Console.WriteLine($"map {mapId}: miniMap node not found");
            return null;
        }
        var (mmpng, _) = WzNodeUtil.ReadCanvas(root, mm.Nodes["canvas"]);
        if (mmpng == null)
        {
            Console.WriteLine($"map {mapId}: miniMap canvas missing");
            return null;
        }
        string mmpath = $"minimap/{mapId}.png";
        SaveOnce(mmpng, Path.Combine(outDir, mmpath), saved);
        map.MiniMap.Image = mmpath;
        map.MiniMap.Width = Int(mm, "width");
        map.MiniMap.Height = Int(mm, "height");
        map.MiniMap.CenterX = Int(mm, "centerX");
        map.MiniMap.CenterY = Int(mm, "centerY");
        if (map.MiniMap.Width <= 0 || map.MiniMap.Height <= 0)
        {
            Console.WriteLine($"map {mapId}: miniMap has a bad rect");
            return null;
        }

        // spawn portal: node "0" ("sp") when it has tm == NoTarget, else the first
        // portal in file order with tm == NoTarget
        var portalNode = scene.Nodes["portal"];
        Wz_Node? spawnPortal = portalNode?.Nodes["0"];
        if (spawnPortal == null || Int(spawnPortal, "tm") != PortalRules.NoTarget)
        {
            spawnPortal = null;
            if (portalNode != null)
                foreach (Wz_Node p in portalNode.Nodes)
                    if (Int(p, "tm") == PortalRules.NoTarget) { spawnPortal = p; break; }
        }
        if (spawnPortal == null)
        {
            Console.WriteLine($"map {mapId}: no spawn portal (tm == {PortalRules.NoTarget})");
            return null;
        }
        int spawnX = Int(spawnPortal, "x"), spawnY = Int(spawnPortal, "y");
        map.Info.SpawnX = spawnX;
        map.Info.SpawnY = spawnY;

        // footholds: flatten to segments; Layer is the top-level group number
        var fh = scene.Nodes["foothold"];
        if (fh != null)
        {
            foreach (Wz_Node group in fh.Nodes)
            {
                if (!int.TryParse(group.Text, out int layer)) continue;
                foreach (Wz_Node sub in WzRoot.Walk(group))
                {
                    if (sub.Nodes["x1"] == null) continue;
                    map.Footholds.Add(new FootholdItem
                    {
                        X1 = Int(sub, "x1"),
                        Y1 = Int(sub, "y1"),
                        X2 = Int(sub, "x2"),
                        Y2 = Int(sub, "y2"),
                        Layer = layer
                    });
                }
            }
        }

        // ground under spawn: foothold covering spawnX, below the portal, closest to its y
        int? groundY = SceneMath.SpawnGroundY(map.Footholds, spawnX, spawnY);
        if (groundY == null)
        {
            Console.WriteLine($"map {mapId}: no foothold under spawn x={spawnX}");
            return null;
        }
        else
        {
            map.Info.SpawnY = groundY.Value;
            Console.WriteLine($"map {mapId}: spawn ground y={groundY.Value}");
        }

        // portals + ladders (cross-map Tn links are verified after all maps load)
        if (portalNode != null)
            foreach (Wz_Node p in portalNode.Nodes)
            {
                var item = new PortalItem
                {
                    Pn = Str(p, "pn"),
                    X = Int(p, "x"),
                    Y = Int(p, "y"),
                    Tm = Int(p, "tm"),
                    Tn = Str(p, "tn")
                };
                if (item.Tm != PortalRules.NoTarget && !ExportPortalArt(root, mapId, p, item, outDir, saved))
                    return null;
                map.Portals.Add(item);
            }
        var ladderNode = scene.Nodes["ladderRope"];
        if (ladderNode != null)
            foreach (Wz_Node l in ladderNode.Nodes)
                map.Ladders.Add(new LadderItem
                {
                    X = Int(l, "x"),
                    Y1 = Int(l, "y1"),
                    Y2 = Int(l, "y2"),
                    Ladder = Int(l, "l") != 0
                });

        // life: mob spawn points and NPC placements (hide=1 skips: quest
        // phantoms). Ids come zero-padded ("0100000"), so parse them; every
        // mob must be exported, NPC art follows after the maps.
        var lifeNode = scene.Nodes["life"];
        if (lifeNode != null)
            foreach (Wz_Node l in lifeNode.Nodes)
            {
                if (Str(l, "type") == "m")
                {
                    if (!int.TryParse(Str(l, "id"), out int mobId) || !Mobs.ContainsKey(mobId))
                    {
                        Console.WriteLine($"map {mapId}: bad or unexported life mob '{Str(l, "id")}'");
                        return null;
                    }
                    map.Life.Add(new LifeItem { MobId = mobId, X = Int(l, "x"), Cy = Int(l, "cy") });
                }
                else if (Str(l, "type") == "n")
                {
                    if (Int(l, "hide") != 0) continue;
                    if (!int.TryParse(Str(l, "id"), out int npcId))
                    {
                        Console.WriteLine($"map {mapId}: bad life npc '{Str(l, "id")}'");
                        return null;
                    }
                    map.Npcs.Add(new NpcSpawn { NpcId = npcId, X = Int(l, "x"), Cy = Int(l, "cy"), FacingRight = Int(l, "f") != 0 });
                }
            }

        int missing = 0;     // an image we should have read but could not: export fails
        int unsupported = 0; // Spine/ani node: has no png by design, only printed

        // back
        var back = scene.Nodes["back"];
        if (back != null)
        {
            foreach (Wz_Node b in back.Nodes)
            {
                string no = Str(b, "no");
                string bS = Str(b, "bS");
                // ani backs keep frames under ani\{no}; like objs, W6 freezes frame 0
                // (the back\{no} node is a 1x1 placeholder, not a frame)
                string path = Int(b, "ani") != 0
                    ? $@"Map\Back\{bS}.img\ani\{no}\0"
                    : $@"Map\Back\{bS}.img\back\{no}";
                var (png, origin) = WzNodeUtil.ReadCanvas(root, root.Find(path));
                if (png == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }
                // png paths mirror the WZ tree (back/{set}/{no}), so one file per
                // source node and no clashes between sets sharing a number
                string img = $"back/{bS}/{no}.png";
                SaveOnce(png, Path.Combine(outDir, img), saved);
                int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                bool flip = Int(b, "f") != 0;
                map.Backs.Add(new BackItem
                {
                    // flipping mirrors the image inside its rectangle, so the anchor
                    // moves to the other edge of it (MeshBatcher.cs:123)
                    X = Int(b, "x") - (flip ? png.Width - ox : ox),
                    Y = Int(b, "y") - oy,
                    Image = img,
                    Front = Int(b, "front") != 0,
                    Flip = flip,
                    Type = Int(b, "type"),
                    Cx = Int(b, "cx"),
                    Cy = Int(b, "cy"),
                    Rx = Int(b, "rx"),
                    Ry = Int(b, "ry")
                });
            }
        }

        // layers: tiles + objs
        foreach (Wz_Node layer in scene.Nodes)
        {
            if (!int.TryParse(layer.Text, out int layerNo)) continue;

            string tS = Str(layer.Nodes["info"], "tS");

            var tileNode = layer.Nodes["tile"];
            if (tileNode != null && tS.Length > 0)
            {
                foreach (Wz_Node t in tileNode.Nodes)
                {
                    int x = Int(t, "x"), y = Int(t, "y");
                    string u = Str(t, "u"), no = Str(t, "no");
                    string path = $@"Map\Tile\{tS}.img\{u}\{no}";
                    var node = root.Find(path);
                    var (png, origin) = WzNodeUtil.ReadCanvas(root, node);
                    if (png == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }
                    string img = $"tiles/{tS}/{u}/{no}.png";
                    SaveOnce(png, Path.Combine(outDir, img), saved);
                    int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                    map.Tiles.Add(new TileItem
                    {
                        X = x - ox,
                        Y = y - oy,
                        Image = img,
                        Layer = layerNo,
                        Z = Int(WzNodeUtil.ResolveUol(node), "z")
                    });
                }
            }
            else if (tileNode != null && tileNode.Nodes.Count > 0)
            {
                missing++;
                Console.WriteLine($"  missing layer {layerNo}: {tileNode.Nodes.Count} tile(s) but no info/tS");
            }

            var objNode = layer.Nodes["obj"];
            if (objNode != null)
            {
                foreach (Wz_Node o in objNode.Nodes)
                {
                    string oS = Str(o, "oS"), l0 = Str(o, "l0"), l1 = Str(o, "l1"), l2 = Str(o, "l2");
                    string path = $@"Map\Obj\{oS}.img\{l0}\{l1}\{l2}";
                    var frame = WzNodeUtil.ResolveUol(root.Find(path));
                    if (frame == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }

                    // animated objs keep frames as children; static ones are the png itself
                    var (png, origin) = WzNodeUtil.ReadCanvas(root, frame);
                    if (png == null)
                    {
                        var first = frame.Nodes["0"];
                        if (first == null)
                        {
                            unsupported++;
                            Console.WriteLine($"  unsupported {path} (spine/ani, no frame 0)");
                            continue;
                        }
                        (png, origin) = WzNodeUtil.ReadCanvas(root, first);
                        if (png == null) { missing++; Console.WriteLine($"  missing {path} (frame 0 has no png)"); continue; }
                    }

                    string img = $"obj/{oS}/{l0}/{l1}/{l2}.png";
                    SaveOnce(png, Path.Combine(outDir, img), saved);
                    int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                    bool flip = Int(o, "f") != 0;
                    map.Objs.Add(new ObjItem
                    {
                        X = Int(o, "x") - (flip ? png.Width - ox : ox),
                        Y = Int(o, "y") - oy,
                        Image = img,
                        Flip = flip,
                        Layer = layerNo,
                        Z = Int(o, "z")
                    });
                }
            }
        }

        Console.WriteLine($"map {mapId}: {map.Backs.Count} back, {map.Tiles.Count} tile, " +
                          $"{map.Objs.Count} obj, {map.Footholds.Count} foothold, " +
                          $"{map.Portals.Count} portal, {map.Ladders.Count} ladder, " +
                          $"spawn ({spawnX},{map.Info.SpawnY})");
        if (unsupported > 0) Console.WriteLine($"  unsupported {unsupported} map item(s) (spine/ani, no image)");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {missing} map item(s)");
            return null;
        }
        // Boss pin (see BossMushmom): the pin must stand on a foothold.
        if (mapId == BossMap)
        {
            var ground = SceneMath.GroundBelow(map.Footholds, BossX, BossY);
            if (ground == null || SceneMath.FootholdY(ground, BossX) != BossY)
            {
                Console.WriteLine($"map {mapId}: boss pin ({BossX},{BossY}) has no ground");
                return null;
            }
            map.Life.Add(new LifeItem { MobId = BossMushmom, X = BossX, Cy = BossY });
            Console.WriteLine($"map {mapId}: boss {BossMushmom} pinned at ({BossX},{BossY})");
        }
        return map;
    }
}
