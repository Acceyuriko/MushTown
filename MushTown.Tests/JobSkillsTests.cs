using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class JobSkillsTests
{
    [TestMethod]
    public void Hero_OwnsEveryWarriorLineSkill_IncludingEndure()
    {
        var all = new[]
        {
            SkillData.SlashBlast, SkillData.IronBody, SkillData.WarLeap, SkillData.WarriorMastery, SkillData.LeapAttack, SkillData.GuardianArmor,
            SkillData.Brandish, SkillData.WeaponMastery, SkillData.SpiritBlade, SkillData.ComboAttack, SkillData.FlashBlade, SkillData.AgileArms, SkillData.FinalAttack, SkillData.PhysicalTraining,
            SkillData.IntrepidSlash, SkillData.BeamBlade, SkillData.Rush, SkillData.ScarringSword, SkillData.ComboSynergy, SkillData.SelfRecovery, SkillData.ChanceAttack, SkillData.Endure,
        };

        Assert.AreEqual(all.Length, JobSkills.Pages(StatTables.Hero).Sum(p => p.Length));
        foreach (int id in all) Assert.IsTrue(JobSkills.Has(StatTables.Hero, id), $"Hero lacks {id}");
        Assert.IsTrue(JobSkills.Has(StatTables.Hero, SkillData.Endure));
    }

    [TestMethod]
    public void FirePoison_OwnsOnlyTheFirstJobPage_NotEndureNorAnyWarriorSkill()
    {
        Assert.AreEqual(5, JobSkills.Pages(StatTables.FirePoisonMage).Length);
        Assert.AreEqual(6, JobSkills.Pages(StatTables.FirePoisonMage)[0].Length);
        Assert.IsTrue(JobSkills.Pages(StatTables.FirePoisonMage).Skip(1).All(p => p.Length == 0));
        foreach (int id in JobSkills.Pages(StatTables.Hero).SelectMany(p => p))
            Assert.IsFalse(JobSkills.Has(StatTables.FirePoisonMage, id), $"Fire/Poison owns {id}");
        Assert.IsFalse(JobSkills.Has(StatTables.FirePoisonMage, SkillData.Endure));
    }

    private static SkillData Def(int id, int maxLevel, int mp = 10) =>
        new() { SkillId = id, MaxLevel = maxLevel, Kind = SkillData.KindActive, Mp = mp };

    [TestMethod]
    public void Level_IsMaxForOwnJob_ZeroForAnotherJob()
    {
        var endure = Def(SkillData.Endure, 30);
        var brandish = Def(SkillData.Brandish, 20);
        Assert.AreEqual(30, JobSkills.Level(StatTables.Hero, endure));
        Assert.AreEqual(20, JobSkills.Level(StatTables.Hero, brandish));
        Assert.AreEqual(0, JobSkills.Level(StatTables.FirePoisonMage, endure));
        Assert.AreEqual(0, JobSkills.Level(StatTables.FirePoisonMage, brandish));
    }

    // The client casts through Progression.CanCast with this level: level 0 never casts.
    [TestMethod]
    public void FirePoison_CannotCastHeroSkills_HeroCan()
    {
        var brandish = Def(SkillData.Brandish, 20);
        var rich = new CharaSlot { Level = 200, Mp = 1000 };
        Assert.IsFalse(Progression.CanCast(rich, brandish, JobSkills.Level(StatTables.FirePoisonMage, brandish), 0));
        Assert.IsTrue(Progression.CanCast(rich, brandish, JobSkills.Level(StatTables.Hero, brandish), 0));
    }

    // RefreshStats (client, needs the game) adds Endure as HpPct x level. Here that one record is built the same
    // way on top of Vitals + the default passives: Fire/Poison stays at the test values, Hero gains HP.
    [TestMethod]
    public void MaxHp_FirePoisonHasNoEndureRecord_HeroDoes()
    {
        var endure = Def(SkillData.Endure, 30);
        endure.HpPct = 1;
        StatModifier Record(int job) => new(StatKey.MAX_HP, StatValueType.PERCENT, endure.HpPct * JobSkills.Level(job, endure), StatSource.Skill);
        var mage = StatAggregator.Aggregate(StatTables.Vitals(StatTables.FirePoisonMage, 200).Concat(DefaultPassives.All).Append(Record(StatTables.FirePoisonMage)));
        var hero = StatAggregator.Aggregate(StatTables.Vitals(StatTables.Hero, 200).Concat(DefaultPassives.All));
        var heroEndure = StatAggregator.Aggregate(StatTables.Vitals(StatTables.Hero, 200).Concat(DefaultPassives.All).Append(Record(StatTables.Hero)));
        Assert.AreEqual(17_991, mage.IntValue(StatKey.MAX_HP));
        Assert.IsTrue(heroEndure.IntValue(StatKey.MAX_HP) > hero.IntValue(StatKey.MAX_HP));
    }
}
