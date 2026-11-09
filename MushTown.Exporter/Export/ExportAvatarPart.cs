using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Hair / face doll parts: one img per cosmetic, one file per body action.
// The body and default look only feed the bone tree; only the target img's
// parts are written (the client appends them to the plain body bake).
public static partial class Export
{
    private static readonly int[] HairCosmetics = ExportList.Load("hairs");
    private static readonly int[] FaceCosmetics = ExportList.Load("faces");

    private static bool ExportAvatarParts(WzRoot baseRoot, WzRoot looseRoot, string outDir)
    {
        var zmap = baseRoot.Find("zmap.img");
        if (zmap == null || zmap.Nodes.Count == 0)
        {
            Console.WriteLine(@"avatar parts: Base\zmap.img missing");
            return false;
        }
        var zNames = zmap.Nodes.Select(z => z.Text).ToList();
        var saved = new HashSet<string>();
        foreach (int id in HairCosmetics)
            foreach (string action in Net.Actions)
                if (!ExportAvatarPart(baseRoot, looseRoot, outDir, action, "hair", id, zNames, saved))
                    return false;
        foreach (int id in FaceCosmetics)
            foreach (string action in Net.Actions)
                if (!ExportAvatarPart(baseRoot, looseRoot, outDir, action, "face", id, zNames, saved))
                    return false;
        Console.WriteLine($"avatar parts: hair {HairCosmetics.Length}, face {FaceCosmetics.Length}");
        return true;
    }

    // action frames of an img: numbered children 0,1,2..., stop at the first gap
    private static int ActionFrameCount(WzRoot looseRoot, string img, string action)
    {
        var actionNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{img}\{action}"));
        if (actionNode == null) return 0;
        int n = 0;
        while (actionNode.Nodes[n.ToString()] != null) n++;
        return n;
    }

    private static bool ExportAvatarPart(WzRoot baseRoot, WzRoot looseRoot, string outDir, string action,
        string category, int cosmetic, List<string> zNames, HashSet<string> saved)
    {
        string id = cosmetic.ToString("D8");
        string img = category == "hair" ? $@"Hair\{id}.img" : $@"Face\{id}.img";
        string tag = $"{category} {id} {action}";
        var targetNode = looseRoot.Find(img);
        if (targetNode == null)
        {
            Console.WriteLine($"{tag}: {img} not found");
            return false;
        }
        int bodyFrames = ActionFrameCount(looseRoot, BodyImgMale, action);
        if (bodyFrames == 0)
        {
            Console.WriteLine($"{tag}: body has no {action} frames");
            return false;
        }
        if (category == "hair")
        {
            int partFrames = ActionFrameCount(looseRoot, img, action);
            if (partFrames != bodyFrames)
            {
                Console.WriteLine($"{tag}: {partFrames} frames, body has {bodyFrames}");
                return false;
            }
        }

        // the default look of the male body feeds the bones: same bone tree on both bodies
        var faceNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{FaceImgMale}\default"));
        if (faceNode == null)
        {
            Console.WriteLine($"{tag}: {FaceImgMale} node not found");
            return false;
        }
        Wz_Node? targetFaceNode = null;
        if (category == "face")
        {
            targetFaceNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{img}\default"));
            if (targetFaceNode == null)
            {
                Console.WriteLine($"{tag}: {img} default node not found");
                return false;
            }
        }

        var data = new CharaData();
        int missing = 0;
        string targetDir = $"chara/{category}/{id}";

        for (int frameIdx = 0; frameIdx < bodyFrames; frameIdx++)
        {
            var bodyFrame = FindImgFrame(looseRoot, BodyImgMale, action, frameIdx);
            if (bodyFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {BodyImgMale} frame uol is broken");
                return false;
            }
            if (!BodyFrameStatic(BodyImgMale, action, frameIdx, bodyFrame)) return false;
            var headFrame = FindImgFrame(looseRoot, HeadImgMale, action, frameIdx);
            if (headFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {HeadImgMale} node not found");
                return false;
            }
            var hairFrame = FindImgFrame(looseRoot, HairImgMale, action, frameIdx);
            if (hairFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {HairImgMale} node not found");
                return false;
            }

            var outFrame = new CharaFrame { Delay = Int(bodyFrame, "delay", 120) };
            var bones = new BoneSet();
            bool face = Int(bodyFrame, "face", 1) != 0;
            var trash = new CharaFrame();
            missing += AddFrameParts(baseRoot, trash, bones, ImgId(BodyImgMale), bodyFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            missing += AddFrameParts(baseRoot, trash, bones, ImgId(HeadImgMale), headFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            if (face)
                missing += AddFrameParts(baseRoot, trash, bones, ImgId(FaceImgMale), faceNode, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            missing += AddFrameParts(baseRoot, trash, bones, ImgId(HairImgMale), hairFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");

            // the target parts: a hair uses its action frame, a face its one default node
            if (category == "hair")
            {
                var targetFrame = FindImgFrame(looseRoot, img, action, frameIdx);
                if (targetFrame == null)
                {
                    Console.WriteLine($"{tag} frame {frameIdx}: target frame missing");
                    return false;
                }
                missing += AddFrameParts(baseRoot, outFrame, bones, id, targetFrame, outDir, frameIdx, saved, zNames, face, action, targetDir);
            }
            else if (face)
            {
                missing += AddFrameParts(baseRoot, outFrame, bones, id, targetFaceNode!, outDir, frameIdx, saved, zNames, face, action, targetDir);
            }

            if ((category == "hair" || face) && outFrame.Parts.Count == 0)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: no parts exported");
                return false;
            }
            outFrame.Parts = outFrame.Parts.OrderByDescending(p => p.Z).ToList();
            data.Frames.Add(outFrame);
        }

        Console.WriteLine($"{tag}: {data.Frames.Count} frames, parts [{string.Join(",", data.Frames.Select(f => f.Parts.Count))}]");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {tag} {missing} avatar part(s)");
            return false;
        }
        WriteJson(Path.Combine(outDir, targetDir, $"{action}.json"), data);
        return true;
    }
}
