using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Item exports: potion defs and wearable defs + icons.
public static partial class Export
{
    private static readonly int[] PotionIds = ExportList.Load("potions");

    // The gear we ship: the img path + the beginner doll part it replaces
    // (null = the gear has no doll art, the rings carry none). One list:
    // the item def and the doll bake both come from here.
    private static readonly (string gear, string? beginner)[] Gears = new[]
    {
        (@"Coat\01040039.img", BeginnerLongcoat),
        (@"Weapon\01442004.img", BeginnerSword),
        (@"Ring\01112134.img", null),
        (@"Ring\01112135.img", null),
        (@"Ring\01112162.img", null),
        // The gear of the two shops (Sid 11000, Pan 21000).
        (@"Weapon\01302007.img", BeginnerSword),
        (@"Weapon\01302005.img", BeginnerSword),
        (@"Weapon\01302004.img", BeginnerSword),
        (@"Weapon\01302015.img", BeginnerSword),
        (@"Coat\01040015.img", BeginnerLongcoat),
        (@"Coat\01040007.img", BeginnerLongcoat),
        (@"Coat\01040012.img", BeginnerLongcoat),
        (@"Coat\01040022.img", BeginnerLongcoat),
    };

    // The wearable ids: the export log and the manifest both print them.
    private static IEnumerable<int> GearIds => Gears.Select(g => int.Parse(ImgId(g.gear)));

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
    // ReadEquips checks every row before the doll bake (a bad pairing must
    // not bake wrong art); WriteEquips writes the defs after it (a failed
    // bake must not leave a def whose art is missing).
    private static List<(string Gear, ItemData Data)>? ReadEquips(WzRoot chara, WzRoot str)
    {
        var defs = new List<(string, ItemData)>();
        foreach ((string gear, string? beginner) in Gears)
        {
            int id = int.Parse(ImgId(gear));
            var info = chara.Find($@"{gear}\info");
            if (info == null)
            {
                Console.WriteLine($"equip {id} has no info");
                return null;
            }
            foreach (Wz_Node c in info.Nodes)
            {
                if (c.Text.StartsWith("inc") && c.Text is not ("incPAD" or "incPDD" or "incSTR" or "incDEX"))
                {
                    Console.WriteLine($"equip {id} has unsupported stat {c.Text}");
                    return null;
                }
            }
            string cat = gear.Split('\\')[0]; // Coat/Weapon/Ring: the Eqp.img shelf matches the Character folder
            string name = str.Find($@"Eqp.img\Eqp\{cat}\{id}\name")?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"equip {id} has no String name");
                return null;
            }
            // The doll art only fits the slots the doll has: the game boots
            // on the same slot (ItemData.Slot), so check the same node. A
            // piece with art must replace the part its slot names; a ring
            // carries no art (its name tag plate does the drawing).
            string slot = Str(info, "islot");
            if (slot == "")
            {
                Console.WriteLine($"equip {id} has no slot");
                return null;
            }
            int tag = Int(info, "nameTag");
            if (beginner == null)
            {
                if (slot != "Ri")
                {
                    Console.WriteLine($"equip {id} has no art (slot {slot} needs one)");
                    return null;
                }
                if (!NameTagNodes.Contains(tag)) // the boot loads the plate art by this node
                {
                    Console.WriteLine($"equip {id} is a ring with no exported name tag ({tag})");
                    return null;
                }
            }
            else if (!ItemData.BeginnerImgs.TryGetValue(slot, out string? part) || part != ImgId(beginner))
            {
                Console.WriteLine($"equip {id} pairs {ImgId(beginner)} with slot {slot}, " +
                    $"which replaces {ItemData.BeginnerImgs.GetValueOrDefault(slot, "nothing")}");
                return null;
            }
            var data = new ItemData
            {
                ItemId = id,
                Name = name,
                Image = $"item/{id}.png",
                Slot = slot,
                Atk = Int(info, "incPAD"),
                Def = Int(info, "incPDD"),
                Str = Int(info, "incSTR"),
                Dex = Int(info, "incDEX"),
                Price = Int(info, "price"),
                NameTag = tag,
                HasArt = beginner != null,
            };
            if (data.Price <= 0 && Int(info, "cash") == 0) // cash gear carries no meso price
            {
                Console.WriteLine($"equip {id} has a bad price {data.Price}");
                return null;
            }
            defs.Add((gear, data));
        }
        return defs;
    }

    private static bool WriteEquips(List<(string Gear, ItemData Data)> defs, WzRoot chara, string outDir)
    {
        foreach (var (gear, data) in defs)
        {
            if (!UiPng.Save(chara, "Character", $@"{gear}\info/icon", Path.Combine(outDir, data.Image)))
                return false;
            WriteJson(Path.Combine(outDir, "item", $"{data.ItemId}.json"), data);
        }
        Console.WriteLine($"equips: {string.Join(" ", GearIds)}");
        return true;
    }
}
