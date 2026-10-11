using System.Text.Json;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Writes assets/ from the local MapleStory client data:
//   assets/maps/{id}.json + tiles/back/obj png files
//   assets/chara/body/stand2.json + part png files
// Usage: export [DataDir] [OutDir]
public static class Export
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // W2 exports one map and one action. Later weeks make both parameters.
    private const int StartMapId = 100000000;
    private const string StartAction = "stand2";

    public static void Run(string dataDir, string outDir)
    {
        var baseRoot = WzRoot.LoadBase(dataDir);
        if (!ExportMap(baseRoot, StartMapId, outDir) ||
            !ExportCharacter(baseRoot, WzRoot.LoadCategory(dataDir, "Character"), outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        Console.WriteLine("export done -> " + Path.GetFullPath(outDir));
    }

    // ---------- map ----------

    private static bool ExportMap(WzRoot root, int mapId, string outDir)
    {
        var scene = root.Find($@"Map\Map\Map1\{mapId}.img");
        if (scene == null)
        {
            Console.WriteLine($"map {mapId} not found");
            return false;
        }

        var map = new MapData { MapId = mapId };
        var saved = new HashSet<string>();

        // info
        var info = scene.Nodes["info"];
        if (info == null)
        {
            Console.WriteLine($"map {mapId}: info node not found");
            return false;
        }
        map.Info.VrLeft = Int(info, "VRLeft");
        map.Info.VrTop = Int(info, "VRTop");
        map.Info.VrRight = Int(info, "VRRight");
        map.Info.VrBottom = Int(info, "VRBottom");

        // spawn portal: node "0" ("sp") when it has tm == 999999999, else the first
        // portal in file order with tm == 999999999 (this map has 12 of them)
        var portalNode = scene.Nodes["portal"];
        Wz_Node? spawnPortal = portalNode?.Nodes["0"];
        if (spawnPortal == null || Int(spawnPortal, "tm") != 999999999)
        {
            spawnPortal = null;
            if (portalNode != null)
                foreach (Wz_Node p in portalNode.Nodes)
                    if (Int(p, "tm") == 999999999) { spawnPortal = p; break; }
        }
        if (spawnPortal == null)
        {
            Console.WriteLine($"map {mapId}: no spawn portal (tm == 999999999)");
            return false;
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
        int groundY = int.MaxValue;
        int bestDist = int.MaxValue;
        foreach (var f in map.Footholds)
        {
            int minX = Math.Min(f.X1, f.X2), maxX = Math.Max(f.X1, f.X2);
            if (spawnX < minX || spawnX > maxX || f.X1 == f.X2) continue;
            int y = f.Y1 == f.Y2 ? f.Y1
                : f.Y1 + (int)Math.Round((f.Y2 - f.Y1) * (double)(spawnX - f.X1) / (f.X2 - f.X1));
            if (y < spawnY) continue; // a foothold above the portal is not the ground
            int dist = y - spawnY;
            if (dist < bestDist)
            {
                bestDist = dist;
                groundY = y;
            }
        }
        if (groundY == int.MaxValue)
            Console.WriteLine($"map {mapId}: no foothold under spawn x={spawnX}, keeping portal y={spawnY}");
        else
        {
            map.Info.SpawnY = groundY;
            Console.WriteLine($"map {mapId}: spawn ground y={groundY}");
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
                string path = $@"Map\Back\{bS}.img\back\{no}";
                var (png, origin) = WzNodeUtil.ReadCanvas(root, root.Find(path));
                if (png == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }
                string img = $"back/{bS}_{no}.png";
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
                    string img = $"tiles/{tS}_{u}_{no}.png";
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

                    string img = $"obj/{oS}_{l0}_{l1}_{l2}.png";
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
                          $"{map.Objs.Count} obj, {map.Footholds.Count} foothold, spawn ({spawnX},{map.Info.SpawnY})");
        if (unsupported > 0) Console.WriteLine($"  unsupported {unsupported} map item(s) (spine/ani, no image)");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {missing} map item(s)");
            return false;
        }
        WriteJson(Path.Combine(outDir, "maps", $"{mapId}.json"), map);
        return true;
    }

    // ---------- character ----------

    private static bool ExportCharacter(WzRoot baseRoot, WzRoot looseRoot, string outDir)
    {
        // loose tree (Character.wz) holds origin/map/z; png bytes live in Base _Canvas
        List<string> zNames = new();
        var zmap = baseRoot.Find("zmap.img");
        if (zmap == null || zmap.Nodes.Count == 0)
        {
            Console.WriteLine(@"Base\zmap.img missing");
            return false;
        }
        foreach (Wz_Node z in zmap.Nodes) zNames.Add(z.Text);

        var action = looseRoot.Find($@"00002000.img\{StartAction}");
        if (action == null)
        {
            Console.WriteLine($"character action {StartAction} not found");
            return false;
        }

        var saved = new HashSet<string>();
        var data = new CharaData();
        int frameIdx = 0;
        int missing = 0;

        var faceNode = looseRoot.Find(@"Face\00020000.img\default");
        if (faceNode == null)
        {
            Console.WriteLine(@"character face node Face\00020000.img\default not found");
            return false;
        }

        foreach (Wz_Node frame in action.Nodes)
        {
            var headNode = looseRoot.Find($@"00012000.img\{StartAction}\{frame.Text}");
            var hairNode = looseRoot.Find($@"Hair\00030000.img\{StartAction}\{frame.Text}");
            if (headNode == null || hairNode == null)
            {
                Console.WriteLine($"character frame {frame.Text}: head/hair node not found");
                return false;
            }

            var outFrame = new CharaFrame { Delay = Int(frame, "delay") };
            var bones = new BoneSet(); // y down, feet at (0,0)

            // body first (it defines the neck/navel bones), then head, face, hair.
            missing += AddFrameParts(baseRoot, outFrame, bones, frame, outDir, frameIdx, saved, zNames, null);
            missing += AddFrameParts(baseRoot, outFrame, bones, headNode, outDir, frameIdx, saved, zNames, "head", "humanEar");
            missing += AddFrameParts(baseRoot, outFrame, bones, faceNode, outDir, frameIdx, saved, zNames, "face");
            missing += AddFrameParts(baseRoot, outFrame, bones, hairNode, outDir, frameIdx, saved, zNames, "hairOverHead");

            // zmap lists front items first; the client draws the list in order,
            // so sort back-to-front (bigger Z first, drawn first).
            outFrame.Parts.Sort((a, b) => b.Z.CompareTo(a.Z));
            data.Frames.Add(outFrame);
            frameIdx++;
        }

        if (data.Frames.Count == 0)
        {
            Console.WriteLine($"character {StartAction} has no frames");
            return false;
        }

        Console.WriteLine($"chara {StartAction}: {data.Frames.Count} frames, " +
                          $"{data.Frames[0].Parts.Count} parts in frame 0");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {missing} character part(s)");
            return false;
        }
        WriteJson(Path.Combine(outDir, "chara", "body", $"{StartAction}.json"), data);
        return true;
    }

    // Adds the png parts of one frame and returns how many images were missing.
    // only=null means every png part.
    private static int AddFrameParts(WzRoot baseRoot, CharaFrame outFrame, BoneSet bones, Wz_Node frame,
        string outDir, int frameIdx, HashSet<string> saved, List<string> zNames, params string[]? only)
    {
        int missing = 0;
        foreach (Wz_Node raw in frame.Nodes)
        {
            // the frame names the slot (hairOverHead...); the uol target only supplies
            // the image and bones, so naming follows the raw node (AvatarCanvas.cs:749)
            string name = raw.Text;
            Wz_Node? part = WzNodeUtil.ResolveUol(raw);
            if (only != null && !only.Contains(name)) continue;
            if (part == null) { missing++; Console.WriteLine($"  missing {name} image in frame {frameIdx}"); continue; }

            // only nodes with a "map" bone are body parts; siblings like delay/face are not
            var mapNode = part.Nodes["map"];
            if (mapNode == null) continue;

            var (png, origin) = WzNodeUtil.ReadCanvas(baseRoot, part);
            if (png == null || origin == null) { missing++; Console.WriteLine($"  missing {name} image in frame {frameIdx}"); continue; }

            string img = $"chara/body/{StartAction}/{frameIdx}_{name}.png";
            SaveOnce(png, Path.Combine(outDir, img), saved);

            var skin = bones.Attach(name, mapNode);

            // zmap lists front items first. No z node or a numeric z goes in front of
            // everything, a z name that is not in zmap goes behind (AvatarCanvas.cs:980-991).
            var zNode = part.Nodes["z"];
            int? zIndex = zNode?.GetValueEx<int?>(null);
            string zName = zIndex == null ? zNode?.GetValueEx<string>("") ?? "" : "";
            int z = zNode == null ? -1
                : zIndex != null
                ? (zIndex.Value < 0 ? zNames.Count - zIndex.Value : -1 - zIndex.Value)
                : (zNames.Contains(zName) ? zNames.IndexOf(zName) : zNames.Count);

            outFrame.Parts.Add(new CharaPart
            {
                Image = img,
                X = skin.x - origin.X,
                Y = skin.y - origin.Y,
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
            (int x, int y) skin = default;
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

    private static int Int(Wz_Node? node, string name) =>
        node?.Nodes[name]?.GetValueEx<int>(0) ?? 0;

    private static string Str(Wz_Node? node, string name) =>
        node?.Nodes[name]?.GetValueEx<string>("") ?? "";
}
