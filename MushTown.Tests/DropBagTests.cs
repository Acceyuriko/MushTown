using MushTown.Shared;

namespace MushTown.Tests;

// The bag a pickup returns to comes from the drop's source tab; monster drops
// have no source and use the item kind.
[TestClass]
public sealed class DropBagTests
{
    [TestMethod]
    public void EquipSource_GoesToEquipBagAndEquipOrder()
    {
        var chara = new CharaSlot();
        var data = new ItemData { ItemId = ItemData.Mop, Slot = "Wp" };

        string tab = DropBagRule.FromSource("equip", data);
        Assert.AreEqual("equip", tab);
        Assert.IsTrue(Inventory.Add(chara, tab, ItemData.Mop, 1));
        Assert.AreEqual(1, chara.EquipBag![ItemData.Mop]);
        Assert.AreEqual(ItemData.Mop, chara.EquipOrder![0]);
    }

    [TestMethod]
    public void UseSource_GoesToUseBagAndUseOrder()
    {
        var chara = new CharaSlot();
        var data = new ItemData { ItemId = ItemData.RedPotion };

        string tab = DropBagRule.FromSource("use", data);
        Assert.AreEqual("use", tab);
        Assert.IsTrue(Inventory.Add(chara, tab, ItemData.RedPotion, 3));
        Assert.AreEqual(3, chara.Use![ItemData.RedPotion]);
        Assert.AreEqual(ItemData.RedPotion, chara.UseOrder![0]);
    }

    [TestMethod]
    public void CashSource_GoesToCashBagAndCashOrder()
    {
        var chara = new CharaSlot();
        const int id = 5400000; // Character Name Change, a non-decorative cash item
        var data = new ItemData { ItemId = id, Cash = true };

        string tab = DropBagRule.FromSource("cash", data);
        Assert.AreEqual("cash", tab);
        Assert.IsTrue(Inventory.Add(chara, tab, id, 1));
        Assert.AreEqual(1, chara.CashBag![id]);
        Assert.AreEqual(id, chara.CashOrder![0]);
    }

    [TestMethod]
    public void DecSource_GoesToDecBagAndDecOrder()
    {
        var chara = new CharaSlot();
        var data = new ItemData { ItemId = ItemData.HairCouponAlt, Cash = true, Cosmetic = ItemData.AltHair };

        string tab = DropBagRule.FromSource("dec", data);
        Assert.AreEqual("dec", tab);
        Assert.IsTrue(Inventory.Add(chara, tab, ItemData.HairCouponAlt, 1));
        Assert.AreEqual(1, chara.DecBag![ItemData.HairCouponAlt]);
        Assert.AreEqual(ItemData.HairCouponAlt, chara.DecOrder![0]);
    }

    // A non-cash fashion bought from the cash shop lives in DEC; dropping it
    // must return it there, not to EQUIP.
    [TestMethod]
    public void NonCashFashionFromDec_StaysInDec()
    {
        var chara = new CharaSlot();
        var data = new ItemData { ItemId = ItemData.LongSword, Slot = "Wp" };

        string tab = DropBagRule.FromSource("dec", data);
        Assert.AreEqual("dec", tab);
        Assert.IsTrue(Inventory.Add(chara, tab, ItemData.LongSword, 1));
        Assert.AreEqual(1, chara.DecBag![ItemData.LongSword]);
        Assert.AreEqual(ItemData.LongSword, chara.DecOrder![0]);
        Assert.IsNull(chara.EquipBag);
    }

    [TestMethod]
    public void MonsterDrops_UseItemKind()
    {
        Assert.AreEqual("equip", DropBagRule.ForMonster(new ItemData { Slot = "Wp" }));
        Assert.AreEqual("use", DropBagRule.ForMonster(new ItemData()));
    }

    [TestMethod]
    public void CodeAndName_RoundTripAllValues()
    {
        foreach (string tab in new[] { "", "use", "equip", "cash", "dec" })
            Assert.AreEqual(tab, DropBagRule.Name(DropBagRule.Code(tab)));
        Assert.ThrowsException<InvalidDataException>(() => DropBagRule.Name(5));
        Assert.ThrowsException<InvalidDataException>(() => DropBagRule.Code("bogus"));
    }
}
