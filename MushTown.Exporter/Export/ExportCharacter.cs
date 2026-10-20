using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Character + gear exports: body bake, wearable parts, bones, static check.
public static partial class Export
{
    // ---------- character ----------

    private const string BodyImg = @"00002000.img";
    private const string HeadImg = @"00012000.img";
    private const string HairImg = @"Hair\00030020.img";
    private const string FaceImg = @"Face\00020000.img";

    // Beginner gear on top of the naked body: the modern beginner default
    // (Purple Muffler Outfit + Yellow Adventure Shoes) plus the one-handed
    // Sword the stand1/walk1/swingO1 art is baked from. The striped boxers
    // stay under the outfit so a worn coat (the one Ma gear in v1) still has
    // legs to show.
    private static readonly string[] CharaEquips = new[]
    {
        @"Pants\01060026.img",
        @"Longcoat\01050287.img",
        @"Shoes\01072833.img",
        @"Weapon\01302000.img",
    };

    // Wearable gear: img path + the beginner img it swaps out of the doll
    // merge (swap position = the beginner img's slot in CharaEquips).
    private static readonly (string gear, string beginner)[] CharaGears = new[]
    {
        (@"Coat\01040039.img", @"Longcoat\01050287.img"),
        (@"Weapon\01442004.img", @"Weapon\01302000.img"),
    };

    // img/action/frame with a uol resolved at both levels (AvatarCanvas.cs:1203-1226)
    private static Wz_Node? FindImgFrame(WzRoot looseRoot, string img, string action, int frameIdx)
    {
        var actionNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{img}\{action}"));
        return WzNodeUtil.ResolveUol(actionNode?.Nodes[frameIdx.ToString()]);
    }

    private static string ImgId(string img) => Path.GetFileNameWithoutExtension(img);

    // The equip's stand pose frame for a frame it has no art of (a polearm on
    // a sword body): stand1 first, then stand2; a pose with fewer frames
    // repeats its last one. The frame is picked by number like FindImgFrame.
    private static (Wz_Node? node, string pose) FindStandFrame(WzRoot looseRoot, string img, int frameIdx)
    {
        foreach (string action in new[] { "stand1", "stand2" })
        {
            var actionNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{img}\{action}"));
            if (actionNode == null) continue;
            var frames = actionNode.Nodes.Where(n => int.TryParse(n.Text, out _)).OrderBy(n => int.Parse(n.Text)).ToList();
            if (frames.Count == 0) continue;
            var frame = frames.FirstOrDefault(n => n.Text == frameIdx.ToString()) ?? frames[^1];
            return (WzNodeUtil.ResolveUol(frame), action);
        }
        return (null, "");
    }

    private static bool ExportCharacter(WzRoot baseRoot, WzRoot looseRoot, string outDir, string action,
        string? gear = null, string? gearBeginner = null, string? hair = null)
    {
        string hairImg = hair ?? HairImg;
        // One gear's parts across one action: same frames, same bones, only
        // the target img's parts kept (prefix beginners feed the trash frame
        // for bones; identical bytes may rewrite beginner pngs, harmless).
        // Isolated parts equal the merged bake: bones only flow forward, so
        // the target sees exactly base + preceding beginners + its own parts.
        string tag = gear == null ? $"character {action}" : $"gear {ImgId(gear)} {action}";
        // loose tree (Character.wz) holds origin/map/z; png bytes live in Base _Canvas
        List<string> zNames = new();
        var zmap = baseRoot.Find("zmap.img");
        if (zmap == null || zmap.Nodes.Count == 0)
        {
            Console.WriteLine($@"{tag}: Base\zmap.img missing");
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
            Console.WriteLine($"{tag}: {FaceImg} node not found");
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
                    Console.WriteLine($"{tag} frame {frameIdx}: {BodyImg} frame uol is broken");
                    return false;
                }
                break;
            }
            if (!BodyFrameStatic(action, frameIdx, bodyFrame)) return false;

            var headFrame = FindImgFrame(looseRoot, HeadImg, action, frameIdx);
            if (headFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {HeadImg} node not found");
                return false;
            }
            var hairFrame = FindImgFrame(looseRoot, hairImg, action, frameIdx);
            if (hairFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {hairImg} node not found");
                return false;
            }

            var outFrame = new CharaFrame { Delay = Int(bodyFrame, "delay", 120) };
            if (outFrame.Delay <= 0)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {BodyImg} bad delay {outFrame.Delay}");
                return false;
            }
            var bones = new BoneSet(); // y down, feet at (0,0)
            bool face = Int(bodyFrame, "face", 1) != 0;

            // body first (it defines the neck/navel bones), then head, face, hair, gear
            var trash = gear == null ? null : new CharaFrame();
            var baseSink = gear == null ? outFrame : trash!;
            string gearDir = gear == null ? "chara/body" : $"chara/gear/{ImgId(gear)}";
            missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(BodyImg), bodyFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(HeadImg), headFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            if (face) // back-facing frames drop the whole face img (AvatarCanvas.cs:1111-1117)
                missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(FaceImg), faceNode, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(hairImg), hairFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            foreach (string img in CharaEquips)
            {
                bool isTarget = img == gearBeginner;
                string path = isTarget ? gear! : img;
                var equipFrame = FindImgFrame(looseRoot, path, action, frameIdx);
                if (equipFrame == null)
                {
                    // equips without this art (weapon/glove climb frames) reuse
                    // their stand pose; body/head/hair stay strict above
                    var (standFrame, pose) = FindStandFrame(looseRoot, path, frameIdx);
                    equipFrame = standFrame;
                    if (equipFrame == null)
                    {
                        Console.WriteLine($"{tag} frame {frameIdx}: {path} node not found");
                        return false;
                    }
                    Console.WriteLine($"{tag} frame {frameIdx}: {path} falls back to {pose}");
                }
                var sink = gear == null || isTarget ? outFrame : trash!;
                missing += AddFrameParts(baseRoot, sink, bones, ImgId(path), equipFrame, outDir, frameIdx, saved, zNames, face, action,
                    isTarget ? gearDir : "chara/body");
                if (isTarget) break; // later beginners add no bones the target could use
            }

            if (outFrame.Parts.Count == 0)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: no parts exported");
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
            Console.WriteLine($"{tag} has no frames");
            return false;
        }

        Console.WriteLine($"{tag}: {data.Frames.Count} frames, " +
                          $"parts [{string.Join(",", data.Frames.Select(f => f.Parts.Count))}]");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {tag} {missing} character part(s)");
            return false;
        }
        string jsonDir = gear == null ? Path.Combine("chara", "body") : Path.Combine("chara", "gear", ImgId(gear));
        WriteJson(Path.Combine(outDir, jsonDir, $"{action}.json"), data);
        return true;
    }

    // Adds the png parts of one frame node and returns how many images were missing.
    private static int AddFrameParts(WzRoot baseRoot, CharaFrame outFrame, BoneSet bones, string imgId,
        Wz_Node frame, string outDir, int frameIdx, HashSet<string> saved, List<string> zNames, bool face, string action, string pngDir)
    {
        int missing = 0;
        foreach (Wz_Node raw in frame.Nodes)
        {
            // the frame names the slot (hairOverHead...); the uol target only supplies
            // the image and bones, so naming follows the raw node (AvatarCanvas.cs:749)
            string name = raw.Text;
            Wz_Node? part = WzNodeUtil.ResolveUol(raw);
            // beginner: face flag from the body frame, EarType 0, hair out (the
            // beginner set has no cap, so hairOverHead shows), first weapon
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

            string img = $"{pngDir}/{action}/{frameIdx}_{imgId}_{name}.png";
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
                Img = imgId,
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
}
