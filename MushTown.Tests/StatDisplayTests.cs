using MushTown.Shared;

namespace MushTown.Tests;

// The stat panel's numbers, rounding and hover source lists (T-20261117144550).
[TestClass]
public class StatDisplayTests
{
    private static StatSnapshot Snap(params StatModifier[] mods) => StatAggregator.Aggregate(mods);

    private static StatModifier M(StatKey key, StatValueType type, double value, StatSource source, string label = "") =>
        new(key, type, value, source, label);

    private static readonly StatSnapshot Defaults = StatAggregator.Aggregate(DefaultPassives.All);

    [TestMethod]
    public void Format_RoundsHalfAwayFromZeroAtTheShownDigits()
    {
        Assert.AreEqual("0.01%", StatDisplay.Format(StatFormat.Pct2, 0.005));
        Assert.AreEqual("37.00%", StatDisplay.Format(StatFormat.Pct2, 37));
        Assert.AreEqual("71.56%", StatDisplay.Format(StatFormat.Pct2, 71.564032));
        Assert.AreEqual("60%", StatDisplay.Format(StatFormat.Pct0, 59.5));
        Assert.AreEqual("59%", StatDisplay.Format(StatFormat.Pct0, 59.49));
        Assert.AreEqual("1,234,567", StatDisplay.Format(StatFormat.Count, 1234567.9)); // an integer stat shows its game value
    }

    [TestMethod]
    public void Text_RowsShowTheDecimalsOfTheDecision()
    {
        // 2 decimals: damage, final damage, ignore defense, boss damage, crit damage, normal damage,
        // cooldown not applied, extra status damage; whole percents: crit rate, buff duration, meso, drop, speed, jump.
        foreach (int row in new[] { 7, 8, 9, 10, 11, 15, 18, 20 })
            Assert.IsTrue(StatDisplay.Text(Defaults, row).EndsWith("%") && StatDisplay.Text(Defaults, row).Contains('.'), row.ToString());
        foreach (int row in new[] { 13, 17, 22, 24, 29, 30, 31, 32 })
            Assert.IsFalse(StatDisplay.Text(Defaults, row).Contains('.'), row.ToString());
        Assert.AreEqual("32.00%", StatDisplay.Text(Defaults, 7));
        Assert.AreEqual("101.00%", StatDisplay.Text(Defaults, 9));
        Assert.AreEqual("95%", StatDisplay.Text(Defaults, 13));
        Assert.AreEqual("50.00%", StatDisplay.Text(Defaults, 15));
        Assert.AreEqual("1,107", StatDisplay.Text(Defaults, 2));
    }

    [TestMethod]
    public void Text_MultiplierRowsAreConvertedAndNotSubtractedTwice()
    {
        Assert.AreEqual("0.00%", StatDisplay.Text(Snap(), StatDisplay.FinalDamageRow)); // no source: multiplier 1
        Assert.AreEqual("0.00%", StatDisplay.Text(Snap(), StatDisplay.IedRow));
        Assert.AreEqual("0.00%", StatDisplay.Text(Snap(), 7)); // damage points are already the panel number
        Assert.AreEqual("35.00%", StatDisplay.Text(Defaults, StatDisplay.FinalDamageRow));
        Assert.AreEqual("71.56%", StatDisplay.Text(Defaults, StatDisplay.IedRow));
    }

    [TestMethod]
    public void Text_MesoLeavesHeroicMultiplierOut_CooldownShowsSecondsAndPercent()
    {
        Assert.AreEqual("5%", StatDisplay.Text(Defaults, 22));
        var s = Snap(
            M(StatKey.CDR_SECONDS, StatValueType.FLAT, 2, StatSource.Equipment),
            M(StatKey.CDR_PCT, StatValueType.FLAT, 6, StatSource.UnionMember));
        Assert.AreEqual("2 sec/6%", StatDisplay.Text(s, StatDisplay.CooldownRow));
        Assert.AreEqual("0 sec/0%", StatDisplay.Text(Snap(), StatDisplay.CooldownRow));
    }

    [TestMethod]
    public void Text_SpeedAndJumpArePercentOfTheBase()
    {
        var s = Snap(
            M(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, StatSource.Base),
            M(StatKey.SPEED, StatValueType.PERCENT, 10, StatSource.Skill),
            M(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0, StatSource.Base));
        Assert.AreEqual("110%", StatDisplay.Text(s, 30));
        Assert.AreEqual("100%", StatDisplay.Text(s, 31));
    }

    [TestMethod]
    public void StatusResistance_CutsTheDurationByTheOfficialFormula()
    {
        Assert.AreEqual(0, StatDisplay.StatusReductionPct(0));
        Assert.AreEqual(55, StatDisplay.StatusReductionPct(87)); // floor(28 * log10(87) + 1)
        Assert.AreEqual(37, StatDisplay.StatusReductionPct(20));
    }

    [TestMethod]
    public void Sources_GroupInTheFixedOrderAndHideBaseAndZeroGroups()
    {
        var s = Snap(
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 10, StatSource.HyperStat),
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 6, StatSource.UnionAttacker),
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 20, StatSource.Base), // the base is not shown
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 20, StatSource.Skill),
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 1, StatSource.Skill),
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 0, StatSource.Equipment)); // a group of 0 is not shown
        var lines = StatDisplay.Sources(s, 15);
        CollectionAssert.AreEqual(new[] { "Skill", "Union Attacker", "Hyper Stat" }, lines.Select(l => l.Label).ToArray());
        CollectionAssert.AreEqual(new[] { "21.00%", "6.00%", "10.00%" }, lines.Select(l => StatDisplay.LineText(15, l)).ToArray());
        Assert.AreEqual("37.00%", StatDisplay.Text(Snap(
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 21, StatSource.Skill),
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 6, StatSource.UnionAttacker),
            M(StatKey.CRIT_DAMAGE, StatValueType.FLAT, 10, StatSource.HyperStat)), 15)); // the reference picture's 37%
    }

    [TestMethod]
    public void Sources_PercentBucketsAreListedApartFromThePoints()
    {
        var s = Snap(
            M(StatKey.STR, StatValueType.FLAT, 100, StatSource.Skill),
            M(StatKey.STR, StatValueType.PERCENT, 15, StatSource.Skill),
            M(StatKey.STR, StatValueType.FINAL_FLAT, 900, StatSource.UnionAttacker));
        var lines = StatDisplay.Sources(s, 2);
        CollectionAssert.AreEqual(new[] { "100", "15%", "900" }, lines.Select(l => StatDisplay.LineText(2, l)).ToArray());
    }

    [TestMethod]
    public void Sources_MultipliedRowsListEverySourceAndATotal()
    {
        var lines = StatDisplay.Sources(Defaults, StatDisplay.IedRow);
        Assert.AreEqual(7, lines.Count); // six sources and the total
        Assert.AreEqual("Total", lines[^1].Label);
        Assert.AreEqual(71.564032, lines[^1].Value, 1e-6);
        Assert.IsTrue(lines.Take(6).All(l => l.Label != "Total" && l.Value > 0));
        Assert.AreEqual(20.0, lines.First(l => l.Label.Contains("Light Wash")).Value, 1e-9);
        var final = StatDisplay.Sources(Defaults, StatDisplay.FinalDamageRow);
        Assert.AreEqual(2, final.Count);
        Assert.AreEqual("35.00%", StatDisplay.LineText(StatDisplay.FinalDamageRow, final[^1]));
        Assert.AreEqual(0, StatDisplay.Sources(Snap(), StatDisplay.IedRow).Count); // no source: nothing, not a lone Total
    }

    [TestMethod]
    public void Sources_FollowTheDefaultPassiveGroups()
    {
        var lines = StatDisplay.Sources(Defaults, 15);
        CollectionAssert.AreEqual(new[] { "Skill", "Union Member", "Union Attacker" }, lines.Select(l => l.Label).ToArray());
        Assert.AreEqual(50.0, lines.Sum(l => l.Value), 1e-9);
        Assert.AreEqual(0, StatDisplay.Sources(Defaults, 22).Count(l => l.Label == "Skill")); // the Heroic x6 is not a listed source
    }

    [TestMethod]
    public void Text_PercentageRowsKeepTheirDecimalsAndTheSourcesAddUpToTheCurrentValue()
    {
        var s = Snap(
            M(StatKey.DAMAGE_PCT, StatValueType.FLAT, 0.5, StatSource.Skill),
            M(StatKey.DAMAGE_PCT, StatValueType.FLAT, 0.7, StatSource.Equipment),
            M(StatKey.EXP_PCT, StatValueType.FLAT, 0.25, StatSource.Skill),
            M(StatKey.EXP_PCT, StatValueType.FLAT, 0.5, StatSource.Trait),
            M(StatKey.MAX_HP, StatValueType.FLAT, 100.9, StatSource.Equipment));
        Assert.AreEqual("1.20%", StatDisplay.Text(s, 7));
        Assert.AreEqual(1.2, StatDisplay.Sources(s, 7).Sum(l => l.Value), 1e-9);
        Assert.AreEqual("0.75%", StatDisplay.Text(s, 26));
        Assert.AreEqual(0.75, StatDisplay.Sources(s, 26).Sum(l => l.Value), 1e-9);
        Assert.AreEqual("100", StatDisplay.Text(s, 0)); // an integer stat is still rounded down
        Assert.AreEqual(1, s.Additive(StatKey.DAMAGE_PCT)); // the formula keeps rounding down
    }
}
