using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The key settings window: the official KeyConfig art. The official grid
// under the keyboard holds the game actions: the player presses an action
// in its cell, drags it onto a key and saves. The official keyboard shows
// the key letters, or the icon of the action a key has.
public sealed class KeyConfigWindow : UiWindow
{
    private readonly MushTownGame _game;
    public KeyConfigWindow(MushTownGame game) : base(HotkeyAction.KeyConfig) { _game = game; }

    // Every number of this window lives in layouts/panel-keys.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-keys.json"));

    // The official key spots (assets/ui/keys.json).
    private sealed class Spot
    {
        public int Code { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Cap { get; set; } = "";
    }

    private sealed class KeyFile
    {
        public List<Spot> Keys { get; set; } = new();
    }

    // The filter names and the category of every action icon
    // (assets/ui/keys_filter.json).
    private sealed class FilterFile
    {
        public List<string> Filters { get; set; } = new();
        public Dictionary<int, int> Categories { get; set; } = new();
    }

    private static KeyFile? _file;
    private static KeyFile Spots => _file ??= MushTownGame.LoadJson<KeyFile>(
        Path.Combine(Path.GetDirectoryName(MushTownGame.LayoutsDir)!, "assets", "ui", "keys.json"));

    private static FilterFile? _filterFile;
    private static FilterFile Filters => _filterFile ??= MushTownGame.LoadJson<FilterFile>(
        Path.Combine(Path.GetDirectoryName(MushTownGame.LayoutsDir)!, "assets", "ui", "keys_filter.json"));

    // The game actions in the grid: the action and the official icon.
    // The minimap is not here: it is always on the screen, it has no key.
    private static readonly (HotkeyAction Action, int Icon)[] Actions =
    {
        (HotkeyAction.Inventory, 1),
        (HotkeyAction.Stat, 2),
        (HotkeyAction.Skill, 3),
        (HotkeyAction.Quest, 8),
        (HotkeyAction.WorldMap, 5),
        (HotkeyAction.Party, 19),
        (HotkeyAction.Menu, 46),
        (HotkeyAction.Settings, 46),
    };

    // The pending bindings, the action the player holds and the picked
    // preset slot.
    private readonly Dictionary<HotkeyAction, string> _draft = new();
    private HotkeyAction? _held;
    private Point _pickAt; // where the action was picked up
    private int _slot;

    // The filter dropdown: the picked filter and the open list.
    private int _filter = 1;
    private bool _filterOpen;

    // The combo key mode: a drop gives the action a second key.
    private bool _comboMode;

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 660), _lay.Get("h", 624));

    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 18));

    public override Point LayoutPos() => new(_lay.Get("x", 353), _lay.Get("y", 72));

    // One key box of the official keyboard (the WZ keyPos spot).
    private Rectangle KeyRect(Spot s) => new(
        Pos.X + s.X + _lay.Get("keyDx", 0),
        Pos.Y + s.Y + _lay.Get("keyDy", 33),
        _lay.Get("keyW", 32), _lay.Get("keyH", 32));

    // One official grid cell (the art draws the cells, the actions sit in them).
    private Rectangle CellRect(int index)
    {
        int cols = _lay.Get("gridCols", 17);
        int x = _lay.Get("gridX", 25) + (index % cols) * _lay.Get("cellPitchX", 36);
        int y = _lay.Get("gridY", 374) + (index / cols) * _lay.Get("cellPitchY", 36);
        return new Rectangle(Pos.X + x, Pos.Y + y, _lay.Get("cellW", 34), _lay.Get("cellH", 34));
    }

    // Open the window: the draft starts from the picked preset (the active
    // one on open; the 1/2/3 buttons reload it).
    public void Open()
    {
        _slot = _game.ActivePreset;
        ReloadDraft();
        _held = null;
        Pos = LayoutPos();
        _game.ShowWindow(HotkeyAction.KeyConfig);
    }

    // The eight visible actions, read back from the picked preset.
    private void ReloadDraft()
    {
        var preset = _game.HotkeyPreset(_slot);
        _draft.Clear();
        foreach (var (action, _) in Actions) _draft[action] = preset.GetValueOrDefault(action, "");
    }

    // Close the window: the unsaved changes and the held action go away.
    public void Close()
    {
        OnClosed();
        Show = false;
    }

    // The game closed the window (Esc): the draft and the held action go away.
    public override void OnClosed()
    {
        _draft.Clear();
        _held = null;
        _filterOpen = false;
        _comboMode = false;
    }

    public override void Build()
    {
        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect()));

        _game.LoadTexture("ui/keys_bg.png");
        _game.LoadTexture("ui/keys_kb.png");
        _game.LoadTexture("ui/hud_statx.png");
        _game.LoadTexture("ui/keys_custom_p.png");
        foreach (string stem in new[] { "ui/keys_save", "ui/keys_reset", "ui/keys_cancel", "ui/keys_copy", "ui/keys_apply", "ui/keys_skill", "ui/keys_qslot", "ui/keys_combo", "ui/keys_pg1", "ui/keys_pg2", "ui/keys_pg3" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture(stem + state);
        foreach (var s in Spots.Keys)
            if (s.Cap != "") _game.LoadTexture("ui/" + s.Cap);
        foreach (var a in Actions) _game.LoadTexture(IconTex(a.Action));
        LoadComboBox();

        // Open fills the draft; the tree reads it when drawing.
        root.Add(new Picture("ui/keys_bg.png"));
        root.Add(new Picture("ui/keys_kb.png", _lay.Get("kbX", 20), _lay.Get("kbY", 100)));

        // The keys: the official letter, or the icon of the action on it.
        foreach (var spot in Spots.Keys)
        {
            var o = new GameObject();
            o.Add(new Rect(KeyRect(spot)));
            o.Add(new KeyCap(this, spot));
            root.AddChild(o);
        }

        // The actions in the official grid: press one, drag it onto a key.
        // The filter picks the actions that show.
        for (int i = 0; i < Actions.Length; i++)
        {
            var a = Actions[i];
            int index = i;
            if (!FilterShows(a.Action)) continue;
            var o = new GameObject();
            o.Add(new Rect(CellRect(index)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Pick(a.Action) });
            o.Add(new ActionCell(this, a.Action));
            root.AddChild(o);
        }

        BuildFilter(root);

        // The Custom tab: the window has only this page, so the tab is the
        // current one. It is a plain picture in the pressed look: no click,
        // no hover.
        root.Add(new Picture("ui/keys_custom_p.png", _lay.Get("customX", 19), _lay.Get("customY", 66)));

        // The presets: copy the draft into the slot, apply the slot. No
        // character (--host/--join): there is no preset to write, so the
        // three buttons do not show.
        if (_game.Hero != null)
        {
            root.AddChild(ArtButton("ui/keys_copy", _lay.Get("copyX", 380), _lay.Get("copyY", 73), 67, 19, Copy));
            root.AddChild(ArtButton("ui/keys_apply", _lay.Get("applyX", 573), _lay.Get("applyY", 73), 67, 19, Apply));
        }
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            string stem = $"ui/keys_pg{i + 1}";
            int x = _lay.Get($"pg{i + 1}X", 505 + 20 * i), y = _lay.Get($"pg{i + 1}Y", 76);
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, 12, 12)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => { _slot = slot; ReloadDraft(); Build(); } }); // the draft and the action grid follow the picked preset
            o.Add(new Image(stem, () => _slot == slot)); // the picked slot stays lit, so the player sees it
            root.AddChild(o);
        }

        // The bottom buttons: combo key, quick slots, reset, skill, cancel,
        // save. The quick slots page comes with K2, so that button is a plain
        // picture: the click does nothing.
        root.AddChild(ComboButton());
        root.Add(new Picture("ui/keys_qslot_n.png", _lay.Get("qslotX", 98), _lay.Get("qslotY", 568)));
        root.AddChild(ArtButton("ui/keys_reset", _lay.Get("resetX", 176), _lay.Get("resetY", 568), 67, 27, Default));
        root.AddChild(ArtButton("ui/keys_skill", _lay.Get("skillX", 245), _lay.Get("skillY", 568), 67, 27, OpenSkill));
        root.AddChild(ArtButton("ui/keys_cancel", _lay.Get("cancelX", 474), _lay.Get("cancelY", 568), 87, 27, Cancel));
        if (_game.Hero != null)
            root.AddChild(ArtButton("ui/keys_save", _lay.Get("saveX", 562), _lay.Get("saveY", 568), 78, 27, Save));

        BuildTitleAndClose(root, "ui/hud_statx.png", _lay.Get("closeX", 636), _lay.Get("closeY", 4), 11);
        BuildDrag(root);
        Host.AddChild(root);
    }

    // The title strip drags the window, the X closes it.
    private void BuildTitleAndClose(GameObject root, string closeArt, int closeX, int closeY, int closeSize)
    {
        var title = new GameObject();
        title.Add(new Rect(TitleRect()));
        title.Add(new Button { OnClick = StartDrag });
        root.AddChild(title);

        var close = new GameObject();
        close.Add(new Rect(new Rectangle(Pos.X + closeX, Pos.Y + closeY, closeSize, closeSize)));
        close.Add(new Button { Kind = ButtonKind.Click, OnClick = Close });
        close.Add(new Picture(closeArt));
        root.AddChild(close);
    }

    // The held action rides the mouse. Letting go of the mouse drops it on the
    // key under the cursor: the HUD passes the same click edge it uses itself.
    private void BuildDrag(GameObject root)
    {
        var drag = new GameObject();
        drag.Add(new HeldIcon(this));
        root.AddChild(drag);
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

    // The Combo Key button: it stays lit while the combo mode is on.
    private GameObject ComboButton()
    {
        int x = _lay.Get("comboX", 20), y = _lay.Get("comboY", 568);
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, 76, 27)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = ToggleComboMode });
        o.Add(new Image("ui/keys_combo", () => _comboMode));
        return o;
    }

    // The filter dropdown: the combo box and the open list.
    private void BuildFilter(GameObject root)
    {
        int x = _lay.Get("filterX", 529), y = _lay.Get("filterY", 337);
        int w = _lay.Get("filterW", 108), h = _lay.Get("filterH", 25);
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, w, h)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = ToggleFilter });
        o.Add(new FilterBox(this));
        o.Add(new Label(FilterName(_filter), _lay.Get("filterTextX", 7), _lay.Get("filterTextY", 5)));
        root.AddChild(o);

        if (!_filterOpen) return;
        int itemH = _lay.Get("filterItemH", 23);
        int listW = _lay.Get("filterListW", 150);
        int top = y + h;

        var bg = new GameObject();
        bg.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + top, listW, itemH * Filters.Filters.Count)));
        bg.Add(new Fill(new Color(238, 238, 238), 0));
        root.AddChild(bg);

        for (int i = 0; i < Filters.Filters.Count; i++)
        {
            int index = i;
            int itemY = top + index * itemH;
            var item = new GameObject();
            item.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + itemY, listW, itemH)));
            item.Add(new Button { Kind = ButtonKind.Click, OnClick = () => PickFilter(index) });
            item.Add(new FilterItem(index));
            root.AddChild(item);
        }
    }

    private void LoadComboBox()
    {
        foreach (string set in new[] { "n", "p" })
            for (int piece = 0; piece < 3; piece++)
                _game.LoadTexture($"ui/keys_cbox_{set}{piece}.png");
    }

    // The filter names (the official combo box items).
    private static string FilterName(int index) =>
        index >= 0 && index < Filters.Filters.Count ? Filters.Filters[index] : "";

    // The filter picks the actions that show: "Not Added" shows the actions
    // with no key, the other filters show one category of actions.
    private bool FilterShows(HotkeyAction action)
    {
        if (_filter == 0) return _draft.GetValueOrDefault(action, "") == "";
        int category = Filters.Categories.GetValueOrDefault(IconOf(action), 0);
        return category + 1 == _filter;
    }

    // One key of the official keyboard: the official letter, or the icon of
    // the action the draft binds to that key.
    private sealed class KeyCap : Component, IDrawable
    {
        private readonly KeyConfigWindow _w;
        private readonly Spot _spot;
        public KeyCap(KeyConfigWindow w, Spot spot) { _w = w; _spot = spot; }
        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Box;
            if (_w.ActionFor(KeyName(_spot.Code)) is { } action)
            {
                var icon = gfx.Tex(IconTex(action));
                gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
                return;
            }
            if (_spot.Cap == "") return;
            var cap = gfx.Tex("ui/" + _spot.Cap);
            gfx.Sb.Draw(cap, new Vector2(r.X + (r.Width - cap.Width) / 2, r.Y + (r.Height - cap.Height) / 2), Color.White);
        }
    }

    // The action in its official grid cell. A combo action shows both keys.
    private sealed class ActionCell : Component, IDrawable
    {
        private readonly KeyConfigWindow _w;
        private readonly HotkeyAction _action;
        public ActionCell(KeyConfigWindow w, HotkeyAction action) { _w = w; _action = action; }
        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Box;
            var icon = gfx.Tex(IconTex(_action));
            gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
            var parts = _w.Keys(_action);
            if (parts.Length < 2) return;
            // The two caps sit at the bottom of the cell, so they do not hide
            // the icon of the action.
            int x = r.X + _lay.Get("comboCapX", 2);
            int y = r.Y + _lay.Get("comboCapY", 18);
            foreach (var part in parts)
            {
                string cap = CapArt(part);
                if (cap == "") continue;
                var tex = gfx.Tex(cap);
                gfx.Sb.Draw(tex, new Vector2(x, y), Color.White);
                x += tex.Width + 1;
            }
        }
    }

    // The filter box: the left, middle and right pieces of the official art.
    private sealed class FilterBox : Component, IDrawable
    {
        private readonly KeyConfigWindow _w;
        public FilterBox(KeyConfigWindow w) => _w = w;
        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Box;
            string set = _w._filterOpen ? "p" : "n";
            gfx.Sb.Draw(gfx.Tex($"ui/keys_cbox_{set}0.png"), new Vector2(r.X, r.Y), Color.White);
            gfx.Sb.Draw(gfx.Tex($"ui/keys_cbox_{set}2.png"), new Vector2(r.X + r.Width - 31, r.Y), Color.White);
            // the middle piece stretches between the two ends
            gfx.Sb.Draw(gfx.Tex($"ui/keys_cbox_{set}1.png"), new Rectangle(r.X + 3, r.Y, r.Width - 34, r.Height), Color.White);
        }
    }

    // One row of the open filter list: the hover fill and the name.
    private sealed class FilterItem : Component, IDrawable
    {
        private readonly int _index;
        public FilterItem(int index) => _index = index;
        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Box;
            if (gfx.Hovered == Owner) gfx.Sb.Draw(gfx.Pixel, r, new Color(167, 167, 167));
            gfx.Sb.Draw(gfx.Ui.GetSmall(FilterName(_index)), new Vector2(r.X + _lay.Get("filterTextX", 7), r.Y + _lay.Get("filterTextY", 5)), Color.Black);
        }
    }

    // The action the player picked up rides the cursor.
    private sealed class HeldIcon : Component, IDrawable
    {
        private readonly KeyConfigWindow _w;
        public HeldIcon(KeyConfigWindow w) => _w = w;
        public void Draw(UiGfx gfx)
        {
            if (_w._held is not { } action) return;
            var icon = gfx.Tex(IconTex(action));
            gfx.Sb.Draw(icon, new Vector2(gfx.Mouse.X - icon.Width / 2, gfx.Mouse.Y - icon.Height / 2), Color.White);
        }
    }

    // The HUD passes its own mouse edge: letting go drops the held action on
    // the key under the cursor.
    public void UpdateDrag(bool held, int gx, int gy)
    {
        if (_held is not { } action || held) return;
        Drop(action, gx, gy);
    }

    // The player presses an action cell: it sticks to the mouse.
    private void Pick(HotkeyAction action)
    {
        _held = action;
        _pickAt = Scene.LastClick;
    }

    // The player lets go: the action takes the key under the mouse. An icon
    // that is dragged off the keyboard takes the key away from the action.
    private void Drop(HotkeyAction action, int gx, int gy)
    {
        _held = null;
        foreach (var s in Spots.Keys)
        {
            if (!KeyRect(s).Contains(gx, gy)) continue;
            string key = KeyName(s.Code);
            if (_comboMode) AddKey(action, key);
            else Bind(action, key);
            return;
        }
        if (_comboMode) return;
        int cell = CellIndex(action);
        if (cell >= 0 && CellRect(cell).Contains(gx, gy)) return; // dropped on itself
        if (Math.Abs(gx - _pickAt.X) + Math.Abs(gy - _pickAt.Y) < 8) return; // a click, not a drag
        _draft[action] = "";
    }

    // The cell of an action in the grid (-1 when the action is not there).
    private static int CellIndex(HotkeyAction action)
    {
        for (int i = 0; i < Actions.Length; i++) if (Actions[i].Action == action) return i;
        return -1;
    }

    // The combo key mode: the action keeps its key and gets a second one
    // ("I+K"). The action then fires on either key.
    private void AddKey(HotkeyAction action, string key)
    {
        if (Reserved(key) || !InputController.TryParseKeys(key, out _)) return;
        var parts = Keys(action);
        if (parts.Length == 0) { Bind(action, key); return; }
        if (parts.Contains(key)) return;
        TakeAway(key, action);
        if (parts.Length == 1) parts = new[] { parts[0], key };
        else parts[1] = key;
        _draft[action] = string.Join("+", parts);
    }

    // The action takes the key. The key is taken away from the other actions.
    private bool Bind(HotkeyAction action, string key)
    {
        if (Reserved(key) || !InputController.TryParseKeys(key, out _)) return false; // Esc/Enter/Tab/arrows/Z are not bindable
        TakeAway(key, action);
        _draft[action] = key;
        return true;
    }

    // K1 edits only the 8 visible actions: a key owned by a hidden UI action
    // (Attack, Jump, Talk, Equip, Guide, KeyConfig) must stay with it, or
    // that action loses the key with no way back. Skill keys are K2's new
    // key window. Item targets are not protected either: saving a draft takes
    // their key too (K2 protects them with the drag).
    private bool Reserved(string key)
    {
        var preset = _game.HotkeyPreset(_slot);
        return preset.Any(kv => !Actions.Any(a => a.Action == kv.Key)
            && kv.Value.Split('+', StringSplitOptions.RemoveEmptyEntries).Contains(key));
    }

    // The key goes away from the other actions (part by part).
    private void TakeAway(string key, HotkeyAction keep)
    {
        foreach (var other in _draft.Keys.ToList())
        {
            if (other == keep) continue;
            var parts = Keys(other);
            if (!parts.Contains(key)) continue;
            _draft[other] = string.Join("+", parts.Where(p => p != key));
        }
    }

    // The keys of an action: one key, or the two of a combo.
    private string[] Keys(HotkeyAction action) =>
        _draft.GetValueOrDefault(action, "").Split('+', StringSplitOptions.RemoveEmptyEntries);

    // Save: the draft goes into the picked preset. K1: Save and Copy do
    // the same; K2 makes Copy pick a source preset.
    private void Save()
    {
        _game.SaveHotkeyPreset(_slot, _draft);
        ReloadDraft();
        Build();
    }

    // Default: the official bindings come back (the button art says Default).
    private void Default()
    {
        _draft.Clear();
        var defaults = KeyBindings.Defaults();
        foreach (var (action, _) in Actions) _draft[action] = defaults.GetValueOrDefault(action, ""); // the 8 visible actions only: the hidden ones keep their keys
        Build();
    }

    // Cancel: the draft goes back to the picked preset's bindings.
    private void Cancel()
    {
        ReloadDraft();
        _held = null;
        Build();
    }

    // Copy: the draft goes into the picked preset (K1: the same as Save).
    private void Copy()
    {
        _game.SaveHotkeyPreset(_slot, _draft);
    }

    // Apply: the picked preset becomes the live one.
    private void Apply()
    {
        ReloadDraft();
        _game.SetActivePreset(_slot);
        Build();
    }

    // The filter button: the list opens and closes.
    private void ToggleFilter()
    {
        _filterOpen = !_filterOpen;
        Build();
    }

    // The player picks a filter: the actions of that category show.
    private void PickFilter(int index)
    {
        _filter = index;
        _filterOpen = false;
        Build();
    }

    // The combo key mode: the next drop gives the action a second key.
    private void ToggleComboMode()
    {
        _comboMode = !_comboMode;
        Build();
    }

    // The skill button: the key window closes and the skill window opens.
    private void OpenSkill()
    {
        OnClosed(); // the same reset as Esc or the title X
        _game.OpenSkillWindow();
        Show = false;
    }

    // The action that has this key.
    private HotkeyAction? ActionFor(string key)
    {
        if (key == "") return null;
        foreach (var a in Actions)
            if (Keys(a.Action).Contains(key)) return a.Action;
        return null;
    }

    // The icon art of an action. The KeyConfig set has no settings icon (the
    // options are a row of the MENU window), so Settings shows the gear of the
    // quick menu row. The HUD quick slot cells draw the same art.
    internal static string IconTex(HotkeyAction action) =>
        action == HotkeyAction.Settings ? "ui/quickmenu_53_n.png" : $"ui/keys_icon_{IconOf(action)}.png";

    // The official icon of an action. Attack and Jump are not in the grid
    // (the key window cannot move them) but the HUD cells show them.
    internal static int IconOf(HotkeyAction action)
    {
        if (action == HotkeyAction.Attack) return 52;
        if (action == HotkeyAction.Jump) return 53;
        foreach (var a in Actions)
            if (a.Action == action) return a.Icon;
        return 0;
    }

    // The WZ cap id is the PC scancode; this is the MonoGame key name.
    private static string KeyName(int code) => KeyNames.GetValueOrDefault(code, "");

    // The cap art of a key name ("ui/keys_cap_<scancode>.png"). A combo
    // ("I+K") shows the cap of its first key. The HUD quick slot cells read
    // their caps from here too, so the scancodes live in keys.json only.
    private static Dictionary<string, string>? _capArt;
    internal static string CapArt(string keyName)
    {
        if (_capArt == null)
        {
            _capArt = new();
            foreach (var s in Spots.Keys)
                if (s.Cap != "") _capArt[KeyName(s.Code)] = "ui/" + s.Cap;
        }
        string first = keyName.Split('+', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return _capArt.GetValueOrDefault(first, "");
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
