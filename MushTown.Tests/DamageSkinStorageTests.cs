using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class DamageSkinStorageTests
{
    private static readonly ItemData Stamp = new() { ItemId = 2438864, DamageSkin = 1332 };
    private static readonly ItemData EightBit = new() { ItemId = 2439523, DamageSkin = 1354 };

    private static CharaSlot Chara(int skin = 0)
    {
        var chara = new CharaSlot { Name = "hero", Mesos = 1_000_000, DamageSkin = skin };
        Inventory.Ensure(chara);
        return chara;
    }

    [TestMethod]
    public void UseCoupon_ConsumesOneAndSavesOldSkin()
    {
        var chara = Chara(1332);
        Inventory.Add(chara, "use", Stamp.ItemId, 2);
        Inventory.Add(chara, "use", EightBit.ItemId, 1);

        Assert.IsTrue(Progression.UseDamageSkinCoupon(chara, EightBit));

        Assert.AreEqual(1354, chara.DamageSkin);
        Assert.AreEqual(0, Inventory.Count(chara, "use", EightBit.ItemId));
        Assert.AreEqual(2, Inventory.Count(chara, "use", Stamp.ItemId));
        CollectionAssert.AreEqual(new[] { 1332 }, chara.DamageSkins);
    }

    [TestMethod]
    public void Coupon_DoubleClickInUseTab_UsesIt()
    {
        var chara = Chara(1332);
        Inventory.Add(chara, "use", Stamp.ItemId, 1);
        Inventory.Add(chara, "use", EightBit.ItemId, 1);
        var defs = new Dictionary<int, ItemData> { [Stamp.ItemId] = Stamp, [EightBit.ItemId] = EightBit };
        int cell = chara.UseOrder!.IndexOf(EightBit.ItemId);
        var hand = new CarryHand();

        Assert.AreEqual(HandAct.Moved, hand.Click(chara, "use", EightBit.ItemId, defs, 0, 0, 1000, cell));
        Assert.AreEqual(HandAct.SkinChanged, hand.Click(chara, "use", EightBit.ItemId, defs, 0, 0, 1200, cell));

        Assert.AreEqual(1354, chara.DamageSkin);
        Assert.AreEqual(0, Inventory.Count(chara, "use", EightBit.ItemId));
        CollectionAssert.AreEqual(new[] { 1332 }, chara.DamageSkins);
    }

    [TestMethod]
    public void UseCoupon_AlreadySavedOldSkin_NoDuplicate()
    {
        var chara = Chara(1332);
        chara.DamageSkins = new List<int> { 1332 };
        Inventory.Add(chara, "use", EightBit.ItemId, 1);

        Assert.IsTrue(Progression.UseDamageSkinCoupon(chara, EightBit));

        CollectionAssert.AreEqual(new[] { 1332 }, chara.DamageSkins);
    }

    [TestMethod]
    public void UseCoupon_FullStorage_KeepsCouponAndOldSkin()
    {
        var chara = Chara(1332);
        chara.DamageSkins = Enumerable.Range(1, Progression.DamageSkinSlots).ToList();
        Inventory.Add(chara, "use", EightBit.ItemId, 1);

        Assert.IsFalse(Progression.UseDamageSkinCoupon(chara, EightBit));

        Assert.AreEqual(1332, chara.DamageSkin);
        Assert.AreEqual(1, Inventory.Count(chara, "use", EightBit.ItemId));
        Assert.AreEqual(Progression.DamageSkinSlots, chara.DamageSkins.Count);
    }

    [TestMethod]
    public void SaveCurrent_AddsOnceAndStopsAtThirtyFive()
    {
        var chara = Chara(1332);

        Assert.IsTrue(Progression.SaveCurrentDamageSkin(chara));
        Assert.IsTrue(Progression.SaveCurrentDamageSkin(chara));
        CollectionAssert.AreEqual(new[] { 1332 }, chara.DamageSkins);

        chara.DamageSkins = Enumerable.Range(1, Progression.DamageSkinSlots).ToList();
        Assert.IsFalse(Progression.SaveCurrentDamageSkin(chara));
        Assert.AreEqual(Progression.DamageSkinSlots, chara.DamageSkins.Count);
    }

    [TestMethod]
    public void Switch_ChargesFeeAndSavesOldSkin()
    {
        var chara = Chara(1332);
        chara.DamageSkins = new List<int> { 1354 };

        Assert.IsTrue(Progression.SwitchDamageSkin(chara, 1354));

        Assert.AreEqual(1354, chara.DamageSkin);
        Assert.AreEqual(1_000_000 - Progression.DamageSkinChangeFee, chara.Mesos);
        CollectionAssert.AreEqual(new[] { 1354, 1332 }, chara.DamageSkins);
    }

    [TestMethod]
    public void Switch_NoMesos_ChangesNothing()
    {
        var chara = Chara(1332);
        chara.Mesos = Progression.DamageSkinChangeFee - 1;
        chara.DamageSkins = new List<int> { 1354 };

        Assert.IsFalse(Progression.SwitchDamageSkin(chara, 1354));

        Assert.AreEqual(1332, chara.DamageSkin);
        Assert.AreEqual(Progression.DamageSkinChangeFee - 1, chara.Mesos);
        Assert.IsFalse(chara.DamageSkins.Contains(1332)); // a failed switch must not save either
    }

    [TestMethod]
    public void Delete_RemovesSelectedSkin()
    {
        var chara = Chara();
        chara.DamageSkins = new List<int> { 1332, 1354, 1426 };

        Assert.IsTrue(Progression.DeleteDamageSkin(chara, 1));

        CollectionAssert.AreEqual(new[] { 1332, 1426 }, chara.DamageSkins);
    }

    [TestMethod]
    public void Delete_OutOfRange_ChangesNothing()
    {
        var chara = Chara(1332);
        chara.DamageSkins = new List<int> { 1332 };

        Assert.IsFalse(Progression.DeleteDamageSkin(chara, -1));
        Assert.IsFalse(Progression.DeleteDamageSkin(chara, 1));
        CollectionAssert.AreEqual(new[] { 1332 }, chara.DamageSkins);
        Assert.AreEqual(1332, chara.DamageSkin);
    }

    [TestMethod]
    public void Delete_LastSkin_LeavesEmptyList()
    {
        var chara = Chara();
        chara.DamageSkins = new List<int> { 1332 };

        Assert.IsTrue(Progression.DeleteDamageSkin(chara, 0));

        Assert.AreEqual(0, chara.DamageSkins.Count);
    }

    [TestMethod]
    public void Delete_CurrentSkin_LeavesCurrentUnchanged()
    {
        var chara = Chara(1354);
        chara.DamageSkins = new List<int> { 1332, 1354, 1426 };

        Assert.IsTrue(Progression.DeleteDamageSkin(chara, 1));

        Assert.AreEqual(1354, chara.DamageSkin);
        CollectionAssert.AreEqual(new[] { 1332, 1426 }, chara.DamageSkins);
    }

    [TestMethod]
    public void Delete_RoundTripsThroughSave()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-dmgdelete-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            hero.DamageSkin = 1354;
            hero.DamageSkins = new List<int> { 1332, 1354, 1426 };
            Assert.IsTrue(Progression.DeleteDamageSkin(hero, 1));

            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;

            Assert.AreEqual(1354, loaded.DamageSkin);
            CollectionAssert.AreEqual(new[] { 1332, 1426 }, loaded.DamageSkins);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SaveLoad_RoundTripsDamageSkins()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-dmgskin-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            hero.DamageSkin = 1426;
            hero.DamageSkins = new List<int> { 1332, 1354, 1426 };

            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;

            Assert.AreEqual(1426, loaded.DamageSkin);
            CollectionAssert.AreEqual(new[] { 1332, 1354, 1426 }, loaded.DamageSkins);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void MissingFields_DefaultToBasicAndEmpty()
    {
        var chara = JsonSerializer.Deserialize<CharaSlot>("{}")!;

        Assert.AreEqual(0, chara.DamageSkin);
        Assert.IsNull(chara.DamageSkins);
        Assert.IsFalse(Progression.DeleteDamageSkin(chara, 0)); // no storage in an old save
        Assert.IsTrue(Progression.SaveCurrentDamageSkin(chara)); // the basic skin needs no cell
        Assert.IsNull(chara.DamageSkins);
    }
}
