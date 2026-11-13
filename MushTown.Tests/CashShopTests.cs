using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class CashShopTests
{
    private static CharaSlot Hero(long mesos) => new() { Name = "hero", Mesos = mesos, Hair = 30020, Face = 20000 };

    private static ItemData Def(int id, string? slot = null) =>
        new() { ItemId = id, Price = 0, Slot = slot };

    [TestMethod]
    public void Buy_Utility_Ok_ChargesAndUsesCashBag()
    {
        var hero = Hero(15000);
        Assert.IsTrue(CashShop.Buy(hero, 5400000, 15000, Def(5400000), 1, false));
        Assert.AreEqual(0, hero.Mesos);
        Assert.AreEqual(1, hero.CashBag![5400000]);
        Assert.IsNull(hero.DecBag);
        Assert.IsNull(hero.EquipBag);
    }

    [TestMethod]
    public void Buy_Fashion_GoesToDecBag()
    {
        var hero = Hero(300);
        Assert.IsTrue(CashShop.Buy(hero, 1302007, 300, Def(1302007, "Wp"), 1, true));
        Assert.AreEqual(0, hero.Mesos);
        Assert.AreEqual(1, hero.DecBag![1302007]);
        Assert.IsNull(hero.CashBag);
        Assert.IsNull(hero.EquipBag);
    }

    [TestMethod]
    public void Buy_CountMultipliesThePrice()
    {
        var hero = Hero(600);
        Assert.IsTrue(CashShop.Buy(hero, 1302007, 300, Def(1302007, "Wp"), 2, true));
        Assert.AreEqual(0, hero.Mesos);
        Assert.AreEqual(2, hero.DecBag![1302007]);
    }

    [TestMethod]
    public void Buy_TenBillionMesos_KeepsLongBalance()
    {
        var hero = Hero(10_000_000_000L);
        Assert.IsTrue(CashShop.Buy(hero, 5400000, 10_000, Def(5400000), 1, false));
        Assert.AreEqual(9_999_990_000L, hero.Mesos);
        Assert.AreEqual(1, hero.CashBag![5400000]);
    }

    [TestMethod]
    public void Buy_OneMesoShort_ChangesNothing()
    {
        var hero = Hero(299);
        Assert.IsFalse(CashShop.Buy(hero, 1302007, 300, Def(1302007, "Wp"), 1, true));
        Assert.AreEqual(299, hero.Mesos);
        Assert.IsNull(hero.CashBag);
        Assert.IsNull(hero.DecBag);
        Assert.IsNull(hero.EquipBag);
    }

    [TestMethod]
    public void BuyHairAndFace_GoToDecBagWithoutChangingTheLook()
    {
        var hero = Hero(2000);
        hero.CashWorn = new List<int> { 5157257, 5158151 };
        Assert.IsTrue(CashShop.BuyHair(hero, 5157296, 1800));
        Assert.IsTrue(CashShop.BuyFace(hero, 5158001, 200));
        Assert.AreEqual(0, hero.Mesos);
        Assert.AreEqual(1, hero.DecBag![5157296]);
        Assert.AreEqual(1, hero.DecBag[5158001]);
        Assert.AreEqual(30020, hero.Hair);
        Assert.AreEqual(20000, hero.Face);
        CollectionAssert.AreEqual(new[] { 5157257, 5158151 }, hero.CashWorn);
    }

    [TestMethod]
    public void BuyHair_NotEnoughMesos_ChangesNothing()
    {
        var hero = Hero(100);
        Assert.IsFalse(CashShop.BuyHair(hero, 5157296, 1800));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsNull(hero.DecBag);
        Assert.AreEqual(30020, hero.Hair);
    }

    [TestMethod]
    public void BuyFace_NotEnoughMesos_ChangesNothing()
    {
        var hero = Hero(100);
        hero.CashWorn = new List<int> { 5157257 };
        Assert.IsFalse(CashShop.BuyFace(hero, 5158001, 200));
        Assert.AreEqual(100, hero.Mesos);
        Assert.IsNull(hero.DecBag);
        Assert.AreEqual(20000, hero.Face);
        CollectionAssert.AreEqual(new[] { 5157257 }, hero.CashWorn);
    }

    [TestMethod]
    public void Buy_FullCashBag_DoesNotChargeOrAdd()
    {
        var hero = Hero(1000);
        for (int i = 0; i < Inventory.Capacity; i++)
            Assert.IsTrue(Inventory.Add(hero, "cash", 100000 + i, 1));
        int before = hero.CashBag!.Count;
        Assert.IsFalse(CashShop.Buy(hero, 999999, 100, Def(999999), 1, false));
        Assert.AreEqual(1000, hero.Mesos);
        Assert.AreEqual(before, hero.CashBag.Count);
        Assert.IsFalse(hero.CashBag.ContainsKey(999999));
        Assert.IsNull(hero.DecBag);
        Assert.IsNull(hero.EquipBag);
    }

    [TestMethod]
    public void ResetSkills_ClearsLevelsAndReturnsSp()
    {
        var hero = Hero(0);
        hero.SkillLv = new Dictionary<int, int> { [1] = 3, [2] = 2 };
        hero.Sp = 4;
        Assert.IsTrue(CashShop.ResetSkills(hero));
        Assert.AreEqual(0, hero.SkillLv.Count);
        Assert.AreEqual(9, hero.Sp);
    }

    [TestMethod]
    public void ResetSkills_NoLearnedSkills_Fails()
    {
        var hero = Hero(0);
        hero.Sp = 4;
        Assert.IsFalse(CashShop.ResetSkills(hero));
        Assert.AreEqual(4, hero.Sp);
    }

    [TestMethod]
    public void Rename_ChangesOnlyTheChosenHero()
    {
        var store = new SaveStore();
        store.Saves[0]!.Slots[0] = Hero(0);
        store.Saves[0]!.Slots[1] = Hero(0);
        store.Saves[0]!.Slots[1]!.Name = "other";
        Assert.IsTrue(CashShop.Rename(store, 0, 0, "newname"));
        Assert.AreEqual("newname", store.Saves[0]!.Slots[0]!.Name);
        Assert.AreEqual("other", store.Saves[0]!.Slots[1]!.Name);
    }

    [TestMethod]
    public void Rename_DuplicateOrInvalidName_Fails()
    {
        var store = new SaveStore();
        store.Saves[0]!.Slots[0] = Hero(0);
        store.Saves[1]!.Slots[0] = Hero(0);
        store.Saves[1]!.Slots[0]!.Name = "taken";
        Assert.IsFalse(CashShop.Rename(store, 0, 0, "taken"));
        Assert.IsFalse(CashShop.Rename(store, 0, 0, "bad name"));
        Assert.IsFalse(CashShop.Rename(store, 0, 0, ""));
        Assert.AreEqual("hero", store.Saves[0]!.Slots[0]!.Name);
    }

    [TestMethod]
    public void Rename_SameName_Fails()
    {
        var store = new SaveStore();
        store.Saves[0]!.Slots[0] = Hero(0);
        Assert.IsFalse(CashShop.Rename(store, 0, 0, "hero"));
        Assert.AreEqual("hero", store.Saves[0]!.Slots[0]!.Name);
    }

    [TestMethod]
    public void Rename_OverTwelveChars_Fails()
    {
        var store = new SaveStore();
        store.Saves[0]!.Slots[0] = Hero(0);
        Assert.IsFalse(CashShop.Rename(store, 0, 0, new string('a', 13)));
        Assert.AreEqual("hero", store.Saves[0]!.Slots[0]!.Name);
    }

    [TestMethod]
    public void CarryHand_DoubleClickCash_Uses()
    {
        var hero = Hero(0);
        Assert.IsTrue(Inventory.Add(hero, "cash", 5400000, 1));
        var hand = new CarryHand();
        var defs = new Dictionary<int, ItemData> { [5400000] = Def(5400000) };
        Assert.AreEqual(HandAct.Moved, hand.Click(hero, "cash", 5400000, defs, Progression.MaxHp(hero.Level), Progression.MaxMp(hero.Level), 1000, 0));
        Assert.AreEqual(HandAct.Used, hand.Click(hero, "cash", 5400000, defs, Progression.MaxHp(hero.Level), Progression.MaxMp(hero.Level), 1100, 0));
    }
}
