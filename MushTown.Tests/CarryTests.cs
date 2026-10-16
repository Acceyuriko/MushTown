using MushTown.Shared;

namespace MushTown.Tests;

// Take-1 carry: one piece leaves the bags into the hand and always has
// a lossless home (drop, wear, drink, or Esc-return).
[TestClass]
public sealed class CarryTests
{
    private const int Red = ItemData.RedPotion; // +50 HP
    private const int Blue = ItemData.BluePotion; // +100 MP
    private const int Sword = 1302000; // Wp test gear
    private const int Axe = 1312001; // Wp test gear (same slot: swaps)

    private static Dictionary<int, ItemData> Defs() => new()
    {
        [Red] = new ItemData { ItemId = Red, Hp = 50 },
        [Blue] = new ItemData { ItemId = Blue, Mp = 100 },
        [Sword] = new ItemData { ItemId = Sword, Slot = "Wp" },
        [Axe] = new ItemData { ItemId = Axe, Slot = "Wp" },
    };

    private static CharaSlot Chara() => new()
    {
        Level = 10,
        Hp = 10,
        Mp = 5,
        Use = new Dictionary<int, int> { [Red] = 3 },
        EquipBag = new Dictionary<int, int> { [Sword] = 2 },
        Equips = new List<int>(),
    };

    [TestMethod]
    public void Potion_PickAndDrop_RoundTrips()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 1000));
        Assert.AreEqual(("use", Red), (hand.Kind, hand.Id));
        Assert.AreEqual(2, c.Use![Red]); // one left the bag
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Blue, defs, 0, 2000)); // any USE cell merges
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(3, c.Use[Red]);
    }

    [TestMethod]
    public void Potion_DoubleClick_DrinksOne()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000);
        Assert.AreEqual(HandAct.Drank, hand.Click(c, "use", Red, defs, 0, 1200));
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(2, c.Use![Red]); // pooled back, then drank one
        Assert.AreEqual(60, c.Hp);
    }

    [TestMethod]
    public void Potion_SlowSecondClick_DropsBack()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000);
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 1000 + CarryHand.DoubleMs + 1));
        Assert.AreEqual(3, c.Use![Red]);
        Assert.AreEqual(10, c.Hp); // nothing drank
    }

    [TestMethod]
    public void Potion_FullHp_NoDoubleDrink()
    {
        var defs = Defs();
        var c = Chara();
        c.Hp = Progression.MaxHp(c.Level);
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000);
        Assert.AreEqual(HandAct.Drank, hand.Click(c, "use", Red, defs, 0, 1200));
        Assert.AreEqual(3, c.Use![Red]); // the guard refused: pooled back, none lost
    }

    [TestMethod]
    public void Potion_OnEquipCell_Rejected()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000);
        Assert.AreEqual(HandAct.None, hand.Click(c, "bag", Sword, defs, 0, 1100));
        Assert.AreEqual(("use", Red), (hand.Kind, hand.Id)); // still held
    }

    [TestMethod]
    public void Equip_BagToWorn_Wears()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "bag", Sword, defs, 0, 1000));
        Assert.AreEqual(1, c.EquipBag![Sword]);
        Assert.AreEqual(HandAct.Wore, hand.Click(c, "worn", Sword, defs, 0, 1100));
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(1, c.Equips!.Count(w => w == Sword));
        Assert.AreEqual(1, c.EquipBag[Sword]);
    }

    [TestMethod]
    public void Equip_WornSwap_OldLandsInHand()
    {
        var defs = Defs();
        var c = Chara();
        c.Equips!.Add(Axe);
        var hand = new CarryHand();
        hand.Click(c, "bag", Sword, defs, 0, 1000);
        Assert.AreEqual(HandAct.Wore, hand.Click(c, "worn", Axe, defs, 0, 1100));
        Assert.AreEqual(("worn", Axe), (hand.Kind, hand.Id));
        Assert.IsFalse(c.Equips.Contains(Axe));
        Assert.AreEqual(1, c.Equips.Count(w => w == Sword));
        hand.Return(c); // Esc: the old piece lands in the bag, never back on the doll
        Assert.AreEqual(1, c.EquipBag[Axe]);
        Assert.AreEqual(1, c.Equips.Count(w => w == Sword));
    }

    [TestMethod]
    public void Equip_WornPickAndDrop_RoundTrips()
    {
        var defs = Defs();
        var c = Chara();
        c.Equips!.Add(Sword);
        c.EquipBag!.Remove(Sword);
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.Wore, hand.Click(c, "worn", Sword, defs, 0, 1000)); // takeoff rebuilds the doll
        Assert.IsFalse(c.Equips.Contains(Sword));
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "bag", Axe, defs, 0, 1100)); // any bag cell merges
        Assert.AreEqual(1, c.EquipBag[Sword]);
    }

    [TestMethod]
    public void Equip_OnUseCell_Rejected()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "bag", Sword, defs, 0, 1000);
        Assert.AreEqual(HandAct.None, hand.Click(c, "use", Red, defs, 0, 1100));
        Assert.AreEqual(("bag", Sword), (hand.Kind, hand.Id));
    }

    [TestMethod]
    public void EmptyCells_DoNothing()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.None, hand.Click(c, "use", Blue, defs, 0, 1000)); // owned: none
        Assert.AreEqual(HandAct.None, hand.Click(c, "bag", Axe, defs, 0, 1100));
        Assert.AreEqual(HandAct.None, hand.Click(c, "worn", Sword, defs, 0, 1200));
        Assert.IsTrue(hand.Empty);
    }

    [TestMethod]
    public void WornReclick_Rewears()
    {
        var defs = Defs();
        var c = Chara();
        c.Equips!.Add(Sword);
        c.EquipBag!.Remove(Sword);
        var hand = new CarryHand();
        hand.Click(c, "worn", Sword, defs, 0, 1000); // pick it off
        Assert.AreEqual(HandAct.Wore, hand.Click(c, "worn", Sword, defs, 0, 1100)); // empty slot: wears back, nothing swaps
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(1, c.Equips.Count(w => w == Sword));
        Assert.IsFalse(c.EquipBag.ContainsKey(Sword));
    }

    [TestMethod]
    public void Swap_SameId_Conserves()
    {
        var defs = Defs();
        var c = Chara();
        c.Equips!.Add(Sword); // one worn, two in the bag
        var hand = new CarryHand();
        hand.Click(c, "bag", Sword, defs, 0, 1000);
        Assert.AreEqual(HandAct.Wore, hand.Click(c, "worn", Sword, defs, 0, 1100));
        Assert.AreEqual(("worn", Sword), (hand.Kind, hand.Id)); // the old copy lands back in hand
        Assert.AreEqual(1, c.Equips.Count(w => w == Sword));
        Assert.AreEqual(1, c.EquipBag![Sword]); // 2 - 1 held + 0 net from the swap
    }

    [TestMethod]
    public void DoubleClick_BrokenByOtherCell()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000);
        Assert.AreEqual(HandAct.None, hand.Click(c, "bag", Sword, defs, 0, 1100)); // reject breaks the double
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 1200)); // drops back, drinks nothing
        Assert.AreEqual(3, c.Use![Red]);
        Assert.AreEqual(10, c.Hp);
    }

    [TestMethod]
    public void Esc_ReturnsToPools()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000);
        hand.Return(c);
        Assert.AreEqual(3, c.Use![Red]);
        hand.Click(c, "bag", Sword, defs, 0, 1100);
        hand.Return(c);
        Assert.AreEqual(2, c.EquipBag![Sword]);
        hand.Return(c); // empty hands: no-op
        Assert.IsTrue(hand.Empty);
    }
}
