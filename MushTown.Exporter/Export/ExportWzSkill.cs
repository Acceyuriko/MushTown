using System.Globalization;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The Fire/Poison skills, read straight from the WZ skill nodes (Shared WzSkillData): every common
// formula worked out for every level (SkillFormula), the lt/rb rectangles, info and action, and the
// tooltip text of String.wz with the max-level numbers filled in. Writes assets/skill/wz/<id>.json
// plus the icon and the disabled icon. Only the 1st job (200.img) is exported for now: the formula
// reader is general, the other jobs are added with their own tickets.
public static partial class Export
{
    // The skill ids to export: the ones on the job pages, plus the skills the window does not list
    // (Elemental Weakness is invisible in WZ).
    private static readonly int[] WzSkillIds = JobSkills.Pages(StatTables.FirePoisonMage).SelectMany(p => p).Append(WzSkillData.ElementalWeakness).ToArray();

    // A skill node that another skill owns (WZ has no link between them): parent -> child.
    private static readonly Dictionary<int, int> WzSkillChildren = new() { [WzSkillData.ManaWave] = 2001012 }; // Mana Wave -> its held fall

    // baseRoot must carry the Packs (the skill nodes live there), skill is the Skill category (the icon pngs),
    // str the String category.
    public static bool ExportWzSkills(WzRoot baseRoot, WzRoot skill, WzRoot str, string outDir)
    {
        foreach (int id in WzSkillIds)
        {
            var data = ReadWzSkill(baseRoot, id);
            var strNode = str.Find($"Skill.img/{id:D7}");
            if (strNode == null)
            {
                Console.WriteLine($"skill {id} has no String entry");
                return false;
            }
            data.Name = Text(strNode.Nodes["name"]);
            var values = WzSkillValues(baseRoot, id);
            data.Desc = Lines(Fill(Text(strNode.Nodes["desc"]), values, data.MaxLevel));
            data.Effect = Lines(Fill(Text(strNode.Nodes["h"]), values, data.MaxLevel));
            data.Icon = $"skill/wz/{id}/icon.png";
            data.Disabled = $"skill/wz/{id}/dis.png";
            if (!SavePassiveIcon(baseRoot, skill, id, Path.Combine(outDir, data.Icon))
                || !SavePassiveIcon(baseRoot, skill, id, Path.Combine(outDir, data.Disabled), "iconDisabled"))
                return false;
            if (WzSkillChildren.TryGetValue(id, out int child)) data.Children.Add(ReadWzSkill(baseRoot, child));
            WriteJson(Path.Combine(outDir, "skill", "wz", $"{id}.json"), data);
            string kind = data.Kind == SkillData.KindPassive ? "passive" : data.Kind == SkillData.KindBuff ? "buff" : "active";
            Console.WriteLine($"wz skill {id} {data.Name}: {kind}, max {data.MaxLevel}, common {string.Join(",", data.Common.Keys)}"
                + $"{(data.Vectors.Count > 0 ? ", vec " + string.Join(",", data.Vectors.Keys) : "")}"
                + $"{(data.Children.Count > 0 ? ", child " + string.Join(",", data.Children.Select(c => c.SkillId)) : "")}");
            Console.WriteLine($"    {string.Join(" / ", data.Effect)}");
        }
        return true;
    }

    private static Wz_Node WzSkillNode(WzRoot baseRoot, int id) =>
        baseRoot.Find($"Skill/{id / 10000:D3}.img/skill/{id:D7}") ?? throw new InvalidDataException($"skill {id} not found");

    // The raw formula text of every number field of common (the String text takes its #names from here).
    private static Dictionary<string, string> WzSkillValues(WzRoot baseRoot, int id) =>
        WzSkillNode(baseRoot, id).Nodes["common"]!.Nodes.Where(v => v.Value is not Wz_Vector).ToDictionary(v => v.Text, Text);

    // One node as the table: no name, icon or text (the caller adds them to a skill of the window).
    private static WzSkillData ReadWzSkill(WzRoot baseRoot, int id)
    {
        var node = WzSkillNode(baseRoot, id);
        var common = node.Nodes["common"]!;
        var data = new WzSkillData { SkillId = id, MaxLevel = int.Parse(Text(common.Nodes["maxLevel"]), CultureInfo.InvariantCulture) };
        foreach (Wz_Node v in common.Nodes)
        {
            if (v.Text == "maxLevel") continue;
            if (v.Value is Wz_Vector vec) data.Vectors[v.Text] = new[] { vec.X, vec.Y };
            else data.Common[v.Text] = SkillFormula.Levels(Text(v), data.MaxLevel);
        }
        if (node.Nodes["psdWT"] is { } byWeapon)
            foreach (Wz_Node weapon in byWeapon.Nodes)
                data.WeaponBonus[int.Parse(weapon.Text, CultureInfo.InvariantCulture)] =
                    weapon.Nodes.ToDictionary(v => v.Text, v => SkillFormula.Levels(Text(v), data.MaxLevel));
        foreach (Wz_Node v in node.Nodes["info"]!.Nodes) data.Info[v.Text] = Text(v);
        if (node.Nodes["action"] is { } action)
            foreach (Wz_Node v in action.Nodes) data.Actions.Add(Text(v));
        data.ElemAttr = Text(node.Nodes["elemAttr"]);
        data.Invisible = node.Nodes["invisible"] != null;
        // No mpCon: always on (passive). info.type 10: a timed self effect (buff). Else cast for an effect.
        data.Kind = !data.Common.ContainsKey("mpCon") ? SkillData.KindPassive
            : data.Info.GetValueOrDefault("type") == "10" ? SkillData.KindBuff
            : SkillData.KindActive;
        return data;
    }
}
