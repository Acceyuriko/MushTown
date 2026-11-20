using System.Globalization;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The Fire/Poison skills, read straight from the WZ skill nodes (Shared WzSkillData): every common
// formula worked out for every level (SkillFormula), the lt/rb rectangles, info and action, and the
// tooltip text of String.wz with the max-level numbers filled in. Writes assets/skill/wz/<id>.json
// plus the icon and the disabled icon. The ids come from the job pages (JobSkills): the 1st job (200.img) and the 2nd job
// actives and passives (210.img: MP Eater, Spell Mastery, High Wisdom, Elemental Drain, Agile Magic, Meditation, Flame Orb, Poison Breath); the formula reader is general, the
// other skills are added with their own tickets.
// A skill with a damage line also gets its cast effect (effect/) and hit spark (hit/) frames, and CastFrames and
// HitFrames in the json; the effect, hit, ball and special art also keep their WZ origin and delay in Art. A skill
// with a ball node (Energy Bolt) also gets the flying bolt (ball/) and BallFrames, and one with a special node the art
// for every mob it hits (special/) and SpecialFrames. A skill with an action also gets AttackDelayMs: the body
// action's attack frame from Character.wz/00002000.img/<action> (the Journey rule, see WzSkillData.AttackDelayFrom).
// Teleport (no damage line) also gets its cast effect: WZ keeps four colors of it in effect/0..3 (0 blue, 1 orange,
// 2 violet, 3 yellow; which job wears which is unknown), the Fire/Poison one uses 0. It has no hit node and no
// action node in this WZ, so it has no HitFrames and an empty Actions.
// Mana Wave (no damage line, no hit node, an effect of 8 pictures) also gets its cast effect (effect/): the WZ effect list
// starts with a blank 1x1 frame of 120 ms (no origin, no picture in the canvas); it is left out, so the effect starts at once
// (CastFrames, Art.Origins and Art.Delays keep the same count). Its child 2001012 (the slow fall) gets the art of its special and specialAffected (SaveWzFloatArt).
// The 2nd job actives (210.img): Flame Orb is a damage skill with no ball and no special: its effect (14 pictures, origins
// walking from 172 to 583 along x) is the orb and its blast, and hit/0 is the spark on each mob. Meditation is a buff in WZ (info type 10),
// but the user made it a passive: it is exported as a passive (no effect and no affected art), and its Effect line is "Magic ATT: +<indieMad>".
// Poison Breath (210.img) is a damage skill with no ball and no special: its effect is the breath at the caster, hit/0 the poison on each mob; its
// Common also keeps dot, dotInterval and dotTime (the damage-over-time numbers), and there is no ball, so its box is centred on the target mob.
// The 3rd job passives (211.img: Burning Magic, Element Amplification, Magic Critical, Elemental Decrease, Teleport Boost) have no art but their
// icons: the generic path above (formulas, text, icon and disabled icon) is all they need.
// The 3rd job actives (211.img): Explosion is a damage skill with no ball and no special (the effect is the blast at the caster, hit/0 the spark on each
// mob). Elemental Adaptation is a buff (info type 10, mpCon, no damage): it has two cast layers, effect and effect0 (both are saved; effect0 goes to
// Art["effect0"] with its png paths), no affected loop and no hit, and the number icons (number/icon1..N, the layer count drawn in) for the buff bar.
// Its special/special0 (the barrier flash when a status is blocked) are not exported: a block shows no effect.
// After pulling a change to this export run `export` again (dotnet run --project MushTown.Exporter -- export): an old
// json with no frames makes the client fail at start (Menu.cs), and a missing png makes it fail too.
public static partial class Export
{
    // The skill ids to export: the ones on the job pages, plus the skills the window does not list
    // (Elemental Weakness is invisible in WZ).
    private static readonly int[] WzSkillIds = JobSkills.Pages(StatTables.FirePoisonMage).SelectMany(p => p)
        .Append(WzSkillData.ElementalWeakness).Append(WzSkillData.EmpiricalKnowledge).ToArray();

    // A skill node that another skill owns (WZ has no link between them): parent -> child.
    private static readonly Dictionary<int, int> WzSkillChildren = new() { [WzSkillData.ManaWave] = 2001012 }; // Mana Wave -> its held fall

    // baseRoot must carry the Packs (the skill nodes live there), chara is the Character category (the action
    // frames), skill is the Skill category (the icon pngs), str the String category.
    public static bool ExportWzSkills(WzRoot baseRoot, WzRoot chara, WzRoot skill, WzRoot str, string outDir)
    {
        var saved = new HashSet<string>();
        foreach (int id in WzSkillIds)
        {
            var data = ReadWzSkill(baseRoot, chara, id);
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
            if (id == WzSkillData.Meditation) // our own lines, not the WZ text (which has the MP cost, the party and the duration: none of them is true for a passive)
            {
                data.Effect = new() { $"Magic ATT: +{(int)data.Value("indieMad", data.MaxLevel)}" };
                data.Desc = new() { "Permanently enhances your Magic ATT." };
            }
            if (id == WzSkillData.ElementalAdaptation) // the WZ text also says the barrier regenerates, eats 2% of max MP per block and then cools down: none of it is done
            {
                int blocks = (int)data.Value("y", data.MaxLevel);
                data.Desc = new() { $"Cover yourself with a veil of fire and poison to block {blocks} debilitating status conditions. Also permanently increases Status Resistance and Elemental Resistance." };
                data.Effect = new() { data.Effect[0], $"Blocks {blocks} debilitating status conditions, one layer each.", data.Effect[2], data.Effect[3] }; // MP cost, our line, cooldown, passive effect
            }
            data.Icon = $"skill/wz/{id}/icon.png";
            data.Disabled = $"skill/wz/{id}/dis.png";
            if (!SavePassiveIcon(baseRoot, skill, id, Path.Combine(outDir, data.Icon))
                || !SavePassiveIcon(baseRoot, skill, id, Path.Combine(outDir, data.Disabled), "iconDisabled"))
                return false;
            if ((data.Common.ContainsKey("damage") || id is WzSkillData.Teleport or WzSkillData.ManaWave or WzSkillData.ElementalAdaptation) && !SaveWzSkillArt(baseRoot, skill, id, data, outDir, saved)) return false;
            if (id == WzSkillData.EmpiricalKnowledge && !SaveMarkArt(baseRoot, id, data, outDir, saved)) return false;
            if ((id is WzSkillData.ElementalDrain or WzSkillData.ElementalAdaptation) && !SaveNumberArt(baseRoot, skill, id, data, outDir)) return false;
            if (WzSkillChildren.TryGetValue(id, out int child))
            {
                var childData = ReadWzSkill(baseRoot, chara, child);
                if (child == WzSkillData.ManaWaveFloat && !SaveWzFloatArt(baseRoot, child, childData, outDir, saved)) return false;
                data.Children.Add(childData);
            }
            WriteJson(Path.Combine(outDir, "skill", "wz", $"{id}.json"), data);
            string kind = data.Kind == SkillData.KindPassive ? "passive" : data.Kind == SkillData.KindBuff ? "buff" : "active";
            Console.WriteLine($"wz skill {id} {data.Name}: {kind}, max {data.MaxLevel}, common {string.Join(",", data.Common.Keys)}"
                + $"{(data.Vectors.Count > 0 ? ", vec " + string.Join(",", data.Vectors.Keys) : "")}"
                + $"{(data.Children.Count > 0 ? ", child " + string.Join(",", data.Children.Select(c => c.SkillId)) : "")}");
            Console.WriteLine($"    {string.Join(" / ", data.Effect)}");
        }
        return true;
    }

    // A damage skill draws its cast effect (effect) and its hit spark (hit/0), and its ball (the flying bolt) and
    // its special (the art on each mob hit) when it has them. The other nodes are not drawn yet.
    // The effect, hit, ball and special also keep their WZ origins and delays (the node in Base has them; the png
    // node of the canvas does not). The cast effect uses both its origin and its delay.
    private static bool SaveWzSkillArt(WzRoot baseRoot, WzRoot skill, int id, WzSkillData data, string outDir, HashSet<string> saved)
    {
        var node = skill.Find($@"_Canvas\{id / 10000}.img\skill\{id}");
        bool teleport = id == WzSkillData.Teleport; // effect/0: the blue one, see the header
        bool noHit = id is WzSkillData.Teleport or WzSkillData.ManaWave or WzSkillData.ElementalAdaptation; // a cast effect only
        var effect = teleport ? node?.Nodes["effect"]?.Nodes["0"] : node?.Nodes["effect"];
        var hit = node?.Nodes["hit"]?.Nodes["0"];
        if (effect == null || (hit == null && !noHit))
        {
            Console.WriteLine($"skill {id} has no effect or no hit 0");
            return false;
        }
        var meta = WzSkillNode(baseRoot, id);
        data.Art["effect"] = ReadWzArt(teleport ? meta.Nodes["effect"]!.Nodes["0"]! : meta.Nodes["effect"]!);
        if (!SaveSkillFrames(effect, $"skill/wz/{id}/effect", data.CastFrames, outDir, saved, id)) return false;
        if (id == WzSkillData.ElementalAdaptation && !SaveEffect0(node!, meta, id, data, outDir, saved)) return false;
        if (hit != null)
        {
            data.Art["hit"] = ReadWzArt(meta.Nodes["hit"]!.Nodes["0"]!);
            if (!SaveSkillFrames(hit, $"skill/wz/{id}/hit", data.HitFrames, outDir, saved, id)) return false;
        }
        foreach ((string name, List<string> into) in new[] { ("ball", data.BallFrames), ("special", data.SpecialFrames) })
        {
            if ((id == WzSkillData.ElementalAdaptation && name == "special") || node!.Nodes[name] is not { } frames) continue; // its special is the barrier flash: not drawn
            if (!SaveSkillFrames(frames, $"skill/wz/{id}/{name}", into, outDir, saved, id)) return false;
            data.Art[name] = ReadWzArt(meta.Nodes[name]!);
        }
        return true;
    }

    // Elemental Adaptation's second cast layer (effect0, z -2 in WZ: behind the hero; the client draws it in front like every cast effect):
    // it plays together with effect. Its first frame is a blank 1x1 with no origin (like Mana Wave's effect): left out. Kept in Art["effect0"]
    // with its png paths in Frames, so the three lists have the same count.
    private static bool SaveEffect0(Wz_Node canvas, Wz_Node meta, int id, WzSkillData data, string outDir, HashSet<string> saved)
    {
        var art = ReadWzArt(meta.Nodes["effect0"]!);
        if (!SaveSkillFrames(canvas.Nodes["effect0"]!, $"skill/wz/{id}/effect0", art.Frames, outDir, saved, id)) return false;
        data.Art["effect0"] = art;
        return true;
    }

    // The slow fall's art (2001012): special (played once when the fall ends) and specialAffected (loops behind the hero
    // while it lasts) each hold three lists of frames (0, 1, 2) that play together (the specialAffected lists have z -1:
    // behind the hero). Each goes to Art["special0"], Art["specialAffected1"]... with its frames (png paths), origins and delays.
    // A frame is an _outlink to a picture in _Canvas, and several lists share pictures, so each frame follows its link.
    private static bool SaveWzFloatArt(WzRoot baseRoot, int id, WzSkillData data, string outDir, HashSet<string> saved)
    {
        var meta = WzSkillNode(baseRoot, id);
        foreach (string name in new[] { "special", "specialAffected" })
        {
            if (meta.Nodes[name] is not { } lists)
            {
                Console.WriteLine($"skill {id} has no {name}");
                return false;
            }
            foreach (Wz_Node list in lists.Nodes.Where(l => int.TryParse(l.Text, out _)))
            {
                var art = ReadWzArt(list);
                foreach (Wz_Node f in list.Nodes.Where(f => int.TryParse(f.Text, out _)).OrderBy(f => int.Parse(f.Text, CultureInfo.InvariantCulture)))
                {
                    if (WzNodeUtil.ReadCanvas(baseRoot, f).png is not { } png)
                    {
                        Console.WriteLine($"skill {id} {name}{list.Text} frame {f.Text}: no picture");
                        return false;
                    }
                    string path = $"skill/wz/{id}/{name}{list.Text}/{art.Frames.Count}.png";
                    SaveOnce(png, Path.Combine(outDir, path), saved);
                    art.Frames.Add(path);
                }
                data.Art[name + list.Text] = art;
            }
        }
        return true;
    }

    // The Empirical Knowledge mark art: the mob/0, mob/1, mob/2 loops (1/2/3 stacks) that play over a marked mob's head. Each
    // frame is an _outlink into _Canvas/000.img/skill/0000255/mob/<k>; the origin and delay live on the loose node.
    private static bool SaveMarkArt(WzRoot baseRoot, int id, WzSkillData data, string outDir, HashSet<string> saved)
    {
        var mob = WzSkillNode(baseRoot, id).Nodes["mob"] ?? throw new InvalidDataException($"skill {id} has no mob node");
        for (int k = 0; k < 3; k++)
        {
            if (mob.Nodes[k.ToString(CultureInfo.InvariantCulture)] is not { } list)
            {
                Console.WriteLine($"skill {id} mob/{k} not found");
                return false;
            }
            var art = ReadWzArt(list);
            foreach (Wz_Node f in list.Nodes.Where(f => int.TryParse(f.Text, out _) && f.Nodes["origin"] != null).OrderBy(f => int.Parse(f.Text, CultureInfo.InvariantCulture)))
            {
                if (WzNodeUtil.ReadCanvas(baseRoot, f).png is not { } png)
                {
                    Console.WriteLine($"skill {id} mob/{k} frame {f.Text}: no picture");
                    return false;
                }
                string path = $"skill/wz/{id}/mob{k}/{art.Frames.Count}.png";
                SaveOnce(png, Path.Combine(outDir, path), saved);
                art.Frames.Add(path);
            }
            if (art.Frames.Count == 0)
            {
                Console.WriteLine($"skill {id} mob/{k}: no frames");
                return false;
            }
            data.Art[$"mob{k}"] = art;
        }
        return true;
    }

    // The number icons of Elemental Drain and Elemental Adaptation (number/icon1..N), the buff-bar art. Drain's is the whole skill icon with the layer
    // count drawn in (origin 0,32 like the icon); Elemental Adaptation's is only the digits, drawn over the skill icon at its origin (the point of the
    // png that sits on the icon's origin, the icon's bottom left). Each child is a stub with an _outlink into the Skill category (like the icon).
    // Saved as skill/wz/{id}/num/{i}.png, in order, with the origins in NumberOrigins.
    private static bool SaveNumberArt(WzRoot baseRoot, WzRoot skill, int id, WzSkillData data, string outDir)
    {
        string id7 = id.ToString("D7");
        string img = (id >= 80000000 ? id / 100 : id / 10000).ToString("D3") + ".img";
        var number = baseRoot.Find($"Skill/{img}/skill/{id7}/number");
        if (number == null)
        {
            Console.WriteLine($"skill {id}: no number node");
            return false;
        }
        foreach (Wz_Node child in number.Nodes
            .Where(n => n.Text.StartsWith("icon") && int.TryParse(n.Text[4..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .OrderBy(n => int.Parse(n.Text[4..], CultureInfo.InvariantCulture)))
        {
            string? link = child.Nodes["_outlink"]?.Value as string;
            if (link == null || !link.StartsWith("Skill/"))
            {
                Console.WriteLine($"skill {id} number/{child.Text}: no icon link");
                return false;
            }
            string path = $"skill/wz/{id}/num/{data.NumberFrames.Count}.png";
            if (!UiPng.Save(skill, "Skill", link["Skill/".Length..], Path.Combine(outDir, path))) return false;
            data.NumberFrames.Add(path);
            data.NumberOrigins.Add(child.Nodes["origin"]?.Value is Wz_Vector v ? new[] { v.X, v.Y } : throw new InvalidDataException($"skill {id} number/{child.Text}: no origin"));
        }
        if (data.NumberFrames.Count == 0)
        {
            Console.WriteLine($"skill {id} number: no icons");
            return false;
        }
        return true;
    }

    // The origin and delay of each int-named frame, in frame order (the order SaveSkillFrames numbers the pngs).
    private static WzArt ReadWzArt(Wz_Node list)
    {
        // A frame with no origin is the blank lead-in of Mana Wave's effect: not a picture, left out.
        var frames = list.Nodes.Where(f => int.TryParse(f.Text, out _) && f.Nodes["origin"] != null).OrderBy(f => int.Parse(f.Text, CultureInfo.InvariantCulture)).ToList();
        return new WzArt
        {
            Origins = frames.Select(f => f.Nodes["origin"]!.Value is Wz_Vector v ? new[] { v.X, v.Y } : throw new InvalidDataException($"frame {f.Text} has no origin")).ToArray(),
            Delays = frames.Select(f => int.Parse(Text(f.Nodes["delay"]), CultureInfo.InvariantCulture)).ToArray(),
        };
    }

    private static Wz_Node WzSkillNode(WzRoot baseRoot, int id) =>
        baseRoot.Find($"Skill/{(id >= 80000000 ? id / 100 : id / 10000):D3}.img/skill/{id:D7}") ?? throw new InvalidDataException($"skill {id} not found");

    // The raw formula text of every number field of common (the String text takes its #names from here).
    private static Dictionary<string, string> WzSkillValues(WzRoot baseRoot, int id) =>
        WzSkillNode(baseRoot, id).Nodes["common"]!.Nodes.Where(v => v.Value is not Wz_Vector).ToDictionary(v => v.Text, Text);

    // One node as the table: no name, icon or text (the caller adds them to a skill of the window).
    private static WzSkillData ReadWzSkill(WzRoot baseRoot, WzRoot chara, int id)
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
        // Agile Magic: psdWeaponBooster/actionSpeed -2 (attack speed +2 stages). It goes into Common as boosterActionSpeed.
        if (node.Nodes["psdWeaponBooster"]?.Nodes["actionSpeed"] is { } booster)
            data.Common["boosterActionSpeed"] = SkillFormula.Levels(Text(booster), data.MaxLevel);
        foreach (Wz_Node v in node.Nodes["info"]!.Nodes) data.Info[v.Text] = Text(v);
        if (node.Nodes["action"] is { } action)
            foreach (Wz_Node v in action.Nodes) data.Actions.Add(Text(v));
        if (data.Actions.Count > 0) data.AttackDelayMs = ReadAttackDelay(chara, data.Actions[0]);
        if (id == WzSkillData.PoisonBreath) data.Script = ReadActionScript(chara, data.Actions[0]); // the other skills still play a plain swing
        data.ElemAttr = Text(node.Nodes["elemAttr"]);
        data.Invisible = node.Nodes["invisible"] != null;
        // No mpCon: always on (passive). info.type 10: a timed self effect (buff). Else cast for an effect.
        // Meditation is the exception: WZ has it as a buff, the user made it a passive (always-on magic attack, no cast).
        data.Kind = !data.Common.ContainsKey("mpCon") || WzSkillData.UserPassives.Contains(id) ? SkillData.KindPassive
            : data.Info.GetValueOrDefault("type") == "10" ? SkillData.KindBuff
            : SkillData.KindActive;
        return data;
    }

    // A body action written as steps: Character.wz/00002000.img/<action>/<n> holds the action name, the frame and the delay of each step.
    // The time of a step is the absolute delay (the sign marks the attack frame, see ReadAttackDelay); missing or zero counts as 100 ms.
    // Poison Breath is alert 2 -180, swingO2 0 -240, swingO2 2 360 (780 ms). The WZ move (a shift of a pixel or so) is not used.
    private static List<ActionStep> ReadActionScript(WzRoot chara, string action)
    {
        var node = chara.Find($@"00002000.img\{action}")
            ?? throw new InvalidDataException($"character action {action} not found");
        return node.Nodes
            .Where(f => int.TryParse(f.Text, out _))
            .OrderBy(f => int.Parse(f.Text, CultureInfo.InvariantCulture))
            .Select(f => new ActionStep
            {
                Action = Text(f.Nodes["action"]),
                Frame = f.Nodes["frame"]!.GetValueEx<int>(0),
                DelayMs = Math.Abs(f.Nodes["delay"]?.GetValueEx<int?>(null) is { } d && d != 0 ? d : 100),
            }).ToList();
    }

    // Journey client's rule: in Character.wz/00002000.img/<action>, a frame whose delay is positive is the
    // attack frame; its time is the sum of the absolute delays before it. Missing or zero delays count as 100 ms.
    // Energy Bolt is -60, -330, +270, so the attack frame is at 390 ms.
    private static int ReadAttackDelay(WzRoot chara, string action)
    {
        var node = chara.Find($@"00002000.img\{action}")
            ?? throw new InvalidDataException($"character action {action} not found");
        var delays = node.Nodes
            .Where(f => int.TryParse(f.Text, out _))
            .OrderBy(f => int.Parse(f.Text, CultureInfo.InvariantCulture))
            .Select(f => f.Nodes["delay"]?.GetValueEx<int?>(null))
            .ToList();
        return WzSkillData.AttackDelayFrom(delays);
    }
}
