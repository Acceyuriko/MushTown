using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class InventoryTests
{
    private const int Red = ItemData.RedPotion;
    private const int Blue = ItemData.BluePotion;
    private const int Sword = 1302000;
    private const int Axe = 1312001;
    private const int Coat = 1040015;

    private static CharaSlot Chara() => new()
    {
        Level = 10,
        Hp = 10,
        Mp = 5,
        Use = new Dictionary<int, int> { [Red] = 3, [Blue] = 1 },
        EquipBag = new Dictionary<int, int> { [Sword] = 1 },
    };

    [TestMethod]
    public void Load_OldSave_FillsOrdersAndDefaultCoupons()
    {
        string path = Path.Combine(Path.GetTempPath(), $"inventory-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Saves":[{"Slots":[{"Name":"old","Use":{"2000000":2},"EquipBag":{"1302007":1}}]}]}""");
            var c = SaveStore.Load(path).Saves[0]!.Slots[0]!;

            Assert.AreEqual(Red, c.UseOrder![0]);
            Assert.AreEqual(ItemData.LongSword, c.EquipOrder![0]);
            Assert.AreEqual(ItemData.HairCouponMale, c.CashWorn![0]);
            Assert.AreEqual(1, c.DecBag![ItemData.HairCouponFemale]);
            Assert.AreEqual(ItemData.DefaultHairMale, c.Hair);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Ensure_OldSave_FillsOrders()
    {
        var c = Chara();
        Inventory.Ensure(c);

        Assert.AreEqual(Inventory.Capacity, c.UseOrder!.Count);
        Assert.AreEqual(Red, c.UseOrder[0]);
        Assert.AreEqual(Blue, c.UseOrder[1]);
        Assert.AreEqual(0, c.UseOrder[2]);
        Assert.AreEqual(Sword, c.EquipOrder![0]);
        Assert.AreEqual(0, c.EquipOrder[1]);
    }

    [TestMethod]
    public void Ensure_OldSave_AddsDefaultCoupons()
    {
        var c = new CharaSlot();
        Inventory.Ensure(c);

        Assert.IsFalse(c.Female);
        Assert.AreEqual(ItemData.DefaultHairMale, c.Hair);
        Assert.AreEqual(ItemData.DefaultFaceMale, c.Face);
        CollectionAssert.AreEquivalent(
            new[] { ItemData.HairCouponMale, ItemData.FaceCouponMale }, c.CashWorn);
        Assert.AreEqual(1, c.DecBag![ItemData.HairCouponFemale]);
        Assert.AreEqual(1, c.DecBag[ItemData.FaceCouponFemale]);
        Assert.AreEqual(ItemData.HairCouponFemale, c.DecOrder![0]);
    }

    [TestMethod]
    public void Ensure_GivenDefaults_DoesNotReaddSoldCoupons()
    {
        var c = new CharaSlot
        {
            DecBag = new Dictionary<int, int>(),
            CashWorn = new List<int>(),
        };
        Inventory.Ensure(c);

        Assert.AreEqual(0, c.DecBag!.Count);
        Assert.AreEqual(0, c.CashWorn!.Count);
    }

    [TestMethod]
    public void Ensure_Female_UsesFemaleDefaults()
    {
        var c = new CharaSlot { Female = true };
        Inventory.Ensure(c);

        Assert.AreEqual(ItemData.DefaultHairFemale, c.Hair);
        Assert.AreEqual(ItemData.DefaultFaceFemale, c.Face);
        CollectionAssert.AreEquivalent(
            new[] { ItemData.HairCouponFemale, ItemData.FaceCouponFemale }, c.CashWorn);
        Assert.AreEqual(1, c.DecBag![ItemData.HairCouponMale]);
        Assert.AreEqual(1, c.DecBag[ItemData.FaceCouponMale]);
    }

    [TestMethod]
    public void Add_StackMerge_UsesNoNewCell()
    {
        var c = new CharaSlot();

        Assert.IsTrue(Inventory.Add(c, "use", Red, 3));
        Assert.IsTrue(Inventory.Add(c, "use", Red, 2));
        Assert.AreEqual(5, c.Use![Red]);
        Assert.AreEqual(1, Inventory.Used(c, "use"));
    }

    [TestMethod]
    public void Add_FullTab_RefusesNewItemButMergesAnExistingStack()
    {
        var c = new CharaSlot { Use = new Dictionary<int, int>() };
        for (int id = 1; id <= Inventory.Capacity; id++) c.Use[id] = 1;
        Inventory.Ensure(c);

        Assert.IsFalse(Inventory.CanAdd(c, "use", 999999, 1));
        Assert.IsFalse(Inventory.Add(c, "use", 999999, 1));
        Assert.IsTrue(Inventory.Add(c, "use", 1, 5)); // an existing stack needs no cell
        Assert.AreEqual(6, c.Use[1]);
    }

    [TestMethod]
    public void Used_CountsOccupiedCells()
    {
        var c = Chara();
        Inventory.Ensure(c);

        Assert.AreEqual(2, Inventory.Used(c, "use"));
        Assert.AreEqual(1, Inventory.Used(c, "equip"));
        Assert.AreEqual(0, Inventory.Used(c, "cash"));
    }

    [TestMethod]
    public void Ensure_EquipCopies_UseOneCellEach()
    {
        var c = new CharaSlot { EquipBag = new Dictionary<int, int> { [Sword] = 4 } };
        Inventory.Ensure(c);

        Assert.AreEqual(4, Inventory.Used(c, "equip"));
        CollectionAssert.AreEqual(new[] { Sword, Sword, Sword, Sword }, c.EquipOrder!.Take(4).ToArray());
    }

    [TestMethod]
    public void Ensure_CashBag_GetsCashOrder()
    {
        var c = new CharaSlot { CashBag = new Dictionary<int, int> { [Red] = 2 } };
        Inventory.Ensure(c);

        Assert.AreEqual(Red, c.CashOrder![0]);
        Assert.AreEqual(1, Inventory.Used(c, "cash"));
    }

    [TestMethod]
    public void Ensure_IsIdempotent()
    {
        var c = Chara();
        Inventory.Ensure(c);
        Inventory.Ensure(c);

        Assert.AreEqual(2, Inventory.Used(c, "use"));
        Assert.AreEqual(1, Inventory.Used(c, "equip"));
    }

    [TestMethod]
    public void Ensure_OldOrder_ClearsGhostsAndTrimsToCapacity()
    {
        var order = new List<int>(new int[130]);
        order[0] = Blue;
        order[1] = Red;
        var c = new CharaSlot
        {
            Use = new Dictionary<int, int> { [Red] = 1 },
            UseOrder = order,
        };

        Inventory.Ensure(c);

        Assert.AreEqual(Inventory.Capacity, c.UseOrder!.Count);
        Assert.AreEqual(0, c.UseOrder[0]);
        Assert.AreEqual(Red, c.UseOrder[1]);
        Assert.AreEqual(1, Inventory.Used(c, "use"));
    }

    [TestMethod]
    public void Move_Empty_MovesTheCell()
    {
        var c = Chara();
        Inventory.Ensure(c);

        Assert.IsTrue(Inventory.Move(c, "use", 0, 5));
        Assert.AreEqual(0, c.UseOrder![0]);
        Assert.AreEqual(Red, c.UseOrder[5]);
        Assert.AreEqual(3, c.Use![Red]); // moving a cell does not change the count
    }

    [TestMethod]
    public void Move_SameId_MergesTheCells()
    {
        var c = Chara();
        c.Use = new Dictionary<int, int> { [Red] = 3 };
        Inventory.Ensure(c);
        c.UseOrder![0] = Red;
        c.UseOrder[2] = Red;

        Assert.AreEqual(Inventory.Capacity, c.UseOrder.Count); // Ensure keeps the 128-cell contract
        Assert.AreEqual(Red, c.UseOrder[2]);
        Assert.IsTrue(Inventory.Move(c, "use", 2, 0));
        Assert.AreEqual(Red, c.UseOrder[0]);
        Assert.AreEqual(0, c.UseOrder[2]);
        Assert.AreEqual(3, c.Use![Red]);
    }

    [TestMethod]
    public void Move_Different_SwapsTheCells()
    {
        var c = Chara();
        Inventory.Ensure(c);

        Assert.IsTrue(Inventory.Move(c, "use", 0, 1));
        Assert.AreEqual(Blue, c.UseOrder![0]);
        Assert.AreEqual(Red, c.UseOrder[1]);
        Assert.AreEqual(3, c.Use![Red]);
        Assert.AreEqual(1, c.Use[Blue]);
    }

    [TestMethod]
    public void Move_EquipSameId_DoesNotMergeCells()
    {
        var c = new CharaSlot { EquipBag = new Dictionary<int, int> { [Sword] = 2 } };
        Inventory.Ensure(c);

        Assert.IsFalse(Inventory.Move(c, "equip", 0, 1));
        Assert.AreEqual(Sword, c.EquipOrder![0]);
        Assert.AreEqual(Sword, c.EquipOrder[1]);
    }

    [TestMethod]
    public void Sort_CurrentTab_ByTypeThenId()
    {
        var c = new CharaSlot
        {
            EquipBag = new Dictionary<int, int> { [Axe] = 1, [Coat] = 1, [Sword] = 1 },
        };
        Inventory.Ensure(c);
        var defs = new Dictionary<int, ItemData>
        {
            [Sword] = new ItemData { ItemId = Sword, Slot = "Wp" },
            [Axe] = new ItemData { ItemId = Axe, Slot = "Wp" },
            [Coat] = new ItemData { ItemId = Coat, Slot = "Ma" },
        };

        Inventory.Sort(c, "equip", defs);

        Assert.AreEqual(Sword, c.EquipOrder![0]);
        Assert.AreEqual(Axe, c.EquipOrder[1]);
        Assert.AreEqual(Coat, c.EquipOrder[2]);
        Assert.AreEqual(0, c.EquipOrder[3]);
    }

    [TestMethod]
    public void Sort_WithoutEnsure_CreatesTheOrders()
    {
        var c = new CharaSlot { EquipBag = new Dictionary<int, int> { [Sword] = 1 } };
        var defs = new Dictionary<int, ItemData>
        {
            [Sword] = new ItemData { ItemId = Sword, Slot = "Wp" },
        };

        Inventory.Sort(c, "equip", defs);

        Assert.AreEqual(Inventory.Capacity, c.EquipOrder!.Count);
        Assert.AreEqual(Sword, c.EquipOrder[0]);
    }

    [TestMethod]
    public void Sort_OnlyTouchesTheCurrentTab()
    {
        var c = new CharaSlot
        {
            Use = new Dictionary<int, int> { [Red] = 1 },
            EquipBag = new Dictionary<int, int> { [Sword] = 1 },
        };
        Inventory.Ensure(c);
        c.UseOrder![5] = Red;
        c.UseOrder[0] = 0;
        var defs = new Dictionary<int, ItemData>
        {
            [Red] = new ItemData { ItemId = Red },
            [Sword] = new ItemData { ItemId = Sword, Slot = "Wp" },
        };

        Inventory.Sort(c, "use", defs);

        Assert.AreEqual(Red, c.UseOrder[0]);
        Assert.AreEqual(Sword, c.EquipOrder![0]);
    }

    [TestMethod]
    public void Remove_EquipCopy_ClearsOneCell()
    {
        var c = new CharaSlot { EquipBag = new Dictionary<int, int> { [Sword] = 3 } };
        Inventory.Ensure(c);

        Assert.IsTrue(Inventory.Remove(c, "equip", Sword, 2));

        Assert.AreEqual(1, c.EquipBag![Sword]);
        Assert.AreEqual(1, Inventory.Used(c, "equip"));
    }

    [TestMethod]
    public void Remove_LastOne_ClearsTheCell()
    {
        var c = Chara();
        Inventory.Ensure(c);

        Assert.IsTrue(Inventory.Remove(c, "use", Red, 3));
        Assert.IsFalse(c.Use!.ContainsKey(Red));
        Assert.AreEqual(0, c.UseOrder![0]);
        Assert.IsFalse(Inventory.Remove(c, "use", Red, 1));
    }

    [TestMethod]
    public void ShopBuy_AddsToOrder()
    {
        var c = new CharaSlot { Mesos = 100 };
        var def = new ItemData { ItemId = Red, Price = 10 };

        Assert.IsTrue(Shop.Buy(c, Red, 10, def, 2));
        Assert.AreEqual(Red, c.UseOrder![0]);
        Assert.AreEqual(2, c.Use![Red]);
    }

    [TestMethod]
    public void ShopSell_LastOne_ClearsOrder()
    {
        var c = new CharaSlot { Use = new Dictionary<int, int> { [Red] = 1 } };
        Inventory.Ensure(c);
        var def = new ItemData { ItemId = Red, Price = 10 };

        Assert.IsTrue(Shop.Sell(c, Red, def));
        Assert.AreEqual(0, c.UseOrder![0]);
    }

    [TestMethod]
    public void UsePotion_LastOne_ClearsOrder()
    {
        var c = new CharaSlot { Use = new Dictionary<int, int> { [Red] = 1 }, Hp = 1 };
        Inventory.Ensure(c);

        Assert.IsTrue(Progression.UsePotion(c, Red, 50, 0));
        Assert.AreEqual(0, c.UseOrder![0]);
    }
}
