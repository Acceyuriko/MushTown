using MushTown.Shared;

namespace MushTown.Tests;

// The totals of research/gmsr-beginner.md section 10.8 (default passives of a
// Lv.200 character), merged by the one aggregator and read with the rules of
// research/stat-formulas-decisions.md.
[TestClass]
public class DefaultPassivesTests
{
    private static readonly StatSnapshot Snap = StatAggregator.Aggregate(DefaultPassives.All);

    [TestMethod]
    public void AllStats_PercentFixedAndFinalValues()
    {
        // Maple Warrior is not in the data: it needs the pure stat (see StatsTests).
        foreach (var key in new[] { StatKey.STR, StatKey.DEX, StatKey.INT, StatKey.LUK })
        {
            Assert.AreEqual(15, Snap.Percent(key), key.ToString());
            Assert.AreEqual(180, Snap.Flat(key), key.ToString()); // Will 5 + Pirate 100 + Union grid 75
        }
        // The Union member blocks are final values: the stat percent does not raise them.
        Assert.AreEqual(900, Snap.FinalFlat(StatKey.STR));
        Assert.AreEqual(600, Snap.FinalFlat(StatKey.DEX));
        Assert.AreEqual(850, Snap.FinalFlat(StatKey.INT));
        Assert.AreEqual(700, Snap.FinalFlat(StatKey.LUK));
        Assert.AreEqual(1107, Snap.IntValue(StatKey.STR)); // floor(180 * 1.15) + 900
    }

    [TestMethod]
    public void Attack_FixedValueAndPercent()
    {
        Assert.AreEqual(60, Snap.Flat(StatKey.ATT));
        Assert.AreEqual(60, Snap.Flat(StatKey.MATT));
        Assert.AreEqual(4, Snap.Percent(StatKey.ATT_PCT));
        Assert.AreEqual(4, Snap.Percent(StatKey.MATT_PCT));
        Assert.AreEqual(62, DamageChain.TotalAttack(Snap)); // floor(60 * 1.04)
        Assert.AreEqual(62, DamageChain.TotalAttack(Snap, magic: true));
    }

    [TestMethod]
    public void DamageBuckets()
    {
        Assert.AreEqual(32, Snap.IntValue(StatKey.DAMAGE_PCT));
        Assert.AreEqual(101, Snap.IntValue(StatKey.BOSS_PCT));
        Assert.AreEqual(40, Snap.IntValue(StatKey.NORMAL_PCT));
        Assert.AreEqual(95, Snap.IntValue(StatKey.CRIT_RATE));
        Assert.AreEqual(50, Snap.IntValue(StatKey.CRIT_DAMAGE));
        Assert.AreEqual(1.35, DamageChain.FinalDamageMultiplier(Snap), 1e-9);
    }

    [TestMethod]
    public void IgnoreDefense_SourcesMultiply()
    {
        // 20, 12, 15, 10, 40 and 12 percent: 1 - 0.8 * 0.88 * 0.85 * 0.9 * 0.6 * 0.88, about 71.6%.
        double left = DamageChain.IedMultiplier(Snap);
        Assert.AreEqual(0.28435968, left, 1e-9);
        Assert.AreEqual(71.564032, (1 - left) * 100, 1e-6);
    }

    [TestMethod]
    public void DamageTaken_IsZeroAndRenOnlyAndMultiplies()
    {
        Assert.AreEqual(0.82 * 0.94, Snap.Multiplier(StatKey.DAMAGE_TAKEN), 1e-9); // takes about 22.9% less
        Assert.IsFalse(DefaultPassives.All.Any(m => m.Key == StatKey.DAMAGE_TAKEN && m.Label.Contains("Pirate")));
        Assert.IsTrue(DefaultPassives.All
            .Where(m => m.Key == StatKey.DAMAGE_TAKEN).All(m => m.Label.Contains("wiki, not verified")));
    }

    [TestMethod]
    public void ElementResistances()
    {
        Assert.AreEqual(5, Snap.IntValue(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT)); // Insight
        Assert.AreEqual(22, Snap.IntValue(StatKey.ELEMENT_RESISTANCE)); // Cygnus Blessing
    }

    [TestMethod]
    public void Economy_ExpAndMeso()
    {
        Assert.AreEqual(42, Snap.IntValue(StatKey.EXP_PCT));
        // Heroic's x6 is its own zone: not in the panel number, no cap. Only the Phantom block (+5) is added.
        Assert.AreEqual(5, Snap.Additive(StatKey.MESO_PCT));
        Assert.AreEqual(6.0, Snap.Multiplier(StatKey.MESO_PCT));
    }

    [TestMethod]
    public void MaxHpAndMp()
    {
        Assert.AreEqual(10200, Snap.Flat(StatKey.MAX_HP));
        Assert.AreEqual(31, Snap.Percent(StatKey.MAX_HP));
        Assert.AreEqual(7600, Snap.Flat(StatKey.MAX_MP));
        Assert.AreEqual(11, Snap.Percent(StatKey.MAX_MP));
    }

    [TestMethod]
    public void ResistanceAndDuration()
    {
        Assert.AreEqual(87, Snap.IntValue(StatKey.STATUS_RESISTANCE));
        Assert.AreEqual(94, Snap.IntValue(StatKey.BUFF_DURATION_PCT));
        Assert.AreEqual(100, Snap.IntValue(StatKey.DEF)); // the Willpower trait
    }

    [TestMethod]
    public void EveryLinkIsListedOnceAtItsHighestLevel()
    {
        var repeats = DefaultPassives.All
            .Where(m => m.Label.StartsWith("Link:"))
            .GroupBy(m => (m.Label, m.Key, m.Type))
            .Where(g => g.Count() > 1);
        Assert.IsFalse(repeats.Any());
    }

    [TestMethod]
    public void EveryRecordNamesItsSource()
    {
        Assert.IsTrue(DefaultPassives.All.Count > 0);
        Assert.IsTrue(DefaultPassives.All.All(m => m.Label != ""));
    }
}
