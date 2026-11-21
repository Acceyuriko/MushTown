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
// Art["effect0"] with its png paths), no affected loop and no hit (its text is the WZ text: it says the same as the design). The layer count of the buff bar is drawn by the client (the WZ number icons are not exported).
// Its special (the barrier flash, played once per blocked status) goes to Art["block"] the same way (png under 2111011/block/); its special0
// (a faint back column with a floor ring, z -2) is not used. Re-run `export` after pulling this: an old export has no block art.
// The 4th job passives (212.img: Infinity, Arcane Aim, Buff Mastery, Fervent Drain) have no art but their icons: the generic path above
// (formulas, text, icon and disabled icon) is all they need. Their numbers are read by WzSkillStats (the always-on final damage, DEF ignored,
// magic attack, buff time and knockback resistance) and by the Client (Infinity's clock, Arcane Aim's stacks, Fervent Drain's per-DoT final damage).
// Re-run `export` after pulling this: an old export has no page IV json and Menu.cs refuses the page IV ids.
// Poison Mist (211.img) is a ground zone (WzSkillData.GroundZones): besides effect (the cast) and hit it keeps effect1 (a second cast layer, played with
// the first) and tile/0..8 (nine looping puffs of 8 pictures; the client lays them out, see GroundZoneFx) as Art["effect1"], Art["tile0"]..Art["tile8"] (png under 2111003/effect1/ and 2111003/tile<i>/).
// A skill with a dot and a flat mob node (a loop of pictures with origins; Poison Mist has one, Poison Breath none) also keeps it as Art["mob"] (png under <id>/mob/): the icon that
// shows over a mob that has the skill's DoT. (Empirical Knowledge's mob node is three loops and goes through SaveMarkArt; Elemental Weakness, Paralyze and Flame Haze
// have mob nodes too but are not DoTs or not made yet.) The node also has pos = 2 and repeat = 1: neither is used (pos 未核实).
// Re-run `export` after pulling this: an old export has none of them and the client refuses to start (Menu.cs).
// Teleport Mastery (211.img, a passive that strikes at the Teleport landing) keeps effect, effect0 (a back layer, played like Elemental Adaptation's), hit and its mob node
// (Art["mob"]: it has a dot; the loop that shows over a mob that is stunned or has its DoT); it has no action of its own to pose (its WZ action alert2 has no attack frame, so AttackDelayMs stays 0).
// Ignite (210.img, a passive) has no art of its own: its fire wall is the node 2100010 (WZ has no link between them, WzSkillData.IgniteWall), whose tile/0..2 (16 looping pictures each)
// go to Art["tile0"]..Art["tile2"] and hit/0 (the spark on each mob the wall hits) to HitFrames and Art["hit"], all under 2101010/ (see GroundZoneFx.StartWall).
// The text of both ends with "Can be toggled On/Off": they are always on here (the user's rule for always-on Buffs), so that line is left out.
// Creeping Toxin (211.img, a summon: a totem and a pool) keeps effect, effect0 (a back layer) and hit like the other damage skills, and its summon nodes summoned, stand and die and its
// pool tiles tile/0..3/<start|loop|end|end2>/<0|1> (SaveSummonArt; the client lays the pool out, see GroundZoneFx). It is also the first skill of WzSkillData.Summons.
// Its blast, the child node 2111014 (WzSkillData.CreepingToxinBlast), is kept as a child skill (the numbers and the box) and its effect as Art["blast"] of Creeping Toxin.
// Flame Orb and Explosion also keep their action as steps (Script, see WzSkillData.ActionScripts), like the other skills of that set.
// After pulling a change to this export run `export` again (dotnet run --project MushTown.Exporter -- export): an old
// json with no frames makes the client fail at start (Menu.cs), and a missing png makes it fail too.
public static partial class Export
{
    // The skill ids to export: the ones on the job pages, plus the skills the window does not list
    // (Elemental Weakness is invisible in WZ).
    private static readonly int[] WzSkillIds = JobSkills.Pages(StatTables.FirePoisonMage).SelectMany(p => p)
        .Append(WzSkillData.ElementalWeakness).Append(WzSkillData.EmpiricalKnowledge).ToArray();

    // A skill node that another skill owns (WZ has no link between them): parent -> child.
    private static readonly Dictionary<int, int> WzSkillChildren = new() { [WzSkillData.ManaWave] = 2001012, [WzSkillData.CreepingToxin] = WzSkillData.CreepingToxinBlast }; // Mana Wave -> its held fall; Creeping Toxin -> its blast

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
            if (id is WzSkillData.Ignite or WzSkillData.TeleportMastery) data.Desc = data.Desc.Where(l => !l.StartsWith("Can be toggled")).ToList(); // always on: no switch
            if (id == WzSkillData.Meditation) // our own lines, not the WZ text (which has the MP cost, the party and the duration: none of them is true for a passive)
            {
                data.Effect = new() { $"Magic ATT: +{(int)data.Value("indieMad", data.MaxLevel)}" };
                data.Desc = new() { "Permanently enhances your Magic ATT." };
            }
            data.Icon = $"skill/wz/{id}/icon.png";
            data.Disabled = $"skill/wz/{id}/dis.png";
            if (!SavePassiveIcon(baseRoot, skill, id, Path.Combine(outDir, data.Icon))
                || !SavePassiveIcon(baseRoot, skill, id, Path.Combine(outDir, data.Disabled), "iconDisabled"))
                return false;
            if (id == WzSkillData.Ignite ? !SaveWallArt(baseRoot, skill, data, outDir, saved)
                : (data.Common.ContainsKey("damage") || id is WzSkillData.Teleport or WzSkillData.ManaWave or WzSkillData.ElementalAdaptation) && !SaveWzSkillArt(baseRoot, skill, id, data, outDir, saved)) return false;
            if (id == WzSkillData.CreepingToxin && !SaveSummonArt(baseRoot, id, data, outDir, saved)) return false;
            if (id == WzSkillData.EmpiricalKnowledge && !SaveMarkArt(baseRoot, id, data, outDir, saved)) return false;
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
        if (id is WzSkillData.ElementalAdaptation or WzSkillData.TeleportMastery or WzSkillData.CreepingToxin && !SaveLayer(node!, meta, id, data, outDir, saved, "effect0", "effect0")) return false; // the cast's back layer
        if (id == WzSkillData.ElementalAdaptation && !SaveLayer(node!, meta, id, data, outDir, saved, "special", "block")) return false; // the block art (special): kept apart from the generic ball/special art
        if (id == WzSkillData.PoisonMist) // the ground zone: effect1 (a second cast layer) and the 9 looping tile variants, each 8 frames, kept as Art["effect1"], Art["tile0".."tile8"]
        {
            if (!SaveLayer(node!, meta, id, data, outDir, saved, "effect1", "effect1")) return false;
            for (int i = 0; i < WzSkillData.ZoneTiles; i++)
                if (!SaveLayer(node!.Nodes["tile"]!, meta.Nodes["tile"]!, id, data, outDir, saved, i.ToString(), $"tile{i}")) return false;
        }
        if (data.Common.ContainsKey("dot") && meta.Nodes["mob"] is { } mobIcon && mobIcon.Nodes["0"]?.Nodes["origin"] != null // a flat mob node: the head icon of the DoT
            && !SaveLayer(node!, meta, id, data, outDir, saved, "mob", "mob")) return false;
        if (hit != null)
        {
            data.Art["hit"] = ReadWzArt(meta.Nodes["hit"]!.Nodes["0"]!);
            if (!SaveSkillFrames(hit, $"skill/wz/{id}/hit", data.HitFrames, outDir, saved, id)) return false;
        }
        foreach ((string name, List<string> into) in new[] { ("ball", data.BallFrames), ("special", data.SpecialFrames) })
        {
            if ((id == WzSkillData.ElementalAdaptation && name == "special") || node!.Nodes[name] is not { } frames) continue; // its special is the block art, saved above as block
            if (!SaveSkillFrames(frames, $"skill/wz/{id}/{name}", into, outDir, saved, id)) return false;
            data.Art[name] = ReadWzArt(meta.Nodes[name]!);
        }
        return true;
    }

    // Ignite's art, read from its wall node 2100010: the three flame loops tile/0..2 (Art["tile0"]..Art["tile2"]) and the spark on each mob hit (hit/0: HitFrames, Art["hit"]).
    // The pngs go under the skill's own folder (skill/wz/2101010/), so the client finds all of Ignite's art in one skill.
    private static bool SaveWallArt(WzRoot baseRoot, WzRoot skill, WzSkillData data, string outDir, HashSet<string> saved)
    {
        int id = data.SkillId, wall = WzSkillData.IgniteWall;
        var canvas = skill.Find($@"_Canvas\{wall / 10000}.img\skill\{wall}");
        var meta = WzSkillNode(baseRoot, wall);
        if (canvas?.Nodes["tile"] == null || canvas.Nodes["hit"]?.Nodes["0"] == null)
        {
            Console.WriteLine($"skill {wall} has no tile or hit 0");
            return false;
        }
        for (int i = 0; i < WzSkillData.WallTiles; i++)
            if (!SaveLayer(canvas.Nodes["tile"]!, meta.Nodes["tile"]!, id, data, outDir, saved, i.ToString(), $"tile{i}")) return false;
        data.Art["hit"] = ReadWzArt(meta.Nodes["hit"]!.Nodes["0"]!);
        return SaveSkillFrames(canvas.Nodes["hit"]!.Nodes["0"]!, $"skill/wz/{id}/hit", data.HitFrames, outDir, saved, id);
    }

    // One more art layer of Elemental Adaptation, WZ node wzName kept in Art[key] with its png paths in Frames, so the three lists have the
    // same count: effect0 (the cast's back layer, z -2 in WZ: it plays together with effect, behind the hero) and special
    // (the block art, saved as block; special0, the faint back column with the floor ring, is not used: one layer is played per block).
    private static bool SaveLayer(Wz_Node canvas, Wz_Node meta, int id, WzSkillData data, string outDir, HashSet<string> saved, string wzName, string key)
    {
        var art = ReadWzArt(meta.Nodes[wzName]!);
        if (!SaveSkillFrames(canvas.Nodes[wzName]!, $"skill/wz/{id}/{key}", art.Frames, outDir, saved, id)) return false;
        art.Origins = art.Origins[..art.Frames.Count]; // special has a last frame in Base (frame 14) with no picture in the canvas: left out
        art.Delays = art.Delays[..art.Frames.Count];
        data.Art[key] = art;
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

    // One list of frames (WZ nodes 0, 1, 2...) kept as Art[key]: the frames that have an origin, each followed through its _outlink to its picture (png under <id>/<key>/).
    private static bool SaveLinkedList(WzRoot baseRoot, Wz_Node list, int id, string key, WzSkillData data, string outDir, HashSet<string> saved)
    {
        var art = ReadWzArt(list);
        foreach (Wz_Node f in list.Nodes.Where(f => int.TryParse(f.Text, out _) && f.Nodes["origin"] != null).OrderBy(f => int.Parse(f.Text, CultureInfo.InvariantCulture)))
        {
            if (WzNodeUtil.ReadCanvas(baseRoot, f).png is not { } png)
            {
                Console.WriteLine($"skill {id} {key} frame {f.Text}: no picture");
                return false;
            }
            string path = $"skill/wz/{id}/{key}/{art.Frames.Count}.png";
            SaveOnce(png, Path.Combine(outDir, path), saved);
            art.Frames.Add(path);
        }
        if (art.Frames.Count == 0)
        {
            Console.WriteLine($"skill {id} {key}: no frames");
            return false;
        }
        data.Art[key] = art;
        return true;
    }

    // Creeping Toxin's summon art: summon/summoned (the totem comes up), summon/stand (it waits, loops) and summon/die (it goes), and the pool tiles
    // tile/<kind 0..3>/<phase start|loop|end|end2>/<layer 0|1> (Art["tile<kind>_<phase>_<layer>"], see WzSkillData.ToxinTile). The two layers of a phase play together.
    private static bool SaveSummonArt(WzRoot baseRoot, int id, WzSkillData data, string outDir, HashSet<string> saved)
    {
        var meta = WzSkillNode(baseRoot, id);
        var summon = meta.Nodes["summon"] ?? throw new InvalidDataException($"skill {id} has no summon node");
        foreach (string name in new[] { "summoned", "stand", "die" })
            if (summon.Nodes[name] is not { } list || !SaveLinkedList(baseRoot, list, id, name, data, outDir, saved))
            {
                Console.WriteLine($"skill {id} summon/{name} failed");
                return false;
            }
        var tiles = meta.Nodes["tile"] ?? throw new InvalidDataException($"skill {id} has no tile node");
        for (int k = 0; k < WzSkillData.ToxinTileKinds; k++)
            foreach (string phase in WzSkillData.ToxinPhases)
                for (int layer = 0; layer < WzSkillData.ToxinLayers; layer++)
                    if (tiles.Nodes[k.ToString(CultureInfo.InvariantCulture)]?.Nodes[phase]?.Nodes[layer.ToString(CultureInfo.InvariantCulture)] is not { } list
                        || !SaveLinkedList(baseRoot, list, id, WzSkillData.ToxinTile(k, phase, layer), data, outDir, saved))
                    {
                        Console.WriteLine($"skill {id} tile {k} {phase} {layer} failed");
                        return false;
                    }
        if (WzSkillNode(baseRoot, WzSkillData.CreepingToxinBlast).Nodes["effect"] is not { } blast || !SaveLinkedList(baseRoot, blast, id, "blast", data, outDir, saved))
        {
            Console.WriteLine($"skill {id} blast effect failed");
            return false;
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
        if (data.Actions.Count > 0 && id != WzSkillData.TeleportMastery) data.AttackDelayMs = ReadAttackDelay(chara, data.Actions[0]); // Teleport Mastery strikes at once: its alert2 has no attack frame
        if (WzSkillData.ActionScripts.Contains(id)) data.Script = ReadActionScript(chara, data.Actions[0]); // the other skills still play a plain swing
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
    // Poison Breath is alert 2 -180, swingO2 0 -240, swingO2 2 360 (780 ms); Energy Bolt alert 2 -60, swingO3 0 -330, swingO3 1 270 (660 ms);
    // Flame Orb is alert 2 -90, swingO2 0 -210, stabO2 1 420 (720 ms, attack frame at 300 ms); Explosion alert 2 -30, swingO2 1 -60, swingO2 0 -270, stabO2 1 -30, stabO2 1 480 (870 ms, attack frame at 390 ms);
    // Poison Mist alert 2 -60, alert 1 -60, alert 0 -360, swingO1 2 180 (660 ms); Elemental Adaptation alert 2 -120, 1 -120, 0 -300, swingO1 2 360, alert 2 90 (990 ms). The WZ move (a shift of a pixel or so) is not used.
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
