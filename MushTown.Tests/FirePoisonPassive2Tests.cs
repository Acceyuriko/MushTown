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
    // 0.30 gives the share back (5% of our max MP) on the first hit of the mob only; otherwise none. Fixed seeds.
    private static double MpRoll(int seed)
    {
        var rng = new Random(seed);
        for (int i = 0; i < 8; i++) rng.NextDouble();
        return rng.NextDouble();
    }

    [TestMethod]
    public void MpEater_RollsOncePerMob_ThirtyPercent_FiveOfMaxMp()
    {
        int procSeed = Enumerable.Range(0, 1000).First(s => MpRoll(s) * 100 < 30);
        int missSeed = Enumerable.Range(0, 1000).First(s => MpRoll(s) * 100 >= 30);
        var build = Mage with { MpEaterProp = 30, MpEaterPct = 5, MpEaterBossPct = 3 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (100, 100));
        var proc = Cast(world, 7, 0, 100, true, new Random(procSeed), build, Bolt, 78);
        Assert.AreEqual(4, proc.Count);
        CollectionAssert.AreEqual(new[] { 5, 0, 0, 0 }, proc.Select(h => h.MpPct).ToArray()); // once, on the first hit
        var miss = Cast(world, 7, 0, 100, true, new Random(missSeed), build, Bolt, 78);
        Assert.IsTrue(miss.All(h => h.MpPct == 0));
        // the client adds that share of its max MP: 5% of 54,450 = 2,722 (rounded down)
        Assert.AreEqual(2722L, 54_450L * proc[0].MpPct / 100);
    }

    // A boss gives the boss share (y = 3).
    [TestMethod]
    public void MpEater_Boss_UsesTheBossShare()
    {
        int procSeed = Enumerable.Range(0, 1000).First(s => MpRoll(s) * 100 < 30);
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, boss: true);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hits = Cast(world, 7, 0, 100, true, new Random(procSeed), Mage with { MpEaterProp = 30, MpEaterPct = 5, MpEaterBossPct = 3 }, Bolt, 78);
        Assert.AreEqual(3, hits[0].MpPct);
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
        Assert.IsTrue(hits.All(h => h.MpPct == 0));
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

    // Over the wire: the Build carries MP Eater's numbers, the server rolls the proc and sends the share on the first hit of the
    // mob (MobHit byte 43); the other player sees the same hit. The caster's client then adds that share of its own max MP.
    [TestMethod]
    public void Loopback_MpEater_ServerSendsTheShareOnTheFirstHit()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
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
        CollectionAssert.AreEqual(new[] { 5, 0, 0, 0 }, hits.Select(h => h.MpPct).ToArray());
        var seen = Enumerable.Range(0, 4).Select(_ => WaitHit(b)).ToList();
        CollectionAssert.AreEqual(new[] { 5, 0, 0, 0 }, seen.Select(h => h.MpPct).ToArray());
    }
}
