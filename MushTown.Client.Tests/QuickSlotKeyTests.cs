using Microsoft.Xna.Framework.Input;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The 32 key cells of the bottom strip: the default keys, the per-preset save, the
// content that follows the key.
[TestClass]
public class QuickSlotKeyTests
{
    [TestMethod]
    public void DefaultKeys_TwoRowsOfSixteen()
    {
        var keys = KeyDefaults.QuickKeys();
        Assert.AreEqual(32, keys.Count);
        Assert.AreEqual("Q W E R D1 D2 D3 D4 D5 D6 D7 D8 D9 D0 U P", string.Join(' ', keys.Take(16)));
        Assert.AreEqual("A S D F LeftControl LeftAlt Z X C V I J K L M N", string.Join(' ', keys.Skip(16)));
        Assert.AreEqual(32, keys.Distinct().Count());
        foreach (string key in keys) Assert.IsTrue(InputController.TryParseKeys(key, out _), key); // every cell key can be bound
    }

    [TestMethod]
    public void Ensure_OldSave_GetsDefaultQuickKeys_KeepsItsPresets()
    {
        var table = new Dictionary<string, string> { ["F"] = "Stat" };
        var chara = new CharaSlot { KeyPresets = new() { table, new(), new() } };
        KeyBindings.Ensure(chara);
        Assert.AreEqual(3, chara.QuickKeys!.Count);
        foreach (var list in chara.QuickKeys) CollectionAssert.AreEqual(KeyDefaults.QuickKeys(), list);
        Assert.AreSame(table, chara.KeyPresets[0]); // the presets the old save had stay as they were
        chara.QuickKeys[1][0] = "Z"; // one preset's list is its own
        CollectionAssert.AreEqual(KeyDefaults.QuickKeys(), chara.QuickKeys[0]);
    }

    [TestMethod]
    public void QuickKeys_SurviveSaveAndLoad_AndAnOldFileGetsTheDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-qk-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            KeyBindings.Ensure(hero);
            hero.QuickKeys![2][5] = "Z";
            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.AreEqual(3, loaded.QuickKeys!.Count);
            Assert.AreEqual("Z", loaded.QuickKeys[2][5]);
            CollectionAssert.AreEqual(KeyDefaults.QuickKeys(), loaded.QuickKeys[0]);

            // a file written before the field: no QuickKeys in it
            string json = File.ReadAllText(path);
            int start = json.IndexOf("\"QuickKeys\":", StringComparison.Ordinal);
            int end = json.IndexOf("],\"ActivePreset\"", start, StringComparison.Ordinal);
            Assert.IsTrue(start > 0 && end > start);
            File.WriteAllText(path, json.Remove(start, end + 2 - start));
            var old = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.IsNull(old.QuickKeys);
            KeyBindings.Ensure(old);
            CollectionAssert.AreEqual(KeyDefaults.QuickKeys(), old.QuickKeys![1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The content follows the key: the default table's targets show in the cell of their key.
    [TestMethod]
    public void DefaultTable_ContentLandsInTheCellOfItsKey()
    {
        var c = new InputController();
        c.Install(KeyDefaults.Table());
        var keys = KeyDefaults.QuickKeys();
        var expected = new (string Key, int Cell, HotkeyAction Action)[]
        {
            ("Q", 0, HotkeyAction.Quest), ("W", 1, HotkeyAction.WorldMap), ("E", 2, HotkeyAction.Equip),
            ("U", 14, HotkeyAction.Guide), ("P", 15, HotkeyAction.Party),
            ("S", 17, HotkeyAction.Stat), ("LeftControl", 20, HotkeyAction.Attack), ("LeftAlt", 21, HotkeyAction.Jump),
            ("Z", 22, HotkeyAction.Pickup), ("I", 26, HotkeyAction.Inventory), ("K", 28, HotkeyAction.Skill), ("L", 29, HotkeyAction.Menu),
        };
        foreach (var (key, cell, action) in expected)
        {
            Assert.AreEqual(key, keys[cell]);
            Assert.AreEqual(BindTarget.Ui(action), c.TargetOf(Enum.Parse<Keys>(key)), key);
        }
        Assert.IsNull(c.TargetOf(Keys.C)); // C is a plain key now: nothing bound, only the cap
    }

    // A skill dropped on a second-row cell binds that cell's key, and the key fires it.
    [TestMethod]
    public void SkillOnASecondRowCell_FiresFromItsKey()
    {
        var table = KeyDefaults.Table();
        string key = KeyDefaults.QuickKeys()[16]; // the first cell of row 2
        KeyBindings.ApplySkills(table, new() { [1001005] = key });
        var c = new InputController();
        c.Install(table);
        c.Poll(new KeyboardState(Enum.Parse<Keys>(key)), default, false, false, 0);
        Assert.IsTrue(c.Fire(new BindTarget(BindKind.Skill, 1001005)));
    }

    // The number keys are plain keys: an item binds to them like to any other key.
    [TestMethod]
    public void NumberKeys_TakeAnItem()
    {
        var table = KeyDefaults.Table();
        KeyBindings.ApplyItems(table, new() { [2000000] = "D5" });
        Assert.AreEqual("Item:2000000", table["D5"]);
    }
}
