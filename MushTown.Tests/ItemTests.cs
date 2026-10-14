using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class ItemTests
{
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
