using MushTown.Shared;

namespace MushTown.Tests;

// The totals of research/gmsr-beginner.md section 10.8 (default passives of a
// Lv.200 character): every default source merged by the one aggregator.
[TestClass]
public class DefaultPassivesTests
{
    private static readonly StatSnapshot Snap = StatAggregator.Aggregate(DefaultPassives.All);

    [TestMethod]
    public void AllStats_PercentAndFixedValues()
    {
        foreach (var key in new[] { StatKey.STR, StatKey.DEX, StatKey.INT, StatKey.LUK })
            Assert.AreEqual(30, Snap.Percent(key), key.ToString());
        Assert.AreEqual(1080, Snap.Flat(StatKey.STR));
        Assert.AreEqual(780, Snap.Flat(StatKey.DEX));
        Assert.AreEqual(1030, Snap.Flat(StatKey.INT));
        Assert.AreEqual(880, Snap.Flat(StatKey.LUK));
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
        Assert.AreEqual(109, Snap.IntValue(StatKey.IED_PCT));
        Assert.AreEqual(95, Snap.IntValue(StatKey.CRIT_RATE));
        Assert.AreEqual(50, Snap.IntValue(StatKey.CRIT_DAMAGE));
        Assert.AreEqual(1.35, DamageChain.FinalDamageMultiplier(Snap), 1e-9);
    }

    [TestMethod]
    public void IgnoreDefense_IsCappedAtOneHundredPercent()
    {
        Assert.AreEqual(0.0, DamageChain.IedMultiplier(Snap));
    }

    [TestMethod]
    public void Economy_ExpAndMeso()
    {
        Assert.AreEqual(42, Snap.IntValue(StatKey.EXP_PCT));
        Assert.AreEqual(505, Snap.IntValue(StatKey.MESO_PCT)); // Heroic x6 (+500) and the Phantom block (+5), added
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
    public void EveryRecordNamesItsSource()
    {
        Assert.IsTrue(DefaultPassives.All.Count > 0);
        Assert.IsTrue(DefaultPassives.All.All(m => m.Source != ""));
    }
}
