using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The key settings window: the official KeyConfig art. The official grid
// under the keyboard holds the game actions: the player presses an action
// in its cell, drags it onto a key and saves.
public sealed class KeyConfigWindow : UiWindow
{
    private readonly MushTownGame _game;
    public KeyConfigWindow(MushTownGame game) : base(HotkeyAction.KeyConfig) { _game = game; }

    // Every number of this window lives in layouts/panel-keys.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-keys.json"));

    // The official key spots (assets/ui/keys.json).
    public sealed class Spot
    {
        public int Code { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Cap { get; set; } = "";
    }

    public sealed class KeyFile
    {
        public List<Spot> Keys { get; set; } = new();
    }

    private static KeyFile? _file;
    private static KeyFile Spots => _file ??= MushTownGame.LoadJson<KeyFile>(
        Path.Combine(Path.GetDirectoryName(MushTownGame.LayoutsDir)!, "assets", "ui", "keys.json"));

    // The game actions in the grid: the action, the official icon, the short
    // code the bound key shows. The icons are the official KeyConfig ones.
    private static readonly (HotkeyAction Action, int Icon, string Code)[] Actions =
    {
        (HotkeyAction.Inventory, 1, "INV"),
        (HotkeyAction.Stat, 2, "STA"),
        (HotkeyAction.Skill, 3, "SKL"),
        (HotkeyAction.Quest, 8, "QST"),
        (HotkeyAction.Minimap, 7, "MIN"),
        (HotkeyAction.WorldMap, 5, "WLD"),
        (HotkeyAction.Settings, 202, "SET"),
    };

    // The pending bindings and the action the player holds.
    private readonly Dictionary<HotkeyAction, string> _draft = new();
    private HotkeyAction? _held;

    // The key boxes are baked once: one texture for the free keys, one for
    // the bound keys (a rounded box per key per frame is too many draws).
    private Texture2D? _freeKey, _boundKey;

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 660), _lay.Get("h", 624));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 18));
    public override Point LayoutPos() => new(_lay.Get("x", 353), _lay.Get("y", 72));

    // One key box on the official key grid (the WZ keyPos spot).
    private Rectangle KeyRect(Spot s) => new(
        Pos.X + s.X + _lay.Get("keyDx", 0),
        Pos.Y + s.Y + _lay.Get("keyDy", 33),
        _lay.Get("keyW", 26), _lay.Get("keyH", 21));

    // One official grid cell (the art draws the cells, the actions sit in them).
    private Rectangle CellRect(int index)
    {
        int cols = _lay.Get("gridCols", 17);
        int x = _lay.Get("gridX", 25) + (index % cols) * _lay.Get("cellPitchX", 36);
        int y = _lay.Get("gridY", 374) + (index / cols) * _lay.Get("cellPitchY", 36);
        return new Rectangle(Pos.X + x, Pos.Y + y, _lay.Get("cellW", 34), _lay.Get("cellH", 34));
    }

    // Open the window: the draft starts from the saved bindings.
    public void Open()
    {
        _draft.Clear();
        foreach (var (action, _, _) in Actions) _draft[action] = _game.HotkeyKey(action);
        _held = null;
        _game.ShowWindow(HotkeyAction.KeyConfig);
    }

    // Close the window: the unsaved changes and the held action go away.
    public void Close()
    {
        _draft.Clear();
        _held = null;
        Show = false;
    }

    public override void Build()
    {
        _game.LoadTexture("ui/keys_bg.png");
        _game.LoadTexture("ui/hud_statx.png");
        foreach (string stem in new[] { "ui/keys_save", "ui/keys_reset" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture(stem + state);
        foreach (var s in Spots.Keys)
            if (s.Cap != "") _game.LoadTexture("ui/" + s.Cap);
        foreach (var a in Actions) _game.LoadTexture($"ui/keys_icon_{a.Icon}.png");

        if (_freeKey == null || _freeKey.IsDisposed)
        {
            int w = _lay.Get("keyW", 26), h = _lay.Get("keyH", 21);
            _freeKey = _game.RoundedBacking(w, h, new Color(238, 238, 238), 3);
            _boundKey = _game.RoundedBacking(w, h, new Color(255, 210, 60), 3);
        }

        // The draft starts from the saved bindings (a window built without
        // Open, the shot mode).
        if (_draft.Count == 0)
            foreach (var (action, _, _) in Actions) _draft[action] = _game.HotkeyKey(action);

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new Picture("ui/keys_bg.png"));

        // The keys: the official cap, or the action that took the key.
        foreach (var spot in Spots.Keys)
        {
            var s = spot;
            var o = new GameObject();
            o.Add(new Rect(KeyRect(s)));
            o.Add(new Invoke(gfx => DrawKey(gfx, s)));
            root.AddChild(o);
        }

        // The actions in the official grid: press one, drag it onto a key.
        for (int i = 0; i < Actions.Length; i++)
        {
            var a = Actions[i];
            int index = i;
            var o = new GameObject();
            o.Add(new Rect(CellRect(index)));
            o.Add(new Button { OnClick = () => Pick(a.Action) });
            o.Add(new Invoke(gfx => DrawCell(gfx, a, index)));
            root.AddChild(o);
        }

        root.AddChild(ArtButton("ui/keys_save", _lay.Get("saveX", 562), _lay.Get("saveY", 568), 78, 27, Save));
        root.AddChild(ArtButton("ui/keys_reset", _lay.Get("resetX", 176), _lay.Get("resetY", 568), 67, 27, Default));

        // The title strip drags the window.
        var title = new GameObject();
        title.Add(new Rect(TitleRect()));
        title.Add(new Button { OnClick = StartDrag });
        root.AddChild(title);

        // The X closes the window.
        var close = new GameObject();
        close.Add(new Rect(new Rectangle(Pos.X + _lay.Get("closeX", 636), Pos.Y + _lay.Get("closeY", 4), 11, 11)));
        close.Add(new Button { Kind = ButtonKind.Click, OnClick = Close });
        close.Add(new Picture("ui/hud_statx.png"));
        root.AddChild(close);

        // The held action rides the mouse.
        var drag = new GameObject();
        drag.Add(new Invoke(DrawHeld));
        root.AddChild(drag);

        Scene.Add(root);
    }

    // One art button (the art has normal, hover and pressed states).
    private GameObject ArtButton(string stem, int dx, int dy, int w, int h, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + dx, Pos.Y + dy, w, h)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Image(stem));
        return o;
    }

    // One key: the free box with the official cap, or the gold box with the
    // action that took the key.
    private void DrawKey(UiGfx gfx, Spot s)
    {
        var r = KeyRect(s);
        string? code = CodeFor(KeyName(s.Code));
        if (code != null)
        {
            gfx.Sb.Draw(_boundKey!, new Vector2(r.X, r.Y), Color.White);
            var t = gfx.Ui.GetSmall(code);
            gfx.Sb.Draw(t, new Vector2(r.X + (r.Width - t.Width) / 2, r.Y + (r.Height - t.Height) / 2), new Color(40, 40, 60));
            return;
        }
        gfx.Sb.Draw(_freeKey!, new Vector2(r.X, r.Y), Color.White);
        if (s.Cap == "") return;
        var cap = gfx.Tex("ui/" + s.Cap);
        gfx.Sb.Draw(cap, new Vector2(r.X + (r.Width - cap.Width) / 2, r.Y + (r.Height - cap.Height) / 2), Color.White);
    }

    // The action in its official grid cell.
    private void DrawCell(UiGfx gfx, (HotkeyAction Action, int Icon, string Code) a, int index)
    {
        var r = CellRect(index);
        var icon = gfx.Tex($"ui/keys_icon_{a.Icon}.png");
        gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
    }

    // The picked up action rides the mouse. Letting go of the mouse drops it
    // on the key under the cursor.
    private void DrawHeld(UiGfx gfx)
    {
        if (_held == null) return;
        var ms = Mouse.GetState();
        var (gx, gy) = _game.MouseGamePos(ms);
        if (ms.LeftButton != ButtonState.Pressed)
        {
            Drop(_held.Value, gx, gy);
            return;
        }
        var icon = gfx.Tex($"ui/keys_icon_{IconOf(_held.Value)}.png");
        gfx.Sb.Draw(icon, new Vector2(gx - icon.Width / 2, gy - icon.Height / 2), Color.White);
    }

    // The player presses an action cell: it sticks to the mouse.
    private void Pick(HotkeyAction action) => _held = action;

    // The player lets go: the action takes the key under the mouse.
    private void Drop(HotkeyAction action, int gx, int gy)
    {
        _held = null;
        foreach (var s in Spots.Keys)
        {
            if (!KeyRect(s).Contains(gx, gy)) continue;
            Bind(action, KeyName(s.Code));
            return;
        }
    }

    // The action takes the key. The key is taken away from the other actions.
    private void Bind(HotkeyAction action, string key)
    {
        if (key == "") return;
        foreach (var other in _draft.Keys.ToList())
            if (other != action && _draft[other] == key) _draft[other] = "";
        _draft[action] = key;
    }

    // Save: the game writes the whole map and saves it once.
    private void Save() => _game.SetHotkeys(_draft);

    // Default: the official bindings come back (the button art says Default).
    private void Default()
    {
        _draft.Clear();
        foreach (var (action, key) in _game.HotkeyDefaults())
            if (action != HotkeyAction.KeyConfig) _draft[action] = key;
    }

    // The short code of the action that has this key.
    private string? CodeFor(string key)
    {
        if (key == "") return null;
        foreach (var a in Actions)
            if (_draft.GetValueOrDefault(a.Action) == key) return a.Code;
        return null;
    }

    // The official icon of an action.
    private static int IconOf(HotkeyAction action)
    {
        foreach (var a in Actions)
            if (a.Action == action) return a.Icon;
        return 0;
    }

    // The WZ cap id is the PC scancode; this is the MonoGame key name.
    private static string KeyName(int code) => KeyNames.GetValueOrDefault(code, "");

    // The cap art of a key name ("ui/keys_cap_<scancode>.png"). The HUD
    // quick slot shows the cap of the current binding.
    private static Dictionary<string, string>? _capArt;
    public static string CapArt(string keyName)
    {
        if (_capArt == null)
        {
            _capArt = new();
            foreach (var s in Spots.Keys)
                if (s.Cap != "") _capArt[KeyName(s.Code)] = "ui/" + s.Cap;
        }
        return _capArt.GetValueOrDefault(keyName, "");
    }

    // Load the cap art (the quick slot can show any bound key).
    public static void LoadCaps(MushTownGame game)
    {
        foreach (var s in Spots.Keys)
            if (s.Cap != "") game.LoadTexture("ui/" + s.Cap);
    }

    private static readonly Dictionary<int, string> KeyNames = BuildKeyNames();

    private static Dictionary<int, string> BuildKeyNames()
    {
        var map = new Dictionary<int, string>
        {
            [1] = "Escape",
            [12] = "OemMinus",
            [13] = "OemPlus",
            [26] = "OemOpenBrackets",
            [27] = "OemCloseBrackets",
            [29] = "LeftControl",
            [39] = "OemSemicolon",
            [40] = "OemQuotes",
            [41] = "OemTilde",
            [42] = "LeftShift",
            [43] = "OemPipe",
            [51] = "OemComma",
            [52] = "OemPeriod",
            [54] = "RightShift",
            [56] = "LeftAlt",
            [57] = "Space",
            [70] = "Scroll",
            [71] = "Home",
            [73] = "PageUp",
            [79] = "End",
            [81] = "PageDown",
            [82] = "Insert",
            [83] = "Delete",
            [87] = "F11",
            [88] = "F12",
        };
        string digits = "1234567890";
        for (int i = 0; i < 10; i++) map[2 + i] = "D" + digits[i];
        string top = "QWERTYUIOP";
        for (int i = 0; i < 10; i++) map[16 + i] = top[i].ToString();
        string home = "ASDFGHJKL";
        for (int i = 0; i < 9; i++) map[30 + i] = home[i].ToString();
        string bottom = "ZXCVBNM";
        for (int i = 0; i < 7; i++) map[44 + i] = bottom[i].ToString();
        for (int i = 0; i < 10; i++) map[59 + i] = "F" + (i + 1);
        return map;
    }
}
