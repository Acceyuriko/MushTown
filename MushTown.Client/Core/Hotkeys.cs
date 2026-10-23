using Microsoft.Xna.Framework.Input;

namespace MushTown.Client;

// The keys that open the panels. The player can rebind them in the key
// settings panel; the defaults are the official client's.
public enum HotkeyAction
{
    Inventory,
    Stat,
    Skill,
    Quest,
    Minimap,
    WorldMap,
    Party,      // the party window
    PartySearch, // no key: the party window opens the search window
    PartyAsk,   // no key: the server sends the invite
    Settings,
    KeyConfig, // no key by default: the options window opens it
    Menu,      // no default key: the key window binds it
    Chat,      // no key: the chat input is clicked
    Guide,     // no key: the green plus of the small guide opens the big guide
    GuideCard, // no key: the MENU window opens the small guide
}

public partial class MushTownGame
{
    // The version of the key set: the settings file says which one it was
    // written for. A file from an older build has no version and holds the
    // old keys, so the defaults are used instead.
    private const int KeyVersion = 1;

    // action -> key name. The key settings panel edits this and saves it.
    private readonly Dictionary<HotkeyAction, string> _hotkeys = new()
    {
        [HotkeyAction.Inventory] = "I",
        [HotkeyAction.Stat] = "C",
        [HotkeyAction.Skill] = "K",
        [HotkeyAction.Quest] = "V",
        [HotkeyAction.Minimap] = "",   // the minimap is always on the screen
        [HotkeyAction.WorldMap] = "M",
        [HotkeyAction.Party] = "P",
        [HotkeyAction.Settings] = "O",
        [HotkeyAction.Menu] = "",
        [HotkeyAction.KeyConfig] = "",
    };

    // The keys held last frame.
    private readonly HashSet<string> _hotkeysDown = new();

    // The keys that are down this frame (the set is reused every frame).
    private readonly HashSet<string> _hotkeysNow = new();

    // The action whose key went down this frame (null when none did).
    // One key can only open one panel: the first action in the list wins.
    // A combo binding ("I+K") fires on either key.
    private HotkeyAction? FreshHotkey(KeyboardState kb)
    {
        HotkeyAction? hit = null;
        _hotkeysNow.Clear();
        foreach (var (action, key) in _hotkeys)
        {
            foreach (var part in key.Split('+', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Enum.TryParse<Keys>(part, out var k) || !kb.IsKeyDown(k)) continue;
                _hotkeysNow.Add(part);
                if (!_hotkeysDown.Contains(part) && hit == null) hit = action;
            }
        }
        _hotkeysDown.Clear();
        foreach (var key in _hotkeysNow) _hotkeysDown.Add(key);
        return hit;
    }

    // The menu seeds the key edges: a key held while the menu is open must
    // not open a panel in the game.
    private void SeedHotkeys(KeyboardState kb)
    {
        _hotkeysDown.Clear();
        foreach (var (action, key) in _hotkeys)
            foreach (var part in key.Split('+', StringSplitOptions.RemoveEmptyEntries))
                if (Enum.TryParse<Keys>(part, out var k) && kb.IsKeyDown(k)) _hotkeysDown.Add(part);
    }

    // Apply the whole binding map at once and save it once. The key
    // settings panel uses this, so a save can not lose a binding.
    // The panel takes a key away from the other actions itself.
    public void SetHotkeys(Dictionary<HotkeyAction, string> map)
    {
        foreach (var action in _hotkeys.Keys.ToList())
            _hotkeys[action] = map.GetValueOrDefault(action, "");
        SaveSettings();
    }

    // The default bindings (the key settings panel restores them).
    public Dictionary<HotkeyAction, string> HotkeyDefaults() => new()
    {
        [HotkeyAction.Inventory] = "I",
        [HotkeyAction.Stat] = "C",
        [HotkeyAction.Skill] = "K",
        [HotkeyAction.Quest] = "V",
        [HotkeyAction.Minimap] = "",   // the minimap is always on the screen
        [HotkeyAction.WorldMap] = "M",
        [HotkeyAction.Party] = "P",
        [HotkeyAction.Settings] = "O",
        [HotkeyAction.Menu] = "",
        [HotkeyAction.KeyConfig] = "",
    };

    // The three saved binding sets (the key window's preset slots).
    private readonly List<Dictionary<HotkeyAction, string>> _hotkeyPresets = new()
    {
        new(), new(), new(),
    };

    // The bindings in a preset slot. An empty slot gives an empty map.
    public Dictionary<HotkeyAction, string> HotkeyPreset(int slot) =>
        slot >= 0 && slot < _hotkeyPresets.Count
            ? new Dictionary<HotkeyAction, string>(_hotkeyPresets[slot])
            : new Dictionary<HotkeyAction, string>();

    // Save a binding set into a preset slot.
    public void SaveHotkeyPreset(int slot, Dictionary<HotkeyAction, string> map)
    {
        if (slot < 0 || slot >= _hotkeyPresets.Count) return;
        _hotkeyPresets[slot] = new Dictionary<HotkeyAction, string>(map);
        SaveSettings();
    }

    // The key bound to an action, or "" when it has none.
    public string HotkeyKey(HotkeyAction action) => _hotkeys.GetValueOrDefault(action) ?? "";

    // Load the saved bindings (a missing file keeps the defaults).
    // A binding is one key ("I") or a combo of two ("I+K").
    // The version says which key set the file was written for: an old file
    // holds the old keys (the party key would fight with the old settings
    // key) and an old key cannot be told from a key the player picked, so
    // an old file keeps the defaults.
    private void LoadHotkeys(Dictionary<string, string>? saved, int version)
    {
        if (saved == null || version != KeyVersion) return;
        foreach (var (action, key) in saved)
            if (Enum.TryParse<HotkeyAction>(action, out var a))
            {
                if (!IsKeyName(key)) continue;
                _hotkeys[a] = key;
            }
    }

    // A key name, or a combo of key names joined by "+".
    private static bool IsKeyName(string key) =>
        key == "" || key.Split('+', StringSplitOptions.RemoveEmptyEntries).All(p => Enum.TryParse<Keys>(p, out _));

    // The bindings for the settings file. Every action is written, an
    // action without a key gets an empty value.
    private Dictionary<string, string> SaveHotkeys()
    {
        var map = new Dictionary<string, string>();
        foreach (var (action, key) in _hotkeys)
            map[action.ToString()] = key;
        return map;
    }

    // Load the saved preset slots (a missing list keeps the empty slots).
    private void LoadHotkeyPresets(List<Dictionary<string, string>>? saved, int version)
    {
        if (saved == null || version != KeyVersion) return;
        for (int i = 0; i < _hotkeyPresets.Count && i < saved.Count; i++)
        {
            var slot = new Dictionary<HotkeyAction, string>();
            foreach (var (action, key) in saved[i])
                if (Enum.TryParse<HotkeyAction>(action, out var a)) slot[a] = key;
            _hotkeyPresets[i] = slot;
        }
    }

    // The preset slots for the settings file.
    private List<Dictionary<string, string>> SaveHotkeyPresets()
    {
        var list = new List<Dictionary<string, string>>();
        foreach (var slot in _hotkeyPresets)
        {
            var map = new Dictionary<string, string>();
            foreach (var (action, key) in slot)
                map[action.ToString()] = key;
            list.Add(map);
        }
        return list;
    }
}
