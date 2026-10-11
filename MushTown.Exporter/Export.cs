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

    public static void Run(string dataDir, string outDir)
    {
        var baseRoot = WzRoot.LoadBase(dataDir);
        ExportMap(baseRoot, 100000000, outDir);
        ExportCharacter(WzRoot.LoadCategory(dataDir, "Character"), outDir);
        Console.WriteLine("export done -> " + Path.GetFullPath(outDir));
    }

    // ---------- map ----------

    private static void ExportMap(WzRoot root, int mapId, string outDir)
    {
        var scene = root.Find($@"Map\Map\Map1\{mapId}.img");
        if (scene == null)
        {
            Console.WriteLine($"map {mapId} not found");
            return;
        }

        var map = new MapData { MapId = mapId };
        var saved = new HashSet<string>();

        // info
        var info = scene.Nodes["info"];
        map.Info.VrLeft = Int(info, "VRLeft");
        map.Info.VrTop = Int(info, "VRTop");
        map.Info.VrRight = Int(info, "VRRight");
        map.Info.VrBottom = Int(info, "VRBottom");
        map.Info.Town = Int(info, "town");

        // spawn portal tm == 999999999
        var portalNode = scene.Nodes["portal"];
        int spawnX = map.Info.VrLeft + 200, spawnY = map.Info.VrBottom;
        if (portalNode != null)
        {
            foreach (Wz_Node p in portalNode.Nodes)
            {
                if (p.Nodes["tm"]?.GetValueEx<int>(-1) == 999999999)
                {
                    spawnX = Int(p, "x");
                    spawnY = Int(p, "y");
                    break;
                }
            }
        }
        map.Info.SpawnX = spawnX;
        map.Info.SpawnY = spawnY;

        // footholds: flatten to segments; Layer = top-level group
        var fh = scene.Nodes["foothold"];
        if (fh != null)
        {
            int layer = 0;
            foreach (Wz_Node group in fh.Nodes)
            {
                foreach (Wz_Node sub in WalkAll(group))
                {
                    if (sub.Nodes["x1"] == null) continue;
                    map.Footholds.Add(new FootholdItem
                    {
                        X1 = Int(sub, "x1"), Y1 = Int(sub, "y1"),
                        X2 = Int(sub, "x2"), Y2 = Int(sub, "y2"),
                        Layer = layer
                    });
                }
                layer++;
            }
        }

        // ground under spawn: foothold covering spawnX closest to the portal y
        int groundY = int.MaxValue;
        int bestDist = int.MaxValue;
        foreach (var f in map.Footholds)
        {
            int minX = Math.Min(f.X1, f.X2), maxX = Math.Max(f.X1, f.X2);
            if (spawnX < minX || spawnX > maxX) continue;
            int y = f.Y1 == f.Y2 ? f.Y1
                : f.Y1 + (int)Math.Round((f.Y2 - f.Y1) * (spawnX - f.X1) / (double)(f.X2 - f.X1));
            int dist = Math.Abs(y - spawnY);
            if (dist < bestDist)
            {
                bestDist = dist;
                groundY = y;
            }
        }
        if (groundY != int.MaxValue) map.Info.SpawnY = groundY;

        // back
        var back = scene.Nodes["back"];
        if (back != null)
        {
            foreach (Wz_Node b in back.Nodes)
            {
                int no = Int(b, "no");
                string bS = Str(b, "bS");
                var (png, _, _) = WzNodeUtil.ReadCanvas(root.Find($@"Map\Back\{bS}.img\back\{no}"));
                if (png == null) continue;
                string img = $"back/{bS}_{no}.png";
                SaveOnce(png, Path.Combine(outDir, img), saved);
                map.Backs.Add(new BackItem { X = Int(b, "x"), Y = Int(b, "y"), Image = img, Front = Int(b, "front") != 0 });
            }
        }

        // layers: tiles + objs
        foreach (Wz_Node layer in scene.Nodes)
        {
            if (!int.TryParse(layer.Text, out _)) continue;

            string tS = Str(layer.Nodes["info"], "tS");

            var tileNode = layer.Nodes["tile"];
            if (tileNode != null && tS.Length > 0)
            {
                foreach (Wz_Node t in tileNode.Nodes)
                {
                    int x = Int(t, "x"), y = Int(t, "y");
                    string u = Str(t, "u"), no = Str(t, "no");
                    var (png, origin, _) = WzNodeUtil.ReadCanvas(root.Find($@"Map\Tile\{tS}.img\{u}\{no}"));
                    if (png == null) continue;
                    string img = $"tiles/{tS}_{u}_{no}.png";
                    SaveOnce(png, Path.Combine(outDir, img), saved);
                    int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                    map.Tiles.Add(new TileItem { X = x - ox, Y = y - oy, Image = img });
                }
            }

            var objNode = layer.Nodes["obj"];
            if (objNode != null)
            {
                foreach (Wz_Node o in objNode.Nodes)
                {
                    string oS = Str(o, "oS"), l0 = Str(o, "l0"), l1 = Str(o, "l1"), l2 = Str(o, "l2");
                    var frame = root.Find($@"Map\Obj\{oS}.img\{l0}\{l1}\{l2}");
                    if (frame == null) continue;

                    // animated objs keep frames as children; static ones are the png itself
                    var (png, origin, _) = WzNodeUtil.ReadCanvas(frame);
                    if (png == null)
                    {
                        var first = frame.Nodes["0"];
                        if (first == null) continue;
                        (png, origin, _) = WzNodeUtil.ReadCanvas(first);
                        if (png == null) continue;
                    }

                    string img = $"obj/{oS}_{l0}_{l1}_{l2}.png";
                    SaveOnce(png, Path.Combine(outDir, img), saved);
                    int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                    map.Objs.Add(new ObjItem
                    {
                        X = Int(o, "x") - ox,
                        Y = Int(o, "y") - oy,
                        Image = img
                    });
                }
            }
        }

        string mapPath = Path.Combine(outDir, "maps", $"{mapId}.json");
        WriteJson(mapPath, map);
        Console.WriteLine($"map {mapId}: {map.Backs.Count} back, {map.Tiles.Count} tile, " +
                          $"{map.Objs.Count} obj, {map.Footholds.Count} foothold, spawn ({spawnX},{map.Info.SpawnY})");
    }

    // ---------- character ----------

    private static void ExportCharacter(WzRoot looseRoot, string outDir)
    {
        // loose tree (Character.wz) holds origin/map/z; png bytes live in Base _Canvas
        List<string> zNames = new();
        var zmap = WzRoot.LastBase.Find("zmap.img");
        if (zmap != null)
            foreach (Wz_Node z in zmap.Nodes) zNames.Add(z.Text);

        var action = looseRoot.Find(@"00002000.img\stand2");
        if (action == null)
        {
            Console.WriteLine("character stand2 not found");
            return;
        }

        var saved = new HashSet<string>();
        var data = new CharaData();
        int frameIdx = 0;

        foreach (Wz_Node frame in action.Nodes)
        {
            var outFrame = new CharaFrame { Delay = Int(frame, "delay") };
            var bones = new Dictionary<string, (int x, int y)>(); // y down, feet at (0,0)

            foreach (Wz_Node part in frame.Nodes)
            {
                var mapNode = part.Nodes["map"];
                var (png, origin, _) = WzNodeUtil.ReadCanvas(part);
                if (mapNode == null || png == null || origin == null) continue;

                string img = $"chara/body/stand2/{frameIdx}_{part.Text}.png";
                SaveOnce(png, Path.Combine(outDir, img), saved);

                // bone math copied from AvatarCanvas.CreateBone
                (int x, int y) skinPos = default;
                bool first = true;
                foreach (Wz_Node boneNode in mapNode.Nodes)
                {
                    var v = boneNode.GetValue<Wz_Vector>();
                    var m = (x: v.X, y: v.Y);
                    if (first) // primary bone: skin offset = -m
                    {
                        first = false;
                        bool found = bones.TryGetValue(boneNode.Text, out var bonePos);
                        if (!found) bones[boneNode.Text] = bonePos = m;
                        skinPos = (bonePos.x - m.x, bonePos.y - m.y);
                    }
                    else if (!bones.ContainsKey(boneNode.Text))
                    {
                        bones[boneNode.Text] = m; // child of root
                    }
                }

                string zName = part.Nodes["z"]?.GetValueEx<string>(null) ?? "";
                int z = zName.Length > 0 && zNames.Contains(zName) ? zNames.IndexOf(zName) : zNames.Count;

                outFrame.Parts.Add(new CharaPart
                {
                    Image = img,
                    X = skinPos.x - origin.X,
                    Y = skinPos.y - origin.Y,
                    Z = z
                });
            }

            // zmap lists front items first; the client draws the list in order,
            // so sort back-to-front (bigger Z first, drawn first).
            outFrame.Parts.Sort((a, b) => b.Z.CompareTo(a.Z));
            data.Frames.Add(outFrame);
            frameIdx++;
        }

        WriteJson(Path.Combine(outDir, "chara", "body", "stand2.json"), data);
        Console.WriteLine($"chara stand2: {data.Frames.Count} frames");
    }

    // ---------- helpers ----------

    private static void SaveOnce(Wz_Png png, string filePath, HashSet<string> saved)
    {
        if (!saved.Add(filePath)) return;
        WzNodeUtil.SavePng(png, filePath);
    }

    private static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    private static int Int(Wz_Node node, string name) =>
        node?.Nodes[name]?.GetValueEx<int>(0) ?? 0;

    private static string Str(Wz_Node node, string name) =>
        node?.Nodes[name]?.GetValueEx<string>(null) ?? "";

    private static IEnumerable<Wz_Node> WalkAll(Wz_Node node)
    {
        foreach (Wz_Node child in node.Nodes)
        {
            yield return child;
            foreach (Wz_Node sub in WalkAll(child)) yield return sub;
        }
    }
}
