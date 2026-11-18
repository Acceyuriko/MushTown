using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Equipment sets: Etc.wz SetItemInfo.img/<id> of every set an exported gear
// belongs to (info/setItemID), written to assets/set/<id>.json. The effect
// lines are only displayed; the effect fields stay the WZ names.
public static partial class Export
{
    // The sets the tooltip supports. Another set has other effect fields and
    // Option nodes (the Frost Fiend and Challenger sets), so its pieces keep
    // SetId 0 and show no set pane until it is added here and in SetRules.
    private static readonly int[] SetIds = { 448 }; // 8th Magician Set: the starter kit

    // The Character.wz folders that hold equipment (the Eqp.img shelf has the same name).
    private static readonly string[] EquipDirs = { "Accessory", "Cap", "Cape", "Coat", "Glove", "Longcoat", "Pants", "Ring", "Shield", "Shoes", "Weapon" };

    private static string? EquipDir(WzRoot chara, int id) =>
        EquipDirs.FirstOrDefault(d => chara.Find($@"{d}\{id:D8}.img") != null);

    private static bool ExportSets(WzRoot etc, WzRoot item, WzRoot str, WzRoot chara, string outDir)
    {
        foreach (int id in SetIds)
        {
            var node = etc.Find($"SetItemInfo.img/{id}");
            if (node == null)
            {
                Console.WriteLine($"set {id} not found in SetItemInfo.img");
                return false;
            }
            var data = new SetData { Id = id, Name = Str(node, "setItemName") };
            if (data.Name == "")
            {
                Console.WriteLine($"set {id} has no name");
                return false;
            }
            // The parts: the ItemID children in key order, grouped by slot.
            var ids = node.Nodes["ItemID"];
            if (ids == null)
            {
                Console.WriteLine($"set {id} has no ItemID");
                return false;
            }
            foreach (var child in ids.Nodes.OrderBy(n => int.Parse(n.Text)))
            {
                int itemId = Convert.ToInt32(child.Value);
                string? dir = EquipDir(chara, itemId);
                if (dir == null)
                {
                    Console.WriteLine($"set {id} item {itemId} is in no Character folder of EquipDirs");
                    return false;
                }
                string slot = Str(chara.Find($@"{dir}\{itemId:D8}.img\info"), "islot");
                if (!ItemCategory.Slots.TryGetValue(slot, out string? label))
                {
                    Console.WriteLine($"set {id} item {itemId} has slot '{slot}', which ItemCategory does not name");
                    return false;
                }
                var part = data.Parts.FirstOrDefault(p => p.Slot == slot);
                if (part == null)
                    data.Parts.Add(part = new SetPart { Slot = slot, Label = label });
                part.Ids.Add(itemId);
            }
            foreach (var part in data.Parts)
            {
                if (part.Ids.Count == 1)
                {
                    int itemId = part.Ids[0];
                    part.Name = str.Find($@"Eqp.img\Eqp\{EquipDir(chara, itemId)}\{itemId}\name")?.GetValueEx<string>("") ?? "";
                }
                else
                {
                    part.Name = part.Slot == "Wp" ? Str(node.Nodes["Desc"], "weapon") : ""; // the weapon part is the only choose-one part: its name is the WZ Desc line
                }
                if (part.Name == "")
                {
                    Console.WriteLine($"set {id} part {part.Slot} has no name");
                    return false;
                }
            }
            if (data.Parts.Count != Int(node, "completeCount"))
            {
                Console.WriteLine($"set {id} has {data.Parts.Count} parts but completeCount {Int(node, "completeCount")}");
                return false;
            }
            // The tiers: Effect/<pieces>. Option children point into ItemOption.img.
            var effect = node.Nodes["Effect"];
            if (effect == null)
            {
                Console.WriteLine($"set {id} has no Effect");
                return false;
            }
            foreach (var tierNode in effect.Nodes.OrderBy(n => int.Parse(n.Text)))
            {
                var tier = new SetTier { Count = int.Parse(tierNode.Text) };
                foreach (var field in tierNode.Nodes)
                {
                    if (field.Text == "Option")
                    {
                        foreach (var opt in field.Nodes)
                            if (!AddSetOption(item, id, tier, Int(opt, "option"), Int(opt, "level")))
                                return false;
                        continue;
                    }
                    tier.Effects.Add(new SetEffect { Key = field.Text, Value = Convert.ToInt32(field.Value) });
                }
                if (tier.Count > data.Parts.Count || tier.Effects.Any(e => !SetRules.IsKnown(e.Key)))
                {
                    Console.WriteLine($"set {id} tier {tier.Count} has a piece count or effect field the tooltip does not know: " +
                        string.Join(" ", tier.Effects.Where(e => !SetRules.IsKnown(e.Key)).Select(e => e.Key)));
                    return false;
                }
                data.Tiers.Add(tier);
            }
            WriteJson(Path.Combine(outDir, "set", $"{id}.json"), data);
            Console.WriteLine($"set {id}: {data.Name}, {data.Parts.Count} parts, tiers {string.Join(" ", data.Tiers.Select(t => t.Count))}");
        }
        return true;
    }

    // One potential option of a set tier: only ignoreTargetDEF is supported
    // (fixedGrade, which sits beside it, is not shown).
    private static bool AddSetOption(WzRoot item, int setId, SetTier tier, int option, int level)
    {
        var lv = item.Find($"ItemOption.img/{option:D6}/level/{level}");
        if (lv == null)
        {
            Console.WriteLine($"set {setId} option {option} level {level} not found in ItemOption.img");
            return false;
        }
        foreach (var f in lv.Nodes)
        {
            if (f.Text == "fixedGrade") continue;
            if (f.Text != "ignoreTargetDEF")
            {
                Console.WriteLine($"set {setId} option {option} has unsupported field {f.Text}");
                return false;
            }
            tier.Effects.Add(new SetEffect { Key = f.Text, Value = Convert.ToInt32(f.Value) });
        }
        return true;
    }
}
