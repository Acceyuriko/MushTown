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
    Settings,
    KeyConfig, // no key by default: the options window opens it
}

// The names the key settings panel shows. They are MonoGame key names, so
// Enum.TryParse turns one into the key to watch; the modifier keys get a
// shorter label here.
public static class HotkeyNames
{
    public static string Label(string key) => key switch
    {
        "LeftControl" or "RightControl" => "Ctrl",
        "LeftAlt" or "RightAlt" => "Alt",
        "LeftShift" or "RightShift" => "Shift",
        _ => key,
    };
}

public partial class MushTownGame
{
    // action -> key name. The key settings panel edits this and saves it.
    private readonly Dictionary<HotkeyAction, string> _hotkeys = new()
    {
        [HotkeyAction.Inventory] = "I",
        [HotkeyAction.Stat] = "C",
        [HotkeyAction.Skill] = "K",
        [HotkeyAction.Quest] = "V",
        [HotkeyAction.Minimap] = "M",
        [HotkeyAction.WorldMap] = "O",
        [HotkeyAction.Settings] = "P",
        [HotkeyAction.KeyConfig] = "",
    };

    // The keys held last frame.
    private readonly HashSet<string> _hotkeysDown = new();

    // The keys that are down this frame (the set is reused every frame).
    private readonly HashSet<string> _hotkeysNow = new();

    // The action whose key went down this frame (null when none did).
    // One key can only open one panel: the first action in the list wins.
    private HotkeyAction? FreshHotkey(KeyboardState kb)
    {
        HotkeyAction? hit = null;
        _hotkeysNow.Clear();
        foreach (var (action, key) in _hotkeys)
        {
            if (!Enum.TryParse<Keys>(key, out var k) || !kb.IsKeyDown(k)) continue;
            _hotkeysNow.Add(key);
            if (!_hotkeysDown.Contains(key) && hit == null) hit = action;
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
            if (Enum.TryParse<Keys>(key, out var k) && kb.IsKeyDown(k)) _hotkeysDown.Add(key);
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
        [HotkeyAction.Minimap] = "M",
        [HotkeyAction.WorldMap] = "O",
        [HotkeyAction.Settings] = "P",
        [HotkeyAction.KeyConfig] = "",
    };

    // The key bound to an action, or "" when it has none.
    public string HotkeyKey(HotkeyAction action) => _hotkeys.GetValueOrDefault(action) ?? "";

    // Load the saved bindings (a missing file keeps the defaults).
    private void LoadHotkeys(Dictionary<string, string>? saved)
    {
        if (saved == null) return;
        foreach (var (action, key) in saved)
            if (Enum.TryParse<HotkeyAction>(action, out var a))
            {
                if (key != "" && !Enum.TryParse<Keys>(key, out _)) continue;
                _hotkeys[a] = key;
            }
    }

    // The bindings for the settings file. Every action is written, an
    // action without a key gets an empty value.
    private Dictionary<string, string> SaveHotkeys()
    {
        var map = new Dictionary<string, string>();
        foreach (var (action, key) in _hotkeys)
            map[action.ToString()] = key;
        return map;
    }
}
