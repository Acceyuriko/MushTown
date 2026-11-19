using MushTown.Shared;

namespace MushTown.Tests;

// The skill window lists the default passives that are skills. The list and the
// stat panel sources must name the same things (the same label), and Union and
// the traits must not be in the list.
[TestClass]
public class DefaultPassiveSkillsTests
{
    [TestMethod]
    public void EverySkillSource_IsInTheList()
    {
        var listed = DefaultPassiveSkills.All.Select(p => p.Label).ToHashSet();
        foreach (var m in DefaultPassives.All.Where(m => m.Source == StatSource.Skill))
            Assert.IsTrue(listed.Contains(m.Label), $"{m.Label} is a skill source but not in the skill window list");
    }

    [TestMethod]
    public void EveryListedSkill_IsASkillSourceOfTheStatPanel()
    {
        var sources = DefaultPassives.All.Where(m => m.Source == StatSource.Skill).Select(m => m.Label).ToHashSet();
        sources.Add(DefaultPassives.MapleWarrior(StatKey.STR, 100).Label); // Maple Warrior is built from the character
        foreach (var p in DefaultPassiveSkills.All.Where(p => p.HasStat))
            Assert.IsTrue(sources.Contains(p.Label), $"{p.Label} is listed but the stat panel has no such skill source");
    }

    [TestMethod]
    public void UnionAndTraits_AreNotInTheList()
    {
        var other = DefaultPassives.All.Where(m => m.Source is StatSource.UnionMember or StatSource.UnionAttacker or StatSource.Trait).Select(m => m.Label).ToHashSet();
        foreach (var p in DefaultPassiveSkills.All)
            Assert.IsFalse(other.Contains(p.Label), p.Label);
    }

    // The potion passive: green = attack speed +1, red = ATT +30 and max HP +2000, blue = MATT +30 and max MP +2000
    // (WZ Consume/0202.img spec of 2023126, 2023125, 2023127), all under the Skill group, and the window text says the same numbers.
    [TestMethod]
    public void MonsterParkPotion_RecordsAndTextMatchTheWz()
    {
        var potion = DefaultPassives.All.Where(m => m.Label == "Monster Park Potion").ToList();
        Assert.AreEqual(5, potion.Count);
        Assert.IsTrue(potion.All(m => m.Source == StatSource.Skill && m.Type == StatValueType.FLAT));
        double Of(StatKey key) => potion.Single(m => m.Key == key).Value;
        Assert.AreEqual(1, Of(StatKey.ATTACK_SPEED));
        Assert.AreEqual(30, Of(StatKey.ATT));
        Assert.AreEqual(30, Of(StatKey.MATT));
        Assert.AreEqual(2000, Of(StatKey.MAX_HP));
        Assert.AreEqual(2000, Of(StatKey.MAX_MP));
        var skill = DefaultPassiveSkills.All.Single(p => p.Label == "Monster Park Potion");
        Assert.AreEqual(2023126, skill.ItemIconId);
        Assert.AreEqual("Attack Speed: +1\nMax HP: +2000, Max MP: +2000\nAttack Power: +30, Magic ATT: +30", skill.Effect);
        StringAssert.DoesNotMatch(skill.Desc!, new System.Text.RegularExpressions.Regex("30 minutes"));
    }

    // A mage: the fixed stage 4, MP Boost +1, Agile Magic +2 and the potion +1 make stage 8 (the swing takes 12/16 of the stage 4 time).
    [TestMethod]
    public void MonsterParkPotion_MageAttackSpeedIsEight()
    {
        var mods = new List<StatModifier> { StatTables.AttackSpeed(true, 0)!.Value };
        mods.AddRange(WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0));
        mods.AddRange(DefaultPassives.All);
        double stage = StatAggregator.Aggregate(mods).Value(StatKey.ATTACK_SPEED);
        Assert.AreEqual(8.0, stage);
        Assert.AreEqual(0.75, StatTables.DelayScale(stage));
    }

    [TestMethod]
    public void Labels_AreUnique_AndEveryEntryHasItsText()
    {
        Assert.AreEqual(DefaultPassiveSkills.All.Count, DefaultPassiveSkills.All.Select(p => p.Label).Distinct().Count());
        // A skill the WZ does not have (WzId 0) must carry its own text and an icon.
        foreach (var p in DefaultPassiveSkills.All.Where(p => p.WzId == 0))
        {
            Assert.IsNotNull(p.Desc, p.Label);
            Assert.IsNotNull(p.Effect, p.Label);
            Assert.IsTrue(p.IconId != 0 || p.ItemIconId != 0, p.Label);
        }
    }
}
