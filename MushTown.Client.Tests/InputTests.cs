using Microsoft.Xna.Framework.Input;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The one input entry: key edges, aliases and target parsing.
[TestClass]
public class InputTests
{
    private static InputController Installed(params (string Key, string Target)[] entries)
    {
        var table = new Dictionary<string, string>();
        foreach (var (key, target) in entries) table[key] = target;
        var controller = new InputController();
        controller.Install(table);
        return controller;
    }

    [TestMethod]
    public void Poll_FreshOnThePressFrameOnly()
    {
        var c = Installed(("I", "Inventory"));
        var target = BindTarget.Ui(HotkeyAction.Inventory);
        c.Poll(new KeyboardState(Keys.I), default, false, false, 0);
        Assert.IsTrue(c.Fresh(target));
        Assert.IsTrue(c.Down(target));
        c.Poll(new KeyboardState(Keys.I), default, false, false, 0);
        Assert.IsFalse(c.Fresh(target));
        Assert.IsTrue(c.Down(target));
        c.Poll(new KeyboardState(), default, false, false, 0);
        Assert.IsFalse(c.Down(target));
    }

    [TestMethod]
    public void Combo_IsAnAlias()
    {
        var c = Installed(("I+K", "Inventory"));
        c.Poll(new KeyboardState(Keys.K), default, false, false, 0);
        Assert.IsTrue(c.Fresh(BindTarget.Ui(HotkeyAction.Inventory)));
    }

    [TestMethod]
    public void TwoKeys_FireTwoActionsInOneFrame()
    {
        var c = Installed(("I", "Inventory"), ("K", "Skill"));
        c.Poll(new KeyboardState(Keys.I, Keys.K), default, false, false, 0);
        Assert.IsTrue(c.Fresh(BindTarget.Ui(HotkeyAction.Inventory)));
        Assert.IsTrue(c.Fresh(BindTarget.Ui(HotkeyAction.Skill)));
    }

    [TestMethod]
    public void Fresh_ASkillTargetFiresFromItsKey()
    {
        var c = Installed(("D1", "Skill:1001005"));
        c.Poll(new KeyboardState(Keys.D1), default, false, false, 0);
        Assert.IsTrue(c.Fresh(new BindTarget(BindKind.Skill, 1001005)));
        Assert.IsFalse(c.Fresh(new BindTarget(BindKind.Skill, 1001006))); // the other skill has no key
    }

    [TestMethod]
    public void Install_ParsesSkillTargetAndKey()
    {
        var c = Installed(("D1", "Skill:1001005"));
        var action = c.Actions.Single();
        Assert.AreEqual(new BindTarget(BindKind.Skill, 1001005), action.Target);
        CollectionAssert.AreEqual(new[] { Keys.D1 }, action.Keys);
    }

    [TestMethod]
    public void Seed_HeldKeysAreNotFresh()
    {
        var c = Installed(("I", "Inventory"));
        var target = BindTarget.Ui(HotkeyAction.Inventory);
        c.Seed(new KeyboardState(Keys.I));
        c.Poll(new KeyboardState(Keys.I), default, false, false, 0);
        Assert.IsFalse(c.Fresh(target));
        Assert.IsTrue(c.Down(target));
    }

    [TestMethod]
    public void Install_SkipsBadKeysAndTargets()
    {
        // "1" is not Keys.D1 and "Escape" is not bindable; "Item:" has no id.
        var c = Installed(("Nope", "Inventory"), ("I", "Nope"), ("", "Inventory"),
            ("1", "Inventory"), ("Escape", "Inventory"), ("D2", "Item:"));
        Assert.AreEqual(0, c.Actions.Count);
    }

    [TestMethod]
    public void Install_ParsesItemTarget()
    {
        var c = Installed(("D1", "Item:2000000"));
        Assert.AreEqual(new BindTarget(BindKind.Item, 2000000), c.Actions.Single().Target);
        Assert.IsFalse(BindTarget.TryParse("Item:x", out _));
    }

    [TestMethod]
    public void TargetOf_TheStripCellsReadTheDefaultTable()
    {
        // The HUD strip: Ctrl is Attack, Alt is Jump, I is Inventory, C is
        // empty (Stat sits on S, which is not a strip cell). D1 is the potion.
        var table = KeyDefaults.Table();
        table["D1"] = "Item:2000000";
        var c = Installed(table.Select(kv => (kv.Key, kv.Value)).ToArray());
        Assert.AreEqual(BindTarget.Ui(HotkeyAction.Attack), c.TargetOf(Keys.LeftControl));
        Assert.AreEqual(BindTarget.Ui(HotkeyAction.Attack), c.TargetOf(Keys.RightControl));
        Assert.AreEqual(BindTarget.Ui(HotkeyAction.Jump), c.TargetOf(Keys.LeftAlt));
        Assert.AreEqual(BindTarget.Ui(HotkeyAction.Inventory), c.TargetOf(Keys.I));
        Assert.AreEqual(new BindTarget(BindKind.Item, 2000000), c.TargetOf(Keys.D1));
        Assert.IsNull(c.TargetOf(Keys.C));
    }

    [TestMethod]
    public void Defaults_ParseAndCoverTheKmsKeys()
    {
        var table = KeyDefaults.Table();
        var expected = new Dictionary<string, string>
        {
            ["S"] = "Stat",
            ["E"] = "Equip",
            ["I"] = "Inventory",
            ["K"] = "Skill",
            ["Q"] = "Quest",
            ["W"] = "WorldMap",
            ["U"] = "Guide",
            ["OemPipe"] = "KeyConfig",
            ["P"] = "Party",
            ["L"] = "Menu",
            ["LeftControl+RightControl"] = "Attack",
            ["LeftAlt+RightAlt"] = "Jump",
            ["Space"] = "Talk",
            ["Z"] = "Pickup",
        };
        CollectionAssert.AreEquivalent(expected, table);
        foreach (var (key, text) in table)
        {
            Assert.IsTrue(InputController.TryParseKeys(key, out _), key);
            Assert.IsTrue(BindTarget.TryParse(text, out _), text);
        }
        Assert.IsTrue(InputController.TryParseKeys("LeftControl+RightControl", out var ctrl));
        Assert.AreEqual(2, ctrl.Length);
        Assert.IsFalse(InputController.TryParseKeys("1", out _)); // the key is D1
        Assert.IsFalse(InputController.TryParseKeys("Escape", out _)); // the Esc layer
        Assert.IsFalse(InputController.TryParseKeys("Up", out _)); // the game reads the arrows
        Assert.IsTrue(InputController.TryParseKeys("Z", out _)); // the pickup is a normal action now
        Assert.IsFalse(BindTarget.TryParse("1", out _)); // an action name, not a number
    }

    [TestMethod]
    public void Ensure_MissingPresetsBecomeThreeDefaults()
    {
        var chara = new CharaSlot();
        KeyBindings.Ensure(chara);
        Assert.AreEqual(3, chara.KeyPresets!.Count);
        Assert.AreEqual("Inventory", chara.KeyPresets[0]["I"]);
        Assert.AreEqual(0, chara.ActivePreset);
    }

    [TestMethod]
    public void Apply_OneKeyOneTarget_SplitsACombo()
    {
        var table = new Dictionary<string, string> { ["LeftControl+RightControl"] = "Attack" };
        KeyBindings.Apply(table, new() { [HotkeyAction.Inventory] = "LeftControl" });
        Assert.AreEqual("Inventory", table["LeftControl"]);
        Assert.AreEqual("Attack", table["RightControl"]);
        Assert.IsFalse(table.ContainsKey("LeftControl+RightControl"));
    }

    [TestMethod]
    public void Apply_AComboTakesItsParts()
    {
        var table = new Dictionary<string, string> { ["U"] = "Guide", ["K"] = "Skill" };
        KeyBindings.Apply(table, new() { [HotkeyAction.Inventory] = "U+K" });
        Assert.AreEqual("Inventory", table["U+K"]);
        Assert.IsFalse(table.ContainsKey("U"));
        Assert.IsFalse(table.ContainsKey("K"));
    }

    // Table level: Apply takes the key from any target, the hidden ones
    // included (the K1 "Reserved" guard is gone).
    [TestMethod]
    public void Apply_AVisibleActionTakesTheKeyConfigKey()
    {
        var table = new Dictionary<string, string> { ["OemPipe"] = "KeyConfig" };
        KeyBindings.Apply(table, new() { [HotkeyAction.Inventory] = "OemPipe" });
        Assert.AreEqual("Inventory", table["OemPipe"]);
        Assert.IsFalse(table.ContainsValue("KeyConfig"));
    }

    [TestMethod]
    public void Apply_DefaultsRestoresTheKeyConfigKey()
    {
        var table = new Dictionary<string, string> { ["OemPipe"] = "Inventory" };
        KeyBindings.Apply(table, KeyBindings.Defaults());
        Assert.AreEqual("KeyConfig", table["OemPipe"]);
        Assert.AreEqual("Inventory", table["I"]);
    }

    [TestMethod]
    public void Apply_KeepsTheSkillAndItemEntries()
    {
        var table = new Dictionary<string, string> { ["D1"] = "Skill:1001005", ["D2"] = "Item:2000000" };
        KeyBindings.Apply(table, new() { [HotkeyAction.Inventory] = "I" });
        Assert.AreEqual("Skill:1001005", table["D1"]);
        Assert.AreEqual("Item:2000000", table["D2"]);
        Assert.AreEqual("Inventory", table["I"]);
    }

    [TestMethod]
    public void ApplySkills_WritesTheSkillTarget()
    {
        var table = new Dictionary<string, string>();
        KeyBindings.ApplySkills(table, new() { [1001005] = "K" });
        Assert.AreEqual("Skill:1001005", table["K"]);
    }

    [TestMethod]
    public void ApplySkills_ASkillTakesTheKeyFromAnAction()
    {
        var table = new Dictionary<string, string> { ["K"] = "Skill" };
        KeyBindings.ApplySkills(table, new() { [1001005] = "K" });
        Assert.AreEqual("Skill:1001005", table["K"]);
    }

    [TestMethod]
    public void Apply_AnActionTakesTheKeyFromASkill()
    {
        var table = new Dictionary<string, string> { ["K"] = "Skill:1001005" };
        KeyBindings.Apply(table, new() { [HotkeyAction.Inventory] = "K" });
        Assert.AreEqual("Inventory", table["K"]);
    }

    [TestMethod]
    public void ApplySkills_AnEmptyKeyClearsTheSkill()
    {
        var table = new Dictionary<string, string> { ["K"] = "Skill:1001005" };
        KeyBindings.ApplySkills(table, new() { [1001005] = "" });
        Assert.IsFalse(table.ContainsKey("K")); // Default: the skill loses its key
    }

    [TestMethod]
    public void ApplySkills_ASkillSplitsAComboPart()
    {
        var table = new Dictionary<string, string> { ["I+K"] = "Inventory" };
        KeyBindings.ApplySkills(table, new() { [1001005] = "K" });
        Assert.AreEqual("Inventory", table["I"]); // the combo keeps its other key
        Assert.AreEqual("Skill:1001005", table["K"]);
    }

    [TestMethod]
    public void RemoveKind_OnlyDropsThatKind()
    {
        var table = new Dictionary<string, string>
        {
            ["D1"] = "Item:2000000",
            ["D2"] = "Skill:1001005",
            ["I"] = "Inventory",
        };
        KeyBindings.RemoveKind(table, BindKind.Item);
        CollectionAssert.AreEquivalent(new[] { "D2", "I" }, table.Keys.ToList());
    }

    [TestMethod]
    public void UiMap_And_Defaults()
    {
        var map = KeyBindings.UiMap(new Dictionary<string, string>
        {
            ["I"] = "Inventory",
            ["Q"] = "Skill:1001005",
        });
        Assert.AreEqual("I", map[HotkeyAction.Inventory]);
        Assert.IsFalse(map.ContainsKey(HotkeyAction.Skill)); // a skill target is not a UI key

        var defaults = KeyBindings.Defaults();
        Assert.AreEqual(Enum.GetValues<HotkeyAction>().Length, defaults.Count);
        Assert.AreEqual("S", defaults[HotkeyAction.Stat]);
        Assert.AreEqual("E", defaults[HotkeyAction.Equip]);
        Assert.AreEqual("U", defaults[HotkeyAction.Guide]);
        Assert.AreEqual("L", defaults[HotkeyAction.Menu]);
        Assert.AreEqual("OemPipe", defaults[HotkeyAction.KeyConfig]);
        Assert.AreEqual("LeftControl+RightControl", defaults[HotkeyAction.Attack]);
        Assert.AreEqual("LeftAlt+RightAlt", defaults[HotkeyAction.Jump]);
        Assert.AreEqual("Space", defaults[HotkeyAction.Talk]);
        Assert.AreEqual("", defaults[HotkeyAction.Settings]);
    }
}
