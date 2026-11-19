using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Fire/Poison 2nd job passives (210.img): High Wisdom, Agile Magic, Spell Mastery (stat records and the mastery),
// MP Eater (the chance rolled by the server, the share given back) and Elemental Drain (final damage for each DoT on the target).
// Shares the helpers of FirePoisonCombatTests (the world, the cast, the Energy Bolt data).
public partial class FirePoisonCombatTests
{
    private static IEnumerable<StatModifier> SecondJobMods() => WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0);

    // High Wisdom: INT +8 x 5 = 40. Agile Magic: INT +2 x 10 = 20 and attack speed +2 stages (WZ actionSpeed -2).
    [TestMethod]
    public void HighWisdomAndAgileMagic_AddIntAndAttackSpeed()
    {
        var mods = SecondJobMods().ToList();
        Assert.IsTrue(mods.Contains(new(StatKey.INT, StatValueType.FLAT, 40, StatSource.Skill, "High Wisdom")));
        Assert.IsTrue(mods.Contains(new(StatKey.INT, StatValueType.FLAT, 20, StatSource.Skill, "Agile Magic")));
        Assert.IsTrue(mods.Contains(new(StatKey.ATTACK_SPEED, StatValueType.FLAT, 2, StatSource.Skill, "Agile Magic")));
        var snap = StatAggregator.Aggregate(mods.Append(new(StatKey.INT, StatValueType.FLAT, 1000, StatSource.Base)));
        Assert.AreEqual(1060, snap.IntValue(StatKey.INT));
    }

    // Spell Mastery at level 10: magic attack +10 (x), crit rate +5 (u(10/2)), mastery +50 (10 + 4 x 10).
    [TestMethod]
    public void SpellMastery_AddsMagicAttackCritAndMastery()
    {
        var snap = StatAggregator.Aggregate(SecondJobMods());
        Assert.AreEqual(10, snap.IntValue(StatKey.MATT));
        Assert.AreEqual(5, snap.IntValue(StatKey.CRIT_RATE)); // no wand worn: the skill's 5 only
        Assert.AreEqual(50, snap.IntValue(StatKey.MASTERY));
    }

    // Mastery is the share of the top damage the bottom stands at: the short staff gives 20%, Spell Mastery adds 50, so 70%.
    // The top does not change. Over 100 it stops at 100.
    [TestMethod]
    public void Mastery_FromTheSkill_RaisesTheBottomOfTheRange()
    {
        var plain = DamageChain.Calculate(Mage, 0, 1, 0, 200);
        var trained = DamageChain.Calculate(Mage with { SkillMasteryPct = 50 }, 0, 1, 0, 200);
        Assert.AreEqual(9840, plain.MaxActual); // 1.20 x (4 x 1000 + 100) x 200 / 100
        Assert.AreEqual(1968, plain.MinActual); // 20%
        Assert.AreEqual(9840, trained.MaxActual);
        Assert.AreEqual(6888, trained.MinActual); // 70%
        Assert.AreEqual(9840, DamageChain.Calculate(Mage with { SkillMasteryPct = 500 }, 0, 1, 0, 200).MinActual); // capped at 100%
        // the stat window range reads the same key
        var job = StatTables.Job(StatTables.FirePoisonMage);
        var baseMods = new List<StatModifier>
        {
            new(StatKey.INT, StatValueType.FLAT, 1000, StatSource.Base),
            new(StatKey.LUK, StatValueType.FLAT, 100, StatSource.Base),
            new(StatKey.MATT, StatValueType.FLAT, 200, StatSource.Base),
        };
        var (minPlain, _) = DamageChain.PanelRange(StatAggregator.Aggregate(baseMods), job);
        var (minTrained, _) = DamageChain.PanelRange(StatAggregator.Aggregate(baseMods.Append(new(StatKey.MASTERY, StatValueType.FLAT, 50, StatSource.Skill))), job);
        Assert.IsTrue(minTrained > minPlain);
    }

    // MP Eater at level 9: prop 3 x 9 + 3 = 30, x d(9/2) + 1 = 5, y d(9/3) = 3. Elemental Drain: x 3, y 5.
    [TestMethod]
    public void MpEaterAndElementalDrain_NumbersFromTheFormulas()
    {
        var skills = WzSkillTests.FirePoisonPassives();
        Assert.AreEqual((30, 5, 3), WzSkillStats.MpEater(skills));
        Assert.AreEqual((3, 5), WzSkillStats.ElementalDrain(skills));
    }

    // The mob roll of MP Eater is the 9th double of the cast of Energy Bolt (4 segments x 2 rolls come first). A double under
    // 0.30 absorbs ceil(maxMP x 5%) of the mob's current MP (at least 1) on the first hit of the mob only; otherwise none.
    private static double MpRoll(int seed)
    {
        var rng = new Random(seed);
        for (int i = 0; i < 8; i++) rng.NextDouble();
        return rng.NextDouble();
    }

    // One mob at x 100 (caster at 0), 1e9 HP, level 200, DEF 10%, the given WZ maxMP.
    private static MobWorld WorldWithMp(TempAssets assets, long maxMp, bool boss = false)
    {
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, boss: boss, maxMp: maxMp);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        return world;
    }

    [TestMethod]
    public void MpEater_AbsorbsCeilOfMaxMp_OnTheFirstHit()
    {
        int procSeed = Enumerable.Range(0, 1000).First(s => MpRoll(s) * 100 < 30);
        int missSeed = Enumerable.Range(0, 1000).First(s => MpRoll(s) * 100 >= 30);
        var build = Mage with { MpEaterProp = 30, MpEaterPct = 5, MpEaterBossPct = 3 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = WorldWithMp(assets, 1000);
        var proc = Cast(world, 7, 0, 100, true, new Random(procSeed), build, Bolt, 78);
        Assert.AreEqual(4, proc.Count);
        CollectionAssert.AreEqual(new[] { 50, 0, 0, 0 }, proc.Select(h => h.MpGain).ToArray()); // ceil(1000 x 5%) once, on the first hit
        var miss = Cast(world, 7, 0, 100, true, new Random(missSeed), build, Bolt, 78);
        Assert.IsTrue(miss.All(h => h.MpGain == 0));
    }

    // A boss gives the boss share (y = 3): ceil(1000 x 3%) = 30.
    [TestMethod]
    public void MpEater_Boss_UsesTheBossShare()
    {
        int procSeed = Enumerable.Range(0, 1000).First(s => MpRoll(s) * 100 < 30);
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = WorldWithMp(assets, 1000, boss: true);
        var hits = Cast(world, 7, 0, 100, true, new Random(procSeed), Mage with { MpEaterProp = 30, MpEaterPct = 5, MpEaterBossPct = 3 }, Bolt, 78);
        Assert.AreEqual(30, hits[0].MpGain);
    }

    // A mob with no WZ maxMP (the snails, spores) gives nothing, even when the roll procs.
    [TestMethod]
    public void MpEater_MobWithNoMaxMp_GivesNothing()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = WorldWithMp(assets, 0);
        var hits = Cast(world, 7, 0, 100, true, new Random(5), Mage with { MpEaterProp = 100, MpEaterPct = 5, MpEaterBossPct = 3 }, Bolt, 78);
        Assert.IsTrue(hits.All(h => h.MpGain == 0));
    }

    // A mob with 1 MP: the first proc takes 1 (ceil(1 x 5%)); with 0 MP left the next proc takes nothing.
    [TestMethod]
    public void MpEater_EmptyMob_GivesNothingOnTheNextProc()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = WorldWithMp(assets, 1);
        var build = Mage with { MpEaterProp = 100, MpEaterPct = 5, MpEaterBossPct = 3 };
        Assert.AreEqual(1, Cast(world, 7, 0, 100, true, new Random(5), build, Bolt, 78)[0].MpGain);
        Assert.AreEqual(0, Cast(world, 7, 0, 100, true, new Random(5), build, Bolt, 78)[0].MpGain);
    }

    // Not learned (prop 0): no roll is spent, so the damage rolls and the random stream are as before.
    [TestMethod]
    public void MpEater_NotLearned_SpendsNoRoll_AndGivesNothing()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (100, 100));
        var rng = new Random(11);
        var hits = Cast(world, 7, 0, 100, true, rng, Mage, Bolt, 78);
        Assert.IsTrue(hits.All(h => h.MpGain == 0));
        var shadow = new Random(11);
        for (int i = 0; i < 8; i++) shadow.NextDouble();
        Assert.AreEqual(shadow.NextDouble(), rng.NextDouble());
    }

    // Elemental Drain: +3% final damage for each DoT on the target, 0 to 5 layers; the 6th DoT adds nothing. The same rolls
    // (seed 3) with 0..6 DoTs give the damage with extraFinalPct 0, 3, 6, 9, 12, 15, 15.
    [TestMethod]
    public void ElementalDrain_ZeroToFiveLayers_SixthDotAddsNothing()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        for (int dots = 1; dots <= 6; dots++) assets.AddWzSkill(DotSkillData(2100000 + dots));
        var world = World(assets, (100, 100));
        var damage = new List<long>();
        for (int dots = 0; dots <= 6; dots++)
        {
            if (dots > 0) world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100000 + dots, 100); // another DoT skill: another layer
            var hit = Cast(world, 7, 0, 100, true, new Random(3), build, Bolt, 78)[0];
            var rng = new Random(3);
            Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraFinalPct: 3 * Math.Min(dots, 5)), hit.Damage, $"{dots} DoTs");
            damage.Add(hit.Damage);
        }
        Assert.IsTrue(damage[0] < damage[1] && damage[4] < damage[5]);
        Assert.AreEqual(damage[5], damage[6]);
        Assert.IsTrue(damage[5] > damage[0] * 1.14 && damage[5] < damage[0] * 1.16); // about +15%
    }

    // Not learned: DoTs on the mob change nothing.
    [TestMethod]
    public void ElementalDrain_NotLearned_NoBonus()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        for (int dots = 1; dots <= 3; dots++) assets.AddWzSkill(DotSkillData(2100000 + dots));
        var world = World(assets, (100, 100));
        var none = Cast(world, 7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0].Damage;
        for (int dots = 1; dots <= 3; dots++) world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100000 + dots, 100);
        Assert.AreEqual(none, Cast(world, 7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0].Damage);
    }

    // The layers count the DoTs on the mobs near the caster (a circle of 800), not the ones far away: a DoT on a mob 1000 px
    // away adds nothing; the same DoT on a mob 100 px away adds one layer.
    [TestMethod]
    public void ElementalDrain_CountsNearbyMobs_NotFarOnes()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(DotSkillData(2100001));
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100},{"MobId":100000,"X":1000,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.SkillStrike(7, 1000, 100, true, new Random(9), Mage, 2100001, 100); // a DoT on the far mob only
        var rng = new Random(3);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200), Cast(world, 7, 0, 100, true, new Random(3), build, Bolt, 78)[0].Damage);
        world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100001, 100); // a DoT on the near mob: one layer
        rng = new Random(3);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraFinalPct: 3), Cast(world, 7, 0, 100, true, new Random(3), build, Bolt, 78)[0].Damage);
    }

    // A basic swing (not a skill) also gets Elemental Drain's final damage: Damage.CalcResult reads extraFinalPct.
    [TestMethod]
    public void ElementalDrain_BasicSwing_GetsTheBonus()
    {
        var build = Mage with { DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(DotSkillData(2100001));
        assets.AddMap(7, """[{"MobId":100000,"X":60,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.SkillStrike(7, 60, 100, true, new Random(9), Mage, 2100001, 100); // a DoT on the near mob
        var hit = world.Strike(7, 0, 100, true, new Random(3), build)[0];
        var rng = new Random(3);
        Assert.AreEqual(Damage.Calc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 200, extraFinalPct: 3), hit.Damage);
    }

    // Over the wire: the Build carries MP Eater's numbers, the server absorbs ceil(maxMP x 5%) from the mob and sends it as an
    // int mpGain on the first hit of the mob; the other player sees the same hit. The caster's client then adds that much MP.
    [TestMethod]
    public void Loopback_MpEater_ServerSendsTheAbsorbedMpOnTheFirstHit()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200, maxMp: 1000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 75);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 75);
        b.Connect("127.0.0.1", Net.Port + 75);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        WaitSpawn(b);
        a.SendBuild(Mage with { SkillMasteryPct = 50, MpEaterProp = 100, MpEaterPct = 5, MpEaterBossPct = 3, DrainPct = 3, DrainMax = 5 }); // the 97-byte Build; every cast procs
        a.SendSkillCast(Bolt, 78);
        var hits = Enumerable.Range(0, 4).Select(_ => WaitHit(a)).ToList();
        CollectionAssert.AreEqual(new[] { 50, 0, 0, 0 }, hits.Select(h => h.MpGain).ToArray());
        var seen = Enumerable.Range(0, 4).Select(_ => WaitHit(b)).ToList();
        CollectionAssert.AreEqual(new[] { 50, 0, 0, 0 }, seen.Select(h => h.MpGain).ToArray());
    }

    // Over the wire: the server tells the caster how many Elemental Drain layers are near him (DrainStacks, owner only).
    [TestMethod]
    public void Loopback_ElementalDrain_ServerSendsTheLayersNearTheCaster()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 76);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 76);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        a.SendBuild(Mage with { DrainPct = 3, DrainMax = 5 });
        a.SendSkillCast(Breath, 100); // Poison Breath puts a DoT on the near mob
        WaitDrain(a, 1);
    }
}
