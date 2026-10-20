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
    public void PanelRange_BeginnerIs5to29()
    {
        Assert.AreEqual((5, 29), Progression.PanelRange(4, 4, Progression.WeaponAtk));
    }

    [TestMethod]
    public void ApplyExp_LevelsUpWithAutoStats()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(0, Progression.ApplyExp(chara, 34));
        Assert.AreEqual(1, chara.Level);
        Assert.AreEqual(34, chara.Exp);
        Assert.AreEqual(1, Progression.ApplyExp(chara, 1)); // 35 = ExpNext(1)
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
        Assert.AreEqual(2, Progression.ApplyExp(chara, 100)); // 35 + 55 = 90, 10 left
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
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0));
        Assert.AreEqual(50, chara.Hp); // clamped to MaxHp(1)
        Assert.AreEqual(1, chara.Use[ItemData.RedPotion]);
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0)); // full pool: nothing happens
        Assert.AreEqual(1, chara.Use[ItemData.RedPotion]);
    }

    [TestMethod]
    public void UsePotion_EmptyOrMissing_ReturnsFalse()
    {
        var chara = new CharaSlot { Name = "hero", Hp = 10 }; // Use null (pre-W10 save)
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0));
        chara.Use = new Dictionary<int, int> { { ItemData.RedPotion, 1 } };
        Assert.IsFalse(Progression.UsePotion(chara, ItemData.BluePotion, 0, 100)); // not in the bag
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0));
        Assert.IsFalse(chara.Use.ContainsKey(ItemData.RedPotion)); // the last bottle leaves no zero behind
    }

    [TestMethod]
    public void UsePotion_Blue_HealsMp()
    {
        var chara = new CharaSlot { Name = "hero", Mp = 1 };
        chara.Use = new Dictionary<int, int> { { ItemData.BluePotion, 1 } };
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.BluePotion, 0, 100));
        Assert.AreEqual(5, chara.Mp); // clamped to MaxMp(1)
    }

    // Each level banks 3 SP for the skill window to spend.
    [TestMethod]
    public void ApplyExp_BanksSkillPoints()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(2, Progression.ApplyExp(chara, 100));
        Assert.AreEqual(6, chara.Sp);
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

    // Learning costs one SP and needs the level; maxed or poor stays put.
    [TestMethod]
    public void LearnSkill_SpendsOnePoint()
    {
        var skill = new SkillData { SkillId = SkillData.SlashBlast, ReqLevel = 1, MaxLevel = 2 };
        var chara = new CharaSlot { Name = "hero", Level = 5, Sp = 2 };
        Assert.IsTrue(Progression.LearnSkill(chara, skill));
        Assert.AreEqual(1, chara.SkillLv[SkillData.SlashBlast]);
        Assert.AreEqual(1, chara.Sp);
        Assert.IsTrue(Progression.LearnSkill(chara, skill));
        Assert.IsFalse(Progression.LearnSkill(chara, skill)); // maxed
        Assert.AreEqual(0, chara.Sp);
        Assert.IsFalse(Progression.LearnSkill(chara, new SkillData { SkillId = 1, ReqLevel = 9, MaxLevel = 1 })); // too young
        Assert.IsFalse(Progression.LearnSkill(chara, new SkillData { SkillId = 2, ReqLevel = 1, MaxLevel = 1 })); // too poor
    }

    // The MAX button: the skill goes as high as the SP reach.
    [TestMethod]
    public void LearnSkillMax_SpendsAllPoints()
    {
        var skill = new SkillData { SkillId = SkillData.SlashBlast, ReqLevel = 1, MaxLevel = 10 };
        var chara = new CharaSlot { Name = "hero", Level = 5, Sp = 5 };
        Assert.AreEqual(5, Progression.LearnSkillMax(chara, skill));
        Assert.AreEqual(5, chara.SkillLv[SkillData.SlashBlast]);
        Assert.AreEqual(0, chara.Sp);
    }

    // MAX stops at the skill's max level and keeps the rest of the SP.
    [TestMethod]
    public void LearnSkillMax_StopsAtMaxLevel()
    {
        var skill = new SkillData { SkillId = SkillData.SlashBlast, ReqLevel = 1, MaxLevel = 3 };
        var chara = new CharaSlot { Name = "hero", Level = 5, Sp = 20 };
        Assert.AreEqual(3, Progression.LearnSkillMax(chara, skill));
        Assert.AreEqual(3, chara.SkillLv[SkillData.SlashBlast]);
        Assert.AreEqual(17, chara.Sp);
    }

    // MAX with no SP (or too young) buys nothing.
    [TestMethod]
    public void LearnSkillMax_NoPoints_BuysNothing()
    {
        var skill = new SkillData { SkillId = SkillData.SlashBlast, ReqLevel = 1, MaxLevel = 10 };
        var chara = new CharaSlot { Name = "hero", Level = 5, Sp = 0 };
        Assert.AreEqual(0, Progression.LearnSkillMax(chara, skill));
        Assert.IsFalse(chara.SkillLv.ContainsKey(SkillData.SlashBlast));
        Assert.AreEqual(0, chara.Sp);
        var young = new CharaSlot { Name = "hero", Level = 1, Sp = 9 };
        Assert.AreEqual(0, Progression.LearnSkillMax(young, new SkillData { SkillId = SkillData.Brandish, ReqLevel = 9, MaxLevel = 10 }));
        Assert.AreEqual(9, young.Sp);
    }

    // Iron Body's +HP% stretches the pool (Endure stacks on top).
    [TestMethod]
    public void BuffedMaxHp_StretchesPool()
    {
        Assert.AreEqual(50, Progression.BuffedMaxHp(1, 0));
        Assert.AreEqual(65, Progression.BuffedMaxHp(1, 30));
    }

    // Level-up refills and potions both cap at the buffed max, not base.
    [TestMethod]
    public void ApplyExp_RefillsToBuffedMax()
    {
        var chara = new CharaSlot { Name = "hero" };
        Assert.AreEqual(1, Progression.ApplyExp(chara, 35, 100));
        Assert.AreEqual(160, chara.Hp); // BuffedMaxHp(2, 100), not MaxHp(2) = 80
    }

    [TestMethod]
    public void UsePotion_HealsToBuffedMax()
    {
        var chara = new CharaSlot { Name = "hero", Hp = 10 };
        chara.Use = new Dictionary<int, int> { { ItemData.RedPotion, 1 } };
        Assert.IsTrue(Progression.UsePotion(chara, ItemData.RedPotion, 50, 0, 100));
        Assert.AreEqual(60, chara.Hp); // over the base 50: the buff holds it
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

    // Takeoff returns the piece to the bag; unworn slots do nothing.
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
}
