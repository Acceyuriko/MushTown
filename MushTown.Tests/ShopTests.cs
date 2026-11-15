using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class ShopTests
{
    private static CharaSlot Hero(long mesos) => new() { Name = "hero", Mesos = mesos };

    private static ItemData Def(int id, int price) =>
        new() { ItemId = id, Price = price };

    // A 0-price item (cash gear) sells for nothing: no free-money loop.
    [TestMethod]
    public void SellPrice_ZeroPrice_IsZero()
    {
        Assert.AreEqual(0, Shop.SellPrice(0));
        Assert.AreEqual(1, Shop.SellPrice(1));
        Assert.AreEqual(1, Shop.SellPrice(2));
    }

    [TestMethod]
    public void BagFor_RoutesEachKind()
    {
        var equip = Def(ItemData.Mop, 50);
        equip.Slot = "Wp";
        Assert.AreEqual("equip", Shop.BagFor(equip));

        Assert.AreEqual("use", Shop.BagFor(Def(ItemData.RedPotion, 3)));

        var cash = Def(5400000, 100);
        cash.Cash = true;
        Assert.AreEqual("cash", Shop.BagFor(cash));

        var decSlot = Def(1112200, 0);
        decSlot.Cash = true;
        decSlot.Slot = "Ri";
        Assert.AreEqual("dec", Shop.BagFor(decSlot));

        var decCosmetic = Def(ItemData.HairCoupon, 0);
        decCosmetic.Cash = true;
        decCosmetic.Cosmetic = 30020;
        Assert.AreEqual("dec", Shop.BagFor(decCosmetic));
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
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, 3, "use"));
        Assert.AreEqual(7, hero.Mesos);
        Assert.AreEqual(1, hero.Use![ItemData.RedPotion]);
    }

    [TestMethod]
    public void Buy_Count_ChargesOnceForTheWholeStack()
    {
        var hero = Hero(100);
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, 3, "use", 4));
        Assert.AreEqual(88, hero.Mesos);
        Assert.AreEqual(4, hero.Use![ItemData.RedPotion]);
    }

    [TestMethod]
    public void Buy_Count_NotEnoughMesos_ChangesNothing()
    {
        var hero = Hero(100);
        Assert.IsFalse(Shop.Buy(hero, ItemData.RedPotion, 30, "use", 4));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Buy_Equip_GoesToTheEquipBag()
    {
        var hero = Hero(100);
        Assert.IsTrue(Shop.Buy(hero, ItemData.Mop, 50, "equip"));
        Assert.AreEqual(50, hero.Mesos);
        Assert.AreEqual(1, hero.EquipBag![ItemData.Mop]);
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Buy_Equip_Count_EachCopyGetsItsOwnCell()
    {
        var hero = Hero(1000);
        Assert.IsTrue(Shop.Buy(hero, ItemData.Mop, 50, "equip", 4));
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
        Assert.IsFalse(Shop.Buy(hero, 999999, 10, "use"));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(999999));
    }

    [TestMethod]
    public void Buy_Broke_ChangesNothing()
    {
        var hero = Hero(2);
        Assert.IsFalse(Shop.Buy(hero, ItemData.RedPotion, 3, "use"));
        Assert.AreEqual(2, hero.Mesos);
        Assert.IsNull(hero.Use);
    }

    [TestMethod]
    public void Sell_Potion_PaysAndDecrements()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 2 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, 10, "use"));
        Assert.AreEqual(5, hero.Mesos);
        Assert.AreEqual(1, hero.Use[ItemData.BluePotion]);
    }

    [TestMethod]
    public void Sell_LastBottle_RemovesTheRow()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.RedPotion] = 1 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.RedPotion, 3, "use"));
        Assert.AreEqual(1, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(ItemData.RedPotion));
    }

    [TestMethod]
    public void Sell_Count_SellsTheWholeStack()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 5 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, 10, "use", 5));
        Assert.AreEqual(25, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(ItemData.BluePotion));
    }

    [TestMethod]
    public void Sell_PartialStack_KeepsRemainder()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 5 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, 10, "use", 2));
        Assert.AreEqual(10, hero.Mesos);
        Assert.AreEqual(3, hero.Use[ItemData.BluePotion]);
    }

    [TestMethod]
    public void Sell_TooMany_FailsAndKeepsTheStack()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 2 };
        Assert.IsFalse(Shop.Sell(hero, ItemData.BluePotion, 10, "use", 3));
        Assert.AreEqual(2, hero.Use[ItemData.BluePotion]);
        Assert.AreEqual(0, hero.Mesos);
    }

    [TestMethod]
    public void BuyAndSell_WithTenBillionMesos_KeepsLongTotals()
    {
        var hero = Hero(10_000_000_000L);
        var def = Def(ItemData.RedPotion, 1_000_000_000);
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, def.Price, "use", 2));
        Assert.AreEqual(8_000_000_000L, hero.Mesos);
        Assert.IsTrue(Shop.Sell(hero, ItemData.RedPotion, def.Price, "use", 2));
        Assert.AreEqual(9_000_000_000L, hero.Mesos);
    }

    [TestMethod]
    public void Sell_BeyondInt_KeepsFullPayout()
    {
        var hero = Hero(0);
        hero.Use = new Dictionary<int, int> { [ItemData.BluePotion] = 3 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.BluePotion, int.MaxValue, "use", 3));
        Assert.AreEqual(3_221_225_469L, hero.Mesos);
        Assert.IsFalse(hero.Use.ContainsKey(ItemData.BluePotion));
    }

    [TestMethod]
    public void Sell_Equip_PaysFromTheEquipBag()
    {
        var hero = Hero(0);
        hero.EquipBag = new Dictionary<int, int> { [ItemData.Mop] = 1 };
        Assert.IsTrue(Shop.Sell(hero, ItemData.Mop, 1900, "equip"));
        Assert.AreEqual(950, hero.Mesos);
        Assert.IsFalse(hero.EquipBag.ContainsKey(ItemData.Mop));
    }

    [TestMethod]
    public void Sell_EmptyOrNullBag_Fails()
    {
        var hero = Hero(0);
        Assert.IsFalse(Shop.Sell(hero, ItemData.RedPotion, 3, "use")); // null bag
        hero.Use = new Dictionary<int, int>();
        Assert.IsFalse(Shop.Sell(hero, ItemData.RedPotion, 3, "use")); // empty bag
        Assert.AreEqual(0, hero.Mesos);
    }

    [TestMethod]
    public void BuyAndSell_ZeroPrice_KeepsWallet()
    {
        var hero = Hero(100);
        Assert.IsTrue(Shop.Buy(hero, 1112200, 0, "equip"));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsTrue(Shop.Sell(hero, 1112200, 0, "equip"));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsFalse(hero.EquipBag!.ContainsKey(1112200));
    }

    [TestMethod]
    public void Sell_AndBuyBack_CashAndDecReturnToTheirOwnBag()
    {
        var hero = Hero(100);
        hero.CashBag = new Dictionary<int, int> { [5400000] = 1 };
        hero.DecBag = new Dictionary<int, int> { [1302007] = 1 };
        Inventory.Ensure(hero);

        Assert.IsTrue(Shop.Sell(hero, 5400000, 100, "cash"));
        Assert.IsTrue(Shop.Sell(hero, 1302007, 100, "dec"));
        Assert.AreEqual(200, hero.Mesos);
        Assert.IsFalse(hero.CashBag.ContainsKey(5400000));
        Assert.IsFalse(hero.DecBag.ContainsKey(1302007));
        Assert.IsFalse(hero.CashOrder!.Contains(5400000));
        Assert.IsFalse(hero.DecOrder!.Contains(1302007));

        Assert.IsTrue(Shop.Buy(hero, 5400000, 100, "cash"));
        Assert.IsTrue(Shop.Buy(hero, 1302007, 100, "dec"));
        Assert.AreEqual(0, hero.Mesos);
        Assert.AreEqual(1, hero.CashBag[5400000]);
        Assert.AreEqual(1, hero.DecBag[1302007]);
        Assert.IsTrue(hero.CashOrder.Contains(5400000));
        Assert.IsTrue(hero.DecOrder.Contains(1302007));
    }
}
