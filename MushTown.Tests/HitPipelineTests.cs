using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The hit pipeline (HitPipeline): statuses first, damage after. A skill that puts a bad status on a mob has it on the mob before its first
// segment is rolled, so the first hit already carries what the status gives. Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    // Poison Breath puts its own DoT on the mob before the first of its 6 segments is rolled: every segment, the first too, takes Burning Magic's
    // final damage. The mob had no DoT before; a DoT already on it (another skill) does not add a second time. A build without Burning Magic gets nothing.
    [TestMethod]
    public void PoisonBreath_FirstSegment_TakesBurningMagic_WithOrWithoutAnotherDot()
    {
        var trained = Mage with { DotFinalPct = 15 };
        List<MobWorld.Hit> Breathe(Build build, bool withOtherDot)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(BreathData());
            assets.AddWzSkill(DotSkillData(2100001));
            var world = OneMob(assets, 1_000_000_000);
            if (withOtherDot) world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100001, 100);
            return world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180);
        }
        foreach (bool withOtherDot in new[] { false, true })
        {
            var hits = Breathe(trained, withOtherDot);
            var rng = new Random(4);
            Assert.AreEqual(6, hits.Count);
            foreach (var hit in hits)
                Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, trained, 180, 200, "", AttackElement.Poison, extraFinalPct: 15), hit.Damage, $"other DoT {withOtherDot}");
        }
        var plain = Breathe(Mage, false);
        var rng2 = new Random(4);
        Assert.AreEqual(Damage.SkillCalc(rng2.NextDouble(), rng2.NextDouble(), 0.1, Mage, 180, 200, "", AttackElement.Poison), plain[0].Damage);
    }

    // The same order for Elemental Drain: the DoT this cast puts on the mob is a layer from the first segment (3% for one layer).
    [TestMethod]
    public void PoisonBreath_FirstSegment_CountsItsOwnDotForElementalDrain()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000);
        var first = world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180)[0];
        var rng = new Random(4);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, build, 180, 200, "", AttackElement.Poison, extraFinalPct: 3), first.Damage);
    }

    // A tick takes Burning Magic also at the end: the last tick comes after the DoT left the mob's list, and the DoT it comes from is still its status.
    [TestMethod]
    public void DotTick_LastTick_StillTakesBurningFinal()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(dotTime: 1));
        var world = OneMob(assets, 1_000_000_000);
        var build = Mage with { DotFinalPct = 15 };
        world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180);
        var rng = new Random(4);
        for (int i = 0; i < 12; i++) rng.NextDouble();
        var tick = Ticks(world, 1000).Single(); // the only tick, at the end of the 1 s
        Assert.IsTrue(tick.Dot);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), 1.0, 0.1, build, 60, 200, "", AttackElement.Poison, extraFinalPct: 15), tick.Damage);
    }

    // A mob that is immune to the skill's element (poison S1) gets no DoT: nothing of the DoT counts for its segments (no Burning Magic, no Drain layer).
    [TestMethod]
    public void PoisonBreath_ImmuneMob_GetsNoDot_AndNoBonusFromIt()
    {
        var build = Mage with { DotFinalPct = 15, DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000, elemAttr: "S1");
        var hits = world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180);
        var rng = new Random(4);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, build, 180, 200, "S1", AttackElement.Poison), hits[0].Damage);
        Assert.AreEqual(0, Ticks(world, 3000).Count);
        Assert.AreEqual(0, world.DrainLayers(7, 0, 100, 5));
    }

    // A cast puts statuses only on the mobs it reaches. Two mobs, only one inside the box of Poison Breath: the mob outside gets no DoT (no Drain
    // layer around it, no tick of its own) and no Weakness mark (a later hit on it is plain); the mob inside gets them.
    [TestMethod]
    public void Cast_PutsStatusOnlyOnTheMobItReaches()
    {
        var build = Mage with { WeaknessProp = 100, DotFinalPct = 15 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(WeaknessData());
        var world = World(assets, (100, 100), (1000, 100)); // the box is 200 wide: only the first is inside
        int near = world.MobsOn(7).Single(m => m.X < 500).Uid, far = world.MobsOn(7).Single(m => m.X > 500).Uid;
        Assert.AreEqual(6, world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180).Count);
        var ticks = Ticks(world, 3000);
        Assert.IsTrue(ticks.Count > 0 && ticks.All(t => t.Uid == near), "only the mob inside ticks");
        Assert.AreEqual(0, world.DrainLayers(7, 1000, 100, 5)); // around the far mob there is no DoT
        Assert.AreEqual(1, world.DrainLayers(7, 100, 100, 5));
        var rng = new Random(3);
        long plain = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200);
        var hit = Cast(world, 7, 900, 100, true, new Random(3), Mage, Bolt, 78)[0];
        Assert.AreEqual(far, hit.Uid);
        Assert.AreEqual(plain, hit.Damage); // no mark: a plain hit
    }

    // The same on the objects: one pipeline attack with two mobs in the world and only one of them a target. The other Mob object has no status at all.
    [TestMethod]
    public void Resolve_MobNotAmongTheTargets_HasNoStatusAtAll()
    {
        var build = Mage with { WeaknessProp = 100, MarkProp = 100 };
        Mob NewMob(int uid) => new() { Uid = uid, Map = 7, Level = 200, Def = 0.1, Hp = 1_000_000_000_000, MaxHp = 1_000_000_000_000 };
        var hit = NewMob(1);
        var outside = NewMob(2);
        var pipeline = StatusPipeline(new List<Mob> { hit, outside }, build, (_, _, _) => { });
        pipeline.Resolve(new[] { hit }, StatusAttack(build));
        Assert.AreEqual(1, hit.Statuses.DotCount);
        Assert.AreEqual(1, hit.Statuses.MarkStacks(5));
        Assert.AreEqual(20, hit.Statuses.Value(MobStatusKind.ElementalWeakness));
        Assert.AreEqual(0, outside.Statuses.DotCount);
        Assert.AreEqual(0, outside.Statuses.MarkStacks(5));
        Assert.AreEqual(0, outside.Statuses.Value(MobStatusKind.ElementalWeakness));
        Assert.IsFalse(outside.Statuses.HasBurningTarget);
    }

    // A hit that kills the mob leaves no status on that Mob object: no DoT layer, no mark stack, no Weakness mark. The same attack on a mob that lives
    // does leave all three (so the zeros are the kill, not a missing status).
    [TestMethod]
    public void Resolve_KilledMob_HasNoStatusOnItsObject()
    {
        var build = Mage with { WeaknessProp = 100, MarkProp = 100 };
        Mob NewMob(long hp) => new() { Uid = 1, Map = 7, Level = 200, Def = 0.1, Hp = hp, MaxHp = 1_000_000_000_000 };
        var survivor = NewMob(1_000_000_000_000);
        StatusPipeline(new List<Mob> { survivor }, build, (_, _, _) => { }).Resolve(new[] { survivor }, StatusAttack(build));
        Assert.AreEqual(1, survivor.Statuses.DotCount);
        Assert.AreEqual(1, survivor.Statuses.MarkStacks(5));
        Assert.AreEqual(20, survivor.Statuses.Value(MobStatusKind.ElementalWeakness));
        var victim = NewMob(1);
        int kills = 0;
        var hits = StatusPipeline(new List<Mob> { victim }, build, (_, _, _) => kills++).Resolve(new[] { victim }, StatusAttack(build));
        Assert.AreEqual(1, kills);
        Assert.AreEqual(0, hits[0].HpLeft);
        Assert.AreEqual(0, victim.Statuses.DotCount);
        Assert.AreEqual(0, victim.Statuses.MarkStacks(5));
        Assert.AreEqual(0, victim.Statuses.Value(MobStatusKind.ElementalWeakness));
        Assert.IsFalse(victim.Statuses.HasBurningTarget);
    }

    // A pipeline with the WZ numbers of Elemental Weakness (+20) and Empirical Knowledge (3 stacks, 10 s, 5 / 5) and no world.
    private static HitPipeline StatusPipeline(List<Mob> mobs, Build build, Action<Mob, Random, int> kill)
    {
        var mark = new WzSkillData { SkillId = WzSkillData.EmpiricalKnowledge, MaxLevel = 9, Kind = SkillData.KindPassive };
        mark.Common["x"] = SkillFormula.Levels("3", 9);
        mark.Common["time"] = SkillFormula.Levels("10", 9);
        mark.Common["y"] = SkillFormula.Levels("5", 9);
        mark.Common["w"] = SkillFormula.Levels("5", 9);
        var pipeline = new HitPipeline(id => id == WzSkillData.ElementalWeakness ? WeaknessData() : mark, mobs, kill, _ => { });
        pipeline.Prepare(build);
        return pipeline;
    }

    // A magic attack of attacker 5 that puts a DoT (Poison, 60%, 10 s) on its targets.
    private static HitPipeline.Attack StatusAttack(Build build) => new()
    {
        Build = build,
        Rng = new Random(2),
        Attacker = 5,
        Skill = Breath,
        Pct = 180,
        Element = AttackElement.Poison,
        Segments = 6,
        Magic = true,
        Dot = new Dot { Skill = Breath, Attacker = 5, Build = build, Pct = 60, Element = AttackElement.Poison, Rng = new Random(2), IntervalMs = 1000, MsLeft = 10_000 },
    };

    // A mob this very hit kills keeps no status: the DoT does not tick, the Weakness mark and the DoT layer are gone with it, and the mob that grows
    // back in its place is clean (a plain hit, no layer).
    [TestMethod]
    public void KilledMob_KeepsNoStatus_TheRespawnIsClean()
    {
        var build = Mage with { WeaknessProp = 100, DotFinalPct = 15, DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(WeaknessData());
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1, 0.1, 0, speed: 1, level: 200); // 1 HP: the first segment kills
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hits = world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180);
        Assert.AreEqual(6, hits.Count);
        Assert.AreEqual(0, hits[0].HpLeft);
        Assert.AreEqual(0, world.DrainLayers(7, 0, 100, 5)); // the dead mob has no DoT layer
        Assert.AreEqual(0, Ticks(world, 3000).Count);
        Ticks(world, 8000); // the grave: the respawn comes 10 s after the death
        Assert.AreEqual(1, world.MobsOn(7).Count);
        Assert.AreEqual(0, world.DrainLayers(7, 0, 100, 5));
        Assert.AreEqual(0, Ticks(world, 3000).Count);
        var rng = new Random(3);
        long plain = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200); // no mark: no +20 damage %
        Assert.AreEqual(plain, Cast(world, 7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0].Damage);
    }

    // Statuses first for the Elemental Weakness mark: the cast that marks already takes +20 damage % on its first segment (the mark is rolled in step 1);
    // the mark then stays on the mob. (The seed rolls: the mark roll is the first double, the segments follow.)
    [TestMethod]
    public void WeaknessMark_FirstHitCarriesIt_ThenLands()
    {
        var build = Mage with { WeaknessProp = 100 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(WeaknessData());
        var world = World(assets, (100, 100));
        var first = Cast(world, 7, 0, 100, true, new Random(3), build, Bolt, 78)[0];
        var rng = new Random(3);
        rng.NextDouble(); // the mark roll
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, build, 78, 200, extraDmgPct: 20), first.Damage);
        var later = Cast(world, 7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0]; // no roll now: the mark is already on the mob
        rng = new Random(3);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraDmgPct: 20), later.Damage);
    }

    // Step 2 is worked out once for one target and one attack: 6 segments on 2 targets make 2 step-2 results (not 12), and every segment only rolls
    // its two dice. The total and each number equal the old way, the whole chain worked out again for each segment (DamageChain.Calculate).
    [TestMethod]
    public void Resolve_ManySegments_StepTwoOncePerTarget_SameNumbersAsRecalculating()
    {
        Mob NewMob(int uid, double def) => new() { Uid = uid, Map = 7, Level = 200, Def = def, Hp = 1_000_000_000_000, MaxHp = 1_000_000_000_000 };
        var mobs = new List<Mob> { NewMob(1, 0.1), NewMob(2, 0.3) };
        mobs[0].Statuses.Add(MobStatusKind.Stun, 0, 60_000); // a status Burning Magic counts: the first target has +15% final damage
        var build = Mage with { MagicDmgPct = 50, DotFinalPct = 15, CritPct = 50 };
        var pipeline = new HitPipeline(_ => throw new InvalidOperationException("no file"), mobs, (_, _, _) => { }, _ => { });
        var hits = pipeline.Resolve(mobs, new HitPipeline.Attack
        {
            Build = build,
            Rng = new Random(8),
            Skill = Bolt,
            Pct = 78,
            Element = AttackElement.Fire,
            Segments = 6,
            Magic = true,
        });
        Assert.AreEqual(12, hits.Count);
        Assert.AreEqual(2, pipeline.Step2Count);
        var rng = new Random(8);
        for (int t = 0; t < 2; t++)
        {
            long total = 0;
            for (int i = 0; i < 6; i++)
            {
                var expected = Damage.SkillResult(rng.NextDouble(), rng.NextDouble(), mobs[t].Def, build, 78, 200, "", AttackElement.Fire,
                    extraDmgPct: 50, extraFinalPct: t == 0 ? 15 : 0);
                Assert.AreEqual(expected.TotalDamage, hits[t * 6 + i].Damage, $"target {t} segment {i}");
                Assert.AreEqual(expected.CritMultiplier > 1.0, hits[t * 6 + i].Crit);
                total += expected.TotalDamage;
            }
            Assert.AreEqual(1_000_000_000_000 - total, hits[t * 6].HpLeft);
        }
        Assert.IsTrue(hits.Take(6).Concat(hits.Skip(6)).Select(h => h.Crit).Distinct().Count() == 2, "the seed gives both crits and plain hits");
    }
}
