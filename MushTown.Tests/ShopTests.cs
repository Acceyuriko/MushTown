using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class ShopTests
{
    private static CharaSlot Hero(int mesos) => new() { Name = "hero", Mesos = mesos };

    private static ItemData Def(int id, int price, string? slot = null) =>
        new() { ItemId = id, Price = price, Slot = slot };

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
        Assert.IsTrue(Shop.Buy(hero, ItemData.RedPotion, 3));
        Assert.AreEqual(7, hero.Mesos);
        Assert.AreEqual(1, hero.Use![ItemData.RedPotion]);
    }

    [TestMethod]
    public void Buy_Broke_ChangesNothing()
    {
        var hero = Hero(2);
        Assert.IsFalse(Shop.Buy(hero, ItemData.RedPotion, 3));
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
