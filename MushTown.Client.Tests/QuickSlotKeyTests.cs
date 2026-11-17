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

    // Cell Q takes X: X is another cell's key, so the two swap. The cell then shows what X binds
    // and X fires it (the content follows the key).
    [TestMethod]
    public void SetQuickKey_AKeyAnotherCellHas_SwapsTheTwoCells()
    {
        var keys = KeyDefaults.QuickKeys();
        var next = KeyBindings.SetQuickKey(keys, 0, "X"); // cell 0 is Q, X is cell 23
        Assert.AreEqual("X", next[0]);
        Assert.AreEqual("Q", next[23]);
        Assert.AreEqual(32, next.Distinct().Count());
        Assert.AreEqual("Q", keys[0]); // the old list is left alone: the strip parses a list once
        var table = KeyDefaults.Table();
        KeyBindings.ApplySkills(table, new() { [1001005] = "X" });
        var c = new InputController();
        c.Install(table);
        Assert.AreEqual(new BindTarget(BindKind.Skill, 1001005), c.TargetOf(Enum.Parse<Keys>(next[0]))); // the cell shows X's skill
        c.Poll(new KeyboardState(Keys.X), default, false, false, 0);
        Assert.IsTrue(c.Fire(new BindTarget(BindKind.Skill, 1001005))); // and pressing X casts it
    }

    [TestMethod]
    public void SetQuickKey_Q_To_W_SwapsTheTwo()
    {
        var next = KeyBindings.SetQuickKey(KeyDefaults.QuickKeys(), 0, "W");
        Assert.AreEqual("W", next[0]);
        Assert.AreEqual("Q", next[1]);
    }

    [TestMethod]
    public void SetQuickKey_AFreeKey_JustReplaces()
    {
        var next = KeyBindings.SetQuickKey(KeyDefaults.QuickKeys(), 0, "B");
        Assert.AreEqual("B", next[0]);
        Assert.IsFalse(next.Contains("Q")); // the old key has no cell now; its binding stays in the table
        Assert.AreEqual(32, next.Count);
    }

    [TestMethod]
    public void SetQuickKey_SameKey_ChangesNothing()
    {
        var keys = KeyDefaults.QuickKeys();
        Assert.AreSame(keys, KeyBindings.SetQuickKey(keys, 0, "Q"));
    }

    [TestMethod]
    public void SetQuickKey_EscEnterTabAndArrows_AreRefused()
    {
        var keys = KeyDefaults.QuickKeys();
        foreach (string name in new[] { "Escape", "Enter", "Tab", "Up", "Down", "Left", "Right", "", "Nonsense" })
            Assert.AreSame(keys, KeyBindings.SetQuickKey(keys, 0, name), name);
    }

    // Edit one preset's list, save, load: the other two presets stay as they were.
    [TestMethod]
    public void QuickKeys_EditOnePreset_OthersStayAfterSaveAndLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-qk2-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SaveStore();
            var hero = store.CreateHero(0, 0, "hero", 110);
            KeyBindings.Ensure(hero);
            hero.QuickKeys![1] = KeyBindings.SetQuickKey(hero.QuickKeys[1], 0, "B");
            store.Save(path);
            var loaded = SaveStore.Load(path).Saves[0]!.Slots[0]!;
            Assert.AreEqual("B", loaded.QuickKeys![1][0]);
            Assert.AreEqual("Q", loaded.QuickKeys[0][0]);
            Assert.AreEqual("Q", loaded.QuickKeys[2][0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The Default button: the default list again, a fresh list each time (one preset never shares another's).
    [TestMethod]
    public void Default_IsTheSameListEachTimeAndNeverShared()
    {
        var a = KeyDefaults.QuickKeys();
        a[0] = "B";
        CollectionAssert.AreNotEqual(a, KeyDefaults.QuickKeys());
        Assert.AreEqual("Q", KeyDefaults.QuickKeys()[0]);
        Assert.AreEqual(SaveStore.QuickKeyCount, KeyDefaults.QuickKeys().Count);
    }

    // Load: three lists of QuickKeyCount, every name a real key. A bad file fails loud.
    private static string SaveWith(Action<CharaSlot> edit)
    {
        string path = Path.Combine(Path.GetTempPath(), $"saves-qk3-{Guid.NewGuid():N}.json");
        var store = new SaveStore();
        var hero = store.CreateHero(0, 0, "hero", 110);
        KeyBindings.Ensure(hero);
        edit(hero);
        store.Save(path);
        return path;
    }

    private static bool KeyOk(string name) => InputController.IsSingleKey(name);

    [TestMethod]
    public void Load_QuickKeysOfTheWrongShape_Throws()
    {
        foreach (var edit in new Action<CharaSlot>[]
        {
            h => h.QuickKeys!.RemoveAt(2),                        // two presets
            h => h.QuickKeys![1].RemoveAt(31),                    // 31 keys
            h => h.QuickKeys![0].Add("B"),                        // 33 keys
        })
        {
            string path = SaveWith(edit);
            try
            {
                var ex = Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path, KeyOk));
                StringAssert.Contains(ex.Message, "QuickKeys");
            }
            finally { File.Delete(path); }
        }
    }

    [TestMethod]
    public void Load_QuickKeysWithABadKeyName_Throws()
    {
        foreach (string bad in new[] { "Nonsense", "", "Escape", "Q+W", "+Q" })
        {
            string path = SaveWith(h => h.QuickKeys![1][3] = bad);
            try
            {
                var ex = Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path, KeyOk));
                StringAssert.Contains(ex.Message, "bad key name");
            }
            finally { File.Delete(path); }
        }
    }

    [TestMethod]
    public void Load_QuickKeysWithANullKeyName_Throws()
    {
        string path = SaveWith(h => h.QuickKeys![2][7] = null!);
        try
        {
            var ex = Assert.ThrowsException<InvalidDataException>(() => SaveStore.Load(path, KeyOk));
            StringAssert.Contains(ex.Message, "QuickKeys");
            StringAssert.Contains(ex.Message, "hero");
        }
        finally { File.Delete(path); }
    }

    // A key with no cap art is a valid key: it saves, loads, and the strip draws its name (QuickCell).
    [TestMethod]
    public void QuickKeys_KeysWithoutCapArt_SaveAndLoad()
    {
        string[] noCap = { "Back", "CapsLock", "RightShift", "NumPad0", "LeftWindows" };
        string path = SaveWith(h => { for (int i = 0; i < noCap.Length; i++) h.QuickKeys![0][i] = noCap[i]; });
        try
        {
            var loaded = SaveStore.Load(path, KeyOk).Saves[0]!.Slots[0]!;
            CollectionAssert.AreEqual(noCap, loaded.QuickKeys![0].Take(noCap.Length).ToArray());
        }
        finally { File.Delete(path); }
    }

    // Every key a cell can take has cap art or none: never a name that points at a missing png.
    // (QuickCell draws the key name when the art is ""; that draw needs a graphics device, the shot checks it.)
    [TestMethod]
    public void CapArt_EveryBindableKey_HasAnExistingPngOrNone()
    {
        string? ui = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "assets", "ui", "keys.json"))) { ui = Path.Combine(d.FullName, "assets", "ui"); break; }
        if (ui == null) { Assert.Inconclusive("no exported assets above the test output: this check needs the repo"); return; }
        int none = 0;
        foreach (var key in Enum.GetValues<Keys>())
        {
            if (!InputController.TryParseKeys(key.ToString(), out _)) continue;
            string cap = KeyConfigWindow.CapArt(key.ToString());
            if (cap == "") { none++; continue; }
            Assert.IsTrue(File.Exists(Path.Combine(ui, "..", cap)), $"{key}: {cap}");
        }
        Assert.IsTrue(none > 0, "Backspace and friends have no cap art");
    }
}
