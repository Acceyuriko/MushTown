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
    public void AllKeys_AreTheTwentySevenContractKeys()
    {
        Assert.AreEqual(27, StatSnapshot.AllKeys.Length);
        Assert.AreEqual(StatKey.STR, StatSnapshot.AllKeys[0]);
        Assert.AreEqual(StatKey.JUMP, StatSnapshot.AllKeys[^1]);
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
