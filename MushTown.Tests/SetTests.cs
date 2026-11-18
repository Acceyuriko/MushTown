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
    }

    // The real tier table of set 448 (assets/set/448.json): each tier has only its own lines.
    private static SetData MagicianWithEffects()
    {
        var set = Magician();
        SetEffect E(string key, int value) => new() { Key = key, Value = value };
        set.Tiers[0].Effects.AddRange(new[] { E("incPDD", 140), E("incMDD", 140), E("incACC", 140), E("incEVA", 140) });
        set.Tiers[1].Effects.AddRange(new[] { E("incMHPr", 9), E("incMMPr", 9), E("nbdR", 2) });
        set.Tiers[2].Effects.AddRange(new[] { E("incMAD", 9), E("incINT", 9), E("nbdR", 4) });
        set.Tiers[3].Effects.AddRange(new[] { E("incPDD", 250), E("incMDD", 250), E("incAllStat", 15), E("ignoreTargetDEF", 10), E("nbdR", 6) });
        set.Tiers[4].Effects.AddRange(new[] { E("incPAD", 10), E("incMAD", 10), E("ignoreTargetDEF", 10), E("nbdR", 8) });
        return set;
    }

    private static readonly int[] SixPieces = { 1004230, 1052800, 1072968, 1082609, 1372207, 1102719 };

    private static StatSnapshot WithSet(int pieces, int weapon = 1372207)
    {
        var sets = new Dictionary<int, SetData> { [448] = MagicianWithEffects() };
        var worn = SixPieces.Take(4).Concat(new[] { weapon, 1102719 }).Take(pieces).ToList();
        return StatAggregator.Aggregate(SetRules.Modifiers(sets, worn));
    }

    [TestMethod]
    public void Modifiers_OnePieceDoesNothing_TwoPiecesGiveOnlyDefense()
    {
        Assert.AreEqual(0, WithSet(1).Modifiers.Count);
        var two = WithSet(2);
        Assert.AreEqual(140, two.Value(StatKey.DEF));
        Assert.AreEqual(0, two.Value(StatKey.NORMAL_PCT));
        Assert.AreEqual(0, two.Value(StatKey.MAX_HP)); // incMDD, incACC and incEVA have no key: no record at all
        Assert.IsTrue(two.Modifiers.All(m => m.Source == StatSource.Set && m.Key == StatKey.DEF));
    }

    // Piece count by piece count, the stats of 3 to 6 pieces are the sum of the tiers up to it.
    [TestMethod]
    public void Modifiers_TiersAddUp()
    {
        var three = WithSet(3);
        Assert.AreEqual(9, three.Modifiers.Single(m => m.Key == StatKey.MAX_HP).Value);
        Assert.AreEqual(9, three.Modifiers.Single(m => m.Key == StatKey.MAX_MP).Value);
        Assert.AreEqual(2, three.Value(StatKey.NORMAL_PCT));

        var four = WithSet(4);
        Assert.AreEqual(9, four.Value(StatKey.MATT));
        Assert.AreEqual(9, four.Value(StatKey.INT));
        Assert.AreEqual(6, four.Value(StatKey.NORMAL_PCT));

        var five = WithSet(5);
        Assert.AreEqual(390, five.Value(StatKey.DEF));
        Assert.AreEqual(15, five.Value(StatKey.STR));
        Assert.AreEqual(15, five.Value(StatKey.DEX));
        Assert.AreEqual(24, five.Value(StatKey.INT)); // 9 + 15
        Assert.AreEqual(15, five.Value(StatKey.LUK));
        Assert.AreEqual(12, five.Value(StatKey.NORMAL_PCT));
        Assert.AreEqual(0.9, five.Multiplier(StatKey.IED_PCT), 1e-9);

        var six = WithSet(6);
        Assert.AreEqual(10, six.Value(StatKey.ATT));
        Assert.AreEqual(19, six.Value(StatKey.MATT));
        Assert.AreEqual(20, six.Value(StatKey.NORMAL_PCT));
        Assert.AreEqual(0.81, six.Multiplier(StatKey.IED_PCT), 1e-9); // two 10% lines multiply: 19%
    }

    [TestMethod]
    public void Modifiers_AnyOfTheWeaponsCountsOnce_TakingAPieceOffDropsTheTier()
    {
        var sets = new Dictionary<int, SetData> { [448] = MagicianWithEffects() };
        var worn = new List<int> { 1004230, 1052800, 1072968, 1082609, 1372207, 1382245 }; // two weapons, no cape
        Assert.AreEqual(5, SetRules.WornCount(sets[448], worn));
        Assert.AreEqual(12, StatAggregator.Aggregate(SetRules.Modifiers(sets, worn)).Value(StatKey.NORMAL_PCT));
        worn.Remove(1382245); // the second weapon off: still 5
        Assert.AreEqual(12, StatAggregator.Aggregate(SetRules.Modifiers(sets, worn)).Value(StatKey.NORMAL_PCT));
        worn.Remove(1372207); // the only weapon off: 4 pieces, the 5 piece tier is gone
        var four = StatAggregator.Aggregate(SetRules.Modifiers(sets, worn));
        Assert.AreEqual(6, four.Value(StatKey.NORMAL_PCT));
        Assert.AreEqual(1.0, four.Multiplier(StatKey.IED_PCT));
        Assert.AreEqual(0, StatAggregator.Aggregate(SetRules.Modifiers(sets, null)).Modifiers.Count);
    }

    // The normal-monster damage of the set counts against a normal monster only, the boss damage is its own number.
    [TestMethod]
    public void NormalMonsterDamage_CountsOnlyAgainstNonBosses()
    {
        var six = WithSet(6);
        Assert.AreEqual(1.20, DamageChain.DamageMultiplier(six, boss: false), 1e-9);
        Assert.AreEqual(1.00, DamageChain.DamageMultiplier(six, boss: true), 1e-9);
    }

    [TestMethod]
    public void Hover_ListsTheSetAsASource()
    {
        var six = WithSet(6);
        Assert.IsTrue(StatDisplay.Sources(six, 11).Any(l => l.Label == "Set Effect" && l.Value == 20)); // row 11: normal enemy damage
        Assert.IsTrue(StatDisplay.Sources(six, 4).Any(l => l.Label == "Set Effect" && l.Value == 24)); // row 4: INT
        var ied = StatDisplay.Sources(six, StatDisplay.IedRow);
        Assert.AreEqual(2, ied.Count);
        Assert.IsTrue(ied.All(l => l.Label == "8th Magician Set (5)" || l.Label == "8th Magician Set (6)"));
    }
}
