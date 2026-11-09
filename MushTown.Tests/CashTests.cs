using MushTown.Shared;

namespace MushTown.Tests;

// W5: decoration coupons move between DEC and the doll, fashion swaps its
// body area, and the attack art follows the visible weapon.
[TestClass]
public sealed class CashTests
{
    private const int CashSword = 1;
    private const int CashSword2 = 2;
    private const int GearSword = 3;
    private const int HairCouponNonDefault = 5157296;
    private const int FaceCouponNonDefault = 5158001;

    private static Dictionary<int, ItemData> Defs() => new()
    {
        [CashSword] = new ItemData { ItemId = CashSword, Slot = "Wp", Attack = 1 },
        [CashSword2] = new ItemData { ItemId = CashSword2, Slot = "Wp", Attack = 4 },
        [GearSword] = new ItemData { ItemId = GearSword, Slot = "Wp", Attack = 2 },
        [ItemData.HairCoupon] = new ItemData { ItemId = ItemData.HairCoupon, Cosmetic = ItemData.DefaultHair },
        [ItemData.HairCouponAlt] = new ItemData { ItemId = ItemData.HairCouponAlt, Cosmetic = ItemData.AltHair },
        [ItemData.FaceCoupon] = new ItemData { ItemId = ItemData.FaceCoupon, Cosmetic = ItemData.DefaultFace },
        [ItemData.FaceCouponAlt] = new ItemData { ItemId = ItemData.FaceCouponAlt, Cosmetic = ItemData.AltFace },
        [HairCouponNonDefault] = new ItemData { ItemId = HairCouponNonDefault, Cosmetic = 65370 },
        [FaceCouponNonDefault] = new ItemData { ItemId = FaceCouponNonDefault, Cosmetic = 20053 },
    };

    private static CharaSlot Chara()
    {
        var chara = new CharaSlot
        {
            Hair = ItemData.DefaultHair,
            Face = ItemData.DefaultFace,
            CashWorn = new List<int> { ItemData.HairCoupon, ItemData.FaceCoupon },
            DecBag = new Dictionary<int, int>
            {
                [CashSword] = 1,
                [CashSword2] = 1,
                [ItemData.HairCouponAlt] = 1,
                [ItemData.FaceCouponAlt] = 1,
                [HairCouponNonDefault] = 1,
                [FaceCouponNonDefault] = 1,
            },
        };
        Inventory.Ensure(chara);
        return chara;
    }

    [TestMethod]
    public void DecDoubleClick_WearsFashion()
    {
        var defs = Defs();
        var chara = Chara();
        var hand = new CarryHand();
        int cell = chara.DecOrder!.IndexOf(CashSword);

        Assert.AreEqual(HandAct.Moved, hand.Click(chara, "dec", CashSword, defs, 100, 1000, cell));
        Assert.AreEqual(HandAct.Wore, hand.Click(chara, "dec", CashSword, defs, 100, 1100, cell));
        CollectionAssert.Contains(chara.CashWorn, CashSword);
        Assert.AreEqual(0, Inventory.Count(chara, "dec", CashSword));
    }

    [TestMethod]
    public void CashFashion_WearAndTakeOff_RoundTrips()
    {
        var defs = Defs();
        var chara = Chara();

        Assert.IsTrue(Progression.WearCash(chara, CashSword, defs));
        CollectionAssert.Contains(chara.CashWorn, CashSword);
        Assert.AreEqual(0, Inventory.Count(chara, "dec", CashSword));

        Assert.IsTrue(Progression.TakeOffCashFashion(chara, CashSword, defs));
        CollectionAssert.DoesNotContain(chara.CashWorn, CashSword);
        Assert.AreEqual(1, Inventory.Count(chara, "dec", CashSword));
    }

    [TestMethod]
    public void CashFashion_SameArea_ReplacesOldAndReturnsIt()
    {
        var defs = Defs();
        var chara = Chara();

        Assert.IsTrue(Progression.WearCash(chara, CashSword, defs));
        Assert.IsTrue(Progression.WearCash(chara, CashSword2, defs));

        CollectionAssert.DoesNotContain(chara.CashWorn, CashSword);
        CollectionAssert.Contains(chara.CashWorn, CashSword2);
        Assert.AreEqual(1, Inventory.Count(chara, "dec", CashSword));
        Assert.AreEqual(0, Inventory.Count(chara, "dec", CashSword2));
    }

    [TestMethod]
    public void HairCoupon_SwapsLookAndReturnsOldCoupon()
    {
        var defs = Defs();
        var chara = Chara();

        Assert.IsTrue(Progression.WearCash(chara, ItemData.HairCouponAlt, defs));

        Assert.AreEqual(ItemData.AltHair, chara.Hair);
        CollectionAssert.Contains(chara.CashWorn, ItemData.HairCouponAlt);
        CollectionAssert.DoesNotContain(chara.CashWorn, ItemData.HairCoupon);
        Assert.AreEqual(1, Inventory.Count(chara, "dec", ItemData.HairCoupon));
        Assert.AreEqual(0, Inventory.Count(chara, "dec", ItemData.HairCouponAlt));
    }

    [TestMethod]
    public void FaceCoupon_SwapsLookAndReturnsOldCoupon()
    {
        var defs = Defs();
        var chara = Chara();

        Assert.IsTrue(Progression.WearCash(chara, ItemData.FaceCouponAlt, defs));

        Assert.AreEqual(ItemData.AltFace, chara.Face);
        CollectionAssert.Contains(chara.CashWorn, ItemData.FaceCouponAlt);
        CollectionAssert.DoesNotContain(chara.CashWorn, ItemData.FaceCoupon);
        Assert.AreEqual(1, Inventory.Count(chara, "dec", ItemData.FaceCoupon));
        Assert.AreEqual(0, Inventory.Count(chara, "dec", ItemData.FaceCouponAlt));
    }

    [TestMethod]
    public void NonDefaultCoupons_SwapLook()
    {
        var defs = Defs();
        var chara = Chara();

        Assert.IsTrue(Progression.WearCash(chara, HairCouponNonDefault, defs));
        Assert.AreEqual(65370, chara.Hair);
        Assert.AreEqual(1, Inventory.Count(chara, "dec", ItemData.HairCoupon));

        Assert.IsTrue(Progression.WearCash(chara, FaceCouponNonDefault, defs));
        Assert.AreEqual(20053, chara.Face);
        Assert.AreEqual(1, Inventory.Count(chara, "dec", ItemData.FaceCoupon));
        CollectionAssert.Contains(chara.CashWorn, HairCouponNonDefault);
        CollectionAssert.Contains(chara.CashWorn, FaceCouponNonDefault);
    }

    [TestMethod]
    public void TakeOffCashFashion_CouponReturnsFalse()
    {
        var chara = Chara();

        Assert.IsFalse(Progression.TakeOffCashFashion(chara, ItemData.HairCoupon, Defs()));
        CollectionAssert.Contains(chara.CashWorn, ItemData.HairCoupon);
        Assert.AreEqual(0, Inventory.Count(chara, "dec", ItemData.HairCoupon));
    }

    [TestMethod]
    public void WearCash_FullBagSameIdStack_KeepsOldWorn()
    {
        var defs = Defs();
        var bag = new Dictionary<int, int> { [CashSword] = 2 };
        for (int i = 0; i < 127; i++) bag[10000 + i] = 1;
        var chara = new CharaSlot
        {
            Hair = ItemData.DefaultHair,
            Face = ItemData.DefaultFace,
            CashWorn = new List<int> { CashSword2 },
            DecBag = bag,
        };
        Inventory.Ensure(chara);
        Assert.AreEqual(Inventory.Capacity, Inventory.Used(chara, "dec"));

        Assert.IsFalse(Progression.WearCash(chara, CashSword, defs));

        CollectionAssert.Contains(chara.CashWorn, CashSword2);
        Assert.AreEqual(2, Inventory.Count(chara, "dec", CashSword));
    }

    [TestMethod]
    public void AttackAction_MapsWeaponCodes()
    {
        var defs = Defs();
        var weapon = new ItemData { ItemId = 99, Slot = "Wp" };
        defs[99] = weapon;
        var chara = new CharaSlot { Equips = new List<int> { 99 } };
        var cases = new (int Code, string Action)[]
        {
            (0, "swingO1"), (1, "swingO1"), (2, "swingT1"), (3, "swingP1"),
            (4, "stabO1"), (5, "stabT1"), (6, "shoot1"), (7, "swingO1"),
        };
        foreach (var (code, action) in cases)
        {
            weapon.Attack = code;
            Assert.AreEqual(action, Progression.AttackAction(chara, defs));
        }
    }

    [TestMethod]
    public void AttackAction_CashWeaponWinsThenGearThenBeginner()
    {
        var defs = Defs();
        var chara = Chara();
        Assert.AreEqual("swingO1", Progression.AttackAction(chara, defs));

        chara.Equips = new List<int> { GearSword };
        Assert.AreEqual("swingT1", Progression.AttackAction(chara, defs));

        chara.CashWorn!.Add(CashSword2);
        Assert.AreEqual("stabO1", Progression.AttackAction(chara, defs)); // cash Attack 4 wins
    }
}
