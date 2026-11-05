using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The map window data the exporter wrote: assets/ui/mapui.json.
internal static class MapUi
{
    private static MapUiData? _data;
    private static readonly Dictionary<int, MapNameUi> _names = new();
    private static readonly MapNameUi NoName = new();

    // The json loads once. The lookup is a dictionary: the windows ask for a
    // name every frame.
    public static MapUiData Data
    {
        get
        {
            if (_data == null)
            {
                _data = MushTownGame.LoadJson<MapUiData>(Path.Combine(MushTownGame.AssetsDir, "ui", "mapui.json"));
                foreach (var n in _data.Maps) _names[n.Id] = n;
            }
            return _data;
        }
    }

    // The official names of one map (empty when the map is not in the list).
    public static MapNameUi Name(int id)
    {
        _ = Data;
        return _names.GetValueOrDefault(id) ?? NoName;
    }

}

// The parts the map windows have: the title strip that drags the window
// and the X that closes it.
internal static class MapWindowParts
{
    // The strip along the top of the window: holding it moves the window.
    public static GameObject TitleDrag(UiWindow win, Rectangle rect)
    {
        var o = new GameObject();
        o.Add(new Rect(rect));
        o.Add(new Button { OnClick = win.StartDrag });
        return o;
    }

    // The 11x11 X in the corner of a window or a panel: the caller says what it closes.
    public static GameObject Close(Action close, Rectangle rect)
    {
        var o = new GameObject();
        o.Add(new Rect(rect));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = close });
        o.Add(new Image("ui/map_x"));
        return o;
    }
}

// The minimap window: the official frame, the map picture with the dots, the
// row of buttons, the layout pair and the two panels on its right side. Every
// part is a component; the window measures itself, places them, and builds the
// buttons, the NPC rows and the filter hit boxes.
public sealed class MinimapWindow : UiWindow
{
    private readonly MushTownGame _game;
    private readonly MinimapState _state = new();

    // The frame art fixes these: the side border is 9 wide, the bottom bar 10
    // tall, the buttons 21x21. They live in the layout file with the rest of
    // the numbers (borderX, bottomH, btnSize), the defaults are the art size.
    private int BorderX => Lay.Get("borderX", 9);
    private int BottomH => Lay.Get("bottomH", 10);
    private int BtnSize => Lay.Get("btnSize", 21);

    private int TopH => Lay.Get(_state.Big ? "bigTop" : "smallTop", _state.Big ? 70 : 30);
    private string Frame => _state.Big ? "mini_frame_big" : "mini_frame";
    private int RowH => Lay.Get("listRowH", 18);

    public MinimapWindow(MushTownGame game) : base(HotkeyAction.Minimap) { _game = game; Show = true; } // the minimap is always on screen

    // Esc does not close the minimap: it is always on screen.
    public override void OnClosed() => Show = true;

    // Every number of this window lives in layouts/panel-minimap.json.
    private static PanelLayout Lay => MinimapLay.Lay;
    private Rectangle _box; // measured on every rebuild

    // The box is measured on every rebuild (map change, layout switch, panel
    // open, first show), the frame asks for it every frame.
    public override Rectangle Rect() => _box;

    // The size rule of the reference client (UIMiniMap.cs: the canvas plus the
    // frame borders, floored by the smallest map area and, in the large layout,
    // by the names). Every map sizes itself, nothing is special cased.
    private Rectangle Measure()
    {
        var mini = _game.GameMap.MiniMap;
        int w = BorderX * 2 + Math.Max(Lay.Get("minArtW", 167), mini.ArtW);
        if (_state.Big) w = Math.Max(w, NameRight() + BorderX);
        int h = TopH + BottomH + Math.Max(Lay.Get("minArtH", 50), mini.ArtH);
        return new Rectangle(0, 0, w, h);
    }

    // The right edge the two names reach: the street name starts at streetX,
    // the map name at nameX, and the wider one decides.
    private int NameRight()
    {
        var name = MapUi.Name(_game.GameMap.MapId);
        int street = Lay.Get("streetX", 50), x = Lay.Get("nameX", 50);
        return Math.Max(street + TextWidth(name.Street), x + TextWidth(name.Name));
    }

    // The baked text is the real measure. A build only happens after
    // LoadContent, so the fonts are always there.
    private int TextWidth(string text) => text == "" ? 0 : _game.Ui.GetSmall(text).Width;

    // The window plus the open panels: the rebuild and the drag clamp both use
    // these rects, so a panel move cannot drift between them.
    private (int W, int H) Extent()
    {
        int w = Rect().Width, h = Rect().Height;
        if (_state.NpcList) { w = Math.Max(w, NpcListRect().Right); h = Math.Max(h, NpcListRect().Bottom); }
        if (_state.Filter) { w = Math.Max(w, FilterRect().Right); h = Math.Max(h, FilterRect().Bottom); }
        return (w, h);
    }

    // The side panels must stay on the screen, so a window that has one open
    // cannot hang over the right or bottom edge.
    public override Point ClampPos(Point p)
    {
        if (!HasPanels) return base.ClampPos(p);
        var span = Extent();
        return MushTownGame.ClampBox(p, Rect().Width, 1366 - span.W, 768 - span.H);
    }

    private bool HasPanels => _state.NpcList || _state.Filter;

    public override Rectangle TitleRect() => new(0, 0, Rect().Width, TopH);
    public override Point LayoutPos() => new(Lay.Get("x", 0), Lay.Get("y", 0));

    // The map area inside the frame.
    private Rectangle Well() => new(BorderX, TopH, Rect().Width - BorderX * 2, Rect().Height - TopH - BottomH);

    // The panels stand on the right of the window, one pixel over it: the NPC
    // list art has a see through left column, the filter art does not, and the
    // 1px overlap hides the seam either way.
    private int PanelX => Rect().Width + Lay.Get("panelGap", -1);
    private int PanelY => Lay.Get("panelY", 0);
    private int NpcListH => Lay.Get("listH", 286);
    private int FilterY => _state.NpcList ? PanelY + NpcListH + Lay.Get("panelGap", -1) : PanelY;
    private Rectangle NpcListRect() => new(PanelX, PanelY, Lay.Get("listW", 184), NpcListH);
    private Rectangle FilterRect() => new(PanelX, FilterY, Lay.Get("filterW", 133), Lay.Get("filterH", 286));

    public override void Build()
    {
        _box = Measure();
        Pos = ClampPos(Pos); // a wider layout or an open panel may push it off the screen
        foreach (string piece in new[] { "nw", "n", "ne", "w", "e", "sw", "s", "se" })
        {
            _game.LoadTexture($"ui/mini_frame_{piece}.png");
            _game.LoadTexture($"ui/mini_frame_big_{piece}.png");
        }
        foreach (string name in new[] { "map", "npc", "filter", "zoomout", "zoomin" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                _game.LoadTexture($"ui/mini_btn_{name}{state}");
        foreach (string dot in new[] { "user", "npc", "npcstart", "npcend", "portal" })
            _game.LoadTexture($"ui/mini_dot_{dot}.png");
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
            _game.LoadTexture($"ui/map_x{state}");
        _game.LoadTexture("ui/mini_filter_bg.png");
        _game.LoadTexture("ui/npclist_bg.png");
        _game.LoadTexture("ui/npclist_dot.png");
        _game.LoadTexture("ui/check_0.png");
        _game.LoadTexture("ui/check_1.png");
        _game.LoadTexture(_game.GameMap.MiniMap.Image);
        string mark = _game.GameMap.MiniMap.Mark;
        if (mark != "") _game.LoadTexture($"ui/map_mark_{mark}.png");
        // The tip of an NPC row of the list: the same box and grey rule the
        // world map tips use.
        if (_state.NpcList)
        {
            foreach (string piece in new[] { "nw", "n", "ne", "w", "e", "sw", "s", "se", "c" })
                _game.LoadTexture($"ui/tip_{piece}.png");
            _game.LoadTexture("ui/wm_tip_line.png");
        }

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it

        var art = new GameObject();
        art.Add(new Rect(Well()));
        art.Add(new MinimapArt { Game = _game, State = _state });
        root.AddChild(art);

        // The frame goes over the map: as a child it draws after the art, and its
        // center is off because the map itself fills the well.
        var frame = new GameObject();
        frame.Add(new Rect(Rect()));
        frame.Add(new Box(Frame, center: false));
        root.AddChild(frame);

        if (_state.Big)
        {
            var head = new GameObject();
            head.Add(new Rect(new Rectangle(0, 0, Rect().Width, TopH)));
            head.Add(new MinimapHeader { Game = _game });
            root.AddChild(head);
        }
        if (_state.NpcList) root.AddChild(NpcListPanel());
        if (_state.Filter) root.AddChild(FilterPanel());

        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        AddRow(root);
        AddLayout(root);
        Host.AddChild(root);
    }

    // The three buttons on the top bar, right aligned like the official window
    // (the vectors live in the layout file): the book opens the world map, the
    // person the NPC list, the funnel the filter.
    private void AddRow(GameObject root)
    {
        (string Name, Action Click)[] row =
        {
            ("map", () => _game.ShowWindow(HotkeyAction.WorldMap)),
            ("npc", () => { _state.NpcList = !_state.NpcList; Dirty = true; }),
            ("filter", () => { _state.Filter = !_state.Filter; Dirty = true; })
        };
        for (int i = 0; i < row.Length; i++)
        {
            var b = new GameObject();
            b.Add(new Rect(new Rectangle(Rect().Width + Lay.Get("rowRight", -26) - (row.Length - 1 - i) * Lay.Get("rowPitch", 22),
                Lay.Get("rowY", 4), BtnSize, BtnSize)));
            b.Add(new Button { Kind = ButtonKind.Click, OnClick = row[i].Click });
            b.Add(new Image($"ui/mini_btn_{row[i].Name}"));
            root.AddChild(b);
        }
    }

    // The minus and the plus switch the layout (the official pair is not a
    // zoom): the small frame, then the big one with the map icon and names.
    private void AddLayout(GameObject root)
    {
        int x = Lay.Get("zoomX", 6), y = Lay.Get("zoomY", 4);
        int pitch = Lay.Get("zoomPitch", 22);
        var minus = new GameObject();
        minus.Add(new Rect(new Rectangle(x, y, BtnSize, BtnSize)));
        minus.Add(new Button { Kind = ButtonKind.Click, OnClick = () => SwitchLayout(false) });
        minus.Add(new Image("ui/mini_btn_zoomout"));
        root.AddChild(minus);
        var plus = new GameObject();
        plus.Add(new Rect(new Rectangle(x + pitch, y, BtnSize, BtnSize)));
        plus.Add(new Button { Kind = ButtonKind.Click, OnClick = () => SwitchLayout(true) });
        plus.Add(new Image("ui/mini_btn_zoomin"));
        root.AddChild(plus);
    }

    // The shots pose the panels and the large layout; a player uses the buttons.
    public void SetBig() => SwitchLayout(true);
    public void SetNpcList() { _state.NpcList = true; Dirty = true; }
    public void SetFilter() { _state.Filter = true; Dirty = true; }

    // The layout changes the frame, so the window is rebuilt once.
    private void SwitchLayout(bool big)
    {
        if (_state.Big == big) return;
        _state.Big = big;
        Dirty = true;
    }

    // The list of this map's NPCs, on the right of the window. The frame art
    // already carries the NPC label. A row shows the same tip as a left column
    // row of the world map.
    private GameObject NpcListPanel()
    {
        int w = NpcListRect().Width;
        var box = new GameObject();
        box.Add(new Rect(NpcListRect()));
        box.Add(new MinimapNpcList { Game = _game });
        box.AddChild(MapWindowParts.Close(() => { _state.NpcList = false; Dirty = true; }, new Rectangle(w + Lay.Get("listCloseX", -21), Lay.Get("listCloseY", 11), 11, 11)));

        for (int i = 0; i < Math.Min(_game.GameMap.Npcs.Count, MinimapNpcList.RowCount); i++)
        {
            var n = _game.GameMap.Npcs[i];
            int y = MinimapNpcList.RowY(i);
            var row = new GameObject();
            row.Add(new Rect(new Rectangle(0, y, w, RowH)));
            row.Add(new Text
            {
                Value = () => _game.NpcName(n.NpcId),
                X = Lay.Get("listTextX", 30),
                W = w - Lay.Get("listTextX", 30),
            });
            row.Add(new LifeStand { Game = _game, Kind = 0, Id = n.NpcId });
            row.Add(new LifeTip());
            box.AddChild(row);
        }
        return box;
    }

    // The filter panel: the official art, the official checkboxes and the rows
    // the exporter read from the window.
    private GameObject FilterPanel()
    {
        int w = FilterRect().Width;
        var box = new GameObject();
        box.Add(new Rect(FilterRect()));
        box.Add(new MinimapFilter { State = _state });
        box.AddChild(MapWindowParts.Close(() => { _state.Filter = false; Dirty = true; }, new Rectangle(w + Lay.Get("filterCloseX", -20), Lay.Get("filterCloseY", 11), 11, 11)));

        foreach (var row in MinimapFilter.Rows)
        {
            var hit = new GameObject();
            // The whole row toggles: the box spans the panel and is as tall as a row.
            hit.Add(new Rect(new Rectangle(0, row.CheckY - Lay.Get("filterHitPad", 2), w, Lay.Get("filterRowH", 18))));
            hit.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _state.Toggle(row.Row) });
            box.AddChild(hit);
        }
        return box;
    }
}

// The world map window (UIMap.img/WorldMap): the left column with the map name,
// the level range and the NPC and monster list, and the map picture with its
// node icons. The official window is two pictures, so the search panel hangs on
// the right edge of this one instead of being a window of its own.
public sealed class WorldMapWindow : UiWindow
{
    private readonly MushTownGame _game;
    private readonly WorldMapState _state = new();
    private readonly TextField _field = new(MapLay.Lay); // the search name field
    private List<SearchHit> _hits = new(); // the last search
    private int _type;   // 0 map, 1 NPC, 2 mob
    private int _scroll; // the first result row on screen
    private int _wheel;  // the last wheel value, for the notch
    private bool _confirm; // the dialog is open
    private int _confirmMapId; // the map the dialog would travel to, fixed when it opens

    private static readonly Color HitMapInk = new(0x0C, 0xE0, 0x93);

    // One row of the result list.
    private sealed record SearchHit(string Name, int MapId, string Region, int Node, string Icon, Color Ink);

    // Taking the field. A click brings no key frame, so the field reads the
    // keyboard itself and counts the held keys as already down.
    private void FocusSearch()
    {
        _field.Focus(Keyboard.GetState());
        Dirty = true;
    }

    // The search button: the board appears and takes the field, or leaves.
    private void ToggleSearch()
    {
        _state.Search = !_state.Search;
        if (_state.Search) FocusSearch(); else _field.Unfocus();
        Dirty = true;
    }

    // Esc closes one layer of this window: the dialog first, then the search board.
    public bool EscLayer()
    {
        if (_confirm) { Cancel(); return true; }
        if (!_state.Search) return false;
        _state.Search = false;
        _state.OpenCombo = -1;
        _field.Unfocus();
        Dirty = true;
        return true;
    }

    // The shot poses the confirm dialog: a player clicks a node twice.
    public void OpenConfirm()
    {
        Locate(); // the node the player stands on, so the dialog names that map
        if (_state.SelMap == 0) return; // no map of ours here: nothing to ask about
        _confirmMapId = _state.SelMap;
        _confirm = true;
        Dirty = true;
    }

    // The shot poses an open list: a player clicks a combo box.
    public void PoseCombo(int index)
    {
        _state.OpenCombo = index;
        Dirty = true;
    }

    // The shot poses the search panel with a query: a player uses the search button.
    public void OpenSearch(string query)
    {
        _state.Search = true;
        _field.Focus(new KeyboardState());
        _field.Clear();
        foreach (char c in query) _field.Type(c);
        Run();
    }

    // The name field has the keys: the chat must not take them.
    public bool SearchFocused => Show && _state.Search && _field.Focused;

    public WorldMapWindow(MushTownGame game) : base(HotkeyAction.WorldMap)
    {
        _game = game;
        _field.Ink = Color.Black; // the search panel is white
    }

    private static PanelLayout Lay => MapLay.Lay;
    private static int SearchW => Lay.Get("searchW", 182);

    public override Rectangle Rect() => new(0, 0, Lay.Get("ww", 852), Lay.Get("wh", 561));
    // The search board hangs on the right edge, so keeping 80 px of the window on
    // screen keeps 80 px of the board there too.
    public override Point ClampPos(Point p) =>
        MushTownGame.ClampBox(p, Rect().Width, 1366 - 80 - (Rect().Width - 1), 768 - Rect().Bottom);

    // A click outside the search panel leaves the field: otherwise the keyboard
    // stays captured and the character cannot walk.
    public bool SearchPanelAt(Point p) =>
        new Rectangle(Pos.X + Lay.Get("ww", 852) - 1, Pos.Y, Lay.Get("searchW", 182), Lay.Get("searchH", 325)).Contains(p);

    public void UnfocusSearch()
    {
        if (!_field.Focused) return;
        _field.Unfocus();
        Dirty = true;
    }

    public override Rectangle TitleRect() => new(0, 0, Rect().Width, Lay.Get("titleH", 30));
    public override Point LayoutPos() => new(Lay.Get("wx", 250), Lay.Get("wy", 30));

    private static readonly string[] Files =
    {
        "ui/wm_frame.png", "ui/wm_info_top.png", "ui/wm_info_body.png",
        "ui/wm_cur.png", "ui/wm_select.png", "ui/wm_life_npc.png", "ui/wm_life_mob.png",
        "ui/wm_tip_line.png", "ui/wm_tip_mob.png", "ui/wm_tip_npc.png",
        "ui/tip_nw.png", "ui/tip_ne.png", "ui/tip_sw.png", "ui/tip_se.png",
        "ui/tip_n.png", "ui/tip_s.png", "ui/tip_w.png", "ui/tip_e.png", "ui/tip_c.png",
        "ui/wm_combo_n0.png", "ui/wm_combo_n1.png", "ui/wm_combo_n2.png",
        "ui/wm_combo_h0.png", "ui/wm_combo_h1.png", "ui/wm_combo_h2.png",
        "ui/wm_combo_p0.png", "ui/wm_combo_p1.png", "ui/wm_combo_p2.png",
        "ui/wm_btn_upper_n.png", "ui/wm_btn_upper_h.png", "ui/wm_btn_upper_p.png",
        "ui/wm_btn_locate_n.png", "ui/wm_btn_locate_h.png", "ui/wm_btn_locate_p.png",
        "ui/wm_btn_search_n.png", "ui/wm_btn_search_h.png", "ui/wm_btn_search_p.png",
        "ui/wm_btn_bookmark_d.png", "ui/wm_btn_navi_d.png",
        "ui/wm_premium_d.png", "ui/wm_teleport_d.png", "ui/wm_navi_d.png",
        "ui/map_x_n.png", "ui/map_x_h.png", "ui/map_x_p.png",
        "ui/wm_s_back.png",
        "ui/wm_s_go_n.png", "ui/wm_s_go_h.png", "ui/wm_s_go_p.png", "ui/wm_s_row.png",
        "ui/wm_s_map.png", "ui/wm_s_npc.png", "ui/wm_s_mob.png",
    };

    public override void Build()
    {
        foreach (string f in Files) _game.LoadTexture(f);
        // The system popup art (Basic.img/sysPop) is only needed while it is up.
        if (_confirm)
        {
            foreach (string f in new[] { "top", "mid", "bottom" }) _game.LoadTexture($"ui/wm_pop_{f}.png");
            foreach (string f in new[] { "yes", "no" })
                foreach (string s in new[] { "n", "h", "p" }) _game.LoadTexture($"ui/wm_pop_{f}_{s}.png");
        }
        var region = WorldMapUi.Region(_state.Shown)!; // the json always carries it
        _game.LoadTexture(region.Image);
        // The pictures of the left column rows: every frame of the rows the
        // column will show, because the tips play them.
        int shown = WorldMapUi.ShownMapId(_state, _game.GameMap.MapId);
        var lives = shown == 0
            ? new List<(int Kind, int Id)>()
            : WorldMapUi.LivesOf(shown).Take(Lay.Get("lifeRows", 14)).ToList();
        foreach (var (kind, id) in lives)
            foreach (string image in _game.LifeIdle(kind, id).Images)
                _game.LoadTexture(image);
        // The node icons and the link plates this region draws. A plate is only
        // painted under the mouse, so it must be loaded before it first shows.
        foreach (int type in region.Nodes.Select(n => n.Type).Where(t => t >= 0).Distinct())
            _game.LoadTexture($"ui/wm_node_{type}.png");
        foreach (var link in region.Links) _game.LoadTexture($"ui/{link.Image}");

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new Picture("ui/wm_frame.png"));
        root.Add(new WorldMapInfo { Game = _game, State = _state });
        AddLifeRows(root, lives);
        // The map area is one object: the plate, the name and the click all come
        // from the same pixel test, so they cannot disagree.
        var art = new GameObject();
        art.Add(new Rect(new Rectangle(Lay.Get("worldX", 198), Lay.Get("worldY", 75), region.W, region.H)));
        art.Add(new Button { Kind = ButtonKind.Click, OnClick = ClickMap });
        art.Add(new WorldMapArt { Game = _game, State = _state });
        root.AddChild(art);
        // The root map is a region picker: its nodes stand where the links are,
        // so there only the links take clicks.
        if (_state.Shown != "WorldMap")
            for (int i = 0; i < region.Nodes.Count; i++) AddNode(root, region, i);

        AddToolbar(root);
        AddCombos(root);
        // The two buttons under the NPC list are drawn with the official
        // disabled art: the teleport rock and the navigation are not in this slice.
        root.AddChild(Fixed("ui/wm_premium_d.png", Lay.Get("premiumX", 135), Lay.Get("premiumY", 476), 36, 36));
        root.AddChild(Fixed("ui/wm_teleport_d.png", Lay.Get("teleportX", 11), Lay.Get("teleportY", 476), Lay.Get("teleportW", 122), Lay.Get("infoBtnH", 36)));
        root.AddChild(Fixed("ui/wm_navi_d.png", Lay.Get("naviStartX", 11), Lay.Get("naviStartY", 516), Lay.Get("naviW", 160), Lay.Get("infoBtnH", 36)));
        if (_state.Search) AddSearch(root);
        // The title and the X come before the popups, so clicking them while a list
        // or the dialog is open falls on that popup's catcher instead.
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MapWindowParts.Close(Close, CloseRect()));
        // The open list goes last of the two: a combo of the search panel must
        // be able to drop over that panel.
        if (_state.OpenCombo >= 0) AddComboList(root);
        // The link name goes last of the panels: a tip drawn inside the map area
        // would slide under the search board hanging on the right edge.
        var tip = new GameObject();
        tip.Add(new TipAnchor());
        tip.Add(new WorldMapTip { Game = _game, State = _state });
        root.AddChild(tip);
        if (_confirm)
        {
            // The dialog is modal: a click anywhere else only cancels. The box is
            // in window space, so the offset pulls it back to the screen corner;
            // it sits above the title, so the window cannot be dragged while open.
            var catcher = new GameObject();
            catcher.Add(new Rect(new Rectangle(-Pos.X, -Pos.Y, MushTownGame.GameWidth, MushTownGame.GameHeight)));
            catcher.Add(new Button { Kind = ButtonKind.Click, OnClick = Cancel });
            root.AddChild(catcher);
            AddConfirm(root);
        }
        Host.AddChild(root);
    }

    // The X of the window: the search, the caret and a pending dialog go too.
    private void Close()
    {
        Show = false;
        OnClosed();
    }

    // The state that belongs to one open: a reopen starts clean.
    private void ResetTransient()
    {
        _state.Search = false;
        _state.OpenCombo = -1;
        _confirm = false;
        _field.Unfocus();
        _field.Clear();
        _hits.Clear();
        _scroll = 0;
    }

    private Rectangle CloseRect() => new(Lay.Get("wcloseX", 829), Lay.Get("wcloseY", 12), 11, 11);
    // A picture at a fixed spot, drawn at its own size (no stretch). The box only
    // carries the size of that picture.
    private GameObject Fixed(string art, int x, int y, int w, int h)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Picture(art, 0, 0));
        return o;
    }

    // A node: the icon is drawn by the art component, this is its click box.
    // The box is as big as the icon, so a big plate is clickable as a whole. A
    // node with no icon (type -1, an event map) gets neither.
    private void AddNode(GameObject root, WorldMapRegion region, int i)
    {
        var node = region.Nodes[i];
        if (node.Type < 0) return;
        var icon = WorldMapUi.Icon(node.Type);
        int px = Lay.Get("worldX", 198) + region.OriginX + node.X;
        int py = Lay.Get("worldY", 75) + region.OriginY + node.Y;
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(px - icon.OX, py - icon.OY, icon.W, icon.H)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Pick(i) });
        o.Add(new WorldMapNodeMark { State = _state, Index = i });
        root.AddChild(o);
    }

    // A click picks the node (the blue arrow follows the state, so no rebuild);
    // a second click on the picked node asks before travelling. The node may
    // stand for several of our maps, so the pick carries the map id as well.
    private void Pick(int i)
    {
        _field.Unfocus();
        var region = WorldMapUi.Region(_state.Shown)!;
        var node = region.Nodes[i];
        if (_state.Sel != i)
        {
            _state.Sel = i;
            _state.SelMap = WorldMapUi.PickMap(node, _game.GameMap.MapId);
            Dirty = true; // the info column names the picked map: its lists follow
            return;
        }
        if (_state.SelMap == 0) return; // nowhere to go
        _confirmMapId = _state.SelMap;
        _confirm = true;
        Dirty = true;
    }

    // The toolbar: upper, locate and search work; the book mark and the
    // navigation button are drawn disabled (they are not in this slice).
    private void AddToolbar(GameObject root)
    {
        int y = Lay.Get("btnY", 40), w = Lay.Get("btnW", 37), h = Lay.Get("btnH", 25);
        AddButton(root, "upper", Lay.Get("upperX", 205), y, w, h, Up);
        AddButton(root, "locate", Lay.Get("locateX", 247), y, w, h, Locate);
        AddButton(root, "search", Lay.Get("searchX", 709), y, w, h, ToggleSearch);
        Disabled(root, "bookmark", Lay.Get("bookmarkX", 751), y, w, h);
        Disabled(root, "navi", Lay.Get("naviX", 793), y, w, h);
    }

    private void AddButton(GameObject root, string name, int x, int y, int w, int h, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Image($"ui/wm_btn_{name}"));
        root.AddChild(o);
    }

    private void Disabled(GameObject root, string name, int x, int y, int w, int h)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Picture($"ui/wm_btn_{name}_d.png"));
        root.AddChild(o);
    }

    // Up: the parent world map (the root has none, so the button does nothing).
    private void Up()
    {
        var region = WorldMapUi.Region(_state.Shown)!;
        if (region.Parent == "") return;
        Switch(region.Parent);
    }

    // The region that holds the map the player stands on, and the node in it. A
    // region wins over the root: the root lists the maps of every region too.
    // Locate: show the world map the player stands on and pick that node.
    private void Locate()
    {
        var (name, node) = Find(_game.GameMap.MapId);
        Switch(name);
        _state.Sel = node;
        _state.SelMap = node < 0 ? 0 : WorldMapUi.PickMap(WorldMapUi.Region(name)!.Nodes[node], _game.GameMap.MapId);
        Dirty = true; // the info column names the shown map: its lists follow
    }

    private void Switch(string name)
    {
        if (_state.Shown == name) return;
        _state.Shown = name;
        _state.Sel = -1;
        Dirty = true;
    }

    // The official system popup: the same box the quest window uses.
    private void AddConfirm(GameObject root)
    {
        int w = Lay.Get("confirmW", 318), h = Lay.Get("confirmH", 143);
        var box = new Rectangle((Rect().Width - w) / 2, (Rect().Height - h) / 2, w, h);
        root.AddChild(SysPopUi.Build(box,
            new List<string> { "Will you teleport to this map?", WorldMapUi.Name(_confirmMapId) }, Go, Cancel, Lay));
    }

    // A click on the map area: leave the search field, then open the link under
    // the mouse if we exported that world map.
    private void ClickMap()
    {
        _field.Unfocus();
        Dirty = true;
        // The answer comes from the last draw, so the region may have been swapped
        // since: check the index against the links actually shown now.
        var links = WorldMapUi.Region(_state.Shown)!.Links;
        if (_state.Hover < 0 || _state.Hover >= links.Count) return;
        var link = links[_state.Hover];
        if (WorldMapUi.Region(link.Target) == null) return; // the plate names it, nothing to open
        Switch(link.Target);
    }


    private void Go()
    {
        // Asking to go to the map we stand on would reload it: just close.
        if (_confirmMapId != _game.GameMap.MapId) _game.TravelTo(_confirmMapId);
        Close();
    }

    private void Cancel()
    {
        _confirm = false;
        Dirty = true;
    }

    // One combo box of the window: where it sits in the window, how wide it is,
    // the text on it, its choices and what a pick does.
    private sealed record Combo(int X, int Y, int W, Func<string> Text, Func<List<string>> Choices, Action<int> Pick);

    // The four boxes: the world, the region and the map of the top strip, then
    // the kind of thing the search looks for. The search one sits in the panel
    // that hangs on the right edge, so its spot is the window spot as well.
    // Built on every read: the layout can change under a running window.
    private Combo[] Combos() => new[]
    {
        // The only world we have.
        new Combo(Lay.Get("comboX", 294), Lay.Get("comboY", 42), Lay.Get("comboW", 98),
            () => "Maple World", () => new List<string> { "Maple World" }, _ => Switch("WorldMap")),
        new Combo(Lay.Get("comboX", 294) + Lay.Get("comboPitch", 101), Lay.Get("comboY", 42), Lay.Get("comboW", 98),
            RegionText, () => WorldMapUi.Regions().Select(r => r.Text).ToList(),
            row => Switch(WorldMapUi.Regions()[row].Target)),
        new Combo(Lay.Get("comboX", 294) + Lay.Get("comboPitch", 101) * 2, Lay.Get("comboY", 42), Lay.Get("comboW", 98),
            MapText, () => WorldMapUi.Ours(_state.Shown).Select(i => WorldMapUi.Region(_state.Shown)!.Nodes[i].Name).ToList(),
            row =>
            {
                var nodes = WorldMapUi.Ours(_state.Shown);
                _state.Sel = nodes[row];
                _state.SelMap = WorldMapUi.PickMap(WorldMapUi.Region(_state.Shown)!.Nodes[nodes[row]], _game.GameMap.MapId);
            }),
        new Combo(Lay.Get("ww", 852) - 1 + Lay.Get("sInX", 15), Lay.Get("sTypeY", 74), Lay.Get("sInW", 152),
            () => TypeName(_type), () => new List<string> { TypeName(0), TypeName(1), TypeName(2) },
            row => { _type = row; Run(); }),
    };

    // The three boxes of the top strip. The WZ gives four, but the fourth is the
    // map name of the picked node, which the left column already shows.
    private void AddCombos(GameObject root)
    {
        for (int i = 0; i < 3; i++) AddCombo(root, Point.Zero, i);
    }

    // One box, hung on a parent that sits at the window spot At (the search
    // box hangs the type box on the panel, the strip hangs its own on the window).
    // The text is a Text component of the same object: the pieces draw first.
    private void AddCombo(GameObject parent, Point at, int index)
    {
        var c = Combos()[index];
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(c.X - at.X, c.Y - at.Y, c.W, BoxH)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => OpenCombo(index) });
        o.Add(new ComboBox());
        o.Add(new Text
        {
            Value = c.Text,
            Ink = Color.Black,
            X = Lay.Get("comboTextX", 6),
            W = ComboTextW(c.W, arrow: true),
        });
        parent.AddChild(o);
    }

    // The room a combo text has: the box less the left edge, the right end (the
    // arrow cap of a closed box, the mirrored cap of a list row) and its pad.
    private static int ComboTextW(int w, bool arrow) =>
        w - Lay.Get("comboTextX", 6) - (arrow ? Lay.Get("comboArrowW", 17) : Lay.Get("comboEndW", 4))
        - Lay.Get("comboTextPad", 2);

    private static int BoxH => Lay.Get("comboRowH", 20);

    // A click on a combo opens its list, and a second click closes it (-1).
    private void OpenCombo(int index)
    {
        _state.OpenCombo = _state.OpenCombo == index ? -1 : index;
        Dirty = true;
    }

    // The second box: the region that is on screen.
    private string RegionText()
    {
        foreach (var (text, target) in WorldMapUi.Regions())
            if (target == _state.Shown) return text;
        return "Maple World"; // the root page is the world itself
    }

    // The third box: the picked node, when it stands for a map we have.
    private string MapText()
    {
        return _state.Sel < 0 ? "" : WorldMapUi.Name(_state.SelMap);
    }

    // The NPC and monster rows of the left column: one object per row, so the
    // picture, the name and the tip of the row are the same components as
    // everywhere else. A row carries a LifeTip and the tip layer of the HUD
    // draws whichever one the mouse is on.
    private void AddLifeRows(GameObject root, List<(int Kind, int Id)> lives)
    {
        int x = Lay.Get("lifeX", 16), y = Lay.Get("lifeY", 145), step = Lay.Get("lifeStep", 20);
        for (int i = 0; i < lives.Count; i++)
        {
            var (kind, id) = lives[i];
            var row = new GameObject();
            row.Add(new Rect(new Rectangle(x, y + i * step, Lay.Get("lifeW", 152), step)));
            row.Add(new Picture(kind == 0 ? "ui/wm_life_npc.png" : "ui/wm_life_mob.png", 0, 0));
            var stand = new LifeStand { Game = _game, Kind = kind, Id = id };
            row.Add(new Text
            {
                Value = () => stand.Name, // the tip reads the same LifeStand: one name source
                Ink = kind == 0 ? WorldMapInfo.NpcInk : WorldMapInfo.MobInk,
                X = Lay.Get("lifeTextX", 22),
                W = Lay.Get("lifeW", 152) - Lay.Get("lifeTextX", 22),
            });
            row.Add(stand);
            row.Add(new LifeTip());
            root.AddChild(row);
        }
    }

    // The open list, and the click that closes it: the catcher is under the rows
    // and over everything else, so a click anywhere else only closes the list.
    private void AddComboList(GameObject root)
    {
        int index = _state.OpenCombo;
        var c = Combos()[index];
        var choices = c.Choices();

        var catcher = new GameObject();
        // The same rule as the confirm dialog: a click anywhere else closes the list.
        catcher.Add(new Rect(new Rectangle(-Pos.X, -Pos.Y, MushTownGame.GameWidth, MushTownGame.GameHeight)));
        catcher.Add(new Button { Kind = ButtonKind.Click, OnClick = () => OpenCombo(-1) });
        root.AddChild(catcher);

        var box = new GameObject();
        box.Add(new Rect(new Rectangle(c.X, c.Y + BoxH, c.W, choices.Count * BoxH)));
        box.Add(new ComboBox { Rows = choices });
        for (int i = 0; i < choices.Count; i++)
        {
            int row = i;
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(0, i * BoxH, c.W, BoxH)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => ComboPick(index, row) });
            o.Add(new Text { Value = () => choices[row], Ink = Color.Black, X = Lay.Get("comboTextX", 6), W = ComboTextW(c.W, arrow: false) });
            box.AddChild(o);
        }
        root.AddChild(box);
    }

    private void ComboPick(int combo, int row)
    {
        _state.OpenCombo = -1;
        Combos()[combo].Pick(row);
        Dirty = true;
    }

    // The search panel, on the right edge of the window: the name field, the
    // kind of thing to look for and the result list.
    private void AddSearch(GameObject root)
    {
        int x = Lay.Get("ww", 852) - 1;
        var box = new GameObject();
        box.Add(new Rect(new Rectangle(x, 0, SearchW, Lay.Get("searchH", 325))));
        box.Add(new Picture("ui/wm_s_back.png"));

        // The field is the left part of the input box: the magnifier sits in its
        // right end, so the text stops before it.
        var input = new GameObject();
        input.Add(new Rect(new Rectangle(Lay.Get("sInX", 15), Lay.Get("sInY", 44),
            Lay.Get("sInW", 152), Lay.Get("sInH", 22))));
        input.Add(new Button { Kind = ButtonKind.Click, OnClick = FocusSearch });
        var field = new GameObject();
        field.Add(new Rect(new Rectangle(6, 0, Lay.Get("sInW", 152) - Lay.Get("goInset", 18) - 10, Lay.Get("sInH", 22))));
        field.Add(_field);
        input.AddChild(field);
        box.AddChild(input);

        AddCombo(box, new Point(x, 0), 3); // the kind of thing to look for

        AddHits(box);

        // The magnifier sits on the field, so it is a child of it: right edge,
        // centred, and it moves with the field.
        var go = new GameObject();
        int size = Lay.Get("goSize", 14);
        go.Add(new Rect(new Rectangle(Lay.Get("sInW", 152) - Lay.Get("goInset", 18), (Lay.Get("sInH", 22) - size) / 2, size, size)));
        go.Add(new Button { Kind = ButtonKind.Click, OnClick = Run });
        go.Add(new Image("ui/wm_s_go"));
        input.AddChild(go);

        box.AddChild(MapWindowParts.Close(() => { _state.Search = false; _state.OpenCombo = -1; _field.Unfocus(); Dirty = true; },
            new Rectangle(Lay.Get("sCloseX", 162), Lay.Get("sCloseY", 10), 11, 11)));
        root.AddChild(box);
    }

    // The result rows that fit, from the scroll point.
    private void AddHits(GameObject box)
    {
        int lx = Lay.Get("sListX", 17), ly = Lay.Get("sListY", 150);
        int rw = Lay.Get("sRowW", 146), rh = Lay.Get("sRowH", 20), rows = Lay.Get("sRows", 8);
        for (int i = 0; i < rows; i++)
        {
            int at = _scroll + i;
            if (at >= _hits.Count) break;
            var hit = _hits[at];
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(lx, ly + i * rh, rw, rh)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => ShowHit(hit) });
            o.Add(new WorldMapSearchRow { Icon = hit.Icon });
            o.Add(new Text { Value = () => hit.Name, Ink = hit.Ink, X = Lay.Get("sTextX", 22), W = rw - Lay.Get("sTextX", 22) });
            box.AddChild(o);
        }
    }

    private static string TypeName(int type) => type switch { 1 => "NPC", 2 => "Mob", _ => "Map" };

    // The results: only what we have. A map hit is a node of a region we
    // exported, an NPC or mob hit is one of the 15 maps' life lists.
    private void Run()
    {
        _hits = new List<SearchHit>();
        _scroll = 0;
        string q = _field.Text.Trim().ToLowerInvariant();
        if (q != "") Collect(q);
        Dirty = true;
    }

    private void Collect(string q)
    {
        // One row per map we have, named by the map itself: a node may stand for
        // several of our maps and its name is only the first one's.
        // One row per name: the same monster on three maps would otherwise fill
        // the list with rows the player cannot tell apart.
        var seen = new HashSet<string>();
        foreach (var map in MapUi.Data.Maps)
        {
            var (region, node) = Find(map.Id);
            if (node < 0) continue;
            if (_type == 0)
            {
                if (map.Name.ToLowerInvariant().Contains(q))
                    _hits.Add(new SearchHit(map.Name, map.Id, region, node, "ui/wm_s_map.png", HitMapInk));
                continue;
            }
            var file = WorldMapUi.MapFile(map.Id);
            if (_type == 1)
                foreach (var npc in file.Npcs)
                {
                    string name = _game.NpcName(npc.NpcId);
                    if (name.ToLowerInvariant().Contains(q) && seen.Add(name))
                        _hits.Add(new SearchHit(name, map.Id, region, node, "ui/wm_s_npc.png", WorldMapInfo.NpcInk));
                }
            else
                foreach (var mob in file.Life)
                {
                    string name = _game.MobName(mob.MobId);
                    if (name.ToLowerInvariant().Contains(q) && seen.Add(name))
                        _hits.Add(new SearchHit(name, map.Id, region, node, "ui/wm_s_mob.png", WorldMapInfo.MobInk));
                }
        }
    }

    // The region that shows a map, and the node in it. A region beats the root.
    private (string Region, int Node) Find(int mapId)
    {
        (string, int)? inRoot = null;
        foreach (var region in WorldMapUi.Data.Maps)
            for (int i = 0; i < region.Nodes.Count; i++)
            {
                if (!region.Nodes[i].Maps.Contains(mapId)) continue;
                if (region.Name != "WorldMap") return (region.Name, i);
                inRoot ??= ("WorldMap", i);
            }
        return inRoot ?? ("WorldMap", -1);
    }

    // A click on a result shows that map on the world map and picks its node.
    private void ShowHit(SearchHit hit)
    {
        _field.Unfocus();
        Switch(hit.Region);
        _state.Sel = hit.Node;
        _state.SelMap = hit.MapId;
        Dirty = true;
    }

    // The typing of the name field: Enter searches, Escape leaves the field, a
    // held key repeats (the field owns that and the rest of the keys).
    public void UpdateKeys(KeyboardState kb, float dtMs)
    {
        switch (_field.Read(kb, dtMs))
        {
            case FieldKey.Submit: Run(); break;
            case FieldKey.Leave: _field.Unfocus(); break;
        }
    }

    // The wheel scrolls the result list.
    public void UpdateWheel()
    {
        var ms = Mouse.GetState();
        int delta = ms.ScrollWheelValue - _wheel;
        _wheel = ms.ScrollWheelValue;
        if (!Show || !_state.Search || delta == 0 || _hits.Count <= Lay.Get("sRows", 8)) return;
        var (gx, gy) = _game.MouseGamePos(ms);
        var list = new Rectangle(Pos.X + Lay.Get("ww", 852) - 1 + Lay.Get("sListX", 17),
            Pos.Y + Lay.Get("sListY", 150), Lay.Get("sRowW", 146), Lay.Get("sRows", 8) * Lay.Get("sRowH", 20));
        if (!list.Contains(gx, gy)) return;
        int want = Math.Clamp(_scroll - delta / 120, 0, _hits.Count - Lay.Get("sRows", 8));
        if (want == _scroll) return;
        _scroll = want;
        Dirty = true;
    }


    // The map changed: the pin follows on its own, so an open window keeps the node
    // the player picked instead of jumping to the new map.
    public void OnMapChanged()
    {
        if (Show) Dirty = true;
    }

    // The window opens on the region the player stands on, with nothing picked:
    // the pin alone shows where we are.
    public override void OnShown()
    {
        _state.Sel = -1;
        _state.SelMap = 0;
        Switch(Find(_game.GameMap.MapId).Region);
    }

    // Esc closes the window: the search and a pending dialog go with it.
    public override void OnClosed() => ResetTransient();
}
