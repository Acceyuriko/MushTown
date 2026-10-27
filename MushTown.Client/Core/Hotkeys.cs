using MushTown.Shared;

namespace MushTown.Client;

// Every action a key can open. The key window lists the panel actions;
// Attack, Jump, Talk, Equip, Guide and KeyConfig are bound but not listed.
public enum HotkeyAction
{
    Inventory,   // I
    Stat,        // S: the stat page of the stat window
    Skill,       // K
    Quest,       // Q
    Minimap,     // no key: always on screen
    WorldMap,    // W
    Party,       // P
    PartySearch, // no key: the party window opens the search window
    PartyAsk,    // no key: the server sends the invite
    Settings,    // no default key: the options window is opened from the MENU
    KeyConfig,   // OemPipe (\)
    Menu,        // L
    Chat,        // no key: the chat input is clicked
    Guide,       // U: the big guide (the small guide's green plus also opens it)
    GuideCard,   // no key: the MENU window opens the small guide
    Equip,       // E: the equipment page of the stat window
    Attack,      // Ctrl: the basic swing
    Jump,        // Alt: the jump
    Talk,        // Space: talk to the nearest NPC
}

// The official KMS defaults: key name -> target. Ctrl/Alt are aliases of
// both sides; a combo fires on either key.
public static class KeyDefaults
{
    public static Dictionary<string, string> Table() => new()
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
}

// The table operations, testable without a game instance.
public static class KeyBindings
{
    // A character without the field (the old save) starts from the defaults.
    public static void Ensure(CharaSlot chara)
    {
        if (chara.KeyPresets == null)
            chara.KeyPresets = new List<Dictionary<string, string>>
            {
                KeyDefaults.Table(), KeyDefaults.Table(), KeyDefaults.Table(),
            };
    }

    // The default action -> key map (the key window's Default button).
    public static Dictionary<HotkeyAction, string> Defaults()
    {
        var map = UiMap(KeyDefaults.Table());
        foreach (var action in Enum.GetValues<HotkeyAction>()) map.TryAdd(action, "");
        return map;
    }

    // The UI targets of a table as action -> key.
    public static Dictionary<HotkeyAction, string> UiMap(Dictionary<string, string> table)
    {
        var map = new Dictionary<HotkeyAction, string>();
        foreach (var (key, text) in table)
            if (BindTarget.TryParse(text, out var target) && target.Kind == BindKind.Ui)
                map[(HotkeyAction)target.Id] = key;
        return map;
    }

    // The key bound to a UI action, or "".
    public static string FindKey(Dictionary<string, string> table, HotkeyAction action)
    {
        foreach (var (key, text) in table)
            if (BindTarget.TryParse(text, out var target) && target.IsUi(action)) return key;
        return "";
    }

    // Write the given actions into one table. A key (or a combo part) taken
    // here leaves the target it came from; a target left with no key goes.
    public static void Apply(Dictionary<string, string> table, Dictionary<HotkeyAction, string> map)
    {
        foreach (var (action, key) in map)
        {
            string target = action.ToString();
            foreach (string old in table.Where(kv => kv.Value == target).Select(kv => kv.Key).ToList())
                table.Remove(old);
            if (key == "") continue;
            foreach (string part in key.Split('+', StringSplitOptions.RemoveEmptyEntries))
                RemoveKeyPart(table, part);
            table[key] = target;
        }
    }

    // Take one key out of every entry (a combo loses the part; an empty
    // combo goes). "LeftControl" also leaves "LeftControl+RightControl"
    // as just "RightControl". A shortened key that is already taken means
    // the table was invalid (two targets on one key); the last write wins.
    private static void RemoveKeyPart(Dictionary<string, string> table, string part)
    {
        foreach (var (key, target) in table.ToList())
        {
            var parts = key.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (!parts.Contains(part)) continue;
            var left = parts.Where(p => p != part).ToArray();
            table.Remove(key);
            if (left.Length > 0) table[string.Join('+', left)] = target;
        }
    }
}

public partial class MushTownGame
{
    // The live table when there is no character (--host/--join): read by
    // the HUD every frame, so it is built once and never written.
    private static readonly Dictionary<string, string> DefaultKeys = KeyDefaults.Table();
    private bool _keysDirty; // the key window wrote a preset mid-frame

    // The key window's Save/Apply rebuilds the input table at the next
    // Update, before Poll, so the rest of this frame still uses the old one.
    private void InstallPendingKeys()
    {
        if (!_keysDirty) return;
        _keysDirty = false;
        InstallInput();
    }

    public int ActivePreset
    {
        get
        {
            var chara = Hero;
            return chara == null ? 0 : chara.ActivePreset;
        }
    }

    private Dictionary<string, string> LiveKeys
    {
        get
        {
            var chara = Hero;
            if (chara == null) return DefaultKeys;
            KeyBindings.Ensure(chara);
            return chara.KeyPresets![chara.ActivePreset];
        }
    }

    private void InstallInput() => _input.Install(LiveKeys);

    public string HotkeyKey(HotkeyAction action) => KeyBindings.FindKey(LiveKeys, action);

    // One preset as an action -> key map (the key window). No character
    // (--host/--join) shows the defaults, like the live table.
    public Dictionary<HotkeyAction, string> HotkeyPreset(int slot)
    {
        var chara = Hero;
        if (chara == null) return KeyBindings.Defaults();
        KeyBindings.Ensure(chara);
        return KeyBindings.UiMap(chara.KeyPresets![slot]);
    }

    // Write the given actions into one preset. Other targets (skills; items
    // arrive in week 2) stay; the live table follows when this preset is active.
    public void SaveHotkeyPreset(int slot, Dictionary<HotkeyAction, string> map)
    {
        var chara = Hero!; // the key window hides Save/Copy without a character
        KeyBindings.Ensure(chara);
        KeyBindings.Apply(chara.KeyPresets![slot], map);
        if (slot == chara.ActivePreset) _keysDirty = true;
        SaveChara();
    }

    public void SetActivePreset(int slot)
    {
        var chara = Hero!; // the key window hides Apply without a character
        KeyBindings.Ensure(chara);
        chara.ActivePreset = slot;
        _keysDirty = true;
        SaveChara();
    }
}
