using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SetTests
{
    // The shape of the 8th Magician Set: five fixed parts and one choose-one
    // weapon part (three of the eight weapons here), tiers 2 to 6.
    private static SetData Magician() => new()
    {
        Id = 448,
        Name = "8th Magician Set",
        Parts =
        {
            new SetPart { Slot = "Cp", Ids = { 1004230 } },
            new SetPart { Slot = "MaPn", Ids = { 1052800 } },
            new SetPart { Slot = "So", Ids = { 1072968 } },
            new SetPart { Slot = "Gv", Ids = { 1082609 } },
            new SetPart { Slot = "Wp", Ids = { 1212101, 1372207, 1382245 } },
            new SetPart { Slot = "Sr", Ids = { 1102719 } },
        },
        Tiers =
        {
            new SetTier { Count = 2 }, new SetTier { Count = 3 }, new SetTier { Count = 4 },
            new SetTier { Count = 5 }, new SetTier { Count = 6 },
        },
    };

    [TestMethod]
    public void WornCount_CountsEachPartOnce()
    {
        var set = Magician();
        Assert.AreEqual(0, SetRules.WornCount(set, null));
        Assert.AreEqual(0, SetRules.WornCount(set, new List<int> { 1302000 })); // a non-set piece counts nothing
        Assert.AreEqual(3, SetRules.WornCount(set, new List<int> { 1004230, 1052800, 1372207, 1302000 }));
        Assert.AreEqual(6, SetRules.WornCount(set, new List<int> { 1004230, 1052800, 1072968, 1082609, 1372207, 1102719 }));
    }

    [TestMethod]
    public void TierActive_NeedsAtLeastItsPieceCount()
    {
        var set = Magician();
        var three = new List<int> { 1004230, 1052800, 1372207 };
        var active = set.Tiers.Where(t => SetRules.TierActive(set, t, three)).Select(t => t.Count).ToList();
        CollectionAssert.AreEqual(new[] { 2, 3 }, active); // 4, 5 and 6 stay dim
        var all = new List<int> { 1004230, 1052800, 1072968, 1082609, 1372207, 1102719 };
        Assert.IsTrue(set.Tiers.All(t => SetRules.TierActive(set, t, all)));
        Assert.IsFalse(set.Tiers.Any(t => SetRules.TierActive(set, t, null)));
    }

    // Any of the weapon candidates lights the weapon part; two of them are still one piece.
    [TestMethod]
    public void ChooseOnePart_AnyCandidateCountsOnce()
    {
        var set = Magician();
        var weapon = set.Parts[4];
        Assert.IsTrue(SetRules.PartWorn(weapon, new List<int> { 1212101 }));
        Assert.IsTrue(SetRules.PartWorn(weapon, new List<int> { 1382245 }));
        Assert.IsFalse(SetRules.PartWorn(weapon, new List<int> { 1004230 }));
        Assert.AreEqual(1, SetRules.WornCount(set, new List<int> { 1212101, 1382245 }));
    }

    [TestMethod]
    public void Of_NonSetItemHasNoSet()
    {
        var sets = new Dictionary<int, SetData> { [448] = Magician() };
        Assert.IsNull(SetRules.Of(new ItemData { ItemId = 1302000 }, sets));
        Assert.AreEqual("8th Magician Set", SetRules.Of(new ItemData { ItemId = 1372207, SetId = 448 }, sets)!.Name);
    }

    [TestMethod]
    public void Line_UsesTheEffectWords()
    {
        Assert.AreEqual("Defense : 140", SetRules.Line(new SetEffect { Key = "incPDD", Value = 140 }));
        Assert.AreEqual("Max HP : 9%", SetRules.Line(new SetEffect { Key = "incMHPr", Value = 9 }));
        Assert.AreEqual("Damage vs. Normal Monsters : +2%", SetRules.Line(new SetEffect { Key = "nbdR", Value = 2 }));
        Assert.AreEqual("Ignore Enemy Defense : +10%", SetRules.Line(new SetEffect { Key = "ignoreTargetDEF", Value = 10 }));
    }

    [TestMethod]
    public void Category_NamesWeaponsBySlotAndId()
    {
        Assert.AreEqual("Wand", ItemCategory.Of(1372207, "Wp"));
        Assert.AreEqual("Staff (One-Handed Weapon)", ItemCategory.Of(1382245, "Wp"));
        Assert.AreEqual("Hat", ItemCategory.Of(1004230, "Cp"));
        Assert.IsNull(ItemCategory.Of(1990000, "Wp")); // an unknown weapon group: the exporter refuses it
        Assert.AreEqual("Slow (8)", ItemCategory.Speed(8));
        Assert.AreEqual("Normal (6)", ItemCategory.Speed(6));
    }
}
