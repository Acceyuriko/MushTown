using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Skill exports: the feel-number table plus icons, effects, hits, buff loops.
public static partial class Export
{
    // Skill table: v1 feel numbers (SkillData documents the column rules).
    // The modern client keeps skill stats server-side: no level/mpCon/damage
    // nodes exist anywhere in the data (verified in W11 recon). Art cut for
    // v1 (documented, all loud if misread): effect0 second acts, hit variant
    // 1+, Flash charge UI (special/number), Combo counter art (state/special),
    // Beam shootobj, Scarring mob mark. War Leap plays effect variant 0.
    private static readonly Dictionary<int, SkillData> Skills = new()
    {
        // 100 Warrior.
        [SkillData.SlashBlast] = new() { ReqLevel = 1, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 5, CooldownMs = 3000, DmgLo = 150, DmgHi = 250, MobCount = 6, Range = 160, CastLockMs = 600 },
        [SkillData.WarLeap] = new() { ReqLevel = 3, MaxLevel = 1, Kind = SkillData.KindActive, Mp = 3, CooldownMs = 5000 },
        [SkillData.LeapAttack] = new() { ReqLevel = 5, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 8, CooldownMs = 5000, DmgLo = 150, DmgHi = 250, MobCount = 6, Range = 160, CastLockMs = 600 },
        [SkillData.IronBody] = new() { ReqLevel = 2, MaxLevel = 10, Kind = SkillData.KindBuff, Mp = 10, CooldownMs = 10000, BuffMs = 120000, HpPct = 3, DefPct = 2 },
        [SkillData.WarriorMastery] = new() { ReqLevel = 4, MaxLevel = 10, Kind = SkillData.KindPassive, HpPct = 2, MovePct = 1 },
        [SkillData.GuardianArmor] = new() { ReqLevel = 6, MaxLevel = 10, Kind = SkillData.KindPassive, Atk = 2, DefPct = 1 },
        // 110 Fighter.
        [SkillData.Brandish] = new() { ReqLevel = 6, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 8, CooldownMs = 4000, DmgLo = 140, DmgHi = 230, MobCount = 6, Range = 170, CastLockMs = 600 },
        [SkillData.FlashBlade] = new() { ReqLevel = 8, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 10, CooldownMs = 6000, DmgLo = 130, DmgHi = 220, MobCount = 5, Range = 150, CastLockMs = 400 },
        [SkillData.ComboAttack] = new() { ReqLevel = 7, MaxLevel = 5, Kind = SkillData.KindBuff, Mp = 12, CooldownMs = 5000, BuffMs = 180000, OrbChance = 20, OrbMax = 5, OrbDmg = 4 },
        [SkillData.SpiritBlade] = new() { ReqLevel = 7, MaxLevel = 10, Kind = SkillData.KindBuff, Mp = 12, CooldownMs = 5000, BuffMs = 120000, AtkPct = 2 },
        [SkillData.WeaponMastery] = new() { ReqLevel = 6, MaxLevel = 10, Kind = SkillData.KindPassive, DmgPct = 2, CritPct = 1 },
        [SkillData.AgileArms] = new() { ReqLevel = 8, MaxLevel = 10, Kind = SkillData.KindPassive, Str = 2 },
        [SkillData.FinalAttack] = new() { ReqLevel = 8, MaxLevel = 5, Kind = SkillData.KindPassive, ProcPct = 12 },
        [SkillData.PhysicalTraining] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindPassive, Str = 2, Dex = 1 },
        // 111 Crusader.
        [SkillData.IntrepidSlash] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 12, CooldownMs = 5000, DmgLo = 200, DmgHi = 350, MobCount = 6, Range = 180, CastLockMs = 700 },
        [SkillData.BeamBlade] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 10, CooldownMs = 4000, DmgLo = 160, DmgHi = 260, MobCount = 4, Range = 200, CastLockMs = 600 },
        [SkillData.Rush] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 10, CooldownMs = 6000, DmgLo = 120, DmgHi = 200, MobCount = 6, Range = 150, CastLockMs = 400 },
        [SkillData.ScarringSword] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 12, CooldownMs = 5000, DmgLo = 180, DmgHi = 300, MobCount = 4, Range = 170, CastLockMs = 600 },
        [SkillData.ComboSynergy] = new() { ReqLevel = 9, MaxLevel = 5, Kind = SkillData.KindPassive, SynDmg = 2, SynChance = 6 },
        [SkillData.SelfRecovery] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindPassive, RegenHp = 5, RegenMp = 3 },
        [SkillData.ChanceAttack] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindPassive, CritPct = 2 },
        [SkillData.Endure] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindPassive, HpPct = 3, DefPct = 2 },
    };

    // Warrior line (100/110/111): icon + disabled icon + effect frames +
    // hit spark frames (variant 0) + buff loop frames (affected, buffs with
    // art only). Actives without an effect node fail loud (an invisible cast
    // is a data bug); buffs and passives without art are fine (Iron Body and
    // Combo Attack have no effect node; passives are icon-only by design).
    // War Leap carries 4 effect variants: v1 plays variant 0, flipped.
    private static bool ExportSkills(WzRoot skill, WzRoot str, string outDir, HashSet<string> saved)
    {
        foreach (int id in Skills.Keys.OrderBy(x => x))
        {
            var data = Skills[id];
            data.SkillId = id; // the key is the id (initializers don't repeat it)
            data.Effect.Clear(); data.Hit.Clear(); data.Affected.Clear(); // the table is static: a retry must not append twice
            string img = $"{id / 10000}.img";
            var node = skill.Find($@"_Canvas\{img}\skill\{id}");
            if (node == null)
            {
                Console.WriteLine($"skill {id} not found");
                return false;
            }
            string name = str.Find($@"Skill.img\{id}\name")?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"skill {id} has no String name");
                return false;
            }
            data.Name = name;
            data.Icon = $"skill/{id}/icon.png";
            data.Disabled = $"skill/{id}/dis.png";
            if (!UiPng.Save(skill, "Skill", $@"_Canvas/{img}/skill/{id}/icon", Path.Combine(outDir, data.Icon)))
                return false;
            if (!UiPng.Save(skill, "Skill", $@"_Canvas/{img}/skill/{id}/iconDisabled", Path.Combine(outDir, data.Disabled)))
                return false;
            var effect = node.Nodes["effect"];
            if (effect == null && data.Kind == SkillData.KindActive)
            {
                Console.WriteLine($"skill {id} has no effect");
                return false;
            }
            if (effect != null)
            {
                Wz_Node? frames = id == SkillData.WarLeap ? effect.Nodes["0"] : effect;
                if (frames == null)
                {
                    Console.WriteLine($"skill {id} has no effect variant 0");
                    return false;
                }
                if (!SaveSkillFrames(frames, $"skill/{id}/effect", data.Effect, outDir, saved, id)) return false;
            }
            var hit = node.Nodes["hit"]?.Nodes["0"];
            if (node.Nodes["hit"] != null && hit == null)
            {
                Console.WriteLine($"skill {id} has a hit node but no hit 0");
                return false;
            }
            if (hit != null && !SaveSkillFrames(hit, $"skill/{id}/hit", data.Hit, outDir, saved, id)) return false;
            var affected = node.Nodes["affected"];
            if (affected != null && !SaveSkillFrames(affected, $"skill/{id}/affected", data.Affected, outDir, saved, id)) return false;
            Console.WriteLine($"skill {id} {name}: effect x{data.Effect.Count}, hit x{data.Hit.Count}, affected x{data.Affected.Count}");
            WriteJson(Path.Combine(outDir, "skill", $"{id}.json"), data);
        }
        return true;
    }

    // Saves one flat frame list (int-named png children) as dir/{i}.png.
    private static bool SaveSkillFrames(Wz_Node list, string dir, List<string> into, string outDir, HashSet<string> saved, int id)
    {
        var frames = new List<(int key, Wz_Node node)>();
        foreach (Wz_Node f in list.Nodes)
        {
            if (!int.TryParse(f.Text, out int key))
            {
                Console.WriteLine($"skill {id} {dir}: non-frame child {f.Text}");
                return false;
            }
            frames.Add((key, f));
        }
        int i = 0;
        foreach ((int _, Wz_Node f) in frames.OrderBy(t => t.key))
        {
            if (f.Value is not Wz_Png png)
            {
                Console.WriteLine($"skill {id} {dir} frame {f.Text}: not a png");
                return false;
            }
            string path = $"{dir}/{i}.png";
            SaveOnce(png, Path.Combine(outDir, path), saved);
            into.Add(path);
            i++;
        }
        if (into.Count == 0)
        {
            Console.WriteLine($"skill {id} {dir}: no frames");
            return false;
        }
        return true;
    }
}
