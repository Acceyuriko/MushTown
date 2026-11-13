using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class DamageChainTests
{
    private static StatSnapshot Snap(params StatModifier[] mods) => StatAggregator.Aggregate(mods);
    private static StatModifier M(StatKey key, StatValueType type, double value) => new(key, type, value, "test");

    private static readonly JobProfile OneHand = new(StatKey.STR, StatKey.DEX, WeaponType.OneHandSword);

    private static Build B(long str = 100, long dex = 50, long atk = 160,
        int dmgPct = 0, int critPct = 0, int bossPct = 0, int normalPct = 0,
        int finalDamagePct = 0, int critDamage = 0, int iedPct = 0, int level = 1) =>
        new(str, dex, atk, dmgPct, critPct, 0, bossPct, normalPct, finalDamagePct,
            critDamage, iedPct, level, (int)WeaponType.OneHandSword);

    [TestMethod]
    public void Step1_ProductionChain_UsesFourMainPlusSub()
    {
        var result = DamageChain.Calculate(B(100, 50, 160), 0.0, 0.0, 0.0, 0);
        Assert.AreEqual(450, result.StatValue);
    }

    [TestMethod]
    public void Step2_TotalAttack_AddsFlatThenPercentThenFinalFlat()
    {
        var s = Snap(
            M(StatKey.ATT, StatValueType.FLAT, 100),
            M(StatKey.ATT, StatValueType.PERCENT, 50),
            M(StatKey.ATT, StatValueType.FINAL_FLAT, 10));
        Assert.AreEqual(160, DamageChain.TotalAttack(s));
    }

    [TestMethod]
    public void Step3And4_ProductionChain_MasteryActualAndPanelRange()
    {
        var result = DamageChain.Calculate(B(100, 50, 160), 0.0, 0.0, 0.0, 0);
        Assert.AreEqual(178, result.MinActual);
        Assert.AreEqual(892, result.MaxActual);
        Assert.AreEqual(179, result.MinPanel);
        Assert.AreEqual(892, result.MaxPanel);
    }

    [TestMethod]
    public void Step4_ProductionChain_AppliesDamageAndFinalDamage()
    {
        var result = DamageChain.Calculate(B(100, 50, 160, 50, finalDamagePct: 20), 0.0, 0.0, 0.0, 0);
        Assert.AreEqual(321, result.MinPanel);
        Assert.AreEqual(1605, result.MaxPanel);
    }

    [TestMethod]
    public void Step5_FinalDamageMultipliesEverySource()
    {
        var s = Snap(
            M(StatKey.FINAL_DAMAGE_PCT, StatValueType.FLAT, 20),
            M(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.5),
            M(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 2.0));
        Assert.AreEqual(3.6, DamageChain.FinalDamageMultiplier(s), 1e-9); // (1 + 20%) * 1.5 * 2
    }

    [TestMethod]
    public void Step6_ProductionChain_UsesOnlyTheTargetBucket()
    {
        var boss = DamageChain.Calculate(B(100, 0, 100, bossPct: 400), 0.0, 0.0, 0.0, 0, boss: true);
        Assert.AreEqual(500.0, boss.MinSkill, 1e-9);
        Assert.AreEqual(2480.0, boss.MaxSkill, 1e-9);

        var normal = DamageChain.Calculate(B(100, 0, 100, bossPct: 400, normalPct: 100), 0.0, 0.0, 0.0, 0);
        Assert.AreEqual(200.0, normal.MinSkill, 1e-9);
        Assert.AreEqual(992.0, normal.MaxSkill, 1e-9);
    }

    [TestMethod]
    public void Step7_ProductionChain_CritDefenseElementAndLevel()
    {
        var result = DamageChain.Calculate(B(100, 50, 100, critPct: 100, critDamage: 20,
            iedPct: 50, level: 7), 1.0, 0.0, 0.5, 7, "F2I3", AttackElement.Fire);
        Assert.AreEqual(1.7, result.CritMultiplier, 1e-9);
        Assert.AreEqual(0.75, result.DefenseMultiplier, 1e-9);
        Assert.AreEqual(0.5, result.ElementMultiplier, 1e-9);
        Assert.AreEqual(1.10, result.LevelMultiplier, 1e-9);
        Assert.AreEqual(391, result.TotalDamage);

        var above = DamageChain.Calculate(B(level: 7), 0.0, 0.0, 0.0, 1);
        Assert.AreEqual(1.20, above.LevelMultiplier, 1e-9);
        var below = DamageChain.Calculate(B(level: 1), 0.0, 0.0, 0.0, 7);
        Assert.AreEqual(0.85, below.LevelMultiplier, 1e-9);
    }

    [TestMethod]
    public void Step8_ProductionChain_ReturnsTotalDamage()
    {
        var build = B(100, 50, 160);
        var result = DamageChain.Calculate(build, 1.0, 1.0, 0.0, 0);
        Assert.AreEqual(892, result.TotalDamage);
        Assert.AreEqual(result.TotalDamage, Damage.Calc(1.0, 1.0, 0.0, build, 0));
    }

    [TestMethod]
    public void CurrentDemoOneHandPanel_IsTheNewFormulaValue()
    {
        var s = Snap(
            M(StatKey.STR, StatValueType.FLAT, 28),
            M(StatKey.DEX, StatValueType.FLAT, 10),
            M(StatKey.ATT, StatValueType.FLAT, 15));
        Assert.AreEqual((5L, 22L), DamageChain.PanelRange(s, OneHand));
    }

    [TestMethod]
    public void HeroTwoHandPanel_UsesTheHeroCoefficient()
    {
        var s = Snap(
            M(StatKey.STR, StatValueType.FLAT, 28),
            M(StatKey.DEX, StatValueType.FLAT, 10),
            M(StatKey.ATT, StatValueType.FLAT, 15));
        Assert.AreEqual((6L, 26L), DamageChain.PanelRange(s, StatTables.Job(StatTables.Hero)));
    }

    [TestMethod]
    public void BeginnerPanel_IsTheNewFormulaValue()
    {
        var s = Snap(
            M(StatKey.STR, StatValueType.FLAT, 4),
            M(StatKey.DEX, StatValueType.FLAT, 4),
            M(StatKey.ATT, StatValueType.FLAT, Progression.WeaponAtk));
        var job = new JobProfile(StatKey.STR, StatKey.DEX, WeaponType.Default);
        Assert.AreEqual((2L, 7L), DamageChain.PanelRange(s, job));
    }
}
