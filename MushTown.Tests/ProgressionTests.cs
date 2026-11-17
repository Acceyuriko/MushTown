using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class ProgressionTests
{
    [TestMethod]
    public void ExpNext_LinearCurve()
    {
        Assert.AreEqual(35, Progression.ExpNext(1));
        Assert.AreEqual(115, Progression.ExpNext(5));
        Assert.AreEqual(215, Progression.ExpNext(10));
    }

    [TestMethod]
    public void Vitals_GrowPerLevel()
    {
        Assert.AreEqual(50, Progression.MaxHp(1));
        Assert.AreEqual(80, Progression.MaxHp(2));
        Assert.AreEqual(320, Progression.MaxHp(10));
        Assert.AreEqual(5, Progression.MaxMp(1));
        Assert.AreEqual(10, Progression.MaxMp(2));
    }

    [TestMethod]
    public void Vitals_AboveInt_StayLong()
    {
        var chara = new CharaSlot
        {
            Name = "hero",
            Level = 500_000_000,
            Hp = 1_000_000_000L,
            Mp = 1_000_000_000L,
            Use = new Dictionary<int, int> { [ItemData.RedPotion] = 1 },
        };
        Assert.IsTrue(Progression.MaxHp(chara.Level) > int.MaxValue);
        Assert.IsTrue(Progression.MaxMp(chara.Level) > int.MaxValue);
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 2_000_000_000L, 2_000_000_000L,
            Progression.MaxHp(chara.Level), Progression.MaxMp(chara.Level)));
        Assert.IsTrue(chara.Hp > int.MaxValue);
        Assert.IsTrue(chara.Mp > int.MaxValue);
    }

    [TestMethod]
    public void PanelRange_BeginnerIs2to7()
    {
        var s = StatAggregator.Aggregate(new[]
        {
            new StatModifier(StatKey.STR, StatValueType.FLAT, 4, StatSource.Skill),
            new StatModifier(StatKey.DEX, StatValueType.FLAT, 4, StatSource.Skill),
            new StatModifier(StatKey.ATT, StatValueType.FLAT, Progression.WeaponAtk, StatSource.Skill),
        });
        var job = new JobProfile(StatKey.STR, StatKey.DEX, WeaponType.Default);
        Assert.AreEqual((2L, 7L), DamageChain.PanelRange(s, job));
    }

    [TestMethod]
    public void ApplyExp_LevelsUpWithAutoStats()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(0, Progression.ApplyExp(chara, 34, Progression.MaxHp(1)));
        Assert.AreEqual(1, chara.Level);
        Assert.AreEqual(34, chara.Exp);
        Assert.AreEqual(1, Progression.ApplyExp(chara, 1, Progression.MaxHp(2))); // 35 = ExpNext(1)
        Assert.AreEqual(2, chara.Level);
        Assert.AreEqual(0, chara.Exp);
        Assert.AreEqual(8, chara.Str); // +4 auto
        Assert.AreEqual(5, chara.Dex); // +1 auto
        Assert.AreEqual(Progression.MaxHp(2), chara.Hp); // refilled
        Assert.AreEqual(Progression.MaxMp(2), chara.Mp);
    }

    [TestMethod]
    public void ApplyExp_BigAward_LevelsTwice()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(2, Progression.ApplyExp(chara, 100, Progression.MaxHp(3))); // 35 + 55 = 90, 10 left
        Assert.AreEqual(3, chara.Level);
        Assert.AreEqual(10, chara.Exp);
        Assert.AreEqual(12, chara.Str);
        Assert.AreEqual(6, chara.Dex);
    }

    [TestMethod]
    public void UsePotion_DrinksAndHeals()
    {
        var chara = new CharaSlot { Name = "hero", Hp = 10 };
        chara.Use = new Dictionary<int, int> { { ItemData.RedPotion, 2 } };
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            Progression.MaxHp(1), Progression.MaxMp(1)));
        Assert.AreEqual(50, chara.Hp); // clamped to MaxHp(1)
        Assert.AreEqual(1, chara.Use[ItemData.RedPotion]);
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            Progression.MaxHp(1), Progression.MaxMp(1))); // full pool: nothing happens
        Assert.AreEqual(1, chara.Use[ItemData.RedPotion]);
    }

    [TestMethod]
    public void UsePotion_EmptyOrMissing_ReturnsFalse()
    {
        var chara = new CharaSlot { Name = "hero", Hp = 10 }; // Use null (pre-W10 save)
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            Progression.MaxHp(1), Progression.MaxMp(1)));
        chara.Use = new Dictionary<int, int> { { ItemData.RedPotion, 1 } };
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.BluePotion, 0, 100,
            Progression.MaxHp(1), Progression.MaxMp(1))); // not in the bag
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            Progression.MaxHp(1), Progression.MaxMp(1)));
        Assert.IsFalse(chara.Use.ContainsKey(ItemData.RedPotion)); // the last bottle leaves no zero behind
    }

    [TestMethod]
    public void UsePotion_Blue_HealsMp()
    {
        var chara = new CharaSlot { Name = "hero", Mp = 1 };
        chara.Use = new Dictionary<int, int> { { ItemData.BluePotion, 1 } };
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.BluePotion, 0, 100,
            Progression.MaxHp(1), Progression.MaxMp(1)));
        Assert.AreEqual(5, chara.Mp); // clamped to MaxMp(1)
    }

    // Active damage lerps lo->hi over the skill's levels (level 1 pays lo).
    [TestMethod]
    public void SkillDamage_LerpsOverLevels()
    {
        var skill = new SkillData { DmgLo = 150, DmgHi = 250, MaxLevel = 11 };
        Assert.AreEqual(150, Progression.SkillDamage(skill, 1));
        Assert.AreEqual(250, Progression.SkillDamage(skill, 11));
        Assert.AreEqual(200, Progression.SkillDamage(skill, 6));
        Assert.AreEqual(150, Progression.SkillDamage(new SkillData { DmgLo = 150, MaxLevel = 1 }, 1));
    }

    // Level-up refills and potions both use the max supplied by the caller.
    [TestMethod]
    public void ApplyExp_RefillsToGivenMax()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(1, Progression.ApplyExp(chara, 35, 160));
        Assert.AreEqual(160, chara.Hp);
    }

    [TestMethod]
    public void UsePotion_HealsToGivenMax()
    {
        var chara = new CharaSlot { Name = "hero", Hp = 10 };
        chara.Use = new Dictionary<int, int> { { ItemData.RedPotion, 1 } };
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0, 60, 5));
        Assert.AreEqual(60, chara.Hp);
    }

    private static Dictionary<int, ItemData> EquipDefs() => new()
    {
        [ItemData.Mop] = new ItemData { ItemId = ItemData.Mop, Slot = "Wp", Atk = 47 },
        [1442000] = new ItemData { ItemId = 1442000, Slot = "Wp", Atk = 32 },
        [ItemData.BambooRing] = new ItemData { ItemId = ItemData.BambooRing, Slot = "Ri", NameTag = 58 },
        [ItemData.InkRing] = new ItemData { ItemId = ItemData.InkRing, Slot = "Ri", NameTag = 59 },
    };

    // Wearing swaps the same-slot piece out; wearing the worn id again is a
    // no-op (out and back in); empty bags do nothing.
    [TestMethod]
    public void WearEquip_SwapsSameSlot()
    {
        var chara = new CharaSlot
        {
            Name = "hero",
            EquipBag = new Dictionary<int, int> { { ItemData.Mop, 1 } },
            Equips = new List<int> { 1442000 },
        };
        Assert.IsTrue(Progression.WearEquip(chara, ItemData.Mop, EquipDefs()));
        Assert.AreEqual(1, chara.Equips.Count);
        Assert.AreEqual(ItemData.Mop, chara.Equips[0]);
        Assert.AreEqual(1, chara.EquipBag[1442000]); // the axe swapped out
        Assert.IsFalse(chara.EquipBag.ContainsKey(ItemData.Mop));

        chara.EquipBag[ItemData.Mop] = 1;
        Assert.IsTrue(Progression.WearEquip(chara, ItemData.Mop, EquipDefs()));
        Assert.AreEqual(1, chara.Equips.Count);
        Assert.AreEqual(ItemData.Mop, chara.Equips[0]);
        Assert.AreEqual(1, chara.EquipBag[ItemData.Mop]); // out and back in

        chara.EquipBag.Remove(1442000);
        Assert.IsFalse(Progression.WearEquip(chara, 1442000, EquipDefs())); // not in the bag
    }

    // Every demo slot uses the same generic one-piece rule as weapons.
    [TestMethod]
    public void WearEquip_EveryDemoSlotSwapsSameSlot()
    {
        foreach (var (id, slot, _) in DemoSlots.All)
        {
            int oldId = id + 1_000_000;
            var defs = new Dictionary<int, ItemData>
            {
                [id] = new() { ItemId = id, Slot = slot },
                [oldId] = new() { ItemId = oldId, Slot = slot },
            };
            var chara = new CharaSlot
            {
                Name = "hero",
                EquipBag = new Dictionary<int, int> { { id, 1 } },
                Equips = new List<int> { oldId },
            };

            Assert.IsTrue(Progression.WearEquip(chara, id, defs), $"wear {id} ({slot})");
            CollectionAssert.AreEqual(new[] { id }, chara.Equips, $"{slot} should replace {oldId}");
            Assert.AreEqual(1, chara.EquipBag![oldId], $"{slot} old item should return to the bag");
            Assert.IsFalse(chara.EquipBag.ContainsKey(id), $"{slot} new item should leave the bag");
        }
    }

    // Rings share one finger: wearing a second swaps the first out, and a
    // ring coexists with the weapon (different slots).
    [TestMethod]
    public void WearEquip_RingsSwapOneFinger()
    {
        var chara = new CharaSlot
        {
            Name = "hero",
            EquipBag = new Dictionary<int, int> { { ItemData.Mop, 1 }, { ItemData.BambooRing, 1 }, { ItemData.InkRing, 1 } },
        };
        Assert.IsTrue(Progression.WearEquip(chara, ItemData.Mop, EquipDefs()));
        Assert.IsTrue(Progression.WearEquip(chara, ItemData.BambooRing, EquipDefs()));
        Assert.AreEqual(2, chara.Equips!.Count);
        Assert.IsTrue(Progression.WearEquip(chara, ItemData.InkRing, EquipDefs()));
        Assert.AreEqual(2, chara.Equips.Count);
        Assert.IsTrue(chara.Equips.Contains(ItemData.Mop));
        Assert.IsTrue(chara.Equips.Contains(ItemData.InkRing));
        Assert.AreEqual(1, chara.EquipBag[ItemData.BambooRing]); // swapped out
    }

    // A top, pants and overall share the body area: an overall takes both
    // boxes and sends the top and pants back to the bag.
    [TestMethod]
    public void WearEquip_OverallTakesTopAndPants()
    {
        var defs = new Dictionary<int, ItemData>
        {
            [1] = new() { ItemId = 1, Slot = "Ma" },
            [2] = new() { ItemId = 2, Slot = "Pn" },
            [3] = new() { ItemId = 3, Slot = "MaPn" },
        };
        var chara = new CharaSlot
        {
            Name = "hero",
            EquipBag = new Dictionary<int, int> { { 3, 1 } },
            Equips = new List<int> { 1, 2 },
        };

        Assert.IsTrue(Progression.WearEquip(chara, 3, defs));
        CollectionAssert.AreEqual(new[] { 3 }, chara.Equips);
        Assert.AreEqual(1, chara.EquipBag![1]);
        Assert.AreEqual(1, chara.EquipBag[2]);
    }

    // A full bag must not eat the second old piece when an overall takes
    // both a top and pants.
    [TestMethod]
    public void WearEquip_OverallFullBagKeepsOldPieces()
    {
        var defs = new Dictionary<int, ItemData>
        {
            [1] = new() { ItemId = 1, Slot = "Ma" },
            [2] = new() { ItemId = 2, Slot = "Pn" },
            [3] = new() { ItemId = 3, Slot = "MaPn" },
        };
        var chara = new CharaSlot
        {
            Name = "hero",
            Equips = new List<int> { 1, 2 },
        };
        Inventory.Ensure(chara);
        for (int i = 0; i < Inventory.Capacity - 1; i++)
            Assert.IsTrue(Inventory.Add(chara, "equip", 1000 + i, 1));
        Assert.IsTrue(Inventory.Add(chara, "equip", 3, 1));

        Assert.IsFalse(Progression.WearEquip(chara, 3, defs));
        CollectionAssert.AreEqual(new[] { 1, 2 }, chara.Equips);
        Assert.AreEqual(1, Inventory.Count(chara, "equip", 3));
    }

    // Wearing a top or pants under an overall swaps the overall back out.
    [TestMethod]
    public void WearEquip_TopAndPantsSwapOutOverall()
    {
        var defs = new Dictionary<int, ItemData>
        {
            [1] = new() { ItemId = 1, Slot = "Ma" },
            [2] = new() { ItemId = 2, Slot = "Pn" },
            [3] = new() { ItemId = 3, Slot = "MaPn" },
        };
        var chara = new CharaSlot
        {
            Name = "hero",
            EquipBag = new Dictionary<int, int> { { 1, 1 }, { 2, 1 } },
            Equips = new List<int> { 3 },
        };

        Assert.IsTrue(Progression.WearEquip(chara, 1, defs));
        CollectionAssert.AreEqual(new[] { 1 }, chara.Equips);
        Assert.AreEqual(1, chara.EquipBag![3]);
        Assert.IsTrue(Progression.WearEquip(chara, 2, defs));
        CollectionAssert.AreEqual(new[] { 1, 2 }, chara.Equips);
        Assert.AreEqual(1, chara.EquipBag[3]);
    }

    // Takeoff returns every demo piece to the bag; unworn slots do nothing.
    [TestMethod]
    public void TakeOffEquip_EveryDemoSlotReturnsToBag()
    {
        foreach (var (id, slot, _) in DemoSlots.All)
        {
            var chara = new CharaSlot { Name = "hero", Equips = new List<int> { id } };

            Assert.IsTrue(Progression.TakeOffEquip(chara, id), $"take off {id} ({slot})");
            Assert.AreEqual(0, chara.Equips.Count, $"{slot} should be empty");
            Assert.AreEqual(1, chara.EquipBag![id], $"{slot} should return to the bag");
        }
    }

    [TestMethod]
    public void TakeOffEquip_ReturnsToBag()
    {
        var chara = new CharaSlot { Name = "hero", Equips = new List<int> { ItemData.Mop } };
        Assert.IsFalse(Progression.TakeOffEquip(chara, 1442000)); // not worn
        Assert.IsTrue(Progression.TakeOffEquip(chara, ItemData.Mop));
        Assert.AreEqual(0, chara.Equips.Count);
        Assert.AreEqual(1, chara.EquipBag![ItemData.Mop]);
        Assert.IsFalse(Progression.TakeOffEquip(chara, ItemData.Mop)); // already off
        Assert.IsFalse(Progression.WearEquip(new CharaSlot { Name = "nobody" }, ItemData.Mop, EquipDefs())); // no bag
    }

    [TestMethod]
    public void TakeOffEquip_FullBag_StaysWorn()
    {
        var chara = new CharaSlot
        {
            Name = "hero",
            Equips = new List<int> { ItemData.Mop },
            EquipBag = Enumerable.Range(0, Inventory.Capacity).ToDictionary(n => 1300000 + n, _ => 1),
        };
        Inventory.Ensure(chara);

        Assert.IsFalse(Progression.TakeOffEquip(chara, ItemData.Mop));
        Assert.AreEqual(1, chara.Equips.Count);
        Assert.AreEqual(0, chara.EquipBag!.GetValueOrDefault(ItemData.Mop));
    }
}
