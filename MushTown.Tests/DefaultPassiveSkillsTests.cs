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
        foreach (var p in DefaultPassiveSkills.All)
            Assert.IsTrue(sources.Contains(p.Label), $"{p.Label} is listed but the stat panel has no such skill source");
    }

    [TestMethod]
    public void UnionAndTraits_AreNotInTheList()
    {
        var other = DefaultPassives.All.Where(m => m.Source is StatSource.UnionMember or StatSource.UnionAttacker or StatSource.Trait).Select(m => m.Label).ToHashSet();
        foreach (var p in DefaultPassiveSkills.All)
            Assert.IsFalse(other.Contains(p.Label), p.Label);
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
            Assert.AreNotEqual(0, p.IconId, p.Label);
        }
    }
}
