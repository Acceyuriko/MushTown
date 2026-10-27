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
        // "1" is not Keys.D1 and "Escape" is not bindable; "Item:" is not a
        // target (items come in week 2).
        var c = Installed(("Nope", "Inventory"), ("I", "Nope"), ("", "Inventory"),
            ("1", "Inventory"), ("Escape", "Inventory"), ("D2", "Item:2000000"));
        Assert.AreEqual(0, c.Actions.Count);
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
        Assert.IsFalse(InputController.TryParseKeys("Z", out _)); // the fixed pickup
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

    // Table level: Apply takes the key from any target. The K1 key window
    // keeps hidden targets' keys out of reach (Reserved); K2 allows this.
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
    public void Apply_KeepsTheSkillEntries()
    {
        var table = new Dictionary<string, string> { ["D1"] = "Skill:1001005" };
        KeyBindings.Apply(table, new() { [HotkeyAction.Inventory] = "I" });
        Assert.AreEqual("Skill:1001005", table["D1"]);
        Assert.AreEqual("Inventory", table["I"]);
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
