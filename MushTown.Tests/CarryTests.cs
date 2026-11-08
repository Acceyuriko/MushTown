using MushTown.Shared;

namespace MushTown.Tests;

// Positional carry: one click picks a whole cell into the hand; a second
// click places/swaps it. Double-clicking the source drinks or wears it.
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

    private static CharaSlot Chara()
    {
        var chara = new CharaSlot
        {
            Level = 10,
            Hp = 10,
            Mp = 5,
            Use = new Dictionary<int, int> { [Red] = 3 },
            EquipBag = new Dictionary<int, int> { [Sword] = 1 },
            Equips = new List<int>(),
        };
        Inventory.Ensure(chara);
        return chara;
    }

    [TestMethod]
    public void Potion_PickAndPlace_RoundTrips()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 1000, 0));
        Assert.AreEqual(("use", Red), (hand.Kind, hand.Id));
        Assert.AreEqual(3, c.Use![Red]); // moving a cell keeps the count

        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 2000, 2));
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(0, c.UseOrder![0]);
        Assert.AreEqual(Red, c.UseOrder[2]);
        Assert.AreEqual(3, c.Use[Red]);
    }

    [TestMethod]
    public void Potion_DoubleClick_DrinksOne()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000, 0);
        Assert.AreEqual(HandAct.Drank, hand.Click(c, "use", Red, defs, 0, 1200, 0));
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(2, c.Use![Red]);
        Assert.AreEqual(60, c.Hp);
    }

    [TestMethod]
    public void Potion_SlowSecondClick_DropsBack()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000, 0);
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 1000 + CarryHand.DoubleMs + 1, 0));
        Assert.IsTrue(hand.Empty);
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
        hand.Click(c, "use", Red, defs, 0, 1000, 0);
        Assert.AreEqual(HandAct.Drank, hand.Click(c, "use", Red, defs, 0, 1200, 0));
        Assert.AreEqual(3, c.Use![Red]); // the guard refused: none lost
    }

    [TestMethod]
    public void Potion_OnEquipCell_Rejected()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000, 0);
        Assert.AreEqual(HandAct.None, hand.Click(c, "equip", Sword, defs, 0, 1100, 0));
        Assert.AreEqual(("use", Red), (hand.Kind, hand.Id)); // still held
    }

    [TestMethod]
    public void Equip_BagToWorn_Wears()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "equip", Sword, defs, 0, 1000, 0));
        Assert.AreEqual(1, c.EquipBag![Sword]);
        Assert.AreEqual(HandAct.Wore, hand.Click(c, "equip", Sword, defs, 0, 1200, 0));
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(1, c.Equips!.Count(w => w == Sword));
        Assert.IsFalse(c.EquipBag.ContainsKey(Sword));
        Assert.AreEqual(0, c.EquipOrder![0]);
    }

    [TestMethod]
    public void Equip_DifferentId_SwapsCells()
    {
        var defs = Defs();
        var c = Chara();
        c.EquipBag = new Dictionary<int, int> { [Sword] = 1, [Axe] = 1 };
        Inventory.Ensure(c);
        var hand = new CarryHand();

        Assert.AreEqual(HandAct.Moved, hand.Click(c, "equip", Sword, defs, 0, 1000, 0));
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "equip", Axe, defs, 0, 1100, 1));
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(Axe, c.EquipOrder![0]);
        Assert.AreEqual(Sword, c.EquipOrder[1]);
        Assert.AreEqual(1, c.EquipBag[Axe]);
        Assert.AreEqual(1, c.EquipBag[Sword]);
    }

    [TestMethod]
    public void StaleCell_RemovedElsewhere_DropsThePreviewAndPicksFresh()
    {
        var defs = Defs();
        var c = Chara();
        Assert.IsTrue(Inventory.Add(c, "use", Blue, 1));
        var hand = new CarryHand();
        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Red, defs, 0, 1000, 0));
        Assert.IsTrue(Inventory.Remove(c, "use", Red, 3)); // sold or drunk elsewhere

        Assert.AreEqual(HandAct.Moved, hand.Click(c, "use", Blue, defs, 0, 1100, 1));
        Assert.AreEqual(("use", Blue), (hand.Kind, hand.Id));
    }

    [TestMethod]
    public void StaleCell_EmptyClick_ReturnsTheHand()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000, 0);
        Inventory.Remove(c, "use", Red, 3);

        Assert.AreEqual(HandAct.None, hand.Click(c, "use", 0, defs, 0, 1100, 0));
        Assert.IsTrue(hand.Empty);
    }

    [TestMethod]
    public void Esc_ReturnsTheCell()
    {
        var defs = Defs();
        var c = Chara();
        var hand = new CarryHand();
        hand.Click(c, "use", Red, defs, 0, 1000, 0);
        hand.Return();
        Assert.IsTrue(hand.Empty);
        Assert.AreEqual(Red, c.UseOrder![0]);
        Assert.AreEqual(3, c.Use![Red]);
    }
}
