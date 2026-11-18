using System.Globalization;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The default passives that are real skills (Shared DefaultPassiveSkills): the
// name and text come from String.wz/Skill.img/<id>, the numbers from the skill
// node (common, or level/<max> when there is no common) at the highest level,
// the icon from the _outlink of the skill's icon node. Writes
// assets/skill/passive/passives.json and one png per icon.
public static partial class Export
{
    // baseRoot must carry the Packs (the skill nodes live there), skill is the
    // Skill category (the icon pngs live there), str the String category, item the Item category (the icon of
    // a passive that borrows an item icon).
    public static bool ExportPassiveSkills(WzRoot baseRoot, WzRoot skill, WzRoot str, WzRoot item, string outDir)
    {
        var data = new DefaultPassiveSkillData();
        foreach (var p in DefaultPassiveSkills.All)
        {
            var row = new DefaultPassiveSkillRow { Label = p.Label, MaxLevel = 1 };
            string desc = p.Desc ?? "", effect = p.Effect ?? "";
            var values = new Dictionary<string, string>();
            if (p.WzId != 0)
            {
                string id = p.WzId.ToString("D7");
                string img = (p.WzId >= 80000000 ? p.WzId / 100 : p.WzId / 10000).ToString("D3") + ".img";
                var node = baseRoot.Find($"Skill/{img}/skill/{id}");
                var strNode = str.Find($"Skill.img/{id}");
                if (node == null || strNode == null)
                {
                    Console.WriteLine($"passive skill {id} not found ({img})");
                    return false;
                }
                row.Name = Text(strNode.Nodes["name"]);
                if (p.Desc == null) desc = Text(strNode.Nodes["desc"]);
                if (p.Effect == null) effect = Text(strNode.Nodes["h"]);
                var common = node.Nodes["common"];
                if (common != null)
                {
                    foreach (Wz_Node v in common.Nodes) values[v.Text] = Text(v);
                    if (values.TryGetValue("maxLevel", out string? max)) row.MaxLevel = int.Parse(max, CultureInfo.InvariantCulture);
                }
                else
                {
                    // The old skills keep one node per level: the highest level is the only one.
                    var level = node.Nodes["level"]!.Nodes.Last();
                    foreach (Wz_Node v in level.Nodes) values[v.Text] = Text(v);
                    row.MaxLevel = int.Parse(level.Text, CultureInfo.InvariantCulture);
                }
            }
            else row.Name = p.Label.Split(" (")[0]; // Heroic: the label without its bracket
            row.Desc = Lines(Fill(desc, values, row.MaxLevel));
            row.Effect = Lines(Fill(effect, values, row.MaxLevel));

            int iconId = p.IconId != 0 ? p.IconId : p.WzId;
            row.Icon = p.ItemIconId != 0 ? $"skill/passive/item{p.ItemIconId}.png" : $"skill/passive/{iconId}.png";
            if (!(p.ItemIconId != 0
                ? UiPng.Save(item, "Item", $"Consume/{p.ItemIconId / 10000:D4}.img/{p.ItemIconId:D8}/info/icon", Path.Combine(outDir, row.Icon))
                : SavePassiveIcon(baseRoot, skill, iconId, Path.Combine(outDir, row.Icon)))) return false;
            data.Rows.Add(row);
            Console.WriteLine($"passive {row.Name}: Lv.{row.MaxLevel}  {string.Join(" / ", row.Effect)}");
        }
        WriteJson(Path.Combine(outDir, "skill", "passive", "passives.json"), data);
        return true;
    }

    private static string Text(Wz_Node? node) => Convert.ToString(node?.Value, CultureInfo.InvariantCulture) ?? "";

    // The icon node is a 1x1 stub with an _outlink to the real canvas in the Skill category.
    private static bool SavePassiveIcon(WzRoot baseRoot, WzRoot skill, int id, string path, string node = "icon")
    {
        string id7 = id.ToString("D7");
        string img = (id >= 80000000 ? id / 100 : id / 10000).ToString("D3") + ".img";
        string? link = baseRoot.Find($"Skill/{img}/skill/{id7}/{node}")?.Nodes["_outlink"]?.Value as string;
        if (link == null || !link.StartsWith("Skill/"))
        {
            Console.WriteLine($"passive skill {id7}: no icon link");
            return false;
        }
        return UiPng.Save(skill, "Skill", link["Skill/".Length..], path);
    }

    // The WZ text has the two characters backslash-n as a line break. Each #name is
    // the number of that name at the highest level: a name with no number fails
    // loud (the text must never show a raw mark). #c ... # is a color span: the tooltip
    // keeps its text and drops the marks (a name is tried first: #cooltime is a number).
    private static string Fill(string text, Dictionary<string, string> values, int level)
    {
        text = text.Replace(@"\r\n", "\n").Replace(@"\n", "\n");
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '#') { sb.Append(text[i]); continue; }
            int end = i + 1;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            string name = text[(i + 1)..end];
            if (values.TryGetValue(name, out string? formula))
            {
                sb.Append(SkillFormula.Eval(formula, level).ToString("0.##", CultureInfo.InvariantCulture));
                i = end - 1;
            }
            else if (name.StartsWith('c') && text.IndexOf('#', i + 2) is var close and > 0)
            {
                sb.Append(text, i + 2, close - i - 2);
                i = close;
            }
            else throw new InvalidDataException($"skill text has no number for #{name}: {text}");
        }
        return sb.ToString();
    }

    private static List<string> Lines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l != "").ToList();
}
