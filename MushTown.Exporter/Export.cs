using System.Text.Json;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Writes assets/ from the local MapleStory client data:
//   assets/maps/{id}.json + tiles/back/obj png files
//   assets/chara/body/{action}.json + part png files
//   assets/ui/*.png (login/server/chars/job/setup art, normal states only)
// Usage: export [DataDir] [OutDir]
public static class Export
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // W6 exports the Maple Island chain. Every map must resolve here: WZ dir
    // rule is not id/1e8, so FindMapImg searches Map0..Map9.
    private static readonly int[] MapIds = new[] { 10000, 20000, 1000000, 1010000, 1020000, 2000000 };

    // Mob table: our life mobs have no own art img, so each borrows its
    // same-named twin (String names verified identical). Stats are v1 feel
    // numbers: the modern client keeps them server-side, no maxHP node exists
    // anywhere in the data (verified in W8 recon).
    private static readonly Dictionary<int, (string art, int hp, double def)> Mobs = new()
    {
        [100000] = ("0100100", 40, 0),     // Snail
        [100001] = ("0100101", 55, 0),     // Blue Snail
        [100003] = ("0120100", 70, 0),     // Spore (twin 120100)
        [100004] = ("1210102", 110, 0.05), // Orange Mushroom (twin 1210102)
    };

    // W7.5 lobby art: (node path, file name). Normal button states only;
    // hover is a white overlay drawn by the client, so no hover art needed.
    // Must match Menu.cs UiFiles: every file there must be listed here.
    private static readonly (string node, string file)[] UiFiles = new[]
    {
        ("LoginBack.img/Title/0", "loginbg.png"),
        ("Login.img/Title_new/backgrd", "loginwin.png"),
        ("Login.img/Title_new/BtLogin/normal/0", "btn_login.png"),
        ("LoginBack.img/WorldSelect/default/0/0", "worldbg.png"),
        ("Login.img/WorldSelect/worldList/top", "row_top.png"),
        ("Login.img/WorldSelect/worldList/middle", "row_mid.png"),
        ("Login.img/WorldSelect/worldList/bottom", "row_bot.png"),
        ("Login.img/WorldSelect/channelList/common/top", "ch_top.png"),
        ("Login.img/WorldSelect/channelList/common/middle", "ch_mid.png"),
        ("Login.img/WorldSelect/channelList/common/bottom", "ch_bot.png"),
        ("Login.img/CharSelect/canvas:charInfo", "charinfo.png"),
        ("Login.img/CharSelect/BtSelect/normal/0", "btn_start.png"),
        ("Login.img/CharSelect/BtNew/normal/0", "btn_create.png"),
        ("Login.img/CharSelect/BtDelete/normal/0", "btn_delete.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/normal/0", "btn_deleteyes.png"),
        ("Login.img/CharSelect/selectWorld", "selectworld.png"),
        ("Login.img/CharSelect/nameTag/0/0", "tag_l.png"),
        ("Login.img/CharSelect/nameTag/0/1", "tag_m.png"),
        ("Login.img/CharSelect/nameTag/0/2", "tag_r.png"),
        ("Login.img/ClassSelect/list/backgrnd", "listbg.png"),
        ("Login.img/ClassSelect/list/class/button:classEnabled/110/normal/0", "cell_hero.png"),
        ("Login.img/ClassSelect/button:make/normal/0", "btn_make.png"),
        ("LoginBack.img/CustomizeChar/000/0/0", "setupbg.png"),
        ("Login.img/CustomizeChar/000/BtYes/normal/0", "btn_confirm.png"),
        ("Login.img/CustomizeChar/000/BtNo/normal/0", "btn_cancel.png"),
    };

    public static void Run(string dataDir, string outDir)
    {
        var baseRoot = WzRoot.LoadBase(dataDir);
        var saved = new HashSet<string>(); // png names are global: one set for all maps
        var maps = new Dictionary<int, MapData>();
        foreach (int mapId in MapIds)
        {
            var map = ExportMap(baseRoot, mapId, outDir, saved);
            if (map == null)
            {
                Environment.ExitCode = 1;
                return;
            }
            maps[mapId] = map;
        }
        if (!VerifyLinks(maps))
        {
            Environment.ExitCode = 1;
            return;
        }
        foreach (var map in maps.Values)
            WriteJson(Path.Combine(outDir, "maps", $"{map.MapId}.json"), map);
        var chara = WzRoot.LoadCategory(dataDir, "Character");
        foreach (string action in Net.Actions)
        {
            if (!ExportCharacter(baseRoot, chara, outDir, action))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        var mob = WzRoot.LoadCategory(dataDir, "Mob");
        foreach (int mobId in Mobs.Keys.OrderBy(id => id))
        {
            if (!ExportMob(mob, mobId, outDir, saved))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        var ui = WzRoot.LoadCategory(dataDir, "UI");
        foreach ((string node, string file) in UiFiles)
        {
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        Console.WriteLine("export done -> " + Path.GetFullPath(outDir));
    }

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

    private static MapData? ExportMap(WzRoot root, int mapId, string outDir, HashSet<string> saved)
    {
        var scene = FindMapImg(root, mapId);
        if (scene == null)
        {
            Console.WriteLine($"map {mapId} not found");
            return null;
        }

        var map = new MapData { MapId = mapId };

        // info
        var info = scene.Nodes["info"];
        if (info == null)
        {
            Console.WriteLine($"map {mapId}: info node not found");
            return null;
        }
        map.Info.VrLeft = Int(info, "VRLeft");
        map.Info.VrTop = Int(info, "VRTop");
        map.Info.VrRight = Int(info, "VRRight");
        map.Info.VrBottom = Int(info, "VRBottom");

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

        // life: mob spawn points only (NPCs wait for W14). The id comes as a
        // zero-padded string ("0100000"), so parse it; every mob must be exported.
        var lifeNode = scene.Nodes["life"];
        if (lifeNode != null)
            foreach (Wz_Node l in lifeNode.Nodes)
            {
                if (Str(l, "type") != "m") continue;
                if (!int.TryParse(Str(l, "id"), out int mobId) || !Mobs.ContainsKey(mobId))
                {
                    Console.WriteLine($"map {mapId}: bad or unexported life mob '{Str(l, "id")}'");
                    return null;
                }
                map.Life.Add(new LifeItem { MobId = mobId, X = Int(l, "x"), Cy = Int(l, "cy") });
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
        return map;
    }

    // ---------- character ----------

    private const string BodyImg = @"00002000.img";
    private const string HeadImg = @"00012000.img";
    private const string HairImg = @"Hair\00030000.img";
    private const string FaceImg = @"Face\00020000.img";

    // Beginner gear on top of the naked body. Body, head and hair must carry
    // every action; gear without the art falls back to its stand2 frame (face
    // stays special: one default emotion node shared by every frame).
    private static readonly string[] CharaEquips = new[]
    {
        @"Cap\01000000.img",
        @"Coat\01040036.img",
        @"Pants\01060026.img",
        @"Shoes\01070001.img",
        @"Glove\01080001.img",
        @"Cape\01100000.img",
        @"Weapon\01442000.img",
    };

    // img/action/frame with a uol resolved at both levels (AvatarCanvas.cs:1203-1226)
    private static Wz_Node? FindImgFrame(WzRoot looseRoot, string img, string action, int frameIdx)
    {
        var actionNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{img}\{action}"));
        return WzNodeUtil.ResolveUol(actionNode?.Nodes[frameIdx.ToString()]);
    }

    private static string ImgId(string img) => Path.GetFileNameWithoutExtension(img);

    private static bool ExportCharacter(WzRoot baseRoot, WzRoot looseRoot, string outDir, string action)
    {
        // loose tree (Character.wz) holds origin/map/z; png bytes live in Base _Canvas
        List<string> zNames = new();
        var zmap = baseRoot.Find("zmap.img");
        if (zmap == null || zmap.Nodes.Count == 0)
        {
            Console.WriteLine($@"character {action}: Base\zmap.img missing");
            return false;
        }
        foreach (Wz_Node z in zmap.Nodes) zNames.Add(z.Text);

        var saved = new HashSet<string>();
        var data = new CharaData();
        int missing = 0;

        // the face emotion is one node shared by all frames, not per-frame data
        var faceNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{FaceImg}\default"));
        if (faceNode == null)
        {
            Console.WriteLine($"character {action}: {FaceImg} node not found");
            return false;
        }

        // frames run 0,1,2... and stop at the first gap (AvatarCanvas.cs:511-522)
        for (int frameIdx = 0; ; frameIdx++)
        {
            var bodyFrame = FindImgFrame(looseRoot, BodyImg, action, frameIdx);
            if (bodyFrame == null)
            {
                // a real gap ends the action; a dangling frame uol is loud
                var bodyAction = WzNodeUtil.ResolveUol(looseRoot.Find($@"{BodyImg}\{action}"));
                if (bodyAction?.Nodes[frameIdx.ToString()] != null)
                {
                    Console.WriteLine($"character {action} frame {frameIdx}: {BodyImg} frame uol is broken");
                    return false;
                }
                break;
            }
            if (!BodyFrameStatic(action, frameIdx, bodyFrame)) return false;

            var headFrame = FindImgFrame(looseRoot, HeadImg, action, frameIdx);
            if (headFrame == null)
            {
                Console.WriteLine($"character {action} frame {frameIdx}: {HeadImg} node not found");
                return false;
            }
            var hairFrame = FindImgFrame(looseRoot, HairImg, action, frameIdx);
            if (hairFrame == null)
            {
                Console.WriteLine($"character {action} frame {frameIdx}: {HairImg} node not found");
                return false;
            }

            var outFrame = new CharaFrame { Delay = Int(bodyFrame, "delay", 120) };
            if (outFrame.Delay <= 0)
            {
                Console.WriteLine($"character {action} frame {frameIdx}: {BodyImg} bad delay {outFrame.Delay}");
                return false;
            }
            var bones = new BoneSet(); // y down, feet at (0,0)
            bool face = Int(bodyFrame, "face", 1) != 0;

            // body first (it defines the neck/navel bones), then head, face, hair, gear
            missing += AddFrameParts(baseRoot, outFrame, bones, ImgId(BodyImg), bodyFrame, outDir, frameIdx, saved, zNames, face, action);
            missing += AddFrameParts(baseRoot, outFrame, bones, ImgId(HeadImg), headFrame, outDir, frameIdx, saved, zNames, face, action);
            if (face) // back-facing frames drop the whole face img (AvatarCanvas.cs:1111-1117)
                missing += AddFrameParts(baseRoot, outFrame, bones, ImgId(FaceImg), faceNode, outDir, frameIdx, saved, zNames, face, action);
            missing += AddFrameParts(baseRoot, outFrame, bones, ImgId(HairImg), hairFrame, outDir, frameIdx, saved, zNames, face, action);
            foreach (string img in CharaEquips)
            {
                var equipFrame = FindImgFrame(looseRoot, img, action, frameIdx);
                if (equipFrame == null)
                {
                    // equips without this art (weapon/glove climb frames) reuse
                    // their stand2 frame; body/head/hair stay strict above
                    equipFrame = FindImgFrame(looseRoot, img, "stand2", frameIdx);
                    if (equipFrame == null)
                    {
                        Console.WriteLine($"character {action} frame {frameIdx}: {img} node not found");
                        return false;
                    }
                    Console.WriteLine($"character {action} frame {frameIdx}: {img} falls back to stand2");
                }
                missing += AddFrameParts(baseRoot, outFrame, bones, ImgId(img), equipFrame, outDir, frameIdx, saved, zNames, face, action);
            }

            if (outFrame.Parts.Count == 0)
            {
                Console.WriteLine($"character {action} frame {frameIdx}: no parts exported");
                return false;
            }

            // zmap lists front items first; the client draws the list in order,
            // so sort back-to-front (bigger Z first, drawn first). Stable: equal
            // Z keeps file order (the W3 glove pair shares one Z).
            outFrame.Parts = outFrame.Parts.OrderByDescending(p => p.Z).ToList();
            data.Frames.Add(outFrame);
        }

        if (data.Frames.Count == 0)
        {
            Console.WriteLine($"character {action} has no frames");
            return false;
        }

        Console.WriteLine($"character {action}: {data.Frames.Count} frames, " +
                          $"parts [{string.Join(",", data.Frames.Select(f => f.Parts.Count))}]");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {action} {missing} character part(s)");
            return false;
        }
        WriteJson(Path.Combine(outDir, "chara", "body", $"{action}.json"), data);
        return true;
    }

    // One mob's art: every action's frames in numeric order (die1 starts at 1,
    // not 0, hence the sort). Frames are bare pngs with no origin or delay,
    // so each gets delay 100 and the client anchors center-bottom.
    private static bool ExportMob(WzRoot mob, int mobId, string outDir, HashSet<string> saved)
    {
        var img = mob.Find($@"_Canvas\{Mobs[mobId].art}.img");
        if (img == null)
        {
            Console.WriteLine($"mob {mobId}: art {Mobs[mobId].art}.img not found");
            return false;
        }
        var data = new MobData { MobId = mobId, MaxHp = Mobs[mobId].hp, Def = Mobs[mobId].def };
        foreach (Wz_Node action in img.Nodes)
        {
            if (action.Text == "info") continue;
            var outAction = new MobAction { Name = action.Text };
            var frames = new List<(int key, Wz_Node node)>();
            foreach (Wz_Node f in action.Nodes)
            {
                if (!int.TryParse(f.Text, out int key))
                {
                    Console.WriteLine($"mob {mobId} {action.Text}: non-frame child {f.Text}");
                    return false;
                }
                frames.Add((key, f));
            }
            int i = 0;
            foreach ((int _, Wz_Node f) in frames.OrderBy(t => t.key))
            {
                if (f.Value is not Wz_Png png)
                {
                    Console.WriteLine($"mob {mobId} {action.Text} frame {f.Text}: not a png");
                    return false;
                }
                string path = $"mob/{mobId}/{action.Text}/{i}.png";
                SaveOnce(png, Path.Combine(outDir, path), saved);
                outAction.Frames.Add(new MobFrame { Image = path, Delay = 100 });
                i++;
            }
            if (outAction.Frames.Count == 0)
            {
                Console.WriteLine($"mob {mobId} {action.Text}: no frames");
                return false;
            }
            data.Actions.Add(outAction);
        }
        // W8 plays the idle (stand, or move when the mob has no stand, like
        // the blue snail), hit1 (flinch), die1 (death); W12 AI drives move.
        foreach (string need in new[] { "hit1", "die1" })
            if (!data.Actions.Any(a => a.Name == need))
            {
                Console.WriteLine($"mob {mobId}: no {need} action");
                return false;
            }
        if (!data.Actions.Any(a => a.Name is "stand" or "move"))
        {
            Console.WriteLine($"mob {mobId}: no stand or move action");
            return false;
        }
        Console.WriteLine($"mob {mobId}: " +
            string.Join(", ", data.Actions.Select(a => $"{a.Name} x{a.Frames.Count}")));
        WriteJson(Path.Combine(outDir, "mob", $"{mobId}.json"), data);
        return true;
    }

    // Adds the png parts of one frame node and returns how many images were missing.
    private static int AddFrameParts(WzRoot baseRoot, CharaFrame outFrame, BoneSet bones, string imgId,
        Wz_Node frame, string outDir, int frameIdx, HashSet<string> saved, List<string> zNames, bool face, string action)
    {
        int missing = 0;
        foreach (Wz_Node raw in frame.Nodes)
        {
            // the frame names the slot (hairOverHead...); the uol target only supplies
            // the image and bones, so naming follows the raw node (AvatarCanvas.cs:749)
            string name = raw.Text;
            Wz_Node? part = WzNodeUtil.ResolveUol(raw);
            // beginner: face flag from the body frame, EarType 0, hair out (this cap
            // leaves hairOverHead showing, verified in logs/w3_stand2.png), first weapon
            // a dangling part has no parent; the frame itself tells multi weapons apart
            bool multiWeapon = (part?.ParentNode ?? frame).Nodes["weapon1"] != null;
            bool visible = SceneMath.PartVisible(name, face, earType: 0, hairCover: false,
                showHairShade: false, weaponIndex: 0, multiWeapon: multiWeapon);
            if (part == null)
            {
                // a hidden part needs no image; a visible one with a dangling uol is loud
                if (visible)
                {
                    missing++;
                    Console.WriteLine($"  character {action} frame {frameIdx}: missing {imgId} {name} image");
                }
                continue;
            }
            if (!visible) continue;

            var (png, origin) = WzNodeUtil.ReadCanvas(baseRoot, part);
            if (png == null)
            {
                // frame props (delay/face/...) carry no image; anything else is a real
                // miss. Deliberately stricter than the reference, which also skips
                // dangling uols and imageless containers without a word.
                if (part.Value is Wz_Png || part.Nodes.Count > 0)
                {
                    missing++;
                    Console.WriteLine($"  character {action} frame {frameIdx}: missing {imgId} {name} image");
                }
                continue;
            }

            string img = $"chara/body/{action}/{frameIdx}_{imgId}_{name}.png";
            SaveOnce(png, Path.Combine(outDir, img), saved);

            // a part without a "map" bone hangs on the root (AvatarCanvas.cs:791-794)
            var mapNode = part.Nodes["map"];
            var skin = mapNode == null ? (x: 0, y: 0) : bones.Attach(name, mapNode);

            // zmap lists front items first. A missing z lands in front of everything,
            // a numeric z maps around it (negatives go behind), and a z name that is
            // not in zmap goes behind (AvatarCanvas.cs:980-991).
            var zNode = part.Nodes["z"];
            int? zIndex = zNode?.GetValueEx<int?>(null);
            string zName = zIndex == null ? zNode?.GetValueEx<string>("") ?? "" : "";
            int z;
            if (zNode == null) z = -1;
            else if (zIndex != null) z = zIndex.Value < 0 ? zNames.Count - zIndex.Value : -1 - zIndex.Value;
            else if (zName == "") z = -1; // empty z counts as missing (AvatarCanvas.cs:980)
            else z = zNames.Contains(zName) ? zNames.IndexOf(zName) : zNames.Count;

            int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
            outFrame.Parts.Add(new CharaPart
            {
                Image = img,
                X = skin.x - ox,
                Y = skin.y - oy,
                Z = z
            });
        }
        return missing;
    }

    // Bone tree of one animation frame. Mirrors AvatarCanvas.CreateBone/AppendBone.
    private sealed class BoneSet
    {
        private readonly Dictionary<string, (string? parent, int x, int y)> _nodes = new();

        private (int x, int y) Abs(string name)
        {
            var (parent, x, y) = _nodes[name];
            while (parent != null)
            {
                var p = _nodes[parent];
                x += p.x;
                y += p.y;
                parent = p.parent;
            }
            return (x, y);
        }

        // Returns where the part's image anchor lands. Bones come from the part's
        // "map" node: the first entry is the bone the image hangs on, the rest are
        // joints that other parts (head, hair, face) attach to.
        public (int x, int y) Attach(string partName, Wz_Node mapNode)
        {
            string? cascadeParent = null;
            (int x, int y) skin = default; // empty map stays at root (reference drops it; ours shows it)
            bool first = true;

            foreach (Wz_Node boneNode in mapNode.Nodes)
            {
                if (boneNode.Text == "muzzle") continue; // skipped by the reference too (AvatarCanvas.cs:776)
                var v = boneNode.GetValue<Wz_Vector>();
                string name = boneNode.Text;

                if (first)
                {
                    first = false;
                    if (_nodes.ContainsKey(name)) // shared bone: hang on a joint at -map
                    {
                        string joint = "@" + name + "_" + partName;
                        _nodes[joint] = (name, -v.X, -v.Y);
                        cascadeParent = joint;
                        skin = Abs(joint);
                    }
                    else // new bone at +map, image offset cancels it out
                    {
                        _nodes[name] = (null, v.X, v.Y);
                        cascadeParent = null;
                        skin = (0, 0);
                    }
                }
                else
                {
                    _nodes[name] = (cascadeParent, v.X, v.Y);
                }
            }
            return skin;
        }
    }

    // ---------- helpers ----------

    private static void SaveOnce(Wz_Png png, string filePath, HashSet<string> saved)
    {
        if (!saved.Add(filePath)) return;
        WzNodeUtil.SavePng(png, filePath);
    }

    private static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    private static int Int(Wz_Node? node, string name, int defaultValue = 0) =>
        node?.Nodes[name]?.GetValueEx<int>(defaultValue) ?? defaultValue;

    // Poses are baked static; a body frame carrying flip/move/rotate fails the
    // export (no W4 action uses them). Only the body frame is checked: the
    // reference plays these from the body action alone (AvatarCanvas.cs:617), ignoring other imgs' values.
    private static bool BodyFrameStatic(string action, int frameIdx, Wz_Node frame)
    {
        var move = frame.Nodes["move"]?.GetValueEx<Wz_Vector>(new Wz_Vector(0, 0));
        int mx = move?.X ?? 0, my = move?.Y ?? 0;
        if (Int(frame, "flip") == 0 && Int(frame, "rotate") == 0 && mx == 0 && my == 0)
            return true;
        Console.WriteLine($"character {action} frame {frameIdx}: {BodyImg} has flip/move/rotate");
        return false;
    }

    private static string Str(Wz_Node? node, string name) =>
        node?.Nodes[name]?.GetValueEx<string>("") ?? "";
}
