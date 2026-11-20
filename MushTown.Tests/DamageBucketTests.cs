using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The damage formula audit (T-20261120094717): every bonus lands in the bucket its WZ text names. The rule (AGENTS.md): a bonus that does not say
// "final damage" is a damage % (the additive zone: extraDmgPct, or the DAMAGE/BOSS/NORMAL stat points); only an explicit "final damage" is its own
// multiplying zone (extraFinalPct, or a MULTIPLIER record on FINAL_DAMAGE_PCT). One test for each source, and one for each path (a basic swing, a
// skill hit, a DoT tick). Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private static readonly StatKey[] DamageKeys =
    {
        StatKey.DAMAGE_PCT, StatKey.FINAL_DAMAGE_PCT, StatKey.BOSS_PCT, StatKey.NORMAL_PCT, StatKey.STATUS_DAMAGE_PCT, StatKey.IED_PCT,
    };

    private static Mob AuditMob() => new() { Uid = 1, Map = 7, Level = 200, Def = 0.1, Hp = 1_000_000_000_000, MaxHp = 1_000_000_000_000 };

    // A seed whose first double is far from the 1% chance of the test builds: the mark roll does not proc, so the stacks stay as they were put.
    private const int NoProcSeed = 2;

    // The two zones of DamageChain itself: extraDmgPct adds to the damage % points (the same hit as +that many DmgPct), extraFinalPct multiplies
    // like the build's own final damage, extraIedPct is one more DEF-ignored source (1 - a)(1 - b).
    [TestMethod]
    public void Zones_ExtraDamageAdds_ExtraFinalMultiplies_ExtraIedCombinesAsASource()
    {
        var b = Mage with { DmgPct = 100, FinalDamagePct = 40, IedPct = 20 };
        long Hit(Build build, double dmg = 0, double final = 0, double ied = 0) =>
            DamageChain.Calculate(build, 0.5, 1.0, 0.1, 200, extraDmgPct: dmg, extraFinalPct: final, extraIedPct: ied).TotalDamage;
        Assert.AreEqual(Hit(b with { DmgPct = 150 }), Hit(b, dmg: 50), 2); // +50 damage: 100 + 50 points, not x1.5
        Assert.AreEqual(Hit(b with { FinalDamagePct = 61 }), Hit(b, final: 15), 2); // +15 final damage: 1.40 x 1.15 = 1.61
        Assert.AreEqual(Hit(b with { IedPct = 24 }), Hit(b, ied: 5), 2); // 1 - 0.8 x 0.95 = 24%
        Assert.AreNotEqual(Hit(b, dmg: 50), Hit(b, final: 50)); // with 100 damage % on the build, the two zones are not the same number
        Assert.IsTrue(Hit(b, final: 50) > Hit(b, dmg: 50));
    }

    // The stat records of the Fire/Poison passives: which key and which kind of record each one writes. None of them writes a damage point
    // (DAMAGE, BOSS, NORMAL, STATUS) or IED; only Elemental Decrease writes final damage, as a multiplier (its WZ text says "Final Damage").
    [TestMethod]
    public void FirePoisonPassives_WriteTheirOwnKeys_OnlyElementalDecreaseTouchesFinalDamage()
    {
        var expected = new Dictionary<string, (StatKey, StatValueType)[]>
        {
            ["Magic Armor"] = new[] { (StatKey.DEF, StatValueType.FLAT) },
            ["MP Boost"] = new[] { (StatKey.MAX_MP, StatValueType.FLAT), (StatKey.MAX_MP, StatValueType.PERCENT), (StatKey.ATTACK_SPEED, StatValueType.FLAT) },
            ["Teleport"] = new[] { (StatKey.SPEED, StatValueType.PERCENT), (StatKey.SPEED_MAX, StatValueType.FLAT) },
            ["High Wisdom"] = new[] { (StatKey.INT, StatValueType.FLAT) }, // "Permanently increase INT"
            ["Agile Magic"] = new[] { (StatKey.INT, StatValueType.FLAT), (StatKey.ATTACK_SPEED, StatValueType.FLAT) },
            ["Spell Mastery"] = new[] { (StatKey.MATT, StatValueType.FLAT), (StatKey.CRIT_RATE, StatValueType.FLAT), (StatKey.MASTERY, StatValueType.FLAT) },
            ["Meditation"] = new[] { (StatKey.MATT, StatValueType.FLAT) }, // a passive now: magic attack, no damage bucket
            ["Elemental Adaptation (Fire, Poison)"] = new[] { (StatKey.STATUS_RESISTANCE, StatValueType.FLAT), (StatKey.ELEMENT_RESISTANCE, StatValueType.FLAT) }, // resistances (always on), no damage bucket
            ["Magic Critical"] = new[] { (StatKey.CRIT_RATE, StatValueType.FLAT), (StatKey.CRIT_DAMAGE, StatValueType.FLAT) },
            ["Elemental Decrease"] = new[] { (StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER), (StatKey.IGNORE_ELEMENT_RESISTANCE_PCT, StatValueType.FLAT) },
            ["Element Amplification"] = new[] { (StatKey.MP_COST_PCT, StatValueType.FLAT), (StatKey.MAGIC_DAMAGE_PCT, StatValueType.FLAT) }, // "Damage: +50%"
            ["Burning Magic"] = new[] { (StatKey.DOT_TIME_PCT, StatValueType.FLAT), (StatKey.DOT_FINAL_PCT, StatValueType.FLAT) }, // "increases final damage by 15%"
        };
        var mods = WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0).ToList();
        foreach (var (name, keys) in expected)
            CollectionAssert.AreEquivalent(keys, mods.Where(m => m.Label == name).Select(m => (m.Key, m.Type)).ToArray(), name);
        Assert.AreEqual(expected.Count, mods.Select(m => m.Label).Distinct().Count());
        var touching = mods.Where(m => DamageKeys.Contains(m.Key)).ToList();
        Assert.AreEqual(1, touching.Count);
        Assert.AreEqual("Elemental Decrease", touching[0].Label);
        Assert.AreEqual(1.40, touching[0].Value, 1e-9);
    }

    // Element Amplification is "Damage: +50%" of the magic skills: the damage zone, for magic hits only. It is not a panel damage point (the
    // snapshot has no DAMAGE_PCT from it), and the pipeline adds it to the additive sum: a magic hit equals the same hit with 50 more extraDmgPct;
    // a basic swing does not take it.
    [TestMethod]
    public void ElementAmplification_IsDamageZone_MagicHitsOnly()
    {
        var build = Mage with { DmgPct = 100, MagicDmgPct = 50 }; // DmgPct 100: the damage zone and the final zone give different numbers
        var mob = AuditMob();
        var pipeline = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { });
        long Hit(bool magic)
        {
            mob.Hp = 1_000_000_000_000;
            return pipeline.Resolve(new[] { mob }, new HitPipeline.Attack { Build = build, Rng = new Random(NoProcSeed), Pct = 100, Magic = magic })[0].Damage;
        }
        var rng = new Random(NoProcSeed);
        long plain = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, build, 100, 200);
        rng = new Random(NoProcSeed);
        double r1 = rng.NextDouble(), r2 = rng.NextDouble();
        long asDamage = Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraDmgPct: 50);
        Assert.AreEqual(asDamage, Hit(true));
        Assert.AreNotEqual(asDamage, Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraFinalPct: 50)); // the other bucket gives another number
        Assert.AreEqual(plain, Hit(false));
        var snap = StatAggregator.Aggregate(WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0));
        Assert.AreEqual(0, snap.Value(StatKey.DAMAGE_PCT));
        Assert.AreEqual(50, snap.Value(StatKey.MAGIC_DAMAGE_PCT));
    }

    // Elemental Weakness (+20 "magical damage"), the Element Amplification and the mark's damage % (Empirical Knowledge "Damage per Stack") are
    // damage %: they add together into one sum (20 + 50 + 2 stacks x 5 = 80); the mark's "Enemy DEF Ignored per Stack" is its own DEF-ignored source.
    [TestMethod]
    public void WeaknessAmplificationAndMark_AddInTheDamageZone_MarkIedIsDefIgnored()
    {
        var build = Mage with { DmgPct = 100, MagicDmgPct = 50, MarkProp = 1 };
        var mob = AuditMob();
        mob.Statuses.Add(MobStatusKind.ElementalWeakness, 20, 60_000);
        mob.Statuses.SetMark(5, 2, 60_000);
        var hit = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { })
            .Resolve(new[] { mob }, new HitPipeline.Attack { Build = build, Rng = new Random(NoProcSeed), Attacker = 5, Pct = 100, Magic = true })[0];
        var rng = new Random(NoProcSeed);
        rng.NextDouble(); // the mark roll (no proc: the two stacks stay)
        double r1 = rng.NextDouble(), r2 = rng.NextDouble();
        Assert.AreEqual(Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraDmgPct: 80, extraIedPct: 10), hit.Damage);
        Assert.AreNotEqual(hit.Damage, Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraFinalPct: 80, extraIedPct: 10)); // the other bucket gives another number
    }

    // Elemental Drain ("Final Damage: +3%" for each DoT, up to 5) and Burning Magic ("increases final damage by 15%") are final damage: two sources that
    // multiply (1.03 x 1.15), not points that add (1.18).
    [TestMethod]
    public void DrainAndBurning_AreFinalDamage_AndMultiply()
    {
        var build = Mage with { DmgPct = 100, DrainPct = 3, DrainMax = 5, DotFinalPct = 15 };
        var mob = AuditMob();
        mob.Statuses.AddDot(DotOf(2100001, 60_000)); // one DoT: one Drain layer, and a Burning Magic target
        var hit = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { })
            .Resolve(new[] { mob }, new HitPipeline.Attack { Build = build, Rng = new Random(3), Pct = 100, Map = 7 })[0];
        var rng = new Random(3);
        double final = (1.03 * 1.15 - 1) * 100;
        double r1 = rng.NextDouble(), r2 = rng.NextDouble();
        Assert.AreEqual(Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraFinalPct: final), hit.Damage);
        Assert.AreNotEqual(hit.Damage, Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraDmgPct: final)); // the damage zone gives another number
        Assert.AreNotEqual(Damage.SkillCalc(0.5, 1.0, 0.1, build, 100, 200, extraFinalPct: 18), Damage.SkillCalc(0.5, 1.0, 0.1, build, 100, 200, extraFinalPct: final));
    }

    // Burning Magic's WZ text names the status of the enemy, not who put it there: a DoT of another attacker counts too.
    [TestMethod]
    public void BurningMagic_CountsADotFromAnyAttacker()
    {
        var build = Mage with { DmgPct = 100, DotFinalPct = 15 };
        var mob = AuditMob();
        mob.Statuses.AddDot(DotOf(2100001, 60_000, attacker: 9));
        var hit = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { })
            .Resolve(new[] { mob }, new HitPipeline.Attack { Build = build, Rng = new Random(3), Attacker = 5, Pct = 100 })[0];
        var rng = new Random(3);
        double r1 = rng.NextDouble(), r2 = rng.NextDouble();
        Assert.AreEqual(Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraFinalPct: 15), hit.Damage);
        Assert.AreNotEqual(hit.Damage, Damage.SkillCalc(r1, r2, 0.1, build, 100, 200, extraDmgPct: 15)); // the damage zone gives another number
    }

    // The three paths, one mob in one state (Weakness +20, a DoT, 2 mark stacks of attacker 5). A DoT tick is no special case: it takes what a
    // skill hit takes, and differs in the four known ways only (no crit, no roll for the marks, no MP Eater, no new status). A basic swing is not
    // magic: it does not take Weakness and Element Amplification; it takes the mark, Elemental Drain and Burning Magic.
    [TestMethod]
    public void Paths_SwingSkillHitAndTick_TakeTheirBonuses()
    {
        var build = Mage with { DmgPct = 100, MagicDmgPct = 50, DotFinalPct = 15, DrainPct = 3, DrainMax = 5, MarkProp = 1, CritPct = 0 };
        var mob = AuditMob();
        mob.Statuses.Add(MobStatusKind.ElementalWeakness, 20, 60_000);
        mob.Statuses.AddDot(DotOf(2100001, 60_000, attacker: 5));
        mob.Statuses.SetMark(5, 2, 60_000);
        var pipeline = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { });
        long Hit(HitPipeline.Attack a)
        {
            mob.Hp = 1_000_000_000_000;
            return pipeline.Resolve(new[] { mob }, a)[0].Damage;
        }
        double final = (1.03 * 1.15 - 1) * 100; // one DoT: one Drain layer; the DoT makes the mob a Burning Magic target
        long Expected(int seed, bool markRoll, double dmg, int pct, bool swapped = false)
        {
            var rng = new Random(seed);
            if (markRoll) rng.NextDouble();
            double r1 = rng.NextDouble(), r2 = rng.NextDouble();
            return swapped // the same numbers in the other two buckets
                ? Damage.SkillCalc(r1, r2, 0.1, build, pct, 200, extraDmgPct: final, extraFinalPct: dmg, extraIedPct: 10)
                : Damage.SkillCalc(r1, r2, 0.1, build, pct, 200, extraDmgPct: dmg, extraFinalPct: final, extraIedPct: 10);
        }
        var swing = new HitPipeline.Attack { Build = build, Rng = new Random(NoProcSeed), Attacker = 5, Pct = 100, Map = 7 };
        Assert.AreEqual(Expected(NoProcSeed, true, 10, 100), Hit(swing)); // the mark only: 2 stacks x 5
        Assert.AreNotEqual(Expected(NoProcSeed, true, 10, 100), Expected(NoProcSeed, true, 10, 100, swapped: true));
        var skill = new HitPipeline.Attack { Build = build, Rng = new Random(NoProcSeed), Attacker = 5, Pct = 100, Magic = true, Map = 7 };
        Assert.AreEqual(Expected(NoProcSeed, true, 20 + 50 + 10, 100), Hit(skill)); // Weakness + Amplification + the mark
        Assert.AreNotEqual(Expected(NoProcSeed, true, 80, 100), Expected(NoProcSeed, true, 80, 100, swapped: true));
        var tick = new HitPipeline.Attack { Build = build, Rng = new Random(NoProcSeed), Attacker = 5, Pct = 100, Magic = true, Tick = true, Skill = 2100001, Map = 7 };
        var rngTick = new Random(NoProcSeed); // no mark roll, no crit roll: one die
        double rollTick = rngTick.NextDouble();
        long tickDamage = Damage.SkillCalc(rollTick, 1.0, 0.1, build, 100, 200, extraDmgPct: 80, extraFinalPct: final, extraIedPct: 10);
        Assert.AreEqual(tickDamage, Hit(tick));
        Assert.AreNotEqual(tickDamage, Damage.SkillCalc(rollTick, 1.0, 0.1, build, 100, 200, extraDmgPct: final, extraFinalPct: 80, extraIedPct: 10));
    }

    // A tick reads Elemental Drain around the place where its DoT was cast: the mob in the circle gives a layer (also on the last tick, when the DoT
    // is no longer on its list: the DoT the tick comes from still counts), a mob outside gives none.
    [TestMethod]
    public void DotTick_DrainReadsThePlaceWhereTheDotWasCast()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        var mob = AuditMob(); // at (0, 0), no DoT on its list: the tick is the last one
        var pipeline = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { });
        long Tick(float casterX)
        {
            mob.Hp = 1_000_000_000_000;
            return pipeline.Resolve(new[] { mob }, new HitPipeline.Attack
            {
                Build = build,
                Rng = new Random(3),
                Pct = 60,
                Magic = true,
                Tick = true,
                Skill = Breath,
                Map = 7,
                CasterX = casterX,
            })[0].Damage;
        }
        var rng = new Random(3);
        double roll = rng.NextDouble();
        Assert.AreEqual(Damage.SkillCalc(roll, 1.0, 0.1, build, 60, 200, extraFinalPct: 3), Tick(100)); // 100 px away: inside 800
        Assert.AreEqual(Damage.SkillCalc(roll, 1.0, 0.1, build, 60, 200), Tick(5000)); // far from the cast: no layer
    }

    // The mark of the source also counts on its own DoT's tick (stacks as they are: the tick rolls no proc and does not refresh the time).
    [TestMethod]
    public void DotTick_TakesTheSourcesMarkAsItIs_RollsNoProc()
    {
        var build = Mage with { MarkProp = 100 }; // a swing would proc for certain; a tick must not even roll
        var mob = AuditMob();
        mob.Statuses.SetMark(5, 1, 60_000);
        var pipeline = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { });
        var hit = pipeline.Resolve(new[] { mob }, new HitPipeline.Attack
        {
            Build = build,
            Rng = new Random(3),
            Attacker = 5,
            Pct = 60,
            Magic = true,
            Tick = true,
            Skill = Breath,
            Map = 7,
        })[0];
        var rng = new Random(3);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), 1.0, 0.1, build, 60, 200, extraDmgPct: 5, extraIedPct: 5), hit.Damage);
        Assert.AreEqual(1, mob.Statuses.MarkStacks(5)); // no proc, no new stack
    }

    // The default passives (Lv.200 account): each source in its WZ bucket. Heroic is "Final Damage" (multiplier); the Links that say "Damage" are damage
    // points, "Boss Damage" boss points, "Ignore Defense" a DEF-ignored multiplier; Insight is ignore elemental resistance (WZ UI Stat/15: the
    // sources add), and it is not IED.
    [TestMethod]
    public void DefaultPassives_EachSourceInItsBucket()
    {
        (StatKey, StatValueType)[] Of(string label) =>
            DefaultPassives.All.Where(m => m.Label == label).Select(m => (m.Key, m.Type)).ToArray();
        CollectionAssert.Contains(Of("Heroic (Reboot, Lv.200-249)"), (StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER));
        foreach (var damage in new[] { "Link: Wild Rage (Demon Avenger)", "Link: Noble Fire (Adele)", "Link: Innate Gift (Khali)", "Link: Nature's Friend (Lara)" })
            CollectionAssert.Contains(Of(damage), (StatKey.DAMAGE_PCT, StatValueType.FLAT), damage);
        foreach (var boss in new[] { "Link: Fury Unleashed (Demon Slayer)", "Link: Noble Fire (Adele)", "Link: Focus Spirit (Lynn)", "Link: Qi Cultivation (Mo Xuan)" })
            CollectionAssert.Contains(Of(boss), (StatKey.BOSS_PCT, StatValueType.FLAT), boss);
        foreach (var ied in new[] { "Link: Light Wash (Luminous)", "Link: Rhinne's Blessing (Zero)", "Link: Bravado (Hoyoung)", "Trait: Ambition Lv.100" })
            CollectionAssert.Contains(Of(ied), (StatKey.IED_PCT, StatValueType.MULTIPLIER), ied);
        CollectionAssert.AreEqual(new[] { (StatKey.IGNORE_ELEMENT_RESISTANCE_PCT, StatValueType.FLAT) }, Of("Trait: Insight Lv.100"));
        foreach (var crit in new[] { "Link: Judgment (Kinesis)", "Link: Guiding Stars (Shine)" })
            CollectionAssert.Contains(Of(crit), (StatKey.CRIT_DAMAGE, StatValueType.FLAT), crit);
        // Heroic is the only default source of final damage
        CollectionAssert.AreEqual(new[] { "Heroic (Reboot, Lv.200-249)" },
            DefaultPassives.All.Where(m => m.Key == StatKey.FINAL_DAMAGE_PCT).Select(m => m.Label).ToArray());
    }

    // Insight (5) and Elemental Decrease (10) are two sources of the same stat and add (15). It is not IED. The element multiplier rule itself is
    // locked by the Elemental Decrease tests; here only the sum and the effect on the multiplier.
    [TestMethod]
    public void Insight_AddsToElementalDecrease_NotToIed()
    {
        var snap = StatAggregator.Aggregate(DefaultPassives.All.Concat(WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0)));
        Assert.AreEqual(15, snap.IntValue(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT));
        var insightOnly = StatAggregator.Aggregate(DefaultPassives.All);
        Assert.AreEqual(5, insightOnly.IntValue(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT));
        Assert.AreEqual(0.5 * 1.15, Damage.SkillResult(0.5, 1.0, 0.1, Mage with { IgnoreElemPct = 15 }, 100, 200, "S2", AttackElement.Poison).ElementMultiplier, 1e-9);
    }

    // Equipment: one piece that carries every field EquipModifiers reads. The records are the stats, HP/MP, DEF, magic attack, speed and jump;
    // none of them is a damage bucket (DAMAGE, FINAL, BOSS, NORMAL, STATUS, IED) or a crit key. Gear has no damage line in WZ.
    [TestMethod]
    public void Equipment_EveryField_WritesNoDamageBucket()
    {
        var piece = new ItemData
        {
            ItemId = 1,
            Slot = "Wp",
            Atk = 50,
            Str = 1,
            Dex = 2,
            Int = 3,
            Luk = 4,
            Mad = 60,
            Def = 7,
            MaxHp = 80,
            MaxMp = 90,
            Speed = 5,
            Jump = 6,
            Mdd = 8,
            Acc = 9,
            Eva = 10, // shown only: no record
        };
        var mods = ItemData.EquipModifiers(new List<int> { 1 }, new Dictionary<int, ItemData> { [1] = piece }).ToList();
        var expected = new Dictionary<StatKey, double>
        {
            [StatKey.STR] = 1,
            [StatKey.DEX] = 2,
            [StatKey.INT] = 3,
            [StatKey.LUK] = 4,
            [StatKey.MATT] = 60,
            [StatKey.DEF] = 7,
            [StatKey.MAX_HP] = 80,
            [StatKey.MAX_MP] = 90,
            [StatKey.SPEED] = 5,
            [StatKey.JUMP] = 6,
        };
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), mods.Select(m => m.Key).ToList());
        foreach (var m in mods) Assert.AreEqual(expected[m.Key], m.Value, m.Key.ToString());
        Assert.IsFalse(mods.Any(m => DamageKeys.Contains(m.Key) || m.Key == StatKey.CRIT_RATE || m.Key == StatKey.CRIT_DAMAGE));
    }

    // Set lines: every effect name found in the exported sets (assets/set/*.json): its record and bucket. "Damage vs. Normal Monsters" is the NORMAL
    // damage point (additive), "Ignore Enemy Defense" is an IED multiplier; the rest are stats; incMDD, incACC and incEVA have no key.
    [TestMethod]
    public void SetEffects_EachExportedName_GoesToItsBucket()
    {
        var expected = new Dictionary<string, (StatKey, StatValueType, double)[]>
        {
            ["nbdR"] = new[] { (StatKey.NORMAL_PCT, StatValueType.FLAT, 20.0) },
            ["ignoreTargetDEF"] = new[] { (StatKey.IED_PCT, StatValueType.MULTIPLIER, 0.8) },
            ["incPAD"] = new[] { (StatKey.ATT, StatValueType.FLAT, 20.0) },
            ["incMAD"] = new[] { (StatKey.MATT, StatValueType.FLAT, 20.0) },
            ["incPDD"] = new[] { (StatKey.DEF, StatValueType.FLAT, 20.0) },
            ["incINT"] = new[] { (StatKey.INT, StatValueType.FLAT, 20.0) },
            ["incAllStat"] = new[]
            {
                (StatKey.STR, StatValueType.FLAT, 20.0), (StatKey.DEX, StatValueType.FLAT, 20.0),
                (StatKey.INT, StatValueType.FLAT, 20.0), (StatKey.LUK, StatValueType.FLAT, 20.0),
            },
            ["incMHPr"] = new[] { (StatKey.MAX_HP, StatValueType.PERCENT, 20.0) },
            ["incMMPr"] = new[] { (StatKey.MAX_MP, StatValueType.PERCENT, 20.0) },
            ["incMDD"] = Array.Empty<(StatKey, StatValueType, double)>(),
            ["incACC"] = Array.Empty<(StatKey, StatValueType, double)>(),
            ["incEVA"] = Array.Empty<(StatKey, StatValueType, double)>(),
        };
        foreach (var (name, records) in expected)
        {
            var set = new SetData { Id = 1, Parts = { new SetPart { Ids = { 10 } } }, Tiers = { new SetTier { Count = 1, Effects = { new SetEffect { Key = name, Value = 20 } } } } };
            var mods = SetRules.Modifiers(new Dictionary<int, SetData> { [1] = set }, new List<int> { 10 }).ToList();
            CollectionAssert.AreEquivalent(records.ToList(), mods.Select(m => (m.Key, m.Type, m.Value)).ToList(), name);
            Assert.IsTrue(SetRules.IsKnown(name), name);
        }
    }

    // Elemental Drain counts only the caster's own DoTs (WZ: "your damage-over-time effects"). Three players on one mob: player 0 has two DoT skills on it,
    // player 1 has one, player 2 has none. Each reads its own layers (2, 1, 0): the DoTs of the others do not count, in the layer count and in the damage.
    [TestMethod]
    public void ElementalDrain_CountsOnlyTheCastersOwnDots()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        foreach (int id in new[] { 2100001, 2100002, 2100003 }) assets.AddWzSkill(DotSkillData(id));
        var world = World(assets, (100, 100));
        world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100001, 100, attacker: 0);
        world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100003, 100, attacker: 0);
        world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100002, 100, attacker: 1);
        Assert.AreEqual(2, world.DrainLayers(7, 0, 100, 5, 0));
        Assert.AreEqual(1, world.DrainLayers(7, 0, 100, 5, 1));
        Assert.AreEqual(0, world.DrainLayers(7, 0, 100, 5, 2));
        long BoltDamage(int attacker)
        {
            var hits = world.SkillStrike(7, 0, 100, true, new Random(3), build, Bolt, 78, attacker);
            hits.AddRange(world.Tick(1000, new()).Strikes.Select(s => s.hit));
            return hits[0].Damage;
        }
        long Expected(double final)
        {
            var rng = new Random(3);
            return Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraFinalPct: final);
        }
        Assert.AreEqual(Expected(6), BoltDamage(0));
        Assert.AreEqual(Expected(3), BoltDamage(1));
        Assert.AreEqual(Expected(0), BoltDamage(2));
    }

    // End to end through MobWorld: a cast puts its DoT on a mob 1000 px from the origin, from the place (900, 100). The first tick reads the layer around that
    // place (one layer: 3%). If WzStrike did not record the place, or DotTick did not read it, the tick would count around (0, 0): 1000 px, out of range, no layer.
    [TestMethod]
    public void DotTick_EndToEnd_DrainUsesTheRecordedCastPlace()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = World(assets, (1000, 100));
        world.SkillStrike(7, 900, 100, true, new Random(4), build, Breath, 180);
        var tick = Ticks(world, 1000).First(h => h.Dot);
        var rng = new Random(4);
        for (int i = 0; i < 12; i++) rng.NextDouble(); // the six segments of the cast
        double roll = rng.NextDouble();
        long withLayer = Damage.SkillCalc(roll, 1.0, 0.1, build, 60, 200, "", AttackElement.Poison, extraFinalPct: 3);
        long noLayer = Damage.SkillCalc(roll, 1.0, 0.1, build, 60, 200, "", AttackElement.Poison);
        Assert.AreNotEqual(noLayer, withLayer);
        Assert.AreEqual(withLayer, tick.Damage);
    }

    // Warrior line. Weapon Mastery's WZ text: "Final Damage: +#pdR%": a final damage multiplier, not a damage point (the stat panel's damage % stays 0).
    [TestMethod]
    public void WeaponMastery_IsFinalDamage()
    {
        var mastery = new SkillData { SkillId = SkillData.WeaponMastery, MaxLevel = 10, Kind = SkillData.KindPassive, DmgPct = 2 };
        var mod = mastery.FinalDamageModifier(10);
        Assert.AreEqual(new StatModifier(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.2, StatSource.Skill), mod);
        var snap = StatAggregator.Aggregate(new[] { mod, new StatModifier(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.35, StatSource.Skill, "Heroic") });
        Assert.AreEqual(1.2 * 1.35, DamageChain.FinalDamageMultiplier(snap), 1e-9); // it multiplies with the other final damage sources
        Assert.AreEqual(0, snap.Value(StatKey.DAMAGE_PCT));
        Assert.AreEqual(1.0, mastery.FinalDamageModifier(0).Value); // not learned: neutral
    }
}
