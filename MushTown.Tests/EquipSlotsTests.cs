using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class EquipSlotsTests
{
    private static readonly Dictionary<int, ItemData> Defs = new()
    {
        [1] = new ItemData { ItemId = 1, Slot = "Wp" },
        [2] = new ItemData { ItemId = 2, Slot = "Ma", Cash = true },
        [5] = new ItemData { ItemId = 5, Slot = "Ri" },
        [6] = new ItemData { ItemId = 6, Slot = "Wp", Cash = true },
        [3] = new ItemData { ItemId = 3, Cosmetic = 30020 },
        [4] = new ItemData { ItemId = 4, Cosmetic = 20000 },
    };

    [TestMethod]
    public void Worn_WeaponFillsWeaponOnly()
    {
        var chara = new CharaSlot { Equips = new List<int> { 1 } };

        CollectionAssert.AreEqual(new[] { ("weapon", 1) }, EquipSlots.Worn(chara, Defs));
    }

    [TestMethod]
    public void Worn_LongcoatFillsTopAndBottom()
    {
        var chara = new CharaSlot { Equips = new List<int> { 2 } };

        CollectionAssert.AreEqual(new[] { ("top", 2), ("bottom", 2) }, EquipSlots.Worn(chara, Defs));
    }

    [TestMethod]
    public void Worn_RingFillsNothing()
    {
        var chara = new CharaSlot { Equips = new List<int> { 5 } };

        Assert.AreEqual(0, EquipSlots.Worn(chara, Defs).Count);
    }

    [TestMethod]
    public void Worn_EmptyOrUnknownIdFillsNothing()
    {
        Assert.AreEqual(0, EquipSlots.Worn(new CharaSlot(), Defs).Count);
        Assert.AreEqual(0, EquipSlots.Worn(new CharaSlot { Equips = new List<int> { 99 } }, Defs).Count);
    }

    [TestMethod]
    public void Cash_HairAndFaceCouponsFillTheirBoxes()
    {
        var chara = new CharaSlot { Hair = 30020, Face = 20000, CashWorn = new List<int> { 3, 4 } };

        CollectionAssert.AreEqual(new[] { ("hair", 3), ("face", 4) }, EquipSlots.Cash(chara, Defs));
    }

    [TestMethod]
    public void Cash_CouponForAnotherLookFillsNothing()
    {
        var chara = new CharaSlot { Hair = 33390, Face = 20002, CashWorn = new List<int> { 3, 4 } };

        Assert.AreEqual(0, EquipSlots.Cash(chara, Defs).Count);
    }

    [TestMethod]
    public void Cash_LongcoatFillsTopAndBottom()
    {
        var chara = new CharaSlot { CashWorn = new List<int> { 2 } };

        CollectionAssert.AreEqual(new[] { ("top", 2), ("bottom", 2) }, EquipSlots.Cash(chara, Defs));
    }

    [TestMethod]
    public void Cash_WeaponFillsWeapon()
    {
        var chara = new CharaSlot { CashWorn = new List<int> { 6 } };

        CollectionAssert.AreEqual(new[] { ("weapon", 6) }, EquipSlots.Cash(chara, Defs));
    }
}
