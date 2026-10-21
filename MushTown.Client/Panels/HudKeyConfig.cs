using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The key settings window: the official KeyConfig art. The official grid
// under the keyboard holds the game actions: the player presses an action
// in its cell, drags it onto a key and saves. The official keyboard shows
// the key letters, or the icon of the action a key has.
// The window has two pages: the custom key bindings and the quick slots.
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

    // The filter names and the category of every action icon
    // (assets/ui/keys_filter.json).
    public sealed class FilterFile
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
    private static readonly (HotkeyAction Action, int Icon)[] Actions =
    {
        (HotkeyAction.Inventory, 1),
        (HotkeyAction.Stat, 2),
        (HotkeyAction.Skill, 3),
        (HotkeyAction.Quest, 8),
        (HotkeyAction.Minimap, 7),
        (HotkeyAction.WorldMap, 5),
        (HotkeyAction.Settings, 46),
    };

    // The quick slot items: the HUD quick slot cells. The action is the
    // hotkey the item opens, the fixed key is the one it always has.
    private static readonly (string File, string Name, HotkeyAction? Action, string Fixed)[] QuickSlots =
    {
        ("hud_key_ctrl.png", "Attack", null, "Ctrl"),
        ("hud_key_alt.png", "Jump", null, "Alt"),
        ("hud_key_i.png", "Items", HotkeyAction.Inventory, ""),
        ("hud_key_c.png", "Stats", HotkeyAction.Stat, ""),
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

    // The quick slots page and the quick slot item that waits for a key.
    private bool _quickSlots;
    private int _qslot = -1;
    private KeyboardState _keys; // the keys last frame

    public override Rectangle Rect() => _quickSlots
        ? new Rectangle(Pos.X, Pos.Y, _lay.Get("qsW", 660), _lay.Get("qsH", 735))
        : new Rectangle(Pos.X, Pos.Y, _lay.Get("w", 660), _lay.Get("h", 624));

    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 18));

    public override Point LayoutPos() => _quickSlots
        ? new Point(_lay.Get("qsX", 353), _lay.Get("qsY", 33))
        : new Point(_lay.Get("x", 353), _lay.Get("y", 72));

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

    // One official quick slot cell (the quick slots page).
    private Rectangle QSlotRect(int index)
    {
        int cols = _lay.Get("qsCols", 16);
        int x = _lay.Get("qsCellX", 52) + (index % cols) * _lay.Get("qsCellPitchX", 36);
        int y = _lay.Get("qsCellY", 572) + (index / cols) * _lay.Get("qsCellPitchY", 40);
        return new Rectangle(Pos.X + x, Pos.Y + y, _lay.Get("qsCellW", 30), _lay.Get("qsCellH", 30));
    }

    // Open the window: the draft starts from the saved bindings.
    public void Open()
    {
        _draft.Clear();
        foreach (var (action, _) in Actions) _draft[action] = _game.HotkeyKey(action);
        _held = null;
        _game.ShowWindow(HotkeyAction.KeyConfig);
    }

    // Open the window on the quick slots page (the shot mode).
    public void OpenQuickSlots()
    {
        Open();
        _quickSlots = true;
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
        _qslot = -1;
    }

    public override void Build()
    {
        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        if (_quickSlots) BuildQuickSlots(root);
        else BuildCustom(root);
        Scene.Add(root);
    }

    // The custom page: the keyboard, the action grid and the buttons.
    private void BuildCustom(GameObject root)
    {
        _game.LoadTexture("ui/keys_bg.png");
        _game.LoadTexture("ui/keys_kb.png");
        _game.LoadTexture("ui/hud_statx.png");
        _game.LoadTexture("ui/keys_custom_p.png");
        foreach (string stem in new[] { "ui/keys_save", "ui/keys_reset", "ui/keys_cancel", "ui/keys_copy", "ui/keys_apply", "ui/keys_skill", "ui/keys_qslot", "ui/keys_combo", "ui/keys_pg1", "ui/keys_pg2", "ui/keys_pg3" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture(stem + state);
        foreach (var s in Spots.Keys)
            if (s.Cap != "") _game.LoadTexture("ui/" + s.Cap);
        foreach (var a in Actions) _game.LoadTexture($"ui/keys_icon_{a.Icon}.png");
        LoadComboBox();

        // The draft starts from the saved bindings (a window built without
        // Open, the shot mode).
        if (_draft.Count == 0)
            foreach (var (action, _) in Actions) _draft[action] = _game.HotkeyKey(action);

        root.Add(new Picture("ui/keys_bg.png"));
        root.Add(new Picture("ui/keys_kb.png", _lay.Get("kbX", 20), _lay.Get("kbY", 100)));

        // The keys: the official letter, or the icon of the action on it.
        foreach (var spot in Spots.Keys)
        {
            var s = spot;
            var o = new GameObject();
            o.Add(new Rect(KeyRect(s)));
            o.Add(new Invoke(gfx => DrawKey(gfx, s)));
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
            o.Add(new Invoke(gfx => DrawCell(gfx, a, index)));
            root.AddChild(o);
        }

        BuildFilter(root);

        // The Custom tab: the window has only this page, so the tab is the
        // current one. It is a plain picture in the pressed look: no click,
        // no hover.
        root.Add(new Picture("ui/keys_custom_p.png", _lay.Get("customX", 19), _lay.Get("customY", 66)));

        // The presets: copy the draft into the slot, apply the slot.
        root.AddChild(ArtButton("ui/keys_copy", _lay.Get("copyX", 380), _lay.Get("copyY", 73), 67, 19, Copy));
        root.AddChild(ArtButton("ui/keys_apply", _lay.Get("applyX", 573), _lay.Get("applyY", 73), 67, 19, Apply));
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            string stem = $"ui/keys_pg{i + 1}";
            int x = _lay.Get($"pg{i + 1}X", 505 + 20 * i), y = _lay.Get($"pg{i + 1}Y", 76);
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, 12, 12)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _slot = slot });
            // The picked slot is drawn pressed, so the player sees it.
            o.Add(new Invoke(gfx =>
            {
                string art = _slot == slot ? stem + "_p.png"
                    : gfx.Hovered == o ? (gfx.Pressed ? stem + "_p.png" : stem + "_h.png")
                    : stem + "_n.png";
                gfx.Sb.Draw(gfx.Tex(art), new Vector2(Pos.X + x, Pos.Y + y), Color.White);
            }));
            root.AddChild(o);
        }

        // The bottom buttons: combo key, quick slots, reset, skill, cancel, save.
        root.AddChild(ComboButton());
        root.AddChild(ArtButton("ui/keys_qslot", _lay.Get("qslotX", 98), _lay.Get("qslotY", 568), 76, 27, ShowQuickSlots));
        root.AddChild(ArtButton("ui/keys_reset", _lay.Get("resetX", 176), _lay.Get("resetY", 568), 67, 27, Default));
        root.AddChild(ArtButton("ui/keys_skill", _lay.Get("skillX", 245), _lay.Get("skillY", 568), 67, 27, OpenSkill));
        root.AddChild(ArtButton("ui/keys_cancel", _lay.Get("cancelX", 474), _lay.Get("cancelY", 568), 87, 27, Cancel));
        root.AddChild(ArtButton("ui/keys_save", _lay.Get("saveX", 562), _lay.Get("saveY", 568), 78, 27, Save));

        BuildTitleAndClose(root, "ui/hud_statx.png", _lay.Get("closeX", 636), _lay.Get("closeY", 4), 11);
        BuildDrag(root);
    }

    // The quick slots page: the official second page with the quick slot
    // cells under the keyboard.
    private void BuildQuickSlots(GameObject root)
    {
        _game.LoadTexture("ui/keys_qs_bg.png");
        _game.LoadTexture("ui/keys_qsclose.png");
        foreach (string stem in new[] { "ui/keys_qssave", "ui/keys_qscancel", "ui/keys_custom" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture(stem + state);
        foreach (var q in QuickSlots) _game.LoadTexture("ui/" + q.File);
        foreach (var s in Spots.Keys)
            if (s.Cap != "") _game.LoadTexture("ui/" + s.Cap);

        root.Add(new Picture("ui/keys_qs_bg.png"));

        // Our quick slot items sit in the first cells. The other cells stay
        // empty: we have nothing for them.
        for (int i = 0; i < QuickSlots.Length; i++)
        {
            var q = QuickSlots[i];
            int index = i;
            var o = new GameObject();
            o.Add(new Rect(QSlotRect(index)));
            // Only the items with a hotkey can be rebound.
            if (q.Action != null) o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => PickQuickSlot(index) });
            o.Add(new Invoke(gfx => DrawQuickSlot(gfx, q, index)));
            root.AddChild(o);
        }

        // Back to the custom page, cancel and save.
        root.AddChild(ArtButton("ui/keys_custom", _lay.Get("qsCustomX", 280), _lay.Get("qsCustomY", 33), 103, 27, ShowCustom));
        root.AddChild(ArtButton("ui/keys_qscancel", _lay.Get("qsCancelX", 474), _lay.Get("qsCancelY", 681), 87, 27, Cancel));
        root.AddChild(ArtButton("ui/keys_qssave", _lay.Get("qsSaveX", 562), _lay.Get("qsSaveY", 681), 78, 27, Save));

        BuildTitleAndClose(root, "ui/keys_qsclose.png", _lay.Get("qsCloseX", 643), _lay.Get("qsCloseY", 4), 13);
        BuildDrag(root);
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

    // The held action rides the mouse.
    private void BuildDrag(GameObject root)
    {
        var drag = new GameObject();
        drag.Add(new Invoke(DrawHeld));
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
        o.Add(new Invoke(gfx =>
        {
            string art = _comboMode ? "ui/keys_combo_p.png"
                : gfx.Hovered == o ? "ui/keys_combo_h.png"
                : "ui/keys_combo_n.png";
            gfx.Sb.Draw(gfx.Tex(art), new Vector2(Pos.X + x, Pos.Y + y), Color.White);
        }));
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
        o.Add(new Invoke(gfx => DrawComboBox(gfx, x, y, w, h)));
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
            item.Add(new Invoke(gfx =>
            {
                if (gfx.Hovered == item)
                    gfx.Sb.Draw(gfx.Pixel, new Rectangle(Pos.X + x, Pos.Y + itemY, listW, itemH), new Color(167, 167, 167));
                gfx.Sb.Draw(gfx.Ui.GetSmall(FilterName(index)), new Vector2(Pos.X + x + _lay.Get("filterTextX", 7), Pos.Y + itemY + _lay.Get("filterTextY", 5)), Color.Black);
            }));
            root.AddChild(item);
        }
    }

    // The combo box: the left, middle and right pieces of the official art.
    private void DrawComboBox(UiGfx gfx, int x, int y, int w, int h)
    {
        string set = _filterOpen ? "p" : "n";
        gfx.Sb.Draw(gfx.Tex($"ui/keys_cbox_{set}0.png"), new Vector2(Pos.X + x, Pos.Y + y), Color.White);
        gfx.Sb.Draw(gfx.Tex($"ui/keys_cbox_{set}2.png"), new Vector2(Pos.X + x + w - 31, Pos.Y + y), Color.White);
        // the middle piece stretches between the two ends
        gfx.Sb.Draw(gfx.Tex($"ui/keys_cbox_{set}1.png"), new Rectangle(Pos.X + x + 3, Pos.Y + y, w - 34, h), Color.White);
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

    // One key: the official letter, or the icon of the action on the key.
    private void DrawKey(UiGfx gfx, Spot s)
    {
        var r = KeyRect(s);
        var action = ActionFor(KeyName(s.Code));
        if (action != null)
        {
            var icon = gfx.Tex($"ui/keys_icon_{IconOf(action.Value)}.png");
            gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
            return;
        }
        if (s.Cap == "") return;
        var cap = gfx.Tex("ui/" + s.Cap);
        gfx.Sb.Draw(cap, new Vector2(r.X + (r.Width - cap.Width) / 2, r.Y + (r.Height - cap.Height) / 2), Color.White);
    }

    // The action in its official grid cell. A combo action shows both keys.
    private void DrawCell(UiGfx gfx, (HotkeyAction Action, int Icon) a, int index)
    {
        var r = CellRect(index);
        var icon = gfx.Tex($"ui/keys_icon_{a.Icon}.png");
        gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
        var parts = Keys(a.Action);
        if (parts.Length < 2) return;
        int x = r.X + 2;
        foreach (var part in parts)
        {
            string cap = CapArt(part);
            if (cap == "") continue;
            var tex = gfx.Tex(cap);
            gfx.Sb.Draw(tex, new Vector2(x, r.Y + 2), Color.White);
            x += tex.Width + 1;
        }
    }

    // One quick slot item: the official item art and the key it has.
    private void DrawQuickSlot(UiGfx gfx, (string File, string Name, HotkeyAction? Action, string Fixed) q, int index)
    {
        var r = QSlotRect(index);
        var art = gfx.Tex("ui/" + q.File);
        gfx.Sb.Draw(art, new Vector2(r.X + (r.Width - art.Width) / 2, r.Y + (r.Height - art.Height) / 2), Color.White);
        if (q.Action == null) return;
        string cap = CapArt(_draft.GetValueOrDefault(q.Action.Value, ""));
        if (cap == "") return;
        var tex = gfx.Tex(cap);
        gfx.Sb.Draw(tex, new Vector2(r.X + (r.Width - tex.Width) / 2, r.Y + (r.Height - tex.Height) / 2), Color.White);
    }

    // The picked up action rides the mouse. Letting go of the mouse drops it
    // on the key under the cursor.
    private void DrawHeld(UiGfx gfx)
    {
        // A quick slot item that waits for a key takes the key press.
        if (_qslot >= 0) PickQuickSlotKey();

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
        if (Math.Abs(gx - _pickAt.X) + Math.Abs(gy - _pickAt.Y) < 8) return; // a click, not a drag
        _draft[action] = "";
    }

    // The combo key mode: the action keeps its key and gets a second one
    // ("I+K"). The action then fires on either key.
    private void AddKey(HotkeyAction action, string key)
    {
        var parts = Keys(action);
        if (parts.Length == 0) { Bind(action, key); return; }
        if (parts.Contains(key)) return;
        TakeAway(key, action);
        if (parts.Length == 1) parts = new[] { parts[0], key };
        else parts[1] = key;
        _draft[action] = string.Join("+", parts);
    }

    // The action takes the key. The key is taken away from the other actions.
    private void Bind(HotkeyAction action, string key)
    {
        if (key == "") return;
        TakeAway(key, action);
        _draft[action] = key;
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

    // Save: the game writes the whole map and saves it once. The KeyConfig
    // action has no cell in the window, so its current key is kept.
    private void Save()
    {
        var map = new Dictionary<HotkeyAction, string>(_draft)
        {
            [HotkeyAction.KeyConfig] = _game.HotkeyKey(HotkeyAction.KeyConfig),
        };
        _game.SetHotkeys(map);
    }

    // Default: the official bindings come back (the button art says Default).
    private void Default()
    {
        _draft.Clear();
        foreach (var (action, key) in _game.HotkeyDefaults())
            if (action != HotkeyAction.KeyConfig) _draft[action] = key;
    }

    // Cancel: the draft goes back to the saved bindings (the changes are lost).
    private void Cancel()
    {
        _draft.Clear();
        foreach (var (action, _) in Actions) _draft[action] = _game.HotkeyKey(action);
        _held = null;
    }

    // Copy: the draft goes into the picked preset slot.
    private void Copy() => _game.SaveHotkeyPreset(_slot, _draft);

    // Apply: the picked preset slot comes into the draft (the player can
    // still press Save). An empty slot has no bindings, so the official
    // client ignores it: the draft keeps its keys.
    private void Apply()
    {
        var slot = _game.HotkeyPreset(_slot);
        if (slot.Count == 0) return;
        _draft.Clear();
        foreach (var (action, _) in Actions) _draft[action] = slot.GetValueOrDefault(action, "");
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

    // The quick slots page.
    private void ShowQuickSlots()
    {
        _quickSlots = true;
        Pos = LayoutPos();
    }

    // Back to the custom page.
    private void ShowCustom()
    {
        _quickSlots = false;
        Pos = LayoutPos();
    }

    // The skill button: the skill window opens.
    private void OpenSkill()
    {
        _game.OpenSkillWindow();
        Show = false; // the skill window is under the key window
    }

    // The player picks a quick slot item: it waits for a key.
    private void PickQuickSlot(int index) => _qslot = index;

    // The quick slot item takes the key the player pressed.
    private void PickQuickSlotKey()
    {
        var kb = Keyboard.GetState();
        foreach (var key in kb.GetPressedKeys())
        {
            if (_keys.IsKeyDown(key)) continue;
            _keys = kb;
            var q = QuickSlots[_qslot];
            if (q.Action != null) Bind(q.Action.Value, key.ToString());
            _qslot = -1;
            return;
        }
        _keys = kb;
    }

    // The action that has this key.
    private HotkeyAction? ActionFor(string key)
    {
        if (key == "") return null;
        foreach (var a in Actions)
            if (Keys(a.Action).Contains(key)) return a.Action;
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

    // The cap art of a key name ("ui/keys_cap_<scancode>.png"). A combo
    // ("I+K") shows the cap of its first key.
    private static Dictionary<string, string>? _capArt;
    public static string CapArt(string keyName)
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
