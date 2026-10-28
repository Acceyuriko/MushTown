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

    // The game actions in the grid: every icon of the official KeyConfig art,
    // in its icon order. An action with no window behind it is still here on
    // purpose: pressing its key does nothing, and the pool then tells the
    // player (and us) what is not built yet. GuideCard, PartySearch and
    // PartyAsk have no icon of their own, so they are not in the pool.
    private static readonly (HotkeyAction Action, int Icon)[] Actions =
    {
        (HotkeyAction.Equip, 0),
        (HotkeyAction.Inventory, 1),
        (HotkeyAction.Stat, 2),
        (HotkeyAction.Skill, 3),
        (HotkeyAction.Friends, 4),
        (HotkeyAction.WorldMap, 5),
        (HotkeyAction.MapleChat, 6),
        (HotkeyAction.Minimap, 7),
        (HotkeyAction.Quest, 8),
        (HotkeyAction.KeyConfig, 9),
        (HotkeyAction.ToAll, 10),
        (HotkeyAction.Whisper, 11),
        (HotkeyAction.ToParty, 12),
        (HotkeyAction.ToBuddy, 13),
        (HotkeyAction.QuickSlots, 15),
        (HotkeyAction.Chat, 16),
        (HotkeyAction.Guild, 17),
        (HotkeyAction.ToGuild, 18),
        (HotkeyAction.Party, 19),
        (HotkeyAction.QuestHelper, 20),
        (HotkeyAction.MapleNews, 21),
        (HotkeyAction.CashShop, 22),
        (HotkeyAction.ToAlliance, 23),
        (HotkeyAction.Legion, 25),
        (HotkeyAction.Medals, 26),
        (HotkeyAction.Bosses, 27),
        (HotkeyAction.Profession, 29),
        (HotkeyAction.Events, 31),
        (HotkeyAction.BattleAnalysis, 35),
        (HotkeyAction.Guide, 39),
        (HotkeyAction.EnhanceEquip, 41),
        (HotkeyAction.MonsterCollection, 42),
        (HotkeyAction.SoulWeapon, 43),
        (HotkeyAction.ChangeChannel, 45),
        (HotkeyAction.Menu, 46),
        (HotkeyAction.Screenshot, 47),
        (HotkeyAction.PictureMode, 48),
        (HotkeyAction.Achievements, 49),
        (HotkeyAction.Pickup, 50),
        (HotkeyAction.Sit, 51),
        (HotkeyAction.Attack, 52),
        (HotkeyAction.Jump, 53),
        (HotkeyAction.Talk, 54),
        (HotkeyAction.ToTown, 55),
        (HotkeyAction.GoHome, 56),
        (HotkeyAction.Mup, 57),
        (HotkeyAction.StorageRoom, 58),
        (HotkeyAction.QuickMove, 59),
        (HotkeyAction.Archive, 60),
        (HotkeyAction.SoulCollection, 61),
        (HotkeyAction.Emoticon1, 100),
        (HotkeyAction.Emoticon2, 101),
        (HotkeyAction.Emoticon3, 102),
        (HotkeyAction.Emoticon4, 103),
        (HotkeyAction.Emoticon5, 104),
        (HotkeyAction.Emoticon6, 105),
        (HotkeyAction.Emoticon7, 106),
        (HotkeyAction.MapleStorage, 200),
        (HotkeyAction.SafeMode, 201),
        (HotkeyAction.MuteGame, 202),
        (HotkeyAction.Emoticons, 203),
        (HotkeyAction.KeySettings1, 204),
        (HotkeyAction.KeySettings2, 205),
        (HotkeyAction.KeySettings3, 206),
        (HotkeyAction.ComboKey, 207),
        (HotkeyAction.SolErda, 210),
        (HotkeyAction.CharPreset, 212),
        (HotkeyAction.SwapPreset, 213),
        (HotkeyAction.Planner, 214),
        (HotkeyAction.Sequence, 215),
        (HotkeyAction.Decorations, 216),
        (HotkeyAction.AutoHarvest, 217),
        (HotkeyAction.BuffShopper, 218),
        (HotkeyAction.Familiar, 219),
        (HotkeyAction.ToSpouse, 220),
        (HotkeyAction.TraceResto, 221),
        (HotkeyAction.MapleRelay, 300),
    };

    // The pending bindings, the action the player holds and the picked
    // preset slot.
    private readonly Dictionary<HotkeyAction, string> _draft = new();
    private readonly Dictionary<int, string> _draftSkills = new(); // the skills the draft bound (from the skill window)
    private HotkeyAction? _held;
    private Point _pickAt; // where the action was picked up
    private int _slot;

    // Key -> what sits on it, built once per Build: the 73 key caps read it
    // every frame, and walking the draft 73 times would split a string 73
    // times a frame for every action.
    private readonly Dictionary<string, HotkeyAction> _keyAction = new();
    private readonly Dictionary<string, int> _keySkill = new();

    private const int DragSlop = 8; // a press that moves less than this is a click, not a drag

    // Default was pressed: the official table has no item keys either, so Save
    // drops them (the draft only carries actions and skills).
    private bool _dropItems;

    // The filter dropdown: the picked filter and the open list. Open() puts
    // the filter back to Not Added, so the window shows what is still free.
    private int _filter;
    private bool _filterOpen;

    // The copy dialog: open, and the source preset it points at.
    private bool _copyOpen;
    private int _copyFrom;

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
        _filter = 0; // Not Added: the window always opens on the actions that are still free
        Pos = LayoutPos();
        _game.ShowWindow(HotkeyAction.KeyConfig);
    }

    // The pool actions, read back from the picked preset.
    private void ReloadDraft()
    {
        var preset = _game.HotkeyPreset(_slot);
        _draft.Clear();
        foreach (var (action, _) in Actions) _draft[action] = preset.GetValueOrDefault(action, "");
        _draftSkills.Clear();
        foreach (var (id, key) in _game.HotkeySkills(_slot)) _draftSkills[id] = key;
        _dropItems = false; // the draft is the preset again: nothing left over from a Default
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
        _draftSkills.Clear();
        _held = null;
        _game.ClearHeldSkill(); // a skill picked in the skill window would follow the mouse with no window to drop it on
        _filterOpen = false;
        _comboMode = false;
        _copyOpen = false;
    }

    public override void Build()
    {
        Host.Clear();
        RebuildLookup();
        var root = new GameObject();
        root.Add(new Rect(Rect()));

        _game.LoadTexture("ui/keys_bg.png");
        _game.LoadTexture("ui/keys_kb.png");
        _game.LoadTexture("ui/hud_statx.png");
        _game.LoadTexture("ui/keys_custom_p.png");
        foreach (string stem in new[] { "ui/keys_save", "ui/keys_reset", "ui/keys_cancel", "ui/keys_copy", "ui/keys_apply", "ui/keys_skill", "ui/keys_qslot", "ui/keys_combo" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture(stem + state);
        foreach (string stem in new[] { "ui/keys_pg1", "ui/keys_pg2", "ui/keys_pg3" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png", "_c.png" })
                _game.LoadTexture(stem + state);
        foreach (var s in Spots.Keys)
            if (s.Cap != "") _game.LoadTexture("ui/" + s.Cap);
        foreach (var a in Actions) _game.LoadTexture(IconTex(a.Action));
        if (_copyOpen) LoadCopyDialog(); // the dialog art is only needed while it shows
        LoadComboBox();

        // Open fills the draft; the tree reads it when drawing.
        root.Add(new Picture("ui/keys_bg.png"));
        root.Add(new Picture("ui/keys_kb.png", _lay.Get("kbX", 20), _lay.Get("kbY", 100)));

        // The keys: the official letter, or the icon of the action on it. A
        // key with an action on it can be picked up again, so an action that
        // left the pool is not stuck on the keyboard forever.
        foreach (var spot in Spots.Keys)
        {
            var o = new GameObject();
            o.Add(new Rect(KeyRect(spot)));
            o.Add(new KeyCap(this, spot));
            int code = spot.Code;
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => PickKey(code) });
            root.AddChild(o);
        }

        // The actions in the official grid: press one, drag it onto a key.
        // The filter picks the actions that show.
        for (int i = 0; i < Actions.Length; i++)
        {
            var a = Actions[i];
            int index = i;
            if (!FilterShows(a.Action, a.Icon)) continue;
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

        // The preset buttons: Copy opens the source dialog, Apply makes the
        // picked preset the live one. No character (--host/--join): there is
        // no preset to write, so the buttons do not show.
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
            o.Add(new Image(stem, () => _slot == slot, "_c")); // the picked page is lit green by the checked art
            root.AddChild(o);
        }

        // The bottom buttons: combo key, quick slots, reset, skill, cancel,
        // save. The quick slots page is gone, so that button is a plain
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
        if (_copyOpen) BuildCopyDialog(root); // the dialog sits above everything, the drop icon included
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
        bg.Add(new Button { Kind = ButtonKind.Click, OnClick = () => { _filterOpen = false; Build(); } }); // a click on the list but not on an item closes it
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

    // The copy dialog art: the background, the two buttons and the three
    // preset picks (the picked one shows its checked look).
    private void LoadCopyDialog()
    {
        _game.LoadTexture("ui/keys_pcopy_bg.png");
        foreach (string stem in new[] { "ui/keys_pcopy_copy", "ui/keys_pcopy_cancel", "ui/keys_pcopy_pg1", "ui/keys_pcopy_pg2", "ui/keys_pcopy_pg3" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture(stem + state);
        for (int page = 1; page <= 3; page++) _game.LoadTexture($"ui/keys_pcopy_pg{page}_c.png");
    }

    // The copy dialog: the official art spells the question, so the player
    // only picks the source preset and presses Copy. The dialog is modal:
    // a click outside it lands on the catcher and does nothing.
    private void BuildCopyDialog(GameObject root)
    {
        int x = _lay.Get("pcX", 164), y = _lay.Get("pcY", 184);
        int w = _lay.Get("pcW", 332), h = _lay.Get("pcH", 256);

        // The whole screen, not just the window: the dialog is modal, a click
        // on the HUD behind it must not land.
        var block = new GameObject();
        block.Add(new Rect(new Rectangle(0, 0, MushTownGame.GameWidth, MushTownGame.GameHeight)));
        block.Add(new Button { Kind = ButtonKind.Silent });
        root.AddChild(block);

        var dlg = new GameObject();
        dlg.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, w, h)));
        dlg.Add(new Picture("ui/keys_pcopy_bg.png"));
        root.AddChild(dlg);

        for (int i = 0; i < 3; i++)
        {
            int from = i;
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(
                Pos.X + x + _lay.Get($"pcPg{i + 1}X", 207 + 33 * i), Pos.Y + y + _lay.Get("pcPgY", 152),
                _lay.Get("pcPgW", 18), _lay.Get("pcPgH", 18))));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => { _copyFrom = from; Build(); } });
            o.Add(new Image($"ui/keys_pcopy_pg{i + 1}", () => _copyFrom == from, "_c"));
            dlg.AddChild(o);
        }

        var now = new GameObject(); // the preset the player is on: the art leaves a blank for it
        now.Add(new Rect(new Rectangle(Pos.X + x + _lay.Get("pcNowX", 216), Pos.Y + y + _lay.Get("pcNowY", 82),
            _lay.Get("pcNowW", 20), _lay.Get("pcNowH", 14))));
        now.Add(new Label((_slot + 1).ToString(), 0, 0));
        dlg.AddChild(now);

        dlg.AddChild(DialogButton("ui/keys_pcopy_copy", _lay.Get("pcCopyX", 81), _lay.Get("pcCopyY", 207), CopyNow));
        dlg.AddChild(DialogButton("ui/keys_pcopy_cancel", _lay.Get("pcCancelX", 169), _lay.Get("pcCancelY", 207), CloseCopy));
    }

    // One button of the copy dialog, placed inside the dialog box.
    private GameObject DialogButton(string stem, int dx, int dy, Action onClick)
    {
        int x = _lay.Get("pcX", 164) + dx, y = _lay.Get("pcY", 184) + dy;
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, _lay.Get("pcBtnW", 83), _lay.Get("pcBtnH", 26))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Image(stem));
        return o;
    }

    // The filter names (the official combo box items).
    private static string FilterName(int index) =>
        index >= 0 && index < Filters.Filters.Count ? Filters.Filters[index] : "";

    // The filter picks the actions that show: "Not Added" shows the actions
    // with no key, the other filters show one category of actions.
    private bool FilterShows(HotkeyAction action, int icon)
    {
        // A bound action leaves the pool: its cell stays where it is and goes
        // blank, so the grid never shuffles under the mouse.
        if (_draft.GetValueOrDefault(action, "") != "") return false;
        if (_filter == 0) return true;
        return Filters.Categories.GetValueOrDefault(icon, 0) + 1 == _filter;
    }

    // One key of the official keyboard: the official letter, or the icon of
    // the action or skill the draft binds to that key.
    private sealed class KeyCap : Component, IDrawable
    {
        private readonly KeyConfigWindow _w;
        private readonly Spot _spot;
        public KeyCap(KeyConfigWindow w, Spot spot) { _w = w; _spot = spot; }
        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Box;
            string key = KeyName(_spot.Code);
            if (_w._keyAction.TryGetValue(key, out var action))
            {
                DrawCentered(gfx, r, IconTex(action));
                return;
            }
            if (_w._keySkill.TryGetValue(key, out int skill))
            {
                DrawCentered(gfx, r, _w._game.SkillIconTex(skill));
                return;
            }
            if (_spot.Cap == "") return;
            DrawCentered(gfx, r, "ui/" + _spot.Cap);
        }

        private static void DrawCentered(UiGfx gfx, Rectangle r, string tex)
        {
            if (tex == "") return;
            var icon = gfx.Tex(tex);
            gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
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

    // The action the player picked up rides the cursor (a skill picked up in
    // the skill window rides it too, and the skill window draws that one).
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

    // The HUD passes its own mouse edge: letting go drops the held action or
    // the held skill on the key under the cursor.
    public void UpdateDrag(bool held, int gx, int gy)
    {
        if (held) return;
        if (_game.HeldSkill != 0)
        {
            int skill = _game.HeldSkill;
            _game.ClearHeldSkill();
            DropSkill(skill, gx, gy);
            return;
        }
        if (_held is not { } action) return;
        Drop(action, gx, gy);
    }

    // The player presses an action cell: it sticks to the mouse.
    private void Pick(HotkeyAction action)
    {
        _held = action;
        _pickAt = Scene.LastClick;
    }

    // The player presses a key that has an action on it: the action comes off
    // the keyboard and follows the mouse, so a bound action is not stuck.
    private void PickKey(int code)
    {
        if (ActionFor(KeyName(code)) is not { } action) return;
        Pick(action);
    }

    // The player lets go: the action takes the key under the mouse, or loses
    // its key when it is let go on the action pool. Anywhere else (the title
    // bar, a button, the gap between two keys) changes nothing.
    private void Drop(HotkeyAction action, int gx, int gy)
    {
        _held = null;
        if (Math.Abs(gx - _pickAt.X) + Math.Abs(gy - _pickAt.Y) < DragSlop) return; // a click, not a drag
        foreach (var s in Spots.Keys)
        {
            if (!KeyRect(s).Contains(gx, gy)) continue;
            if (!OnKeyWindow(gx, gy)) return; // another window covers that key
            string key = KeyName(s.Code);
            if (_comboMode) AddKey(action, key);
            else Bind(action, key);
            Build(); // the pool hides the bound action: its cells are built from the draft
            return;
        }
        if (_comboMode) return;
        // Not the pool, or the open filter list covering it: the binding stays.
        if (!PoolRect().Contains(gx, gy) || _filterOpen) return;
        _draft[action] = ""; // let go on the pool: the action loses its key
        Build();
    }

    // The black action pool under the keyboard: the cells the draft binds.
    private Rectangle PoolRect()
    {
        int cols = _lay.Get("gridCols", 17);
        int rows = (Actions.Length + cols - 1) / cols;
        return new Rectangle(
            Pos.X + _lay.Get("gridX", 25), Pos.Y + _lay.Get("gridY", 374),
            cols * _lay.Get("cellPitchX", 36), rows * _lay.Get("cellPitchY", 36));
    }

    // True when the topmost object under the point belongs to the key window:
    // a key hidden behind another window must not take a drop.
    private bool OnKeyWindow(int gx, int gy)
    {
        for (var o = Scene.TopAt(gx, gy); o != null; o = o.Parent)
            if (o == Host) return true;
        return false;
    }

    // A skill from the skill window lands on one key of the keyboard. It does
    // not take a combo (the action combo mode is for actions only) and a drop
    // off the keyboard leaves the draft alone: the skill pool is not here. A
    // plain click on the skill row is not a drop, and a key another window
    // covers does not take it either.
    private void DropSkill(int skill, int gx, int gy)
    {
        var at = _game.HeldSkillAt;
        if (Math.Abs(gx - at.X) + Math.Abs(gy - at.Y) < DragSlop) return; // a click on the skill row, not a drag
        foreach (var s in Spots.Keys)
        {
            if (!KeyRect(s).Contains(gx, gy)) continue;
            if (!OnKeyWindow(gx, gy)) return; // the skill window covers that key
            string key = KeyName(s.Code);
            if (!InputController.TryParseKeys(key, out _)) return;
            TakeAway(key, null);
            _draftSkills[skill] = key;
            Build();
            return;
        }
    }

    // The combo key mode: the action keeps its key and gets a second one
    // ("I+K"). The action then fires on either key.
    private void AddKey(HotkeyAction action, string key)
    {
        if (!InputController.TryParseKeys(key, out _)) return;
        var parts = Keys(action);
        if (parts.Length == 0) { Bind(action, key); return; }
        if (parts.Contains(key)) return;
        TakeAway(key, action);
        if (parts.Length == 1) parts = new[] { parts[0], key };
        else parts[1] = key;
        _draft[action] = string.Join("+", parts);
    }

    // The action takes the key. The key is taken away from the other actions;
    // a key that a skill or an item holds leaves it when the draft is saved.
    private void Bind(HotkeyAction action, string key)
    {
        if (!InputController.TryParseKeys(key, out _)) return; // Esc/Enter/Tab/arrows are not bindable
        TakeAway(key, action);
        _draft[action] = key;
    }

    // The key goes away from the other actions and the bound skills (part by
    // part). A null keep means the new owner is a skill.
    private void TakeAway(string key, HotkeyAction? keep)
    {
        foreach (var other in _draft.Keys.ToList())
        {
            if (other == keep) continue;
            var parts = Keys(other);
            if (!parts.Contains(key)) continue;
            _draft[other] = string.Join("+", parts.Where(p => p != key));
        }
        foreach (var id in _draftSkills.Keys.ToList())
        {
            var parts = _draftSkills[id].Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (!parts.Contains(key)) continue;
            _draftSkills[id] = string.Join("+", parts.Where(p => p != key));
        }
    }

    // The keys of an action: one key, or the two of a combo.
    private string[] Keys(HotkeyAction action) =>
        _draft.GetValueOrDefault(action, "").Split('+', StringSplitOptions.RemoveEmptyEntries);

    // Save: the draft goes into the picked preset.
    private void Save()
    {
        _game.SaveHotkeyPreset(_slot, _draft, _draftSkills, _dropItems);
        ReloadDraft();
        Build();
    }

    // Default: the official bindings come back (the button art says Default).
    private void Default()
    {
        _draft.Clear();
        // The defaults have no skill keys. Blank every skill the draft knows
        // instead of dropping them: Save writes the blank out, so a saved
        // preset really loses its skill keys. Items are not in the draft at
        // all, so Save drops them by kind.
        foreach (var id in _draftSkills.Keys.ToList()) _draftSkills[id] = "";
        _dropItems = true;
        var defaults = KeyBindings.Defaults();
        foreach (var (action, _) in Actions) _draft[action] = defaults.GetValueOrDefault(action, ""); // the pool only: the sub-windows keep whatever they have
        Build();
    }

    // Cancel: the draft goes back to the picked preset's bindings.
    private void Cancel()
    {
        ReloadDraft();
        _held = null;
        Build();
    }

    // Copy: the official dialog asks which preset the current one takes its
    // bindings from. The picked preset starts as the source, so a Copy press
    // that was not followed by a pick changes nothing.
    private void Copy()
    {
        _copyOpen = true;
        _copyFrom = _slot;
        Build();
    }

    // Copy in the dialog: the source preset covers the picked one.
    private void CopyNow()
    {
        if (_copyFrom != _slot) _game.CopyHotkeyPreset(_copyFrom, _slot); // copying a preset onto itself is a no-op
        _copyOpen = false;
        ReloadDraft();
        Build();
    }

    // Cancel in the dialog: nothing changes.
    private void CloseCopy()
    {
        _copyOpen = false;
        Build();
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

    // The skill button: the skill window opens above this one, so the player
    // drags a skill row onto a key. The key window stays open.
    private void OpenSkill() => _game.OpenSkillWindow();

    // The action that has this key (the pool only: the sub-windows have no
    // cell to pick from).
    private HotkeyAction? ActionFor(string key) => _keyAction.TryGetValue(key, out var action) ? action : null;

    // Key -> action and key -> skill, rebuilt whenever the tree is: the key
    // caps read it every frame.
    private void RebuildLookup()
    {
        _keyAction.Clear();
        foreach (var (action, keys) in _draft)
            foreach (var key in keys.Split('+', StringSplitOptions.RemoveEmptyEntries))
                _keyAction.TryAdd(key, action);
        _keySkill.Clear();
        foreach (var (id, keys) in _draftSkills)
            foreach (var key in keys.Split('+', StringSplitOptions.RemoveEmptyEntries))
                _keySkill.TryAdd(key, id);
    }

    // The icon art of an action, "" when the action has no official icon.
    // The HUD quick slot cells draw the same art.
    internal static string IconTex(HotkeyAction action)
    {
        int icon = IconOf(action);
        return icon < 0 ? "" : $"ui/keys_icon_{icon}.png";
    }

    // The official icon of an action; -1 for the actions that are not in the
    // pool (Settings, the sub-windows): they have no cell and no art.
    internal static int IconOf(HotkeyAction action)
    {
        foreach (var a in Actions)
            if (a.Action == action) return a.Icon;
        return -1;
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
