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
        // Hero (and every job without a row): 50 + 28 x (level - 1) + 700 HP, 5 + 12 x (level - 1) + 300 MP.
        Assert.AreEqual(750, Progression.MaxHp(1));
        Assert.AreEqual(778, Progression.MaxHp(2));
        Assert.AreEqual(1002, Progression.MaxHp(10));
        Assert.AreEqual(305, Progression.MaxMp(1));
        Assert.AreEqual(317, Progression.MaxMp(2));
        Assert.AreEqual(Progression.MaxHp(10), Progression.MaxHp(10, 999)); // a job with no row plays the Hero numbers
    }

    // The design values of research/hp-mp.md section 7.3 at level 200.
    [TestMethod]
    public void Vitals_At200_HeroAndFirePoisonMage()
    {
        Assert.AreEqual(6322, Progression.MaxHp(200, StatTables.Hero));
        Assert.AreEqual(2693, Progression.MaxMp(200, StatTables.Hero));
        Assert.AreEqual(3534, Progression.MaxHp(200, StatTables.FirePoisonMage));
        Assert.AreEqual(5083, Progression.MaxMp(200, StatTables.FirePoisonMage));
    }

    // Base + the max-level job skills + Heroic (the default passives that touch HP/MP are Heroic only here:
    // no equipment, no Link, no Union, no trait). WZ skill values; the base is a design value.
    [TestMethod]
    public void MaxHpMp_At200_WithJobSkillsAndHeroic()
    {
        var heroic = DefaultPassives.All.Where(m => m.Label.StartsWith("Heroic") && m.Key is StatKey.MAX_HP or StatKey.MAX_MP);
        var hero = StatAggregator.Aggregate(StatTables.Vitals(StatTables.Hero, 200).Concat(heroic));
        Assert.AreEqual(12_626, hero.IntValue(StatKey.MAX_HP)); // floor((6322 + 20 x 200 + 200) x 1.20)
        Assert.AreEqual(2_793, hero.IntValue(StatKey.MAX_MP)); // 2693 + 100
        var mage = StatAggregator.Aggregate(StatTables.Vitals(StatTables.FirePoisonMage, 200).Concat(heroic));
        Assert.AreEqual(3_734, mage.IntValue(StatKey.MAX_HP)); // 3534 + 200
        Assert.AreEqual(35_019, mage.IntValue(StatKey.MAX_MP)); // floor((5083 + 120 x 200 + 100) x 1.20)
    }

    // Everything the game adds without equipment at level 200: Vitals + every default passive
    // (Heroic, the Links, the Union, the traits). Hyper Stats are 0 and Endure is client skill data, both left out.
    [TestMethod]
    public void MaxHpMp_At200_WithDefaultPassives()
    {
        var hero = StatAggregator.Aggregate(StatTables.Vitals(StatTables.Hero, 200).Concat(DefaultPassives.All));
        Assert.AreEqual(30_988, hero.IntValue(StatKey.MAX_HP)); // floor((6322 + 4000 + 10200) x 1.51)
        Assert.AreEqual(11_425, hero.IntValue(StatKey.MAX_MP)); // floor((2693 + 7600) x 1.11)
        var mage = StatAggregator.Aggregate(StatTables.Vitals(StatTables.FirePoisonMage, 200).Concat(DefaultPassives.All));
        Assert.AreEqual(17_991, mage.IntValue(StatKey.MAX_HP)); // floor((3534 + 10200) x 1.31)
        Assert.AreEqual(48_054, mage.IntValue(StatKey.MAX_MP)); // floor((5083 + 24000 + 7600) x 1.31)
    }

    // The 500,000 cap stays on the new records.
    [TestMethod]
    public void MaxHpMp_StayCapped()
    {
        var huge = StatAggregator.Aggregate(StatTables.Vitals(StatTables.FirePoisonMage, 5_000));
        Assert.AreEqual(500_000, huge.IntValue(StatKey.MAX_MP));
    }

    // The hover lists the skill group of max MP: the per-level points and the percent, named by their source group.
    [TestMethod]
    public void MaxMp_SourcesShowTheSkillPointsAndPercent()
    {
        var mage = StatAggregator.Aggregate(StatTables.Vitals(StatTables.FirePoisonMage, 200));
        var lines = StatDisplay.Sources(mage, 1); // row 1 is MP
        Assert.IsTrue(lines.Any(l => l.Label == "Skill" && !l.Bucket && l.Value == 24_000));
        Assert.IsTrue(lines.Any(l => l.Label == "Skill" && l.Bucket && l.Value == 20));
    }

    // Same for the hero's max HP: Warrior Mastery's 20 per level x 200 levels, and Iron Body's percent.
    [TestMethod]
    public void MaxHp_SourcesShowTheSkillPointsAndPercent()
    {
        var hero = StatAggregator.Aggregate(StatTables.Vitals(StatTables.Hero, 200));
        var lines = StatDisplay.Sources(hero, 0); // row 0 is HP
        Assert.IsTrue(lines.Any(l => l.Label == "Skill" && !l.Bucket && l.Value == 4_000));
        Assert.IsTrue(lines.Any(l => l.Label == "Skill" && l.Bucket && l.Value == 20));
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
        Assert.AreEqual(0, Progression.ApplyExp(chara, 34, Progression.MaxHp(1), Progression.MaxMp(1)));
        Assert.AreEqual(1, chara.Level);
        Assert.AreEqual(34, chara.Exp);
        Assert.AreEqual(1, Progression.ApplyExp(chara, 1, Progression.MaxHp(2), Progression.MaxMp(2))); // 35 = ExpNext(1)
        Assert.AreEqual(2, chara.Level);
        Assert.AreEqual(0, chara.Exp);
        Assert.AreEqual(8, chara.Str); // +4 auto
        Assert.AreEqual(5, chara.Dex); // +1 auto
        Assert.AreEqual(Progression.MaxHp(2), chara.Hp); // refilled
        Assert.AreEqual(Progression.MaxMp(2), chara.Mp);
    }

    [TestMethod]
    public void ClampPools_NewHeroStartsFull_OldPoolsKeepTheirValue()
    {
        var hero = new SaveStore().CreateHero(0, 0, "hero", 110);
        Progression.ClampPools(hero, 29_358, 9_546); // the first frame in the game
        Assert.AreEqual((29_358L, 9_546L), (hero.Hp, hero.Mp));
        var old = new CharaSlot { Name = "old", Hp = 1_000, Mp = 200 };
        Progression.ClampPools(old, 29_358, 9_546); // a loaded hero is never healed
        Assert.AreEqual((1_000L, 200L), (old.Hp, old.Mp));
        Progression.ClampPools(old, 500, 100); // a smaller max pulls the pools down
        Assert.AreEqual((500L, 100L), (old.Hp, old.Mp));
    }

    [TestMethod]
    public void ApplyExp_At200_DoesNotOverflowOrCrash()
    {
        var chara = new CharaSlot { Name = "hero", Level = Progression.StartLevel };
        Assert.AreEqual(4015, Progression.ExpNext(200));
        Assert.AreEqual(0, Progression.ApplyExp(chara, 4014, Progression.MaxHp(201), Progression.MaxMp(201)));
        Assert.AreEqual(1, Progression.ApplyExp(chara, 1, Progression.MaxHp(201), Progression.MaxMp(201)));
        Assert.AreEqual(201, chara.Level); // the bar keeps going past 200: no cap, no throw
        Assert.AreEqual(0, chara.Exp);
    }

    [TestMethod]
    public void ApplyExp_BigAward_LevelsTwice()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(2, Progression.ApplyExp(chara, 100, Progression.MaxHp(3), Progression.MaxMp(3))); // 35 + 55 = 90, 10 left
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
            50, 5));
        Assert.AreEqual(50, chara.Hp); // clamped to the max the caller gives
        Assert.AreEqual(1, chara.Use[ItemData.RedPotion]);
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            50, 5)); // full pool: nothing happens
        Assert.AreEqual(1, chara.Use[ItemData.RedPotion]);
    }

    [TestMethod]
    public void UsePotion_EmptyOrMissing_ReturnsFalse()
    {
        var chara = new CharaSlot { Name = "hero", Hp = 10 }; // Use null (pre-W10 save)
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            50, 5));
        chara.Use = new Dictionary<int, int> { { ItemData.RedPotion, 1 } };
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.BluePotion, 0, 100,
            50, 5)); // not in the bag
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0,
            50, 5));
        Assert.IsFalse(chara.Use.ContainsKey(ItemData.RedPotion)); // the last bottle leaves no zero behind
    }

    [TestMethod]
    public void UsePotion_Blue_HealsMp()
    {
        var chara = new CharaSlot { Name = "hero", Mp = 1 };
        chara.Use = new Dictionary<int, int> { { ItemData.BluePotion, 1 } };
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.BluePotion, 0, 100,
            50, 5));
        Assert.AreEqual(5, chara.Mp); // clamped to the max the caller gives
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
        Assert.AreEqual(1, Progression.ApplyExp(chara, 35, 160, 90_000)); // a mage's max MP is far over the base formula
        Assert.AreEqual(160, chara.Hp);
        Assert.AreEqual(90_000, chara.Mp); // the MP refill is the caller's max too
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
