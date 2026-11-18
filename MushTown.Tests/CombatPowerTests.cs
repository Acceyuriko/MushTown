using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class CombatPowerTests
{
    private static StatModifier B(StatKey key, double value) => new(key, StatValueType.FLAT, value, StatSource.Base);

    // A mage: main INT x4, sub LUK x1, magic attack (the physical attack is ignored), damage + boss added,
    // final damage multiplied, crit zone 1.35 + crit damage.
    [TestMethod]
    public void Mage_ReadsMagicAttackAndWeighsMainFourSubOne()
    {
        var s = StatAggregator.Aggregate(new[]
        {
            B(StatKey.INT, 1000), B(StatKey.LUK, 100), B(StatKey.STR, 9999), B(StatKey.DEX, 9999),
            B(StatKey.MATT, 100), B(StatKey.ATT, 9999),
            B(StatKey.DAMAGE_PCT, 20), B(StatKey.BOSS_PCT, 30), B(StatKey.CRIT_DAMAGE, 15),
            new StatModifier(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.2, StatSource.Equipment),
        });
        var job = StatTables.Job(StatTables.FirePoisonMage);
        // 0.01 x 1 x (4 x 1000 + 100) x (1 + 0.2 + 0.3) x 1.2 x (1.35 + 0.15) x 100
        Assert.AreEqual(11070, CombatPower.Calculate(s, job, StatTables.MaxWeaponCoefficient(StatTables.FirePoisonMage)));
    }

    // A warrior: main STR x4, sub DEX x1, physical attack; no crit damage means the 1.35 base alone.
    [TestMethod]
    public void Warrior_ReadsAttackAndTheCritBase()
    {
        var s = StatAggregator.Aggregate(new[] { B(StatKey.STR, 2000), B(StatKey.DEX, 300), B(StatKey.INT, 9999), B(StatKey.ATT, 200), B(StatKey.MATT, 9999) });
        var job = StatTables.Job(StatTables.Hero);
        // 0.01 x 1 x (4 x 2000 + 300) x 1.35 x 200
        Assert.AreEqual(22410, CombatPower.Calculate(s, job, StatTables.MaxWeaponCoefficient(StatTables.Hero)));
    }

    // A weaker weapon than the job's best scales the CP by the coefficient ratio.
    [TestMethod]
    public void WeaponRatio_ScalesByTheJobsBestWeapon()
    {
        var s = StatAggregator.Aggregate(new[] { B(StatKey.STR, 1000), B(StatKey.ATT, 100) });
        var best = StatTables.Job(StatTables.Hero);
        var sword = best with { Weapon = WeaponType.OneHandSword };
        double max = StatTables.MaxWeaponCoefficient(StatTables.Hero);
        Assert.AreEqual(1.44, max);
        long full = CombatPower.Calculate(s, best, max);
        long weaker = CombatPower.Calculate(s, sword, max);
        Assert.AreEqual((long)Math.Floor(0.01 * 4000 * 1.35 * 100), full); // ratio 1
        Assert.AreEqual((long)Math.Floor(0.01 * (1.24 / 1.44) * 4000 * 1.35 * 100), weaker);
    }

    // The mage has one coefficient (1.20), so its ratio is 1 with a wand, a staff or no weapon.
    [TestMethod]
    public void Mage_HasOnlyOneCoefficient()
    {
        Assert.AreEqual(1.20, StatTables.MaxWeaponCoefficient(StatTables.FirePoisonMage));
        Assert.AreEqual(1.20, StatTables.Weapon(WeaponType.ShortStaff).Coefficient);
        Assert.AreEqual(1.20, StatTables.Weapon(WeaponType.Staff).Coefficient);
    }

    // Ignore defense, crit rate, level and the physical attack of a mage are not in the formula.
    [TestMethod]
    public void LeftOutTerms_DoNotMoveIt()
    {
        var mods = new List<StatModifier> { B(StatKey.INT, 1000), B(StatKey.LUK, 100), B(StatKey.MATT, 100) };
        var job = StatTables.Job(StatTables.FirePoisonMage);
        long plain = CombatPower.Calculate(StatAggregator.Aggregate(mods), job, 1.2);
        mods.Add(new StatModifier(StatKey.IED_PCT, StatValueType.MULTIPLIER, 0.2, StatSource.Equipment));
        mods.Add(B(StatKey.CRIT_RATE, 50));
        mods.Add(B(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT, 10));
        Assert.AreEqual(plain, CombatPower.Calculate(StatAggregator.Aggregate(mods), job, 1.2));
    }

    // Every source counts: the Skill and Buff records move the combat power like the others.
    [TestMethod]
    public void EverySourceMovesIt()
    {
        var job = StatTables.Job(StatTables.FirePoisonMage);
        var mods = new List<StatModifier> { B(StatKey.INT, 1000), B(StatKey.LUK, 100), B(StatKey.MATT, 100) };
        long plain = CombatPower.Calculate(StatAggregator.Aggregate(mods), job, 1.2);
        foreach (var source in Enum.GetValues<StatSource>())
        {
            var more = mods.Append(new StatModifier(StatKey.MATT, StatValueType.FLAT, 100, source));
            Assert.IsTrue(CombatPower.Calculate(StatAggregator.Aggregate(more), job, 1.2) > plain, source.ToString());
        }
    }
}
