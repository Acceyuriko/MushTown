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
        Assert.AreEqual((5, 29), Progression.PanelRange(4, 4, 32));
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
}
