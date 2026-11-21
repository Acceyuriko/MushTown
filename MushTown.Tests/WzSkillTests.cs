using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class WzSkillTests
{
    // One WZ skill from its common formulas (the text of Skill/200.img/skill/<id>/common).
    private static WzSkillData Skill(int id, int maxLevel, params (string Field, string Formula)[] common)
    {
        var data = new WzSkillData
        {
            SkillId = id,
            MaxLevel = maxLevel,
            Name = id switch
            {
                WzSkillData.MpBoost => "MP Boost",
                WzSkillData.Teleport => "Teleport",
                WzSkillData.HighWisdom => "High Wisdom",
                WzSkillData.AgileMagic => "Agile Magic",
                WzSkillData.SpellMastery => "Spell Mastery",
                WzSkillData.MpEater => "MP Eater",
                WzSkillData.ElementalDrain => "Elemental Drain",
                WzSkillData.BurningMagic => "Burning Magic",
                WzSkillData.ElementAmplification => "Element Amplification",
                WzSkillData.MagicCritical => "Magic Critical",
                WzSkillData.ElementalDecrease => "Elemental Decrease",
                WzSkillData.TeleportBoost => "Teleport Boost",
                WzSkillData.Meditation => "Meditation",
                WzSkillData.ElementalAdaptation => "Elemental Adaptation (Fire, Poison)",
                WzSkillData.Infinity => "Infinity",
                WzSkillData.ArcaneAim => "Arcane Aim",
                WzSkillData.BuffMastery => "Buff Mastery",
                WzSkillData.FerventDrain => "Fervent Drain",
                WzSkillData.TeleportMastery => "Teleport Mastery",
                _ => "Magic Armor",
            }
        };
        foreach (var (field, formula) in common) data.Common[field] = SkillFormula.Levels(formula, maxLevel);
        return data;
    }

    // The skills that only add numbers (Teleport's passive part too), with the WZ formulas of 200.img.
    internal static Dictionary<int, WzSkillData> FirePoisonPassives() => new()
    {
        [WzSkillData.MagicArmor] = Skill(WzSkillData.MagicArmor, 9, ("pddX", "10*x")),
        [WzSkillData.MpBoost] = BoostWithWandCrit(),
        [WzSkillData.Teleport] = Skill(WzSkillData.Teleport, 5, ("psdSpeed", "2+4*x"), ("speedMax", "4*x"), ("x", "115+15*x"), ("y", "270+5*x")),
        // The 2nd job passives with the formulas of 210.img (common; Agile Magic's psdWeaponBooster/actionSpeed is boosterActionSpeed).
        [WzSkillData.HighWisdom] = Skill(WzSkillData.HighWisdom, 5, ("intX", "8*x")),
        [WzSkillData.AgileMagic] = Skill(WzSkillData.AgileMagic, 10, ("intX", "2*x"), ("boosterActionSpeed", "-2")),
        [WzSkillData.SpellMastery] = Skill(WzSkillData.SpellMastery, 10, ("mastery", "10+4*x"), ("x", "x"), ("cr", "u(x/2)")),
        [WzSkillData.MpEater] = Skill(WzSkillData.MpEater, 9, ("prop", "3*x+3"), ("x", "d(x/2)+1"), ("y", "d(x/3)")),
        [WzSkillData.ElementalDrain] = Skill(WzSkillData.ElementalDrain, 1, ("x", "3"), ("y", "5"), ("range", "800")),
        [WzSkillData.Meditation] = Skill(WzSkillData.Meditation, 20, ("indieMad", "10+x")), // a passive here: always-on magic attack
        // The 3rd job passives with the formulas of 211.img (common).
        [WzSkillData.BurningMagic] = Skill(WzSkillData.BurningMagic, 10, ("x", "50+5*x"), ("z", "5+x")),
        [WzSkillData.ElementAmplification] = Skill(WzSkillData.ElementAmplification, 10, ("costmpR", "5*x"), ("damR", "5*x")),
        [WzSkillData.MagicCritical] = Skill(WzSkillData.MagicCritical, 10, ("cr", "10+2*x"), ("criticaldamage", "3+x")),
        [WzSkillData.ElementalDecrease] = Skill(WzSkillData.ElementalDecrease, 9, ("mdR", "4*x+4"), ("u", "x+1")),
        [WzSkillData.TeleportBoost] = Skill(WzSkillData.TeleportBoost, 1, ("x", "65"), ("y", "25")),
        [WzSkillData.ElementalAdaptation] = Skill(WzSkillData.ElementalAdaptation, 20, ("asrR", "x"), ("terR", "x")), // always-on resistances (the cast shield is not a stat)
    };

    // The 4th job passives (212.img) on top of the lower jobs, with the WZ formulas at max level: Infinity (mdR/x/s), Arcane Aim
    // (prop/x/y/ignoreMobpdpR/time), Buff Mastery (madX/bufftimeR/stanceProp), Fervent Drain (x), and Teleport Mastery's stanceProp (2111007).
    internal static Dictionary<int, WzSkillData> FourthJobPassives()
    {
        var all = FirePoisonPassives();
        all[WzSkillData.TeleportMastery] = Skill(WzSkillData.TeleportMastery, 10, ("stanceProp", "4*x"));
        all[WzSkillData.Infinity] = Skill(WzSkillData.Infinity, 30, ("mdR", "7+14*d(x/5)"), ("x", "5"), ("s", "10"));
        all[WzSkillData.ArcaneAim] = Skill(WzSkillData.ArcaneAim, 30, ("prop", "25+5*d(x/2)"), ("x", "2+d(x/5)"), ("y", "5"), ("ignoreMobpdpR", "5+u(x/2)"), ("time", "5"));
        all[WzSkillData.BuffMastery] = Skill(WzSkillData.BuffMastery, 10, ("madX", "3*x"), ("bufftimeR", "5*x"), ("stanceProp", "6*x"));
        all[WzSkillData.FerventDrain] = Skill(WzSkillData.FerventDrain, 1, ("x", "5"));
        return all;
    }

    // psdWT/37/cr = 5 in 200.img: the wand (item family 137, weapon code 37) gives 5 crit rate.
    private static WzSkillData BoostWithWandCrit()
    {
        var boost = Skill(WzSkillData.MpBoost, 20, ("mmpR", "x"), ("lv2mmp", "20+5*x"), ("actionSpeed", "-1"));
        boost.WeaponBonus[37] = new() { ["cr"] = SkillFormula.Levels("5", 20) };
        return boost;
    }

    private const int Wand = 1372207, Staff = 1382000;

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

    // Journey's attack-frame rule: the first positive delay is the attack frame; the time is the sum of the
    // absolute delays before it; missing and zero delays count as 100 ms.
    [TestMethod]
    public void AttackDelay_UsesTheFirstPositiveFrame_AndAbsoluteDelaysBeforeIt()
    {
        Assert.AreEqual(390, WzSkillData.AttackDelayFrom(new int?[] { -60, -330, 270 })); // Energy Bolt
        Assert.AreEqual(200, WzSkillData.AttackDelayFrom(new int?[] { -100, 0, 50 })); // zero counts as 100
        Assert.AreEqual(100, WzSkillData.AttackDelayFrom(new int?[] { null, 300 })); // missing counts as 100
        Assert.AreEqual(90, WzSkillData.AttackDelayFrom(new int?[] { -90, 200, 10 })); // the first positive wins
        Assert.ThrowsException<InvalidDataException>(() => WzSkillData.AttackDelayFrom(new int?[] { -60, -330 }));
    }

    [TestMethod]
    public void MagicArmorAndMpBoost_BecomeStatRecords()
    {
        var mods = WzSkillStats.Modifiers(FirePoisonPassives(), 200, 0).ToList();
        Assert.IsTrue(mods.Contains(new(StatKey.DEF, StatValueType.FLAT, 90, StatSource.Skill, "Magic Armor")));
        Assert.IsTrue(mods.Contains(new(StatKey.MAX_MP, StatValueType.FLAT, 24_000, StatSource.Skill, "MP Boost"))); // 120 x level 200
        Assert.IsTrue(mods.Contains(new(StatKey.MAX_MP, StatValueType.PERCENT, 20, StatSource.Skill, "MP Boost")));
        Assert.IsTrue(mods.Contains(new(StatKey.ATTACK_SPEED, StatValueType.FLAT, 1, StatSource.Skill, "MP Boost")));
    }

    // Teleport's passive: psdSpeed 22 is percent points of the base speed (a PERCENT record on SPEED, like the
    // equipment incSpeed), speedMax 20 is percent points added to the SPEED cap. Source Skill, label Teleport.
    [TestMethod]
    public void Teleport_PassiveSpeed_IsPercentPointsAndRaisesTheCap()
    {
        var mods = WzSkillStats.Modifiers(FirePoisonPassives(), 200, 0).ToList();
        Assert.IsTrue(mods.Contains(new(StatKey.SPEED, StatValueType.PERCENT, 22, StatSource.Skill, "Teleport")));
        Assert.IsTrue(mods.Contains(new(StatKey.SPEED_MAX, StatValueType.FLAT, 20, StatSource.Skill, "Teleport")));
        var baseSpeed = new StatModifier(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, StatSource.Base);
        var shoes = new StatModifier(StatKey.SPEED, StatValueType.PERCENT, 5, StatSource.Equipment);
        Assert.AreEqual(105.0, StatAggregator.Aggregate(new[] { baseSpeed, shoes }).SpeedPercent, 1e-6);
        Assert.AreEqual(127.0, StatAggregator.Aggregate(mods.Append(baseSpeed).Append(shoes)).SpeedPercent, 1e-6); // 100 + 5 + 22
    }

    // The cap is 140% of the base; speedMax lifts it by its percent points (20 -> 160%).
    [TestMethod]
    public void SpeedCap_Is140Percent_AndSpeedMaxRaisesIt()
    {
        var baseSpeed = new StatModifier(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, StatSource.Base);
        var boots = new StatModifier(StatKey.SPEED, StatValueType.PERCENT, 80, StatSource.Equipment); // 180% uncapped
        Assert.AreEqual(140.0, StatAggregator.Aggregate(new[] { baseSpeed, boots }).SpeedPercent, 1e-6);
        var max = new StatModifier(StatKey.SPEED_MAX, StatValueType.FLAT, 20, StatSource.Skill);
        Assert.AreEqual(160.0, StatAggregator.Aggregate(new[] { baseSpeed, boots, max }).SpeedPercent, 1e-6);
    }

    // MP Boost: crit rate +5 while a wand is worn (WZ psdWT code 37). The staff (code 38) has no entry: +0.
    [TestMethod]
    public void MpBoost_WandCrit_OnlyWithAWand()
    {
        var crit = new StatModifier(StatKey.CRIT_RATE, StatValueType.FLAT, 5, StatSource.Skill, "MP Boost");
        Assert.IsTrue(WzSkillStats.Modifiers(FirePoisonPassives(), 200, Wand).Contains(crit));
        Assert.IsFalse(WzSkillStats.Modifiers(FirePoisonPassives(), 200, Staff).Any(m => m.Key == StatKey.CRIT_RATE && m.Label == "MP Boost"));
        Assert.IsFalse(WzSkillStats.Modifiers(FirePoisonPassives(), 200, 0).Any(m => m.Key == StatKey.CRIT_RATE && m.Label == "MP Boost"));
        Assert.IsFalse(WzSkillStats.Modifiers(FirePoisonPassives(), 200, 1302000).Any(m => m.Key == StatKey.CRIT_RATE && m.Label == "MP Boost")); // a sword
        Assert.AreEqual(40.0, StatAggregator.Aggregate(WzSkillStats.Modifiers(FirePoisonPassives(), 200, Wand)).Value(StatKey.CRIT_RATE)); // the wand's 5, Spell Mastery's 5 and Magic Critical's 30
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
        var mods = StatTables.Vitals(StatTables.FirePoisonMage, level).Concat(WzSkillStats.Modifiers(FirePoisonPassives(), level, 0));
        long old = (long)Math.Floor((Progression.MaxMp(level, StatTables.FirePoisonMage) + 120.0 * level) * 1.2);
        Assert.AreEqual(old, StatAggregator.Aggregate(mods).IntValue(StatKey.MAX_MP));
    }

    // The mage has the fixed stage 4; MP Boost adds one and Agile Magic two: the panel shows 7 and the delay is shorter.
    [TestMethod]
    public void AttackSpeed_MageStageIsFourPlusTheSkillTotal()
    {
        var mods = new List<StatModifier> { StatTables.AttackSpeed(true, 0)!.Value };
        Assert.AreEqual(4.0, StatAggregator.Aggregate(mods).Value(StatKey.ATTACK_SPEED));
        mods.AddRange(WzSkillStats.Modifiers(FirePoisonPassives(), 200, 0));
        double stage = StatAggregator.Aggregate(mods).Value(StatKey.ATTACK_SPEED);
        Assert.AreEqual(7.0, stage);
        Assert.IsTrue(StatTables.DelayScale(stage) < StatTables.DelayScale(4));
        Assert.AreEqual(1.0, StatTables.DelayScale(4));
        Assert.AreEqual(0.9375, StatTables.DelayScale(5));
    }

    // The window shows the 1st to 4th job pages (page IV: the four 4th job passives); page V waits for its job.
    [TestMethod]
    public void FirstFourPages_HaveTheirSkills_LastEmpty()
    {
        var pages = JobSkills.Pages(StatTables.FirePoisonMage);
        Assert.AreEqual(5, pages.Length);
        CollectionAssert.AreEqual(
            new[] { WzSkillData.EnergyBolt, WzSkillData.Teleport, WzSkillData.ManaWave, WzSkillData.MagicGuard, WzSkillData.MagicArmor, WzSkillData.MpBoost },
            pages[0]);
        CollectionAssert.AreEqual(
            new[] { WzSkillData.MpEater, WzSkillData.SpellMastery, WzSkillData.HighWisdom, WzSkillData.ElementalDrain, WzSkillData.AgileMagic, WzSkillData.Meditation, WzSkillData.FlameOrb, WzSkillData.PoisonBreath, WzSkillData.Ignite },
            pages[1]);
        CollectionAssert.AreEqual(
            new[] { WzSkillData.BurningMagic, WzSkillData.ElementAmplification, WzSkillData.MagicCritical, WzSkillData.ElementalDecrease, WzSkillData.TeleportBoost, WzSkillData.Explosion, WzSkillData.PoisonMist, WzSkillData.ElementalAdaptation, WzSkillData.TeleportMastery, WzSkillData.CreepingToxin },
            pages[2]);
        CollectionAssert.AreEqual(
            new[] { WzSkillData.Infinity, WzSkillData.ArcaneAim, WzSkillData.BuffMastery, WzSkillData.FerventDrain },
            pages[3]);
        Assert.AreEqual(0, pages[4].Length);
        Assert.IsFalse(JobSkills.Has(StatTables.FirePoisonMage, WzSkillData.ElementalWeakness)); // invisible in WZ
    }
}
