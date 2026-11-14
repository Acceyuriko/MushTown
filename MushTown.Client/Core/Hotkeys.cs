using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// Every action a key can open. The key window lists every official icon of
// the KeyConfig art, implemented or not: an action with no window behind it
// simply does nothing when its key fires, so the pool doubles as the list of
// what is still missing. PartySearch, PartyAsk, PartyHp, GuideCard and
// Settings have no icon of their own, so the pool has no cell for them.
public enum HotkeyAction
{
    Inventory,   // I
    Stat,        // S: Character Info
    Skill,       // K
    Quest,       // Q
    Minimap,     // no key: always on screen
    WorldMap,    // W
    Party,       // P
    PartySearch, // no key: the party window opens the search window
    PartyAsk,    // no key: the server sends the invite
    PartyHp,     // no key: the party window's Show Member HP switch opens it
    Settings,    // no default key: the options window is opened from the MENU
    KeyConfig,   // OemPipe (\)
    Menu,        // L
    Chat,        // no key: Enter focuses the chat input (and unfolds the chat)
    Guide,       // U: the big guide (the small guide's green plus also opens it)
    GuideCard,   // no key: the MENU window opens the small guide
    Equip,       // E: the equipment window
    CashShop,    // no key: the MENU window opens the cash shop
    Attack,      // Ctrl: the basic swing
    Jump,        // Alt: the jump
    Talk,        // Space: talk to the nearest NPC
    Pickup,      // Z: pick the nearest drop up

    // The official icons without a window behind them yet: the key window
    // lists them, a key bound to one does nothing. They stay in the enum
    // because the saved table stores the name, so a save with one of these
    // on a key still reads back after the action lands.
    Friends,
    MapleChat,
    ToAll,
    Whisper,
    ToParty,
    ToBuddy,
    QuickSlots,
    Guild,
    ToGuild,
    QuestHelper,
    MapleNews,
    ToAlliance,
    Legion,
    Medals,
    Bosses,
    Profession,
    Events,
    BattleAnalysis,
    EnhanceEquip,
    MonsterCollection,
    SoulWeapon,
    ChangeChannel,
    Screenshot,
    PictureMode,
    Achievements,
    Sit,
    ToTown,
    GoHome,
    Mup,
    StorageRoom,
    QuickMove,
    Archive,
    SoulCollection,
    Emoticon1,
    Emoticon2,
    Emoticon3,
    Emoticon4,
    Emoticon5,
    Emoticon6,
    Emoticon7,
    MapleStorage,
    SafeMode,
    MuteGame,
    Emoticons,
    KeySettings1,
    KeySettings2,
    KeySettings3,
    ComboKey,
    SolErda,
    CharPreset,
    SwapPreset,
    Planner,
    Sequence,
    Decorations,
    AutoHarvest,
    BuffShopper,
    Familiar,
    ToSpouse,
    TraceResto,
    MapleRelay,
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
        ["Z"] = "Pickup",
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
    public static void Apply(Dictionary<string, string> table, Dictionary<HotkeyAction, string> map) =>
        Write(table, map.Select(kv => new KeyValuePair<string, string>(kv.Key.ToString(), kv.Value)));

    // The same for the skills and items the key window binds ("Skill:<id>" /
    // "Item:<id>"). They share the action table, so a key moves between them.
    public static void ApplySkills(Dictionary<string, string> table, Dictionary<int, string> skills) =>
        ApplyTargets(table, "Skill", skills);

    public static void ApplyItems(Dictionary<string, string> table, Dictionary<int, string> items) =>
        ApplyTargets(table, "Item", items);

    private static void ApplyTargets(Dictionary<string, string> table, string kind, Dictionary<int, string> targets) =>
        Write(table, targets.Select(kv => new KeyValuePair<string, string>($"{kind}:{kv.Key}", kv.Value)));

    // Does a binding already carry this key? Writing it again would drop the
    // other half of a combo, so a drop on the key it came from changes nothing.
    public static bool HasPart(string keys, string key) =>
        keys.Split('+', StringSplitOptions.RemoveEmptyEntries).Contains(key);

    // A quick slot's target is gone: the whole binding goes, combo or not.
    public static void ClearTarget(Dictionary<string, string> table, BindTarget target)
    {
        foreach (var (key, text) in table.ToList())
            if (BindTarget.TryParse(text, out var bound) && bound == target) table.Remove(key);
    }

    private static void Write(Dictionary<string, string> table, IEnumerable<KeyValuePair<string, string>> targets)
    {
        foreach (var (target, key) in targets)
        {
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

    // What a click picked up: one action, skill or item target. It rides the
    // mouse until a click drops it on a key, a quick slot cell or the action
    // pool; Esc or a right click throws it away and changes nothing. Nothing
    // comes up while the inventory hand holds a piece: one thing at a time.
    // Not the mouse button (InputController.Held is that): this is the piece a
    // click picked up.
    public BindTarget? Held { get; private set; }
    public bool Holding => Held != null;
    private bool _heldFromSlot; // the quick slot a click picked the target from

    public void Hold(BindTarget target, bool fromSlot = false)
    {
        if (!_hand.Empty) return;
        Held = target;
        _heldFromSlot = fromSlot;
    }

    public void ClearHeld()
    {
        Held = null;
        _heldFromSlot = false;
        ClearWornHand();
    }

    // The icon art of a skill id, or "" when it is not a skill of this
    // character (the key window draws the skill the draft put on a key).
    internal string SkillIconTex(int id) => _skills.TryGetValue(id, out var s) ? s.Icon : "";

    // One preset as an action -> key map (the key window). No character
    // (--host/--join) shows the defaults, like the live table.
    public Dictionary<HotkeyAction, string> HotkeyPreset(int slot)
    {
        var chara = Hero;
        if (chara == null) return KeyBindings.Defaults();
        KeyBindings.Ensure(chara);
        return KeyBindings.UiMap(chara.KeyPresets![slot]);
    }

    // The skills or items a preset binds, read back as id -> key (the key
    // window's draft).
    public Dictionary<int, string> HotkeySkills(int slot) => HotkeyTargets(slot, BindKind.Skill);
    public Dictionary<int, string> HotkeyItems(int slot) => HotkeyTargets(slot, BindKind.Item);

    private Dictionary<int, string> HotkeyTargets(int slot, BindKind kind)
    {
        var map = new Dictionary<int, string>();
        var chara = Hero;
        if (chara == null) return map;
        KeyBindings.Ensure(chara);
        foreach (var (key, text) in chara.KeyPresets![slot])
            if (BindTarget.TryParse(text, out var target) && target.Kind == kind)
                map[target.Id] = key;
        return map;
    }

    // Write the given actions, skills and items into one preset.
    public void SaveHotkeyPreset(int slot, Dictionary<HotkeyAction, string> map,
        Dictionary<int, string> skills, Dictionary<int, string> items)
    {
        var chara = Hero!; // the key window hides Save/Copy without a character
        KeyBindings.Ensure(chara);
        var table = chara.KeyPresets![slot];
        KeyBindings.Apply(table, map);
        KeyBindings.ApplySkills(table, skills);
        KeyBindings.ApplyItems(table, items);
        if (slot == chara.ActivePreset) _keysDirty = true;
        SaveChara();
    }

    // A drop on a quick slot cell while the key window is closed: the key binds
    // now. The caller checked that there is a character to write. The strip
    // reads the live table, so the new binding shows on the next frame, when
    // InstallPendingKeys runs before Poll (never mid-click).
    public void BindLive(Keys key, BindTarget target)
    {
        var chara = Hero!;
        if (_input.TargetOf(key) == target) return; // already on that key: a combo keeps both parts
        string name = key.ToString();
        if (target.Kind == BindKind.Skill)
            SaveHotkeyPreset(chara.ActivePreset, new(), new() { [target.Id] = name }, new());
        else if (target.Kind == BindKind.Item)
            SaveHotkeyPreset(chara.ActivePreset, new(), new(), new() { [target.Id] = name });
        else
            SaveHotkeyPreset(chara.ActivePreset, new() { [(HotkeyAction)target.Id] = name }, new(), new());
    }

    // Copy: the source preset covers the picked one whole (skills and items
    // come along). The live table follows when the picked one is active.
    public void CopyHotkeyPreset(int from, int to)
    {
        var chara = Hero!; // the key window hides Copy without a character
        KeyBindings.Ensure(chara);
        chara.KeyPresets![to] = new Dictionary<string, string>(chara.KeyPresets[from]);
        if (to == chara.ActivePreset) _keysDirty = true;
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
