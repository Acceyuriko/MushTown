using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class ShopTests
{
    private static CharaSlot Hero(long mesos) => new() { Name = "hero", Mesos = mesos };

    private static ItemData Def(int id, int price, string? slot = null) =>
        new() { ItemId = id, Price = price, Slot = slot };

    // A 0-price item (cash gear) sells for nothing: no free-money loop.
    [TestMethod]
    public void SellPrice_ZeroPrice_IsZero()
    {
        Assert.AreEqual(0, Shop.SellPrice(0));
        Assert.AreEqual(1, Shop.SellPrice(1));
        Assert.AreEqual(1, Shop.SellPrice(2));
    }
    [TestMethod]
    public void SellPrice_HalvesMinOne()
    {
        Assert.AreEqual(1, Shop.SellPrice(3)); // red potion
        Assert.AreEqual(5, Shop.SellPrice(10)); // blue potion
        Assert.AreEqual(400, Shop.SellPrice(800)); // sergeant
        Assert.AreEqual(950, Shop.SellPrice(1900)); // mop
        Assert.AreEqual(1, Shop.SellPrice(1));
    }

    [TestMethod]
    public void Buy_Ok_ChargesAndBags()
    {
        var hero = Hero(10);
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, 3, Def(ItemData.RedPotion, 3)));
        Assert.AreEqual(7, hero.Mesos);
        Assert.AreEqual(1, hero.Use![ItemData.RedPotion]);
    }

    [TestMethod]
    public void Buy_Count_ChargesOnceForTheWholeStack()
    {
        var hero = Hero(100);
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, 3, Def(ItemData.RedPotion, 3), 4));
        Assert.AreEqual(88, hero.Mesos);
        Assert.AreEqual(4, hero.Use![ItemData.RedPotion]);
    }

    [TestMethod]
    public void Buy_Count_NotEnoughMesos_ChangesNothing()
    {
        var hero = Hero(100);
        Assert.IsFalse(Shop.Buy(hero, ItemData.RedPotion, 30, Def(ItemData.RedPotion, 30), 4));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Buy_Equip_GoesToTheEquipBag()
    {
        var hero = Hero(100);
        Assert.IsTrue(Shop.Buy(hero, ItemData.Mop, 50, Def(ItemData.Mop, 50, "Wp")));
        Assert.AreEqual(50, hero.Mesos);
        Assert.AreEqual(1, hero.EquipBag![ItemData.Mop]);
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Buy_Equip_Count_EachCopyGetsItsOwnCell()
    {
        var hero = Hero(1000);
        Assert.IsTrue(Shop.Buy(hero, ItemData.Mop, 50, Def(ItemData.Mop, 50, "Wp"), 4));
        Assert.AreEqual(800, hero.Mesos);
        Assert.AreEqual(4, hero.EquipBag![ItemData.Mop]);
        Assert.AreEqual(4, Inventory.Used(hero, "equip"));
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Buy_FullBag_DoesNotCharge()
    {
        var hero = Hero(100);
        hero.Use = new Dictionary<int, int>();
        for (int id = 1; id <= Inventory.Capacity; id++) hero.Use[id] = 1;
        Inventory.Ensure(hero);
        var def = Def(999999, 10);

        Assert.IsFalse(Shop.Buy(hero, 999999, 10, def));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(999999));
    }

    [TestMethod]
    public void Buy_Broke_ChangesNothing()
    {
        var hero = Hero(2);
        Assert.IsFalse(Shop.Buy(hero, ItemData.RedPotion, 3, Def(ItemData.RedPotion, 3)));
        Assert.AreEqual(2, hero.Mesos);
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Sell_Potion_PaysAndDecrements()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 2 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, Def(ItemData.BluePotion, 10)));
        Assert.AreEqual(5, hero.Mesos);
        Assert.AreEqual(1, hero.Use[ItemData.BluePotion]);
    }

    [TestMethod]
    public void Sell_LastBottle_RemovesTheRow()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.RedPotion] = 1 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.RedPotion, Def(ItemData.RedPotion, 3)));
        Assert.AreEqual(1, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(ItemData.RedPotion));
    }

    [TestMethod]
    public void Sell_Count_SellsTheWholeStack()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 5 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, Def(ItemData.BluePotion, 10), 5));
        Assert.AreEqual(25, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(ItemData.BluePotion));
    }

    [TestMethod]
    public void Sell_PartialStack_KeepsRemainder()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 5 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, Def(ItemData.BluePotion, 10), 2));
        Assert.AreEqual(10, hero.Mesos);
        Assert.AreEqual(3, hero.Use[ItemData.BluePotion]);
    }

    [TestMethod]
    public void Sell_TooMany_FailsAndKeepsTheStack()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 2 };
        Assert.IsFalse(Shop.Sell(hero, ItemData.BluePotion, Def(ItemData.BluePotion, 10), 3));
        Assert.AreEqual(2, hero.Use[ItemData.BluePotion]);
        Assert.AreEqual(0, hero.Mesos);
    }

    [TestMethod]
    public void BuyAndSell_WithTenBillionMesos_KeepsLongTotals()
    {
        var hero = Hero(10_000_000_000L);
        var def = Def(ItemData.RedPotion, 1_000_000_000);
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, def.Price, def, 2));
        Assert.AreEqual(8_000_000_000L, hero.Mesos);
        Assert.IsTrue(Shop.Sell(hero, ItemData.RedPotion, def, 2));
        Assert.AreEqual(9_000_000_000L, hero.Mesos);
    }

    [TestMethod]
    public void Sell_BeyondInt_KeepsFullPayout()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 3 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, Def(ItemData.BluePotion, int.MaxValue), 3));
        Assert.AreEqual(3_221_225_469L, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(ItemData.BluePotion));
    }

    [TestMethod]
    public void Sell_Equip_PaysFromTheEquipBag()
    {
        var hero = Hero(0);
        hero.EquipBag = new Dictionary<int, int> { [ItemData.Mop] = 1 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.Mop, Def(ItemData.Mop, 1900, "Wp")));
        Assert.AreEqual(950, hero.Mesos);
        Assert.IsFalse(hero.EquipBag.ContainsKey(ItemData.Mop));
    }

    [TestMethod]
    public void Sell_EmptyOrNullBag_Fails()
    {
        var hero = Hero(0);
        Assert.IsFalse(Shop.Sell(hero, ItemData.RedPotion, Def(ItemData.RedPotion, 3))); // null bag
        hero.Use = new Dictionary<int, int>();
        Assert.IsFalse(Shop.Sell(hero, ItemData.RedPotion, Def(ItemData.RedPotion, 3))); // empty bag
        Assert.AreEqual(0, hero.Mesos);
    }
}
