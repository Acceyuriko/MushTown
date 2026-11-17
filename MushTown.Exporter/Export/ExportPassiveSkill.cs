using System.Globalization;
using System.Text.RegularExpressions;
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
    // Skill category (the icon pngs live there), str the String category.
    public static bool ExportPassiveSkills(WzRoot baseRoot, WzRoot skill, WzRoot str, string outDir)
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
            else row.Name = p.Label.Split(' ')[0]; // Heroic: the first word of the label
            row.Desc = Lines(Fill(desc, values, row.MaxLevel));
            row.Effect = Lines(Fill(effect, values, row.MaxLevel));

            int iconId = p.IconId != 0 ? p.IconId : p.WzId;
            row.Icon = $"skill/passive/{iconId}.png";
            if (!SavePassiveIcon(baseRoot, skill, iconId, Path.Combine(outDir, row.Icon))) return false;
            data.Rows.Add(row);
            Console.WriteLine($"passive {row.Name}: Lv.{row.MaxLevel}  {string.Join(" / ", row.Effect)}");
        }
        WriteJson(Path.Combine(outDir, "skill", "passive", "passives.json"), data);
        return true;
    }

    private static string Text(Wz_Node? node) => Convert.ToString(node?.Value, CultureInfo.InvariantCulture) ?? "";

    // The icon node is a 1x1 stub with an _outlink to the real canvas in the Skill category.
    private static bool SavePassiveIcon(WzRoot baseRoot, WzRoot skill, int id, string path)
    {
        string id7 = id.ToString("D7");
        string img = (id >= 80000000 ? id / 100 : id / 10000).ToString("D3") + ".img";
        string? link = baseRoot.Find($"Skill/{img}/skill/{id7}/icon")?.Nodes["_outlink"]?.Value as string;
        if (link == null || !link.StartsWith("Skill/"))
        {
            Console.WriteLine($"passive skill {id7}: no icon link");
            return false;
        }
        return UiPng.Save(skill, "Skill", link["Skill/".Length..], path);
    }

    // The WZ text has the two characters backslash-n as a line break. Each #name is
    // the number of that name at the highest level: a name with no number fails
    // loud (the text must never show a raw mark).
    private static string Fill(string text, Dictionary<string, string> values, int level)
    {
        text = text.Replace(@"\r\n", "\n").Replace(@"\n", "\n");
        return Regex.Replace(text, @"#(\w+)", m =>
        {
            if (!values.TryGetValue(m.Groups[1].Value, out string? formula))
                throw new InvalidDataException($"passive text has no number for #{m.Groups[1].Value}: {text}");
            return new Formula(formula, level).Value().ToString("0.##", CultureInfo.InvariantCulture);
        });
    }

    private static List<string> Lines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l != "").ToList();

    // The WZ number formulas of these skills: + * /, x = the level, u() rounds up,
    // d() rounds down, logN() is the logarithm to base N. A plain number is a formula too.
    private sealed class Formula
    {
        private readonly string _s;
        private readonly double _level;
        private int _i;

        public Formula(string s, int level) { _s = s.Replace(" ", ""); _level = level; }

        public double Value()
        {
            double v = Sum();
            if (_i != _s.Length) throw new InvalidDataException($"bad formula: {_s}");
            return v;
        }

        private double Sum()
        {
            double v = Product();
            while (_i < _s.Length && _s[_i] == '+')
            {
                _i++;
                v += Product();
            }
            return v;
        }

        private double Product()
        {
            double v = Unit();
            while (_i < _s.Length && _s[_i] is '*' or '/')
            {
                char op = _s[_i++];
                double r = Unit();
                v = op == '*' ? v * r : v / r;
            }
            return v;
        }

        private double Unit()
        {
            int start = _i;
            if (char.IsDigit(_s[_i]))
            {
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
                return double.Parse(_s[start.._i], CultureInfo.InvariantCulture);
            }
            while (_i < _s.Length && char.IsLetterOrDigit(_s[_i])) _i++;
            string name = _s[start.._i];
            if (name == "x") return _level;
            if (_i >= _s.Length || _s[_i] != '(') throw new InvalidDataException($"bad formula: {_s}");
            _i++;
            double arg = Sum();
            _i++; // the closing bracket
            if (name == "u") return Math.Ceiling(arg);
            if (name == "d") return Math.Floor(arg);
            if (name.StartsWith("log")) return Math.Log(arg) / Math.Log(double.Parse(name[3..], CultureInfo.InvariantCulture));
            throw new InvalidDataException($"bad formula: {_s}");
        }
    }
}
