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
    public void CreateHero_StartsWithTenBillionMesos()
    {
        var store = new SaveStore();
        var hero = store.CreateHero(0, 0, "hero", 110);
        Assert.AreEqual(10_000_000_000L, hero.Mesos);
    }

    [TestMethod]
    public void Save_LoadsTenBillionMesos()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            hero.Mesos = 10_000_000_000L;
            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.AreEqual(10_000_000_000L, loaded.Mesos);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_OldIntMesos_BecomesLong()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            hero.Mesos = 12345;
            store.Save(path);
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"Mesos\":12345", "\"Mesos\":2147483647"));
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.AreEqual(2147483647L, loaded.Mesos);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CreateHero_SixtySlots_NameUniquePerFile()
    {
        var store = new SaveStore();
        store.CreateHero(0, 0, "hero", 110);
        store.CreateHero(0, 1, "hero2", 110);
        store.CreateHero(0, 59, "hero60", 110);
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(0, 0, "other", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(0, 60, "other", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(0, -1, "neg", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(3, 0, "nosave", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "hero", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "bad name", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "", 110));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateHero(1, 0, "toolongheroname", 110));
        var chara = store.Saves[0]!.Slots[0]!;
        Assert.AreEqual("hero", chara.Name);
        Assert.AreEqual(200, chara.Level);
        Assert.AreEqual(0, chara.Exp);
        Assert.AreEqual(Progression.FullPool, chara.Hp);
        Assert.AreEqual(Progression.FullPool, chara.Mp);
        Assert.AreEqual(20, chara.Use![ItemData.RedPotion]); // starter kit
        Assert.AreEqual(10, chara.Use[ItemData.BluePotion]);
        Assert.IsNull(chara.X);
        CollectionAssert.AreEquivalent(ItemData.StarterEquips, chara.Equips);
    }

    [TestMethod]
    public void CreateHero_GivesDefaultCoupons()
    {
        var store = new SaveStore();
        var chara = store.CreateHero(0, 0, "hero", 110);

        Assert.AreEqual(0, chara.Skin);
        Assert.AreEqual(ItemData.DefaultHair, chara.Hair);
        Assert.AreEqual(ItemData.DefaultFace, chara.Face);
        CollectionAssert.AreEquivalent(
            new[] { ItemData.HairCoupon, ItemData.FaceCoupon }, chara.CashWorn);
        Assert.AreEqual(1, chara.DecBag![ItemData.HairCouponAlt]);
        Assert.AreEqual(1, chara.DecBag[ItemData.FaceCouponAlt]);
        CollectionAssert.AreEquivalent(
            new[] { ItemData.HairCouponAlt, ItemData.FaceCouponAlt }, chara.DecOrder!.Take(2).ToArray());
    }

    [TestMethod]
    public void Load_OldGenderField_Ignored()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-old-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 0, "hero", 110);
            store.Version = SaveStore.FormatVersion;
            string json = JsonSerializer.Serialize(store).Replace("\"Skin\":0,", "\"Female\":true,");
            StringAssert.Contains(json, "\"Female\":true");
            File.WriteAllText(path, json);

            var loaded = SaveStore.Load(path);
            Assert.AreEqual(0, loaded.Saves[0]!.Slots[0]!.Skin);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_Load_RoundtripsEveryEquipSlot()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-equips-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            var ids = new List<int>(ItemData.StarterEquips);
            ids.AddRange(DemoSlots.All.Select(x => x.Id));
            ids.Add(ItemData.IslandRing);
            hero.Equips = ids;
            store.Save(path);
            CollectionAssert.AreEqual(ids,
                SaveStore.Load(path).Saves[0]!.Slots[0]!.Equips);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_Load_RoundtripsSkin()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-skin-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 0, "hero", 110).Skin = 4;
            store.Save(path);
            Assert.AreEqual(4, SaveStore.Load(path).Saves[0]!.Slots[0]!.Skin);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void SkinTable_HasSixOfficialIds()
    {
        CollectionAssert.AreEqual(
            new[] { 12000, 12001, 12002, 12003, 12011, 12012 },
            Enumerable.Range(0, CharaSlot.SkinCount).Select(CharaSlot.HeadId).ToArray());
        CollectionAssert.AreEqual(
            new[] { 2000, 2001, 2002, 2003, 2011, 2012 },
            Enumerable.Range(0, CharaSlot.SkinCount).Select(CharaSlot.BodyId).ToArray());
    }

    [TestMethod]
    public void Save_Load_RoundtripsJob()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 0, "hero", 120);
            store.Save(path);
            Assert.AreEqual(120, SaveStore.Load(path).Saves[0]!.Slots[0]!.Job);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_OldSlot_MissingVitalsDefault()
    {
        var slot = System.Text.Json.JsonSerializer.Deserialize<CharaSlot>("""{"Name":"hero"}""")!;
        Assert.AreEqual(110, slot.Job);
        Assert.AreEqual(1, slot.Level);
        Assert.AreEqual(0, slot.Exp);
        Assert.AreEqual(Progression.MaxHp(1), slot.Hp);
        Assert.AreEqual(Progression.MaxMp(1), slot.Mp);
        Assert.IsNull(slot.Use); // pre-W10 saves never got the starter kit
    }

    [TestMethod]
    public void Load_OldSaveWithoutOrderLists_FailsLoud()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.Saves[0]!.Slots[0] = new CharaSlot { Name = "old" };
            store.Save(path);
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ShortOrderLists_FailsLoud()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.Saves[0]!.Slots[0] = new CharaSlot
            {
                Name = "short",
                UseOrder = new List<int>(),
                EquipOrder = new List<int>(),
                CashOrder = new List<int>(),
                DecOrder = new List<int>(),
            };
            store.Save(path);
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void DeleteHero_ClearsSlot()
    {
        var store = new SaveStore();
        store.CreateHero(0, 0, "hero", 110);
        store.DeleteHero(0, 0);
        Assert.IsNull(store.Saves[0]!.Slots[0]);
        store.DeleteHero(0, 0); // deleting an empty slot is a no-op
        Assert.ThrowsException<InvalidDataException>(() => store.DeleteHero(0, 60));
        Assert.ThrowsException<InvalidDataException>(() => store.DeleteHero(0, -1));
        store.CreateHero(0, 0, "hero", 110); // the freed name is reusable
        Assert.AreEqual("hero", store.Saves[0]!.Slots[0]!.Name);
    }

    [TestMethod]
    public void DeleteHero_CompactsSlotsInOrder()
    {
        var store = new SaveStore();
        store.CreateHero(0, 0, "a", 110);
        store.CreateHero(0, 1, "b", 110);
        store.CreateHero(0, 2, "c", 110);
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
            var chara = store.CreateHero(1, 0, "hero", 110);
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
            store.CreateHero(0, 1, "hero", 110);
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
            var chara = store.CreateHero(0, 0, "hero", 110);
            chara.Quests.Add(90001);
            chara.QuestsDone.Add(90002);
            chara.QuestKills["90001:0"] = 2;
            chara.QuestHelper.Add(90001);
            store.Save(path);
            var slot = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.IsTrue(slot.Quests.Contains(90001));
            Assert.IsTrue(slot.QuestsDone.Contains(90002));
            Assert.AreEqual(2, slot.QuestKills["90001:0"]);
            Assert.IsTrue(slot.QuestHelper.Contains(90001));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_Load_RoundtripsKeyPresets()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var chara = store.CreateHero(0, 0, "hero", 110);
            chara.KeyPresets = new List<Dictionary<string, string>>
            {
                new() { ["I"] = "Inventory", ["D1"] = "Skill:1001005", ["D2"] = "Item:2000000" },
                new() { ["S"] = "Stat" },
                new(),
            };
            chara.ActivePreset = 1;
            store.Save(path);
            var slot = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            var presets = slot.KeyPresets!;
            Assert.AreEqual(1, slot.ActivePreset);
            Assert.AreEqual("Inventory", presets[0]["I"]);
            Assert.AreEqual("Skill:1001005", presets[0]["D1"]);
            Assert.AreEqual("Item:2000000", presets[0]["D2"]);
            Assert.AreEqual("Stat", presets[1]["S"]);
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
    public void Save_Load_RoundtripsCashFashion()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-cashfashion-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            hero.CashWorn = new List<int> { 1701000, 1040001 };

            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;

            CollectionAssert.AreEqual(new[] { 1701000, 1040001 }, loaded.CashWorn);
            CollectionAssert.AreEqual(hero.Equips, loaded.Equips);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ShortFile_PadsWithSaves()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            // a short outer array: Load pads saves and slots back to the caps
            var source = new SaveStore();
            var hero = source.CreateHero(0, 0, "hero", 110);
            File.WriteAllText(path, "{\"Version\":" + SaveStore.FormatVersion + ",\"Saves\":[null,{\"Slots\":[" + JsonSerializer.Serialize(hero) + "]}]}");
            var store = SaveStore.Load(path);
            Assert.AreEqual(SaveStore.MaxSaves, store.Saves.Count);
            Assert.IsNotNull(store.Saves[0]);
            Assert.IsNotNull(store.Saves[2]);
            Assert.IsTrue(store.Saves[0]!.Slots.All(h => h == null));
            Assert.AreEqual(SaveStore.MaxHeroes, store.Saves[1]!.Slots.Count);
            var loaded = store.Saves[1]!.Slots[0]!;
            Assert.AreEqual("hero", loaded.Name);
            Assert.IsNotNull(loaded.CashWorn);
            Assert.IsNull(store.Saves[1]!.Slots[1]);
            Assert.AreEqual(Inventory.Capacity, loaded.UseOrder!.Count);
            Assert.AreEqual(Inventory.Capacity, loaded.EquipOrder!.Count);
            Assert.AreEqual(Inventory.Capacity, loaded.CashOrder!.Count);
            Assert.AreEqual(Inventory.Capacity, loaded.DecOrder!.Count);
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
            File.WriteAllText(path, """{"Version":1,"Saves":null}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
            File.WriteAllText(path, """{"Version":1,"Saves":[null,null,null,null]}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
            File.WriteAllText(path, """{"Version":1,"Saves":[{"Slots":null}]}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
            // 61 slots breaks the 60 cap (4 nulls would just pad back up)
            string over = string.Join(",", Enumerable.Repeat("null", 61));
            File.WriteAllText(path, """{"Version":1,"Saves":[{"Slots":[""" + over + """]}]}""");
            Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CreateHero_Level200_AllApOnTheMainStat()
    {
        var store = new SaveStore();
        var hero = store.CreateHero(0, 0, "hero", 110); // Hero: STR
        Assert.AreEqual(200, hero.Level);
        Assert.AreEqual(0, hero.Exp);
        Assert.AreEqual(1014, Progression.StartAp);
        Assert.AreEqual((1018L, 4L, 4L, 4L), (hero.Str, hero.Dex, hero.Int, hero.Luk));
        var mage = store.CreateHero(0, 1, "mage", 210); // Fire/Poison: INT
        Assert.AreEqual((4L, 4L, 1018L, 4L), (mage.Str, mage.Dex, mage.Int, mage.Luk));
        Assert.AreEqual((210, 450001000), (mage.Job, mage.Map)); // Fire/Poison, born in Nameless Town
        Assert.IsNull(mage.X); // never played: the map's own spawn point
        Assert.AreEqual(Progression.FullPool, hero.Hp);
        Assert.AreEqual(Progression.FullPool, hero.Mp);
        Assert.AreEqual(SaveStore.NewHeroMesos, hero.Mesos);
    }

    [TestMethod]
    public void Save_Load_RoundtripsLevelApAndMesos()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 0, "hero", 110);
            store.CreateHero(0, 1, "mage", 210);
            store.Save(path);
            var heroes = SaveStore.Load(path).Saves[0]!.Slots;
            Assert.AreEqual((200, 0L, 1018L, 4L, 4L, 4L, SaveStore.NewHeroMesos),
                (heroes[0]!.Level, heroes[0]!.Exp, heroes[0]!.Str, heroes[0]!.Dex, heroes[0]!.Int, heroes[0]!.Luk, heroes[0]!.Mesos));
            Assert.AreEqual((200, 1018L), (heroes[1]!.Level, heroes[1]!.Int));
            Assert.AreEqual((210, 450001000, SaveStore.NewHeroMesos), (heroes[1]!.Job, heroes[1]!.Map, heroes[1]!.Mesos));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_OtherFormatVersion_IsVoid_NoThrow()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            store.CreateHero(0, 0, "old", 110);
            string json = JsonSerializer.Serialize(store);
            // number 0 is any file from before the version field; another number is void too
            File.WriteAllText(path, json);
            Assert.IsTrue(SaveStore.Load(path).Saves.All(s => s!.Slots.All(h => h == null)));
            File.WriteAllText(path, json.Replace("\"Version\":0", "\"Version\":" + (SaveStore.FormatVersion + 1)));
            Assert.IsTrue(SaveStore.Load(path).Saves.All(s => s!.Slots.All(h => h == null)));
            File.WriteAllText(path, json.Replace("\"Version\":0,", "")); // no number at all
            var loaded = SaveStore.Load(path);
            Assert.AreEqual(SaveStore.MaxSaves, loaded.Saves.Count);
            Assert.IsTrue(loaded.Saves.All(s => s!.Slots.All(h => h == null)));
            loaded.CreateHero(0, 0, "old", 110); // the slot and the name are free again
        }
        finally
        {
            File.Delete(path);
        }
    }
}
