using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MushTown.Client;

// What a key can be bound to: a UI action, a skill or an item.
public enum BindKind
{
    Ui,
    Skill,
    Item,
}

public readonly record struct BindTarget(BindKind Kind, int Id)
{
    public static BindTarget Ui(HotkeyAction action) => new(BindKind.Ui, (int)action);

    public bool IsUi(HotkeyAction action) => Kind == BindKind.Ui && Id == (int)action;

    // "Inventory", "Skill:1001005" or "Item:2000000". Only the exact enum
    // names parse: a numeric string is not an action.
    public static bool TryParse(string text, out BindTarget target)
    {
        target = default;
        if (text.StartsWith("Skill:", StringComparison.Ordinal))
        {
            if (!int.TryParse(text.AsSpan(6), out int skill)) return false;
            target = new(BindKind.Skill, skill);
            return true;
        }
        if (text.StartsWith("Item:", StringComparison.Ordinal))
        {
            if (!int.TryParse(text.AsSpan(5), out int item)) return false;
            target = new(BindKind.Item, item);
            return true;
        }
        if (!Enum.TryParse<HotkeyAction>(text, out var action) || Enum.GetName(action) != text) return false;
        target = Ui(action);
        return true;
    }
}

// One bound target. A combo key ("I+K") is an alias: either key fires it.
public sealed class BoundAction
{
    public BindTarget Target { get; }
    public Keys[] Keys { get; }
    public bool Fire { get; internal set; }
    public bool Down { get; internal set; }
    internal bool Suppress { get; set; }
    private int _wait;

    public BoundAction(BindTarget target, Keys[] keys)
    {
        Target = target;
        Keys = keys;
    }

    // Held keys fire again after their throttling interval. A release clears
    // the wait, so the next press starts at once.
    internal void Tick(bool down, int frames)
    {
        if (!down) { _wait = 0; Fire = false; return; }
        if (_wait > 0) { _wait--; Fire = false; return; }
        Fire = true;
        _wait = Math.Max(0, frames - 1);
    }

    internal void Reset()
    {
        _wait = 0;
        Fire = false;
    }
}

// The one input entry: the game reads the keyboard and mouse once a frame
// and updates every bound action. Consumers ask their own action.
public sealed class InputController
{
    private readonly List<BoundAction> _actions = new();
    private readonly HashSet<Keys> _down = new();
    private readonly HashSet<Keys> _last = new();
    private readonly Dictionary<Keys, int> _keyWait = new();

    public IReadOnlyList<BoundAction> Actions => _actions;
    // While a text input has the keys, no action fires: the raw keyboard still
    // reaches the typing window, the bound keys read as nothing.
    public bool Capture { get; set; }
    public Point Mouse { get; private set; } // 1366x768 game coordinates
    public bool Click { get; private set; }
    public bool Held { get; private set; }
    public int Wheel { get; private set; }

    // Rebuild the actions from a key -> target table. The table is small
    // (the keyboard size), never the skill or item count.
    public void Install(Dictionary<string, string> table)
    {
        _actions.Clear();
        foreach (var (key, text) in table)
        {
            if (!TryParseKeys(key, out var keys)) continue;
            if (!BindTarget.TryParse(text, out var target)) continue;
            _actions.Add(new BoundAction(target, keys));
        }
    }

    // Once a frame, with the keyboard and mouse the game already read.
    public void Poll(KeyboardState keyboard, Point mouse, bool click, bool held, int wheel)
    {
        Mouse = mouse;
        Click = click;
        Held = held;
        Wheel = wheel;
        foreach (var key in _keyWait.Keys)
            if (_keyWait[key] > 0) _keyWait[key]--;
        _last.Clear();
        foreach (var key in _down) _last.Add(key);
        _down.Clear();
        foreach (var key in keyboard.GetPressedKeys()) _down.Add(key);
        foreach (var action in _actions)
        {
            bool down = false;
            foreach (var key in action.Keys)
                if (_down.Contains(key)) { down = true; break; }
            if (Capture)
            {
                action.Reset();
                action.Down = false;
                action.Suppress = down; // a key held into the field waits for release
                continue;
            }
            action.Down = down;
            if (action.Suppress)
            {
                action.Fire = false;
                if (!down)
                {
                    action.Reset();
                    action.Suppress = false;
                }
                continue;
            }
            action.Tick(down, FireFrames(action.Target));
        }
    }

    // Per-target throttling: UI switches 60 frames (~1s), pickup 6 (~100ms),
    // items 30 (~500ms). Attack and skills use AttackLock / their own
    // cooldowns, so the input layer does not gate them.
    private static int FireFrames(BindTarget target) => target.Kind switch
    {
        BindKind.Item => 30,
        BindKind.Skill => 0,
        _ => (HotkeyAction)target.Id switch
        {
            HotkeyAction.Pickup => 6,
            HotkeyAction.Attack => 0,
            _ => 60,
        },
    };

    // Keys held while the menu was open must not fire on the first frame.
    public void Seed(KeyboardState keyboard)
    {
        _keyWait.Clear();
        _down.Clear();
        foreach (var key in keyboard.GetPressedKeys()) _down.Add(key);
        _last.Clear();
        foreach (var key in _down) _last.Add(key);
        foreach (var action in _actions)
        {
            action.Reset();
            action.Down = _down.Overlaps(action.Keys);
            action.Suppress = action.Down; // wait for release, then the next press fires
        }
    }

    public bool Fresh(Keys key) => _down.Contains(key) && !_last.Contains(key);

    // Any key of the target pressed this frame that was not pressed last frame.
    public bool Fresh(BindTarget target)
    {
        if (Capture) return false;
        foreach (var action in _actions)
        {
            if (action.Target != target) continue;
            foreach (var key in action.Keys)
                if (_down.Contains(key) && !_last.Contains(key)) return true;
        }
        return false;
    }

    // A raw key (not in the binding table) on the same held/throttle rule.
    public bool Fire(Keys key, int frames)
    {
        if (Capture) { _keyWait.Remove(key); return false; }
        if (!_down.Contains(key)) { _keyWait.Remove(key); return false; }
        if (_keyWait.TryGetValue(key, out int wait) && wait > 0) return false;
        _keyWait[key] = Math.Max(0, frames);
        return true;
    }

    public BoundAction? Action(BindTarget target)
    {
        foreach (var action in _actions)
            if (action.Target == target) return action;
        return null;
    }

    public bool Fire(BindTarget target) => Action(target)?.Fire ?? false;
    public bool Down(BindTarget target) => Action(target)?.Down ?? false;
    public bool Fresh(HotkeyAction action) => Fresh(BindTarget.Ui(action));
    public bool Fire(HotkeyAction action) => Fire(BindTarget.Ui(action));
    public bool Down(HotkeyAction action) => Down(BindTarget.Ui(action));

    // The target on one key; a combo part counts (either Ctrl side shows
    // the Attack cell). Null when the key has nothing.
    public BindTarget? TargetOf(Keys key)
    {
        foreach (var action in _actions)
            foreach (var bound in action.Keys)
                if (bound == key) return action.Target;
        return null;
    }

    // A key name ("D1", "OemPipe", "LeftControl"). A combo joins names
    // with '+'. Only exact enum names parse (a bare "1" is not "D1").
    // Esc, Enter, Tab and the arrows are not bindable: the game itself
    // reads them.
    public static bool TryParseKeys(string text, out Keys[] keys)
    {
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries);
        keys = new Keys[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!Enum.TryParse(parts[i], out keys[i]) || Enum.GetName(keys[i]) != parts[i] || !IsBindable(keys[i]))
            {
                keys = Array.Empty<Keys>();
                return false;
            }
        }
        return keys.Length > 0;
    }

    public static bool IsBindable(Keys key) =>
        key is not (Keys.None or Keys.Escape or Keys.Enter or Keys.Tab
            or Keys.Up or Keys.Down or Keys.Left or Keys.Right);
}

public partial class MushTownGame
{
    private readonly InputController _input = new();

    // The one poll of the keyboard and the mouse: the panels read this copy
    // instead of asking the devices again.
    internal InputController Input => _input;
}
