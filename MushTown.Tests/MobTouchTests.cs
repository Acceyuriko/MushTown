using MushTown.Shared;

namespace MushTown.Tests;

// The mob touch formula (research/mob-touch-damage.md section 7): A, B, the Arcane Force table, the
// 68% / 80% DEF cap, the damage-taken zone. The hand numbers are the research's; roll 0 is the min, roll 1 the max.
[TestClass]
public class MobTouchTests
{
    private const int HappyErda = 12825; // Touch of Happy Erda (level 200)
    private const int Mushmom = 123;     // Touch of Mushmom (level 18, boss)

    private static long Hit(long atk, int mobLevel, int playerLevel, double def, double roll, int barrier = 0, int arc = 0) =>
        MobTouch.Damage(atk, mobLevel, playerLevel, def, barrier, arc, roll);

    // Happy Erda, same level (A 0.85, B 1), research 7.6 example 1.
    [DataTestMethod]
    [DataRow(105, 9176, 10812)]
    [DataRow(1000, 8416, 10051)]
    [DataRow(3000, 6716, 8351)]
    [DataRow(0, 9266, 10901)]
    [DataRow(20000, 1853, 2180)] // the DEF cap: 0.85 x (10901.25 - 8721) and 0.85 x (12825 - 10260)
    public void HappyErda_SameLevel_MinAndMax(int def, int min, int max)
    {
        Assert.AreEqual(min, Hit(HappyErda, 200, 200, def, 0));
        Assert.AreEqual(max, Hit(HappyErda, 200, 200, def, 1));
    }

    [TestMethod]
    public void Roll_PicksBetweenTheMinAndTheMax()
    {
        long mid = Hit(HappyErda, 200, 200, 105, 0.5);

        Assert.AreEqual(9994, mid); // 9176.8 + 0.5 x (10812 - 9176.8) = 9994.4
    }

    // Mushmom (level 18): alpha +182, A 0.775, B 1. DEF 105 is already over the 68% cap, so 105 and 3000 agree.
    [TestMethod]
    public void Mushmom_DoesAboutTenToTwenty()
    {
        foreach (int def in new[] { 105, 3000 })
        {
            Assert.AreEqual(16, Hit(Mushmom, 18, 200, def, 0)); // 0.775 x (104.55 - 83.64) = 16.2
            Assert.AreEqual(19, Hit(Mushmom, 18, 200, def, 1)); // 0.775 x (123 - 98.4) = 19.065
        }
    }

    [TestMethod]
    public void WeakMob_DoesAtLeastOne()
    {
        Assert.AreEqual(1, Hit(2, 1, 200, 105, 0));  // snail: 0.775 x (1.7 - 1.36) = 0.26
        Assert.AreEqual(1, Hit(2, 1, 200, 105, 1));
        Assert.AreEqual(1, Hit(1, 200, 200, 0, 0));
    }

    // Level gap with a B that is not capped: the DEF counts less, so the hit is bigger.
    [TestMethod]
    public void LowerPlayer_DefCountsLessUntilTheCap()
    {
        // player 100, mob 110: alpha -10, A 0.85, B 0.90; DEF 200 counts as 180
        Assert.AreEqual(569, Hit(1000, 110, 100, 200, 0)); // 0.85 x (850 - 180) = 569.5
        Assert.AreEqual(697, Hit(1000, 110, 100, 200, 1)); // 0.85 x (1000 - 180) = 697
        Assert.IsTrue(Hit(1000, 110, 100, 200, 0) > Hit(1000, 100, 100, 200, 0)); // same DEF at the same level cuts more
        // DEF so big that B x DEF is over the cap: the same as no penalty at all
        Assert.AreEqual(Hit(1000, 100, 100, 10000, 0), Hit(1000, 110, 100, 10000, 0));
        Assert.AreEqual(Hit(1000, 100, 100, 10000, 1), Hit(1000, 110, 100, 10000, 1));
    }

    [DataTestMethod]
    [DataRow(182, 0.775)]
    [DataRow(10, 0.775)]
    [DataRow(9, 0.7825)]
    [DataRow(1, 0.8425)]
    [DataRow(0, 0.85)]
    [DataRow(-1, 0.85)]
    [DataRow(-15, 0.85)]
    [DataRow(-16, 0.8575)]
    [DataRow(-20, 0.8575)]
    [DataRow(-21, 0.865)]
    [DataRow(-25, 0.865)]
    [DataRow(-26, 0.8725)]
    [DataRow(-30, 0.8725)]
    [DataRow(-31, 0.88)]
    [DataRow(-100, 0.88)]
    public void A_Table(int alpha, double expected) => Assert.AreEqual(expected, MobTouch.A(alpha), 1e-9);

    [DataTestMethod]
    [DataRow(182, 1.0)]
    [DataRow(0, 1.0)]
    [DataRow(-1, 0.99)]
    [DataRow(-10, 0.90)]
    [DataRow(-11, 0.88)]
    [DataRow(-20, 0.70)]
    [DataRow(-29, 0.52)]
    [DataRow(-30, 0.5)]
    [DataRow(-100, 0.5)]
    public void B_Table(int alpha, double expected) => Assert.AreEqual(expected, MobTouch.B(alpha), 1e-9);

    // The force met is floor(ARC x 100 / barrierArc) percent; every edge of the table.
    [DataTestMethod]
    [DataRow(0, 2.8)]
    [DataRow(9, 2.8)]
    [DataRow(10, 2.4)]
    [DataRow(29, 2.4)]
    [DataRow(30, 1.8)]
    [DataRow(49, 1.8)]
    [DataRow(50, 1.6)]
    [DataRow(69, 1.6)]
    [DataRow(70, 1.4)]
    [DataRow(99, 1.4)]
    [DataRow(100, 1.0)]
    [DataRow(109, 1.0)]
    [DataRow(110, 0.8)]
    [DataRow(129, 0.8)]
    [DataRow(130, 0.4)]
    [DataRow(149, 0.4)]
    [DataRow(150, 0.0)]
    [DataRow(400, 0.0)]
    public void ArcaneForce_Table(int percentMet, double expected) =>
        Assert.AreEqual(expected, MobTouch.ArcaneForce(100, percentMet), 1e-9); // barrierArc 100: ARC = percent

    [TestMethod]
    public void ArcaneForce_RoundsTheMetPercentDown_AndNoBarrierMeansNoForce()
    {
        Assert.AreEqual(2.8, MobTouch.ArcaneForce(30, 0), 1e-9);   // 450001010: ARC 0 is 0%
        Assert.AreEqual(2.8, MobTouch.ArcaneForce(30, 2), 1e-9);   // 6.66% -> 6
        Assert.AreEqual(2.4, MobTouch.ArcaneForce(30, 3), 1e-9);   // exactly 10%
        Assert.AreEqual(1.0, MobTouch.ArcaneForce(30, 30), 1e-9);  // 100%
        Assert.AreEqual(0.0, MobTouch.ArcaneForce(30, 45), 1e-9);  // 150%
        Assert.AreEqual(1.0, MobTouch.ArcaneForce(0, 0), 1e-9);    // a map with no barrierArc: no modifier
        Assert.AreEqual(1.0, MobTouch.ArcaneForce(0, 500), 1e-9);
    }

    // Happy Erda on 450001010 (barrierArc 30) with ARC 0: A = 0.85 x 2.8 = 2.38, DEF 105.
    [TestMethod]
    public void HappyErda_OnTheArcaneMap_ArcZero()
    {
        Assert.AreEqual(25695, Hit(HappyErda, 200, 200, 105, 0, barrier: 30, arc: 0));
        Assert.AreEqual(30273, Hit(HappyErda, 200, 200, 105, 1, barrier: 30, arc: 0));
        // the same touch on a map with no barrierArc (map 10000): no modifier
        Assert.AreEqual(9176, Hit(HappyErda, 200, 200, 105, 0, barrier: 0, arc: 0));
    }

    [TestMethod]
    public void ArcaneForce_150Percent_LeavesOnePoint()
    {
        Assert.AreEqual(1, Hit(HappyErda, 200, 200, 105, 0, barrier: 30, arc: 45));
        Assert.AreEqual(1, Hit(HappyErda, 200, 200, 105, 1, barrier: 30, arc: 45));
    }

    // The damage-taken zone of the default passives (x0.752) comes after the formula; at least 1.
    [TestMethod]
    public void TakenDamage_AfterTheFormula()
    {
        var stats = StatAggregator.Aggregate(DefaultPassives.All);

        Assert.AreEqual(0.752, stats.Multiplier(StatKey.DAMAGE_TAKEN), 1e-9);
        Assert.AreEqual(6900, DamageChain.TakenDamage(stats, Hit(HappyErda, 200, 200, 105, 0)));      // 9176 x 0.752
        Assert.AreEqual(8130, DamageChain.TakenDamage(stats, Hit(HappyErda, 200, 200, 105, 1)));      // 10812 x 0.752
        Assert.AreEqual(19322, DamageChain.TakenDamage(stats, Hit(HappyErda, 200, 200, 105, 0, 30))); // 25695 x 0.752
        Assert.AreEqual(22765, DamageChain.TakenDamage(stats, Hit(HappyErda, 200, 200, 105, 1, 30))); // 30273 x 0.752
        Assert.AreEqual(12, DamageChain.TakenDamage(stats, 16));                                      // Mushmom min
        Assert.AreEqual(14, DamageChain.TakenDamage(stats, 19));                                      // Mushmom max
        Assert.AreEqual(1, DamageChain.TakenDamage(stats, 1));
    }

    // Defense = [1.5 x STR + 0.4 x (DEX + LUK) + added DEF] x (1 + DEF% / 100).
    [TestMethod]
    public void Defense_StatPartPlusAddedDef_ThenPercent()
    {
        Assert.AreEqual(2355.3, StatTables.BaseDefense(1111, 811, 911), 1e-9); // 1666.5 + 0.4 x 1722
        var mods = new List<StatModifier>
        {
            new(StatKey.DEF, StatValueType.FLAT, StatTables.BaseDefense(1000, 500, 500), StatSource.Base), // 1900
            new(StatKey.DEF, StatValueType.FLAT, 100, StatSource.Trait),                                 // Willpower
            new(StatKey.DEF, StatValueType.FLAT, 5, StatSource.Equipment),
            new(StatKey.DEF, StatValueType.PERCENT, 10, StatSource.Skill),
        };

        Assert.AreEqual(2205, StatAggregator.Aggregate(mods).IntValue(StatKey.DEF)); // (1900 + 105) x 1.10 = 2205.5
    }

    // The warrior skills' DEF records stay points (the DefPct field name is old): the Hero's adds
    // DefPct x level points to the added DEF, the Fire/Poison has none.
    [TestMethod]
    public void WarriorDefRecords_StayPoints_HeroOnly()
    {
        var guardian = new SkillData { SkillId = SkillData.GuardianArmor, MaxLevel = 2, DefPct = 10 };
        StatModifier Record(int job) => new(StatKey.DEF, StatValueType.FLAT, guardian.DefPct * JobSkills.Level(job, guardian), StatSource.Skill);
        var baseDef = new StatModifier(StatKey.DEF, StatValueType.FLAT, 1000, StatSource.Base);

        Assert.AreEqual(1020, StatAggregator.Aggregate(new[] { baseDef, Record(StatTables.Hero) }).IntValue(StatKey.DEF));
        Assert.AreEqual(1000, StatAggregator.Aggregate(new[] { baseDef, Record(StatTables.FirePoisonMage) }).IntValue(StatKey.DEF));
    }

    // The dodge chance (StrategyWiki Dodge Rate): [sqrt(DEX + 2 LUK) - sqrt(acc) - 2 x (mob level - player level)], 0..90.
    [TestMethod]
    public void Dodge_LukCountsDoubleDexAndAccCutsIt()
    {
        Assert.AreEqual(10.0, MobTouch.DodgeChance(100, 0, 0, 200, 200), 1e-9); // sqrt(100)
        Assert.AreEqual(10.0, MobTouch.DodgeChance(0, 50, 0, 200, 200), 1e-9); // sqrt(2 x 50): a LUK point is two DEX points
        Assert.AreEqual(6.0, MobTouch.DodgeChance(100, 0, 16, 200, 200), 1e-9); // 10 - sqrt(16)
    }

    [TestMethod]
    public void Dodge_LevelGapMovesItTwoPointsPerLevel()
    {
        Assert.AreEqual(20.0, MobTouch.DodgeChance(100, 0, 0, 195, 200), 1e-9); // 5 levels above the mob: +10
        Assert.AreEqual(4.0, MobTouch.DodgeChance(100, 0, 0, 203, 200), 1e-9);
        Assert.AreEqual(0.0, MobTouch.DodgeChance(100, 0, 0, 205, 200), 1e-9);
    }

    [TestMethod]
    public void Dodge_NeverBelowZeroNeverAboveNinety()
    {
        Assert.AreEqual(0.0, MobTouch.DodgeChance(0, 0, 100, 200, 200)); // -10 counts as 0
        Assert.AreEqual(0.0, MobTouch.DodgeChance(100, 0, 0, 300, 200));
        Assert.AreEqual(90.0, MobTouch.DodgeChance(100, 0, 0, 100, 200)); // 10 + 200
        Assert.AreEqual(90.0, MobTouch.DodgeChance(100, 0, 0, 160, 200)); // 10 + 80 = 90 exactly
    }

    // Lv 200 mage with DEX 816 / LUK 980 (example values): sqrt(816 + 1960) = 52.6878.
    //   Snail (level 1, acc 10):        52.6878 - 3.1623 + 398 = 447.5 -> the 90 cap
    //   Happy Erda (level 200, acc 723): 52.6878 - 26.8887 = 25.7991
    [TestMethod]
    public void Dodge_LowMobIsCappedSameLevelHighAccMobIsLow()
    {
        Assert.AreEqual(90.0, MobTouch.DodgeChance(816, 980, 10, 1, 200));
        Assert.AreEqual(25.7991, MobTouch.DodgeChance(816, 980, 723, 200, 200), 1e-4);
    }
}
