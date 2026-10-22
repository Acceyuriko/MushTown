using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Item exports: potion defs and wearable defs + icons.
public static partial class Export
{
    private static readonly int[] PotionIds = ExportList.Load("potions");

    // Def + icon per wearable: art-less rings ride along (no doll pixels).
    private static readonly string[] EquipGearPaths = new[]
    {
        @"Coat\01040039.img",
        @"Weapon\01442004.img",
        @"Ring\01112134.img",
        @"Ring\01112135.img",
        @"Ring\01112162.img",
    };

    // The potions of lists/potions.json. Heals come from the WZ spec node;
    // icons are the info/icon canvases (_outlink to _Canvas).
    private static bool ExportPotions(WzRoot item, WzRoot str, string outDir)
    {
        foreach (int id in PotionIds)
        {
            string key = id.ToString("D8"); // 02000000
            var node = item.Find($"Consume/0200.img/{key}");
            if (node == null)
            {
                Console.WriteLine($"potion {id} not found");
                return false;
            }
            var spec = node.Nodes["spec"];
            if (spec == null)
            {
                Console.WriteLine($"potion {id} has no spec");
                return false;
            }
            var pinfo = node.Nodes["info"];
            if (pinfo == null)
            {
                Console.WriteLine($"potion {id} has no info");
                return false;
            }
            string name = str.Find($@"Consume.img\{id}\name")?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"potion {id} has no String name");
                return false;
            }
            var data = new ItemData
            {
                ItemId = id,
                Name = name,
                Image = $"item/{id}.png",
                Hp = Int(spec, "hp"),
                Mp = Int(spec, "mp"),
                Price = Int(pinfo, "price"),
            };
            if (data.Price <= 0)
            {
                Console.WriteLine($"potion {id} has a bad price {data.Price}");
                return false;
            }
            // A potion that restores no flat hp and no flat mp is not a
            // potion for us: the official elixirs heal by percent (hpR/mpR),
            // which v1 does not do.
            if (data.Hp <= 0 && data.Mp <= 0)
            {
                Console.WriteLine($"potion {id} restores no flat hp and no flat mp " +
                    $"(take it out of MushTown.Exporter/Export/lists/potions.json)");
                return false;
            }
            if (!UiPng.Save(item, "Item", $"Consume/0200.img/{key}/info/icon", Path.Combine(outDir, data.Image)))
                return false;
            WriteJson(Path.Combine(outDir, "item", $"{id}.json"), data);
        }
        Console.WriteLine($"potions: {string.Join(" ", PotionIds)}");
        return true;
    }

    // One def + icon per wearable gear: Slot and bonuses come from the img's
    // info node. v1 ships only all-supported stats: any other inc* fails loud
    // (a half-working item must never sneak in); reqs/pose/sfx/speed/tuc/
    // cash sit unused by design (ItemData documents the v1 gear rules).
    private static bool ExportEquips(WzRoot chara, WzRoot str, string outDir)
    {
        var shipped = new List<int>();
        foreach (string gear in EquipGearPaths)
        {
            int id = int.Parse(ImgId(gear));
            shipped.Add(id);
            var info = chara.Find($@"{gear}\info");
            if (info == null)
            {
                Console.WriteLine($"equip {id} has no info");
                return false;
            }
            foreach (Wz_Node c in info.Nodes)
            {
                if (c.Text.StartsWith("inc") && c.Text is not ("incPAD" or "incPDD" or "incSTR" or "incDEX"))
                {
                    Console.WriteLine($"equip {id} has unsupported stat {c.Text}");
                    return false;
                }
            }
            string cat = gear.Split('\\')[0]; // Coat/Weapon/Ring: the Eqp.img shelf matches the Character folder
            string name = str.Find($@"Eqp.img\Eqp\{cat}\{id}\name")?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"equip {id} has no String name");
                return false;
            }
            var data = new ItemData
            {
                ItemId = id,
                Name = name,
                Image = $"item/{id}.png",
                Slot = Str(info, "islot"),
                Atk = Int(info, "incPAD"),
                Def = Int(info, "incPDD"),
                Str = Int(info, "incSTR"),
                Dex = Int(info, "incDEX"),
                Price = Int(info, "price"),
                NameTag = Int(info, "nameTag"),
                HasArt = CharaGears.Any(g => ImgId(g.gear) == id.ToString("D8")),
            };
            if (data.Slot == "")
            {
                Console.WriteLine($"equip {id} has no slot");
                return false;
            }
            if (data.Price <= 0 && Int(info, "cash") == 0) // cash gear carries no meso price
            {
                Console.WriteLine($"equip {id} has a bad price {data.Price}");
                return false;
            }
            if (!UiPng.Save(chara, "Character", $@"{gear}\info/icon", Path.Combine(outDir, data.Image)))
                return false;
            WriteJson(Path.Combine(outDir, "item", $"{id}.json"), data);
        }
        Console.WriteLine($"equips: {string.Join(" ", shipped)}");
        return true;
    }
}
