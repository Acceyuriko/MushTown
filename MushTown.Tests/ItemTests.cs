using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class ItemTests
{
    // The balloon style of the worn rings: the first chat ring wins, a
    // name-tag ring names none, and an id with no def is skipped.
    [TestMethod]
    public void WornBalloonStyle_FirstChatRingWins()
    {
        var defs = new Dictionary<int, ItemData>
        {
            [1112134] = new ItemData { ItemId = 1112134, Slot = "Ri", NameTag = 58 },        // name-tag ring
            [1112200] = new ItemData { ItemId = 1112200, Slot = "Ri", ChatBalloon = 1 },     // Pink Quote Ring
            [1112205] = new ItemData { ItemId = 1112205, Slot = "Ri", ChatBalloon = 7 },     // Blue-Flowered Quote Ring
        };

        Assert.AreEqual(0, ItemData.WornBalloonStyle(null, defs));
        Assert.AreEqual(0, ItemData.WornBalloonStyle(new List<int>(), defs));
        Assert.AreEqual(0, ItemData.WornBalloonStyle(new List<int> { 1112134 }, defs));
        Assert.AreEqual(1, ItemData.WornBalloonStyle(new List<int> { 1112200 }, defs));
        Assert.AreEqual(1, ItemData.WornBalloonStyle(new List<int> { 1112134, 1112200, 1112205 }, defs));
        Assert.AreEqual(7, ItemData.WornBalloonStyle(new List<int> { 9999999, 1112205 }, defs));
    }

    // Worn bonuses: the mop's PAD is the weapon, the sergeant's DEX/PDD
    // sum in; null/unknown/potion ids grant nothing.
    [TestMethod]
    public void EquipBonus_SumsWornGear()
    {
        var defs = new Dictionary<int, ItemData>
        {
            [ItemData.Mop] = new ItemData { ItemId = ItemData.Mop, Slot = "Wp", Atk = 47 },
            [ItemData.Sergeant] = new ItemData { ItemId = ItemData.Sergeant, Slot = "Ma", Def = 25, Dex = 1 },
            [ItemData.RedPotion] = new ItemData { ItemId = ItemData.RedPotion, Hp = 50 },
        };

        var b = ItemData.EquipBonus(new List<int> { ItemData.Mop, ItemData.Sergeant }, defs);
        Assert.AreEqual(47, b.weapon);
        Assert.AreEqual(0, b.str);
        Assert.AreEqual(1, b.dex);
        Assert.AreEqual(25, b.def);
        Assert.AreEqual((0, 0, 0, 0), ItemData.EquipBonus(null, defs));
        Assert.AreEqual((0, 0, 0, 0), ItemData.EquipBonus(new List<int> { 999, ItemData.RedPotion }, defs));
    }
}
