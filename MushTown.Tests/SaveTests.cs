using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SaveTests
{
    [TestMethod]
    public void NewStore_ThreeSavesPreexist()
    {
        var store = new SaveStore();
        Assert.AreEqual(SaveStore.MaxSaves, store.Saves.Count);
        Assert.IsTrue(store.Saves.All(s => s != null && s.Slots.Count == SaveStore.MaxHeroes));
    }

    [TestMethod]
    public void CreateHero_SixtySlots_NameUniquePerFile()
    {
        var store = new SaveStore();
        store.CreateHero(0, 0, "hero");
        store.CreateHero(0, 1, "hero2");
        store.CreateHero(0, 59, "hero60");
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(0, 0, "other"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(0, 60, "other"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(0, -1, "neg"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(3, 0, "nosave"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "hero"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "bad name"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, ""));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "toolongheroname"));
        var chara = store.Saves[0]!.Slots[0]!;
        Assert.AreEqual("hero", chara.Name);
        Assert.AreEqual(1, chara.Level);
        Assert.AreEqual(0, chara.Exp);
        Assert.AreEqual(50, chara.Hp);
        Assert.AreEqual(5, chara.Mp);
        Assert.AreEqual(20, chara.Use![ItemData.RedPotion]); // starter kit
        Assert.AreEqual(10, chara.Use[ItemData.BluePotion]);
        Assert.IsNull(chara.X);
        Assert.IsNull(chara.Equips);
    }

    [TestMethod]
    public void Load_OldSlot_MissingVitalsDefault()
    {
        var slot = System.Text.Json.JsonSerializer.Deserialize<CharaSlot>("""{"Name":"hero"}""")!;
        Assert.AreEqual(1, slot.Level);
        Assert.AreEqual(0, slot.Exp);
        Assert.AreEqual(50, slot.Hp);
        Assert.AreEqual(5, slot.Mp);
        Assert.IsNull(slot.Use); // pre-W10 saves never got the starter kit
    }

    [TestMethod]
    public void DeleteHero_ClearsSlot()
    {
        var store = new SaveStore();
        store.CreateHero(0, 0, "hero");
        store.DeleteHero(0, 0);
        Assert.IsNull(store.Saves[0]!.Slots[0]);
        store.DeleteHero(0, 0); // deleting an empty slot is a no-op
        Assert.ThrowsException<InvalidDataException>(() => store.DeleteHero(0, 60));
        Assert.ThrowsException<InvalidDataException>(() => store.DeleteHero(0, -1));
        store.CreateHero(0, 0, "hero"); // the freed name is reusable
        Assert.AreEqual("hero", store.Saves[0]!.Slots[0]!.Name);
    }

    [TestMethod]
    public void DeleteHero_CompactsSlotsInOrder()
    {
        var store = new SaveStore();
        store.CreateHero(0, 0, "a");
        store.CreateHero(0, 1, "b");
        store.CreateHero(0, 2, "c");
        store.DeleteHero(0, 1); // b goes: c slides left, no gap
        Assert.AreEqual("a", store.Saves[0]!.Slots[0]!.Name);
        Assert.AreEqual("c", store.Saves[0]!.Slots[1]!.Name);
        Assert.IsNull(store.Saves[0]!.Slots[2]);
        store.DeleteHero(0, 0); // a goes: c slides to the front
        Assert.AreEqual("c", store.Saves[0]!.Slots[0]!.Name);
        Assert.IsNull(store.Saves[0]!.Slots[1]);
    }

    [TestMethod]
    public void Save_Load_RoundtripsPosition()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var chara = store.CreateHero(1, 0, "hero");
            chara.Map = 20000;
            chara.X = 300;
            chara.Y = 400;
            chara.Exp = 12;
            chara.Hp = 40;
            store.Save(path);
            Assert.IsFalse(File.Exists(path + ".tmp")); // atomic write leaves no tmp behind
            var back = SaveStore.Load(path);
            var slot = back.Saves[1]!.Slots[0]!;
            Assert.AreEqual(20000, slot.Map);
            Assert.AreEqual(300, slot.X);
            Assert.AreEqual(400, slot.Y);
            Assert.AreEqual(12, slot.Exp);
            Assert.AreEqual(40, slot.Hp);
            Assert.AreEqual(20, slot.Use![ItemData.RedPotion]); // int-keyed dict roundtrips
            Assert.AreEqual(10, slot.Use[ItemData.BluePotion]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_TrimsTrailingEmptySlots()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 1, "hero");
            store.Save(path);
            // slot 1 kept, slots 2-59 trimmed; load pads back to 60
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var slots = doc.RootElement.GetProperty("Saves")[0].GetProperty("Slots");
            Assert.AreEqual(2, slots.GetArrayLength()); // [null, hero]: the tail is gone
            Assert.AreEqual("hero", slots[1].GetProperty("Name").GetString());
            var back = SaveStore.Load(path);
            Assert.AreEqual(SaveStore.MaxHeroes, back.Saves[0]!.Slots.Count);
            Assert.AreEqual("hero", back.Saves[0]!.Slots[1]!.Name);
            Assert.IsNull(back.Saves[0]!.Slots[2]);
            Assert.IsNull(back.Saves[0]!.Slots[59]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_MissingFile_StartsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        var store = SaveStore.Load(path);
        Assert.AreEqual(SaveStore.MaxSaves, store.Saves.Count);
        Assert.IsTrue(store.Saves.All(s => s != null && s.Slots.All(h => h == null))); // saves exist, heroes don't
    }

    [TestMethod]
    public void Save_Load_RoundtripsQuests()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var chara = store.CreateHero(0, 0, "hero");
            chara.Quests.Add(90001);
            chara.QuestsDone.Add(90002);
            chara.QuestKills["90001:0"] = 2;
            store.Save(path);
            var slot = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.IsTrue(slot.Quests.Contains(90001));
            Assert.IsTrue(slot.QuestsDone.Contains(90002));
            Assert.AreEqual(2, slot.QuestKills["90001:0"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_Load_RoundtripsQuickSlots()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var chara = store.CreateHero(0, 0, "hero");
            chara.QuickSlots = new List<int> { 2000000, 0, 2000001 };
            store.Save(path);
            var slot = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.AreEqual(3, slot.QuickSlots!.Count);
            Assert.AreEqual(2000000, slot.QuickSlots[0]);
            Assert.AreEqual(0, slot.QuickSlots[1]);
            Assert.AreEqual(2000001, slot.QuickSlots[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_MissingDirectory_CreatesIt()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}");
        string path = Path.Combine(dir, "saves.json");
        try
        {
            var store = new SaveStore();
            store.Save(path); // first run: data/ does not exist yet
            Assert.IsNotNull(SaveStore.Load(path).Saves[0]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Load_BadJson_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{not json");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ShortFile_PadsWithSaves()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            // a file written when the caps were lower: short lists grow with fresh saves, nulls heal
            File.WriteAllText(path, """{"Saves":[null,{"Slots":[{"Name":"hero"}]}]}""");
            var store = SaveStore.Load(path);
            Assert.AreEqual(SaveStore.MaxSaves, store.Saves.Count);
            Assert.IsNotNull(store.Saves[0]);
            Assert.IsNotNull(store.Saves[2]);
            Assert.IsTrue(store.Saves[0]!.Slots.All(h => h == null));
            Assert.AreEqual(SaveStore.MaxHeroes, store.Saves[1]!.Slots.Count);
            Assert.AreEqual("hero", store.Saves[1]!.Slots[0]!.Name);
            Assert.IsNull(store.Saves[1]!.Slots[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_CorruptShape_ThrowsInvalidData()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Saves":null}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
            File.WriteAllText(path, """{"Saves":[null,null,null,null]}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
            File.WriteAllText(path, """{"Saves":[{"Slots":null}]}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
            // 61 slots breaks the 60 cap (4 nulls would just pad back up)
            string over = string.Join(",", Enumerable.Repeat("null", 61));
            File.WriteAllText(path, """{"Saves":[{"Slots":[""" + over + """]}]}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

}
