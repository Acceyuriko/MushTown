using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class MacroBookTests
{
    [TestMethod]
    public void Ensure_MakesFiveEmptyMacrosOfThreeSlots()
    {
        var chara = new CharaSlot();
        MacroBook.Ensure(chara);
        Assert.AreEqual(MacroBook.Count, chara.Macros!.Count);
        Assert.IsTrue(chara.Macros.All(m => m.Count == MacroBook.Slots && m.All(id => id == 0)));
        for (int i = 0; i < MacroBook.Count; i++) Assert.IsTrue(MacroBook.IsEmpty(chara, i));
    }

    [TestMethod]
    public void Place_FromAnotherSlot_MovesTheSkill()
    {
        var chara = new CharaSlot();
        MacroBook.Place(chara, 0, 0, SkillData.SlashBlast); // from the skill window: a plain put
        MacroBook.Place(chara, 2, 1, SkillData.SlashBlast, (0, 0)); // from the slot: the origin empties
        Assert.AreEqual(0, chara.Macros![0][0]);
        Assert.AreEqual(SkillData.SlashBlast, chara.Macros[2][1]);
    }

    [TestMethod]
    public void Place_BackOnTheSameSlot_KeepsTheSkill()
    {
        var chara = new CharaSlot();
        MacroBook.Place(chara, 1, 1, SkillData.IronBody);
        MacroBook.Place(chara, 1, 1, SkillData.IronBody, (1, 1));
        Assert.AreEqual(SkillData.IronBody, chara.Macros![1][1]);
    }

    [TestMethod]
    public void Place_FromTheSkillWindow_LeavesOtherSlotsAlone()
    {
        var chara = new CharaSlot();
        MacroBook.Place(chara, 0, 0, SkillData.SlashBlast);
        MacroBook.Place(chara, 0, 1, SkillData.SlashBlast); // the skill window copy: both stay
        Assert.AreEqual(SkillData.SlashBlast, chara.Macros![0][0]);
        Assert.AreEqual(SkillData.SlashBlast, chara.Macros[0][1]);
    }

    [TestMethod]
    public void IsEmpty_OldSaveWithoutMacros_IsEmpty()
    {
        Assert.IsTrue(MacroBook.IsEmpty(new CharaSlot(), 2));
    }

    [TestMethod]
    public void IsEmpty_AMacroWithOneSkillIsNotEmpty()
    {
        var chara = new CharaSlot();
        MacroBook.Ensure(chara);
        chara.Macros![1][2] = SkillData.SlashBlast;
        Assert.IsFalse(MacroBook.IsEmpty(chara, 1));
        Assert.IsTrue(MacroBook.IsEmpty(chara, 0));
    }

    // The macros and the keys bound to them come back after a restart.
    [TestMethod]
    public void Save_MacrosAndTheirKeyBindingsComeBack()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            MacroBook.Ensure(hero);
            hero.Macros![0] = new List<int> { SkillData.SlashBlast, SkillData.IronBody, SkillData.LeapAttack };
            hero.Macros[3][1] = SkillData.WarLeap;
            hero.KeyPresets = new() { new() { ["D1"] = "Macro:0", ["G"] = "Macro:3" }, new(), new() };
            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            CollectionAssert.AreEqual(new[] { SkillData.SlashBlast, SkillData.IronBody, SkillData.LeapAttack }, loaded.Macros![0]);
            Assert.AreEqual(SkillData.WarLeap, loaded.Macros[3][1]);
            Assert.AreEqual("Macro:0", loaded.KeyPresets![0]["D1"]);
            Assert.AreEqual("Macro:3", loaded.KeyPresets[0]["G"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_OldSaveWithoutMacros_LoadsAndStartsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 0, "hero", 110);
            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.IsNull(loaded.Macros);
            Assert.IsTrue(MacroBook.IsEmpty(loaded, 0));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
