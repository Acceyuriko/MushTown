using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class WzSkillTests
{
    // One WZ skill from its common formulas (the text of Skill/200.img/skill/<id>/common).
    private static WzSkillData Skill(int id, int maxLevel, params (string Field, string Formula)[] common)
    {
        var data = new WzSkillData { SkillId = id, MaxLevel = maxLevel, Name = id == WzSkillData.MpBoost ? "MP Boost" : "Magic Armor" };
        foreach (var (field, formula) in common) data.Common[field] = SkillFormula.Levels(formula, maxLevel);
        return data;
    }

    // The two skills that only add numbers, with the WZ formulas of 200.img.
    internal static Dictionary<int, WzSkillData> FirePoisonPassives() => new()
    {
        [WzSkillData.MagicArmor] = Skill(WzSkillData.MagicArmor, 9, ("pddX", "10*x")),
        [WzSkillData.MpBoost] = Skill(WzSkillData.MpBoost, 20, ("mmpR", "x"), ("lv2mmp", "20+5*x"), ("actionSpeed", "-1")),
    };

    [TestMethod]
    public void Formula_PlainNumberAndVariable()
    {
        Assert.AreEqual(7, SkillFormula.Eval("7", 3));
        Assert.AreEqual(3, SkillFormula.Eval("x", 3));
        Assert.AreEqual(0.4, SkillFormula.Eval("0.4", 3), 1e-9);
        Assert.AreEqual(-1, SkillFormula.Eval("-1", 3));
    }

    [TestMethod]
    public void Formula_FloorCeilAndNesting()
    {
        Assert.AreEqual(2, SkillFormula.Eval("d(x/5)", 12));  // 2.4 -> 2
        Assert.AreEqual(3, SkillFormula.Eval("u(x/5)", 12));  // 2.4 -> 3
        Assert.AreEqual(5, SkillFormula.Eval("u(x/2)", 9));
        Assert.AreEqual(2, SkillFormula.Eval("d(u(x/2)/2)", 9)); // nested: u(4.5) = 5, 5 / 2 = 2.5, rounded down
    }

    [TestMethod]
    public void Formula_OperatorOrderAndBrackets()
    {
        Assert.AreEqual(16, SkillFormula.Eval("10+2*3", 1));
        Assert.AreEqual(36, SkillFormula.Eval("(10+2)*3", 1));
        Assert.AreEqual(14, SkillFormula.Eval("20-2*3", 1));
        Assert.AreEqual(-23, SkillFormula.Eval("-20-x", 3)); // x=3
        Assert.AreEqual(5, SkillFormula.Eval("(((x)))+2", 3));
        Assert.AreEqual(91, SkillFormula.Eval("7+14*d(x/5)", 30));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("x^2")]
    [DataRow("d(x")]
    [DataRow("d(x))")]
    [DataRow("sqrt(x)")]
    [DataRow("3(4)")]
    [DataRow("y")]
    [DataRow("1+")]
    public void Formula_UnseenWriting_Throws(string formula) =>
        Assert.ThrowsException<InvalidDataException>(() => SkillFormula.Eval(formula, 1));

    // Level 1 and max level of the 1st job skills against research/fire-poison-skills.md section 1.1.
    [TestMethod]
    public void Levels_FirstJobSkills_MatchTheResearchTable()
    {
        void Check(string formula, int max, double lv1, double lvMax)
        {
            var values = SkillFormula.Levels(formula, max);
            Assert.AreEqual(max, values.Length);
            Assert.AreEqual(lv1, values[0], formula);
            Assert.AreEqual(lvMax, values[^1], formula);
        }
        Check("16+2*d(x/5)", 20, 16, 24);  // Energy Bolt mpCon
        Check("18+3*x", 20, 21, 78);       // Energy Bolt damage
        Check("30-2*x", 5, 28, 20);        // Teleport mpCon
        Check("115+15*x", 5, 130, 190);    // Teleport horizontal
        Check("270+5*x", 5, 275, 295);     // Teleport vertical
        Check("2+4*x", 5, 6, 22);          // Teleport psdSpeed
        Check("15+7*x", 10, 22, 85);       // Magic Guard x
        Check("10*x", 9, 10, 90);          // Magic Armor pddX
        Check("x", 20, 1, 20);             // MP Boost mmpR
        Check("20+5*x", 20, 25, 120);      // MP Boost lv2mmp
    }

    [TestMethod]
    public void MagicArmorAndMpBoost_BecomeStatRecords()
    {
        var mods = WzSkillStats.Modifiers(FirePoisonPassives(), 200).ToList();
        Assert.IsTrue(mods.Contains(new(StatKey.DEF, StatValueType.FLAT, 90, StatSource.Skill, "Magic Armor")));
        Assert.IsTrue(mods.Contains(new(StatKey.MAX_MP, StatValueType.FLAT, 24_000, StatSource.Skill, "MP Boost"))); // 120 x level 200
        Assert.IsTrue(mods.Contains(new(StatKey.MAX_MP, StatValueType.PERCENT, 20, StatSource.Skill, "MP Boost")));
        Assert.IsTrue(mods.Contains(new(StatKey.ATTACK_SPEED, StatValueType.FLAT, 1, StatSource.Skill, "MP Boost")));
    }

    // The max MP of an existing Fire/Poison save must not change: the old hard-coded rule was
    // floor((base + 120 x level) x 1.20); the skill records give the same number at every level.
    [DataTestMethod]
    [DataRow(1)]
    [DataRow(30)]
    [DataRow(120)]
    [DataRow(200)]
    public void MaxMp_SameAsTheOldHardCodedMpBoost(int level)
    {
        var mods = StatTables.Vitals(StatTables.FirePoisonMage, level).Concat(WzSkillStats.Modifiers(FirePoisonPassives(), level));
        long old = (long)Math.Floor((Progression.MaxMp(level, StatTables.FirePoisonMage) + 120.0 * level) * 1.2);
        Assert.AreEqual(old, StatAggregator.Aggregate(mods).IntValue(StatKey.MAX_MP));
    }

    // The mage has the fixed stage 4; MP Boost adds one: the panel shows 5 and the delay is shorter.
    [TestMethod]
    public void AttackSpeed_MageStageIsFourPlusTheSkillTotal()
    {
        var mods = new List<StatModifier> { StatTables.AttackSpeed(true, 0)!.Value };
        Assert.AreEqual(4.0, StatAggregator.Aggregate(mods).Value(StatKey.ATTACK_SPEED));
        mods.AddRange(WzSkillStats.Modifiers(FirePoisonPassives(), 200));
        double stage = StatAggregator.Aggregate(mods).Value(StatKey.ATTACK_SPEED);
        Assert.AreEqual(5.0, stage);
        Assert.IsTrue(StatTables.DelayScale(stage) < StatTables.DelayScale(4));
        Assert.AreEqual(1.0, StatTables.DelayScale(4));
        Assert.AreEqual(0.9375, StatTables.DelayScale(5));
    }

    // The window shows the 1st job page only; the other pages wait for their jobs.
    [TestMethod]
    public void FirstJobPage_HasSixSkills_OthersEmpty()
    {
        var pages = JobSkills.Pages(StatTables.FirePoisonMage);
        Assert.AreEqual(5, pages.Length);
        CollectionAssert.AreEqual(
            new[] { WzSkillData.EnergyBolt, WzSkillData.Teleport, WzSkillData.ManaWave, WzSkillData.MagicGuard, WzSkillData.MagicArmor, WzSkillData.MpBoost },
            pages[0]);
        Assert.IsTrue(pages.Skip(1).All(p => p.Length == 0));
        Assert.IsFalse(JobSkills.Has(StatTables.FirePoisonMage, WzSkillData.ElementalWeakness)); // invisible in WZ
    }
}
