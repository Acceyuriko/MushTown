using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class StatsTests
{
    private static StatSnapshot Snap(params StatModifier[] mods) => StatAggregator.Aggregate(mods);

    private static StatModifier M(StatKey key, StatValueType type, double value) =>
        new(key, type, value, "test");

    [TestMethod]
    public void Aggregate_OrderIsFlatPercentFinalFlatMultiplier()
    {
        var s = Snap(
            M(StatKey.ATT, StatValueType.FLAT, 10),
            M(StatKey.ATT, StatValueType.FLAT, 5),
            M(StatKey.ATT, StatValueType.PERCENT, 100),
            M(StatKey.ATT, StatValueType.PERCENT, 50),
            M(StatKey.ATT, StatValueType.FINAL_FLAT, 7),
            M(StatKey.ATT, StatValueType.MULTIPLIER, 2),
            M(StatKey.ATT, StatValueType.MULTIPLIER, 3));

        Assert.AreEqual(15, s.Flat(StatKey.ATT));
        Assert.AreEqual(150, s.Percent(StatKey.ATT));
        Assert.AreEqual(7, s.FinalFlat(StatKey.ATT));
        Assert.AreEqual(6, s.Multiplier(StatKey.ATT));
        Assert.AreEqual(264, s.Value(StatKey.ATT)); // (floor(15 * 2.5) + 7) * 6
    }

    [TestMethod]
    public void Aggregate_AllFourValueTypesHaveTheirOwnBucket()
    {
        var s = Snap(
            M(StatKey.CRIT_RATE, StatValueType.FLAT, 4),
            M(StatKey.CRIT_RATE, StatValueType.PERCENT, 50),
            M(StatKey.CRIT_RATE, StatValueType.FINAL_FLAT, 3),
            M(StatKey.CRIT_RATE, StatValueType.MULTIPLIER, 2));

        Assert.AreEqual(18, s.Value(StatKey.CRIT_RATE)); // (4 * 1.5 + 3) * 2
    }

    [TestMethod]
    public void Empty_HasNeutralMultiplierAndNoSources()
    {
        Assert.AreEqual(0.0, StatSnapshot.Empty.Value(StatKey.STR));
        Assert.AreEqual(1.0, StatSnapshot.Empty.Multiplier(StatKey.STR));
        Assert.AreEqual(0.0, StatSnapshot.Empty.Percent(StatKey.STR));
    }

    [TestMethod]
    public void DoubleValue_KeepsMovementFractions()
    {
        var s = Snap(
            M(StatKey.SPEED, StatValueType.FLAT, 1),
            M(StatKey.SPEED, StatValueType.PERCENT, 50));
        Assert.AreEqual(1.5, s.DoubleValue(StatKey.SPEED), 1e-9);
        Assert.AreEqual(1, s.IntValue(StatKey.SPEED));
    }

    [TestMethod]
    public void AllKeys_FirstTwentyEightAreTheContractAndNewKeysComeAfterJump()
    {
        var contract = new[]
        {
            StatKey.STR, StatKey.DEX, StatKey.INT, StatKey.LUK, StatKey.MAX_HP, StatKey.MAX_MP, StatKey.DEF,
            StatKey.ATT, StatKey.MATT, StatKey.ATT_PCT, StatKey.MATT_PCT, StatKey.DAMAGE_PCT, StatKey.BOSS_PCT,
            StatKey.NORMAL_PCT, StatKey.FINAL_DAMAGE_PCT, StatKey.CRIT_RATE, StatKey.CRIT_DAMAGE, StatKey.IED_PCT,
            StatKey.STATUS_RESISTANCE, StatKey.KNOCKBACK_RESISTANCE, StatKey.BUFF_DURATION_PCT, StatKey.CDR_PCT,
            StatKey.COOLDOWN_SKIP_PCT, StatKey.DROP_PCT, StatKey.MESO_PCT, StatKey.EXP_PCT, StatKey.SPEED, StatKey.JUMP,
        };
        Assert.AreEqual(38, StatSnapshot.AllKeys.Length);
        CollectionAssert.AreEqual(contract, StatSnapshot.AllKeys.Take(28).ToArray());
        Assert.AreEqual(27, (int)StatKey.JUMP);
        Assert.AreEqual(28, (int)StatKey.IGNORE_ELEMENT_RESISTANCE_PCT);
        Assert.AreEqual(StatKey.ELEMENT_RESISTANCE, StatSnapshot.AllKeys[^1]);
    }

    [TestMethod]
    public void Caps_TruncateAtTheLimits()
    {
        var s = Snap(
            M(StatKey.MAX_HP, StatValueType.FLAT, 400_000),
            M(StatKey.MAX_HP, StatValueType.PERCENT, 50),
            M(StatKey.MAX_MP, StatValueType.FINAL_FLAT, 600_000),
            M(StatKey.CRIT_RATE, StatValueType.FLAT, 95),
            M(StatKey.CRIT_RATE, StatValueType.FLAT, 40),
            M(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed),
            M(StatKey.SPEED, StatValueType.PERCENT, 100),
            M(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0),
            M(StatKey.JUMP, StatValueType.PERCENT, 100),
            M(StatKey.STATUS_RESISTANCE, StatValueType.FLAT, 900));
        Assert.AreEqual(500_000, s.IntValue(StatKey.MAX_HP));
        Assert.AreEqual(500_000, s.IntValue(StatKey.MAX_MP));
        Assert.AreEqual(100, s.IntValue(StatKey.CRIT_RATE));
        Assert.AreEqual(140.0, s.SpeedPercent, 1e-9);
        Assert.AreEqual(123.0, s.JumpPercent, 1e-9);
        Assert.AreEqual(900, s.IntValue(StatKey.STATUS_RESISTANCE)); // no cap
    }

    [TestMethod]
    public void SpeedAndJumpPercent_AreCurrentOverBase()
    {
        var plain = Snap(
            M(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed),
            M(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0));
        Assert.AreEqual(100.0, plain.SpeedPercent, 1e-9);
        Assert.AreEqual(100.0, plain.JumpPercent, 1e-9);
        var mastery = Snap(
            M(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed),
            M(StatKey.SPEED, StatValueType.PERCENT, 10),
            M(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0),
            M(StatKey.JUMP, StatValueType.PERCENT, 10));
        Assert.AreEqual(110.0, mastery.SpeedPercent, 1e-9);
        Assert.AreEqual(110.0, mastery.JumpPercent, 1e-9);
        Assert.AreEqual(PlayerMover.WalkSpeed * 1.1, mastery.DoubleValue(StatKey.SPEED), 1e-3); // still pixels
    }

    [TestMethod]
    public void Additive_LeavesTheMultiplierZoneOut()
    {
        var s = Snap(
            M(StatKey.MESO_PCT, StatValueType.FLAT, 5),
            M(StatKey.MESO_PCT, StatValueType.MULTIPLIER, 6));
        Assert.AreEqual(5.0, s.Additive(StatKey.MESO_PCT));
        Assert.AreEqual(6.0, s.Multiplier(StatKey.MESO_PCT));
    }

    [TestMethod]
    public void MainStats_FinalValueSkipsPercentAndMapleWarriorOnlyRaisesThePureStat()
    {
        // 1000 pure + 500 gear + Maple Warrior (15% of the pure 1000 = 150), 20% stat percent,
        // plus 900 final (Union member blocks): floor((1000 + 500 + 150) * 1.2) + 900.
        var s = Snap(
            M(StatKey.STR, StatValueType.FLAT, 1000),
            M(StatKey.STR, StatValueType.FLAT, 500),
            DefaultPassives.MapleWarrior(StatKey.STR, 1000),
            M(StatKey.STR, StatValueType.PERCENT, 20),
            M(StatKey.STR, StatValueType.FINAL_FLAT, 900));
        Assert.AreEqual(150.0, DefaultPassives.MapleWarrior(StatKey.STR, 1000).Value);
        Assert.AreEqual(1.0, DefaultPassives.MapleWarrior(StatKey.STR, 9).Value); // floor(9 * 15%)
        Assert.AreEqual(1980 + 900, s.IntValue(StatKey.STR));
    }

    [TestMethod]
    public void WeaponTable_FollowsTheCharacterStatsTable()
    {
        Assert.AreEqual(1.24, StatTables.Weapon(WeaponType.OneHandSword).Coefficient, 1e-9);
        Assert.AreEqual(1.44, StatTables.Weapon(WeaponType.HeroTwoHand).Coefficient, 1e-9);
        Assert.AreEqual(1.34, StatTables.Weapon(WeaponType.TwoHandSword).Coefficient, 1e-9);
        Assert.AreEqual(1.20, StatTables.Weapon(WeaponType.ShortStaff).Coefficient, 1e-9);
        Assert.AreEqual(1.20, StatTables.Weapon(WeaponType.Staff).Coefficient, 1e-9);
        Assert.AreEqual(1.20, StatTables.Weapon(WeaponType.OneHandBlunt).Coefficient, 1e-9);
        Assert.AreEqual(1.20, StatTables.Weapon(WeaponType.OneHandAxe).Coefficient, 1e-9);
        Assert.AreEqual(20.0, StatTables.Weapon(WeaponType.HeroTwoHand).MasteryPct, 1e-9);
    }

    [TestMethod]
    public void WeaponTypeOf_MapsTheFamiliesWeUse()
    {
        Assert.AreEqual(WeaponType.OneHandSword, StatTables.WeaponTypeOf(1302000));
        Assert.AreEqual(WeaponType.HeroTwoHand, StatTables.Job(StatTables.Hero).Weapon);
        Assert.AreEqual(WeaponType.TwoHandSword, StatTables.WeaponTypeOf(1402000));
        Assert.AreEqual(WeaponType.ShortStaff, StatTables.WeaponTypeOf(1372000));
        Assert.AreEqual(WeaponType.Staff, StatTables.WeaponTypeOf(1382000));
        Assert.AreEqual(WeaponType.Default, StatTables.WeaponTypeOf(9999999));
    }

    [TestMethod]
    public void WeaponTable_DefaultIs120()
    {
        var weapon = StatTables.Weapon(WeaponType.Default);
        Assert.AreEqual(1.20, weapon.Coefficient, 1e-9);
        Assert.AreEqual(20.0, weapon.MasteryPct, 1e-9);
    }

    [TestMethod]
    public void JobTable_HeroIsStrDexTwoHandAndMageIsIntLukShortStaff()
    {
        var hero = StatTables.Job(StatTables.Hero);
        Assert.AreEqual(StatKey.STR, hero.Main);
        Assert.AreEqual(StatKey.DEX, hero.Sub);
        Assert.AreEqual(WeaponType.HeroTwoHand, hero.Weapon);

        var mage = StatTables.Job(StatTables.FirePoisonMage);
        Assert.AreEqual(StatKey.INT, mage.Main);
        Assert.AreEqual(StatKey.LUK, mage.Sub);
        Assert.AreEqual(WeaponType.ShortStaff, mage.Weapon);
    }

    [TestMethod]
    public void MobElemAttr_ResistImmuneWeakAndNormal()
    {
        Assert.AreEqual(0.5, MobElemAttr.Multiplier("F2", AttackElement.Fire), 1e-9);
        Assert.AreEqual(0.0, MobElemAttr.Multiplier("F1", AttackElement.Fire), 1e-9);
        Assert.AreEqual(1.5, MobElemAttr.Multiplier("I3", AttackElement.Ice), 1e-9);
        Assert.AreEqual(1.0, MobElemAttr.Multiplier("F2", AttackElement.Ice), 1e-9);
    }

    [TestMethod]
    public void MobData_LevelAndElemAttr_Deserialize()
    {
        using var assets = new TempAssets();
        assets.AddMob(100000, 100, level: 12, elemAttr: "F2I3");
        string json = File.ReadAllText(Path.Combine(assets.Dir, "mob", "100000.json"));
        var mob = System.Text.Json.JsonSerializer.Deserialize<MobData>(json)!;
        Assert.AreEqual(12, mob.Level);
        Assert.AreEqual("F2I3", mob.ElemAttr);
        Assert.AreEqual(0, new MobData().Level);
        Assert.AreEqual("", new MobData().ElemAttr);
    }
}
