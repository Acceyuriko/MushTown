using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The collected damage skins (lists/damageskins.json): the digits and words
// of Etc/DamageSkin.img/<id>, and the coupon that gives each skin. Every
// picture goes through UiPng, which follows _outlink: many nodes borrow
// another skin's picture (1426 uses skin 0 digits and 208's sample), so no
// path is built from the skin id. The base skin (dmg.json) is not touched.
public static partial class Export
{
    private static readonly DamageSkinRow[] DamageSkinList = ExportList.LoadDamageSkins();

    // The fixed price of a damage skin coupon (user's choice, 2026-11-15).
    private const int DamageSkinPrice = 5000;

    private static readonly string[] DamageSkinWords = { "Miss", "guard", "resist", "shot", "counter" };

    private static bool ExportDamageSkins(WzRoot etc, WzRoot item, WzRoot str, string outDir)
    {
        foreach (var row in DamageSkinList)
        {
            if (!ExportCollectedSkin(etc, row.Skin, outDir) || !ExportDamageCoupon(item, str, row, outDir))
                return false;
        }
        Console.WriteLine($"damage skins: {string.Join(" ", DamageSkinList.Select(r => r.Skin))}");
        return true;
    }

    private static bool ExportCollectedSkin(WzRoot etc, int skin, string outDir)
    {
        string root = $"DamageSkin.img/{skin}";
        string dir = $"effect/dmg{skin}";
        var data = new DmgData();
        // NoRed0 keeps the plain file names (the base skin does the same).
        if (!ReadDigits(etc, $"{root}/effect/NoRed0", dir, "", data.Digits, outDir)
            || !ReadDigits(etc, $"{root}/effect/NoRed1", dir, "red1_", data.Red1, outDir)
            || !ReadDigits(etc, $"{root}/effect/NoCri0", dir, "cri0_", data.Cri0, outDir)
            || !ReadDigits(etc, $"{root}/effect/NoCri1", dir, "cri1_", data.Cri1, outDir))
            return false;
        foreach (string word in DamageSkinWords)
        {
            if (etc.Find($"{root}/effect/NoRed0/{word}") == null) continue; // a skin may lack shot/counter
            var picture = ReadPicture(etc, $"{root}/effect/NoRed0/{word}", $"{dir}/word_{word}.png", outDir);
            if (picture == null) return false;
            data.Words.Add(new DmgWord { Name = word, Image = picture.Image, Ox = picture.Ox, Oy = picture.Oy });
        }
        data.Effect3 = ReadPicture(etc, $"{root}/effect/NoCri1/effect3", $"{dir}/effect3.png", outDir);
        data.Sample = ReadPicture(etc, $"{root}/sample", $"{dir}/sample.png", outDir);
        if (data.Effect3 == null || data.Sample == null) return false;
        data.NumberSpace = Int(etc.Find($"{root}/effect/NoRed0"), "numberSpace");
        var custom = etc.Find($"{root}/effect/NoCustom/NoRed0");
        if (custom != null)
        {
            var unit = new DmgUnit { DotSpace = Int(custom, "dotSpace"), NumberSpace = Int(custom, "numberSpace") };
            for (int u = 0; u <= 3; u++)
            {
                var picture = ReadPicture(etc, $"{root}/effect/NoCustom/NoRed0/{u}", $"{dir}/unit_{u}.png", outDir);
                if (picture == null) return false;
                unit.Images.Add(picture);
            }
            data.Unit = unit;
        }
        WriteJson(Path.Combine(outDir, "effect", $"dmg{skin}.json"), data);
        Console.WriteLine($"damage skin {skin}: {data.Words.Count} words{(data.Unit != null ? ", unit" : "")}");
        return true;
    }

    // Digits 0-9 of one WZ set; a missing digit fails the export.
    private static bool ReadDigits(WzRoot etc, string setNode, string dir, string prefix, List<DmgDigit> into, string outDir)
    {
        for (int d = 0; d <= 9; d++)
        {
            var picture = ReadPicture(etc, $"{setNode}/{d}", $"{dir}/{prefix}{d}.png", outDir);
            if (picture == null) return false;
            into.Add(picture);
        }
        return true;
    }

    // One picture: saved under outDir/path, the WZ origin kept (0 when the
    // node has none, like some samples).
    private static DmgDigit? ReadPicture(WzRoot etc, string node, string path, string outDir)
    {
        if (!UiPng.TryRead(etc, "Etc", node, out _, out var origin))
            return null;
        if (!UiPng.Save(etc, "Etc", node, Path.Combine(outDir, path)))
            return null;
        return new DmgDigit { Image = path, Ox = origin?.X ?? 0, Oy = origin?.Y ?? 0 };
    }

    // The coupon: a USE item (Consume/<NNNN>.img), not a cash item. The
    // whitelist names the skin; a WZ damageSkinID that disagrees fails.
    private static bool ExportDamageCoupon(WzRoot item, WzRoot str, DamageSkinRow row, string outDir)
    {
        string key = row.Item.ToString("D8");
        string img = $"Consume/{row.Item / 10000:D4}.img/{key}";
        var node = item.Find(img);
        var info = node?.Nodes["info"];
        if (info == null)
        {
            Console.WriteLine($"damage coupon {row.Item} not found at {img}");
            return false;
        }
        int wzSkin = Int(info, "damageSkinID", row.Skin);
        if (wzSkin != row.Skin)
        {
            Console.WriteLine($"damage coupon {row.Item} says skin {wzSkin}, the list says {row.Skin}");
            return false;
        }
        string name = str.Find($@"Consume.img\{row.Item}\name")?.GetValueEx<string>("") ?? "";
        if (name == "")
        {
            Console.WriteLine($"damage coupon {row.Item} has no String name");
            return false;
        }
        var data = new ItemData
        {
            ItemId = row.Item,
            Name = name,
            Image = $"item/{row.Item}.png",
            Desc = CleanDesc(str.Find($@"Consume.img\{row.Item}\desc")?.GetValueEx<string>("") ?? "", "").Replace("\\r", "").Trim(),
            Price = DamageSkinPrice,
            DamageSkin = row.Skin,
            IconW = UiPng.Width(item, "Item", $"{img}/info/icon"),
        };
        if (data.IconW <= 0)
        {
            Console.WriteLine($"damage coupon {row.Item} has no icon width");
            return false;
        }
        if (!UiPng.Save(item, "Item", $"{img}/info/icon", Path.Combine(outDir, data.Image)))
            return false;
        WriteJson(Path.Combine(outDir, "item", $"{row.Item}.json"), data);
        return true;
    }
}
