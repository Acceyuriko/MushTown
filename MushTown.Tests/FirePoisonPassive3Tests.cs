using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Fire/Poison 3rd job passives (211.img): Magic Critical and Elemental Decrease (stat records, crit and final damage, the elemental
// resistance), Element Amplification (MP cost and damage % of the magic skills), Burning Magic (longer DoT, final damage on a mob with
// a DoT). Teleport Boost is in TeleportTests. Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private static StatSnapshot ThirdJobSnapshot() => StatAggregator.Aggregate(WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0));

    // The WZ formulas at max level: Magic Critical (10) cr 30 and criticaldamage 13; Elemental Decrease (9) mdR 40 and u 10;
    // Element Amplification (10) costmpR 50 and damR 50; Burning Magic (10) x 100 and z 15.
    [TestMethod]
    public void ThirdJobPassives_StatRecords_FromTheFormulas()
    {
        var snap = ThirdJobSnapshot();
        Assert.AreEqual(35, snap.IntValue(StatKey.CRIT_RATE)); // Magic Critical's 30 and Spell Mastery's 5
        Assert.AreEqual(13, snap.IntValue(StatKey.CRIT_DAMAGE));
        Assert.AreEqual(1.40, DamageChain.FinalDamageMultiplier(snap), 1e-9); // final damage +40%: a multiplier
        Assert.AreEqual(10, snap.IntValue(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT));
        Assert.AreEqual(50, snap.IntValue(StatKey.MP_COST_PCT));
        Assert.AreEqual(50, snap.IntValue(StatKey.MAGIC_DAMAGE_PCT));
        Assert.AreEqual(100, snap.IntValue(StatKey.DOT_TIME_PCT));
        Assert.AreEqual(15, snap.IntValue(StatKey.DOT_FINAL_PCT));
    }

    // The stat window rows read the same snapshot: final damage +40%, ignore elemental resistance 10%, crit damage 13%.
    [TestMethod]
    public void ThirdJobPassives_StatWindowRows()
    {
        var snap = ThirdJobSnapshot();
        Assert.AreEqual("40.00%", StatDisplay.Text(snap, StatDisplay.FinalDamageRow));
        Assert.AreEqual("10.00%", StatDisplay.Text(snap, 19));
        Assert.AreEqual("13.00%", StatDisplay.Text(snap, 15));
        Assert.AreEqual("35%", StatDisplay.Text(snap, 13));
    }

    // Magic Critical: the damage number of a crit grows by the crit damage (50 + 13 = +63% instead of +50%).
    [TestMethod]
    public void MagicCritical_CritDamage_RaisesACrit()
    {
        var crit = Mage with { CritPct = 100 };
        long plain = Damage.SkillCalc(0.5, 0.0, 0.1, crit, 100, 200);
        long trained = Damage.SkillCalc(0.5, 0.0, 0.1, crit with { CritDamage = 13 }, 100, 200);
        Assert.AreEqual(plain * 1.63 / 1.5, trained, plain * 0.001);
        Assert.AreEqual(1.63, DamageChain.CritMultiplier(true, 13), 1e-9);
        Assert.AreEqual(1.0, DamageChain.CritMultiplier(false, 13), 1e-9);
    }

    // Elemental Decrease: final damage +40% (a multiplier on the whole hit).
    [TestMethod]
    public void ElementalDecrease_FinalDamage_RaisesTheHit()
    {
        long plain = Damage.SkillCalc(0.5, 1.0, 0.1, Mage, 100, 200);
        long trained = Damage.SkillCalc(0.5, 1.0, 0.1, Mage with { FinalDamagePct = 40 }, 100, 200);
        Assert.AreEqual(plain * 1.4, trained, plain * 0.001);
    }

    // Elemental Decrease: -10% enemy elemental resistance. A resisting mob (S2: x0.5) takes 1 - 0.5 x 0.9 = 0.55; a weak one (x1.5) and a
    // normal one (x1) do not change; a mob that is immune (S1: x0) takes 1 - 1 x 0.9 = 0.1.
    [TestMethod]
    public void ElementalDecrease_ElementResistance_ShrinksTheCutOfAResistingMob()
    {
        var trained = Mage with { IgnoreElemPct = 10 };
        Assert.AreEqual(0.5, Damage.SkillResult(0.5, 1.0, 0.1, Mage, 100, 200, "S2", AttackElement.Poison).ElementMultiplier, 1e-9);
        Assert.AreEqual(0.55, Damage.SkillResult(0.5, 1.0, 0.1, trained, 100, 200, "S2", AttackElement.Poison).ElementMultiplier, 1e-9);
        Assert.AreEqual(1.5, Damage.SkillResult(0.5, 1.0, 0.1, trained, 100, 200, "S3", AttackElement.Poison).ElementMultiplier, 1e-9);
        Assert.AreEqual(1.0, Damage.SkillResult(0.5, 1.0, 0.1, trained, 100, 200, "", AttackElement.Poison).ElementMultiplier, 1e-9);
        Assert.AreEqual(0.1, Damage.SkillResult(0.5, 1.0, 0.1, trained, 100, 200, "S1", AttackElement.Poison).ElementMultiplier, 1e-9);
        Assert.IsTrue(Damage.SkillCalc(0.5, 1.0, 0.1, trained, 100, 200, "S2", AttackElement.Poison) > Damage.SkillCalc(0.5, 1.0, 0.1, Mage, 100, 200, "S2", AttackElement.Poison));
    }

    // Element Amplification: the MP of a damage skill rises by costmpR (24 MP at +50% is 36); the cast is refused when the MP is short (the MP and the
    // cooldown stay untouched). A skill with no damage line (a buff, Teleport, Mana Wave) costs what it costs; without the passive (0) nothing changes.
    [TestMethod]
    public void ElementAmplification_RaisesTheMpCostOfADamageSkill_ShortMpRefuses()
    {
        var cost = new SkillCost(new Dictionary<int, SkillData>());
        var bolt = BoltData(); // mpCon 24, damage 78
        var short35 = new CharaSlot { Mp = 35 };
        var cooldowns = new Dictionary<int, float>();
        Assert.IsFalse(cost.Pay(short35, bolt, 20, cooldowns, 50)); // 36 needed
        Assert.AreEqual(35, short35.Mp);
        Assert.IsFalse(cooldowns.ContainsKey(Bolt));
        Assert.IsTrue(cost.Pay(short35, bolt, 20, cooldowns)); // without the passive 24 is enough
        Assert.AreEqual(11, short35.Mp);
        var exact = new CharaSlot { Mp = 36 };
        Assert.IsTrue(cost.Pay(exact, bolt, 20, new Dictionary<int, float>(), 50));
        Assert.AreEqual(0, exact.Mp);
        var buff = new WzSkillData { SkillId = WzSkillData.Meditation, MaxLevel = 1, Kind = SkillData.KindBuff };
        buff.Common["mpCon"] = SkillFormula.Levels("24", 1);
        var buffer = new CharaSlot { Mp = 100 };
        Assert.IsTrue(cost.Pay(buffer, buff, 1, new Dictionary<int, float>(), 50));
        Assert.AreEqual(76, buffer.Mp); // a buff is not a damage skill: no extra
    }

    // The raised cost is rounded down: 25 MP at +50% pays 37.
    [TestMethod]
    public void ElementAmplification_MpCostRoundsDown()
    {
        var cost = new SkillCost(new Dictionary<int, SkillData>());
        var skill = BoltData();
        skill.Common["mpCon"] = SkillFormula.Levels("25", 20);
        var chara = new CharaSlot { Mp = 100 };
        Assert.IsTrue(cost.Pay(chara, skill, 20, new Dictionary<int, float>(), 50));
        Assert.AreEqual(63, chara.Mp);
    }

    // Element Amplification: a magic skill hit takes +50 damage % (the damage % sum, not final damage); a basic swing does not.
    [TestMethod]
    public void ElementAmplification_RaisesMagicSkillDamage_NotTheBasicSwing()
    {
        var trained = Mage with { MagicDmgPct = 50 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (50, 100));
        var plain = Cast(world, 7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0].Damage;
        var boosted = Cast(world, 7, 0, 100, true, new Random(3), trained, Bolt, 78)[0].Damage;
        var rng = new Random(3);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraDmgPct: 50), boosted);
        Assert.AreEqual(plain * 1.5, boosted, plain * 0.01); // 1 + 50 / 100 on a build with no other damage %
        var swingPlain = world.Strike(7, 0, 100, true, new Random(3), Mage)[0].Damage;
        var swingTrained = world.Strike(7, 0, 100, true, new Random(3), trained)[0].Damage;
        Assert.AreEqual(swingPlain, swingTrained);
    }

    // Burning Magic: the DoT of a cast lasts dotTime x (1 + x%) = 10 s x 2 = 20 s at +100%, one tick a second; without it 10 s.
    [TestMethod]
    public void BurningMagic_LongerDot()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var plain = OneMob(assets, 1_000_000_000);
        plain.SkillStrike(7, 0, 100, true, new Random(4), Mage, Breath, 180);
        Assert.AreEqual(10, Enumerable.Range(0, 30).Sum(_ => Ticks(plain, 1000).Count));
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(BreathData());
        var trained = OneMob(assets2, 1_000_000_000);
        trained.SkillStrike(7, 0, 100, true, new Random(4), Mage with { DotTimePct = 100 }, Breath, 180);
        Assert.AreEqual(20, Enumerable.Range(0, 30).Sum(_ => Ticks(trained, 1000).Count));
    }

    // Burning Magic: +15% final damage on a hit against a mob that has a DoT (from the Elemental Drain test's shape: another DoT skill is put on
    // the mob first). The mob with no DoT, and a build that has not learned it, get nothing.
    [TestMethod]
    public void BurningMagic_FinalDamage_OnlyOnAMobWithADot()
    {
        var trained = Mage with { DotFinalPct = 15 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(DotSkillData(2100001));
        var world = World(assets, (50, 100));
        long Roll(Build b) { var hit = Cast(world, 7, 0, 100, true, new Random(3), b, Bolt, 78)[0]; return hit.Damage; }
        var rng = new Random(3);
        long expectedPlain = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200);
        Assert.AreEqual(expectedPlain, Roll(trained)); // no DoT on the mob yet: nothing
        world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100001, 100); // the mob gets a DoT
        rng = new Random(3);
        long expectedBoosted = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraFinalPct: 15);
        Assert.AreEqual(expectedBoosted, Roll(trained));
        Assert.IsTrue(expectedBoosted > expectedPlain);
        Assert.AreEqual(expectedPlain, Roll(Mage)); // not learned: nothing, even with the DoT on
        var swingPlain = world.Strike(7, 0, 100, true, new Random(3), Mage)[0].Damage;
        var swing = world.Strike(7, 0, 100, true, new Random(3), trained)[0].Damage; // a basic swing is an attack too
        rng = new Random(3);
        Assert.AreEqual(Damage.CalcResult(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 200, extraFinalPct: 15).TotalDamage, swing);
        Assert.IsTrue(swing > swingPlain);
    }

    // Final damage sources multiply: Elemental Drain (3% a layer, one DoT = 3%) and Burning Magic (15%) give 1.03 x 1.15.
    [TestMethod]
    public void BurningMagic_AndElementalDrain_Multiply()
    {
        var build = Mage with { DotFinalPct = 15, DrainPct = 3, DrainMax = 5 };
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(DotSkillData(2100001));
        var world = World(assets, (50, 100));
        world.SkillStrike(7, 0, 100, true, new Random(9), Mage, 2100001, 100);
        var hit = Cast(world, 7, 0, 100, true, new Random(3), build, Bolt, 78)[0];
        var rng = new Random(3);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraFinalPct: (1.03 * 1.15 - 1) * 100), hit.Damage);
    }

    // The DoT tick takes the Element Amplification damage % (damage bucket) and Burning Magic's final damage (final bucket): the mob has the DoT the
    // tick comes from. The same tick (same seed) of a build that learned Burning Magic and one that did not: the ratio is 1.15.
    [TestMethod]
    public void DotTick_TakesElementAmplification_AndBurningFinal()
    {
        long Tick(Build build)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(BreathData());
            var world = OneMob(assets, 1_000_000_000);
            world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180);
            return Ticks(world, 1000).Single().Damage;
        }
        var amplified = Mage with { MagicDmgPct = 50 };
        var rng = new Random(4);
        for (int i = 0; i < 12; i++) rng.NextDouble(); // the 6 segments' rolls
        double roll = rng.NextDouble();
        long without = Tick(amplified);
        long with = Tick(amplified with { DotFinalPct = 15 });
        Assert.AreEqual(Damage.SkillCalc(roll, 1.0, 0.1, amplified, 60, 200, "", AttackElement.Poison, extraDmgPct: 50), without);
        Assert.AreEqual(Damage.SkillCalc(roll, 1.0, 0.1, amplified, 60, 200, "", AttackElement.Poison, extraDmgPct: 50, extraFinalPct: 15), with);
        Assert.AreEqual(without * 1.15, with, without * 0.001);
    }

    // Over the wire: the server judges with the Build it was sent. Steady (mastery 100: the min and the max are the same to 2 points) makes the
    // damage predictable: +100% damage from Element Amplification doubles the hit.
    [TestMethod]
    public void Loopback_ThirdJobBuildFields_ReachTheServerJudge()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 300));
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 90);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 90);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        a.SendBuild(Steady with { MagicDmgPct = 100, DotTimePct = 100, DotFinalPct = 15, IgnoreElemPct = 10 });
        a.SendSkillCast(Breath, 180);
        var hit = WaitHit(a);
        long expected = Damage.SkillCalc(0.5, 1.0, 0.1, Steady, 180, 200, "", AttackElement.Poison, extraDmgPct: 100, extraFinalPct: 15); // the first segment already has the DoT: Burning Magic
        Assert.AreEqual(expected, hit.Damage, 2);
        Assert.IsTrue(hit.Damage > Damage.SkillCalc(0.5, 1.0, 0.1, Steady, 180, 200, "", AttackElement.Poison) * 1.5);
    }
}
