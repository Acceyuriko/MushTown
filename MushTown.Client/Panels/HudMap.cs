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
    // already carries the NPC label.
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
            row.Add(new HoverTip(() => NpcTip(n.NpcId)));
            box.AddChild(row);
        }
        return box;
    }

    // The name of the NPC and the first thing they say: what the tooltip of the
    // list shows.
    private string NpcTip(int id)
    {
        var says = _game.NpcLines(id);
        // The first line, with the WZ macros (#b, #p2001#, ...) turned into text.
        string line = says.Count > 0 ? _game.QText(says[0]) : "";
        return line == "" ? _game.NpcName(id) : $"{_game.NpcName(id)}: {line}";
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
    private readonly CaretText _caret = new(MapLay.Lay); // the search name field
    private readonly HashSet<Keys> _down = new(); // the keys of the last frame, for the typing repeat
    private List<SearchHit> _hits = new(); // the last search
    private int _type;   // 0 map, 1 NPC, 2 mob
    private int _scroll; // the first result row on screen
    private int _wheel;  // the last wheel value, for the notch
    private bool _confirm; // the dialog is open
    private int _confirmMapId; // the map the dialog would travel to, fixed when it opens

    private static readonly Color HitMapInk = new(0x0C, 0xE0, 0x93);

    // One row of the result list.
    private sealed record SearchHit(string Name, int MapId, string Region, int Node, string Icon, Color Ink);

    // Taking the field: the keys held right now count as already down, so they do
    // not type a character the player pressed before the click.
    private void FocusSearch()
    {
        _caret.Focused = true;
        _down.Clear();
        foreach (var key in Keyboard.GetState().GetPressedKeys()) _down.Add(key);
        Dirty = true;
    }

    // Esc closes one layer of this window: the dialog first, then the search board.
    public bool EscLayer()
    {
        if (_confirm) { Cancel(); return true; }
        if (!_state.Search) return false;
        _state.Search = false;
        _state.OpenCombo = -1;
        _caret.Focused = false;
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

    // The shot poses the search panel with a query: a player uses the search button.
    public void OpenSearch(string query)
    {
        _state.Search = true;
        _caret.Focused = true;
        _down.Clear();
        _caret.Clear();
        foreach (char c in query) _caret.Type(c);
        Run();
    }

    // The name field has the keys: the chat must not take them.
    public bool SearchFocused => Show && _state.Search && _caret.Focused;

    public WorldMapWindow(MushTownGame game) : base(HotkeyAction.WorldMap) { _game = game; }

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
        if (!_caret.Focused) return;
        _caret.Focused = false;
        Dirty = true;
    }

    public override Rectangle TitleRect() => new(0, 0, Rect().Width, Lay.Get("titleH", 30));
    public override Point LayoutPos() => new(Lay.Get("wx", 250), Lay.Get("wy", 30));

    private static readonly string[] Files =
    {
        "ui/wm_frame.png", "ui/wm_info_top.png", "ui/wm_info_body.png",
        "ui/wm_cur.png", "ui/wm_select.png", "ui/wm_life_npc.png", "ui/wm_life_mob.png",
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
        tip.Add(new WorldMapLinkTip { State = _state });
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
        _caret.Focused = false;
        _caret.Clear();
        _hits.Clear();
        _scroll = 0;
        _down.Clear();
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
        o.Add(new HoverTip(() => Tip(node)));
        root.AddChild(o);
    }

    // The node tooltip: the names of the maps we have here, then what lives on
    // the first of them. A node of a region we did not export has no name.
    private string Tip(WorldMapNode node)
    {
        var ours = node.Maps.Where(IsOurs).ToList();
        if (ours.Count == 0) return node.Name;
        var lines = new List<string>();
        foreach (int id in ours) lines.Add(WorldMapUi.Name(id));
        var file = WorldMapUi.MapFile(ours[0]);
        foreach (int id in file.Life.Select(l => l.MobId).Distinct().Take(3)) lines.Add(_game.MobName(id));
        foreach (int id in file.Npcs.Select(n => n.NpcId).Distinct().Take(3)) lines.Add(_game.NpcName(id));
        return string.Join("  ", lines);
    }

    // The maps this game has: only those carry a destination and a life list.
    private static bool IsOurs(int id) => MapUi.Data.Maps.Any(m => m.Id == id);

    // A click picks the node (the blue arrow follows the state, so no rebuild);
    // a second click on the picked node asks before travelling. The node may
    // stand for several of our maps, so the pick carries the map id as well.
    private void Pick(int i)
    {
        _caret.Focused = false;
        var region = WorldMapUi.Region(_state.Shown)!;
        var node = region.Nodes[i];
        if (_state.Sel != i)
        {
            _state.Sel = i;
            _state.SelMap = PickMap(node);
            return;
        }
        if (_state.SelMap == 0) return; // nowhere to go
        _confirmMapId = _state.SelMap;
        _confirm = true;
        Dirty = true;
    }

    // The map a node travels to: the one we stand on when it is there, else the
    // first of ours.
    private int PickMap(WorldMapNode node)
    {
        int here = _game.GameMap.MapId;
        if (node.Maps.Contains(here) && IsOurs(here)) return here;
        return node.Maps.FirstOrDefault(IsOurs);
    }

    // The toolbar: upper, locate and search work; the book mark and the
    // navigation button are drawn disabled (they are not in this slice).
    private void AddToolbar(GameObject root)
    {
        int y = Lay.Get("btnY", 40), w = Lay.Get("btnW", 37), h = Lay.Get("btnH", 25);
        AddButton(root, "upper", Lay.Get("upperX", 205), y, w, h, Up);
        AddButton(root, "locate", Lay.Get("locateX", 247), y, w, h, Locate);
        AddButton(root, "search", Lay.Get("searchX", 709), y, w, h, () => { _state.Search = !_state.Search; if (_state.Search) FocusSearch(); else _caret.Focused = false; Dirty = true; });
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
        _state.SelMap = node < 0 ? 0 : PickMap(WorldMapUi.Region(name)!.Nodes[node]);
    }

    private void Switch(string name)
    {
        if (_state.Shown == name) return;
        _state.Shown = name;
        _state.Sel = -1;
        Dirty = true;
    }

    // The official system popup (Basic.img/sysPop): a top cap, a stretchable
    // middle and a bottom bar that carries the two buttons.
    private void AddConfirm(GameObject root)
    {
        int w = Lay.Get("confirmW", 318), h = Lay.Get("confirmH", 143);
        var box = new Rectangle((Rect().Width - w) / 2, (Rect().Height - h) / 2, w, h);
        var o = new GameObject();
        o.Add(new Rect(box));
        o.Add(new Panel("ui/wm_pop_top.png", "ui/wm_pop_mid.png", "ui/wm_pop_bottom.png"));
        o.Add(new SysPopText
        {
            Y = Lay.Get("popTextY", 38),
            Step = Lay.Get("popTextStep", 17),
            Lines = new List<string> { "Will you teleport to this map?", WorldMapUi.Name(_confirmMapId) },
        });
        int by = h - Lay.Get("popBottomH", 59) + Lay.Get("popBtnY", 14);
        o.AddChild(PopButtonAt("ui/wm_pop_yes", Lay.Get("popYesX", 73), by, Go));
        o.AddChild(PopButtonAt("ui/wm_pop_no", Lay.Get("popNoX", 162), by, Cancel));
        root.AddChild(o);
    }

    // A click on the map area: leave the search field, then open the link under
    // the mouse if we exported that world map.
    private void ClickMap()
    {
        _caret.Focused = false;
        Dirty = true;
        // The answer comes from the last draw, so the region may have been swapped
        // since: check the index against the links actually shown now.
        var links = WorldMapUi.Region(_state.Shown)!.Links;
        if (_state.Hover < 0 || _state.Hover >= links.Count) return;
        var link = links[_state.Hover];
        if (WorldMapUi.Region(link.Target) == null) return; // the plate names it, nothing to open
        Switch(link.Target);
    }


    private GameObject PopButtonAt(string stem, int x, int y, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, Lay.Get("popBtnW", 83), Lay.Get("popBtnH", 26))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Image(stem)); // the official button art already carries its label
        return o;
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

    // The top strip: world, region and map. The WZ gives four boxes; the fourth
    // is the map name of the picked node, which the left column already shows.
    private void AddCombos(GameObject root)
    {
        int y = Lay.Get("comboY", 42), pitch = Lay.Get("comboPitch", 101), w = Lay.Get("comboW", 98);
        AddCombo(root, 0, Lay.Get("comboX", 294), y, w, () => "Maple World");
        AddCombo(root, 1, Lay.Get("comboX", 294) + pitch, y, w, RegionText);
        AddCombo(root, 2, Lay.Get("comboX", 294) + pitch * 2, y, w, MapText);
    }

    private void AddCombo(GameObject root, int index, int x, int y, int w, Func<string> text)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, 20)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Combo(index) });
        o.Add(new WorldMapCombo { Text = text });
        root.AddChild(o);
    }

    // A click on a combo opens its list, and a second click closes it.
    private void Combo(int index)
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

    // The open list, and the click that closes it: the catcher is under the rows
    // and over everything else, so a click anywhere else only closes the list.
    private void AddComboList(GameObject root)
    {
        int index = _state.OpenCombo;
        var choices = index switch
        {
            0 => new List<string> { "Maple World" }, // the only world we have
            1 => WorldMapUi.Regions().Select(r => r.Text).ToList(),
            2 => WorldMapUi.Ours(_state.Shown).Select(i => WorldMapUi.Region(_state.Shown)!.Nodes[i].Name).ToList(),
            _ => new List<string> { TypeName(0), TypeName(1), TypeName(2) }, // the search type is the last one
        };
        var (x, top, w) = ComboBox(index);
        int y = top + 20;

        var catcher = new GameObject();
        // The same rule as the confirm dialog: a click anywhere else closes the list.
        catcher.Add(new Rect(new Rectangle(-Pos.X, -Pos.Y, MushTownGame.GameWidth, MushTownGame.GameHeight)));
        catcher.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Combo(-1) });
        root.AddChild(catcher);

        var box = new GameObject();
        box.Add(new Rect(new Rectangle(x, y, w, choices.Count * 20)));
        box.Add(new WorldMapComboList { Rows = choices });
        for (int i = 0; i < choices.Count; i++)
        {
            int row = i;
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(0, i * 20, w, 20)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => ComboPick(index, row) });
            box.AddChild(o);
        }
        root.AddChild(box);
    }

    // Where a combo box sits in the window, and how wide it is.
    private (int X, int Y, int W) ComboBox(int index) => index switch
    {
        1 => (Lay.Get("comboX", 294) + Lay.Get("comboPitch", 101), Lay.Get("comboY", 42), Lay.Get("comboW", 98)),
        2 => (Lay.Get("comboX", 294) + Lay.Get("comboPitch", 101) * 2, Lay.Get("comboY", 42), Lay.Get("comboW", 98)),
        3 => (Lay.Get("ww", 852) - 1 + Lay.Get("sTypeX", 15), Lay.Get("sTypeY", 74), Lay.Get("sTypeW", 152)),
        _ => (Lay.Get("comboX", 294), Lay.Get("comboY", 42), Lay.Get("comboW", 98)),
    };

    private void ComboPick(int combo, int row)
    {
        _state.OpenCombo = -1;
        if (combo == 0) Switch("WorldMap"); // the world box only holds Maple World
        else if (combo == 1)
        {
            var regions = WorldMapUi.Regions();
            Switch(regions[row].Target);
        }
        else if (combo == 2)
        {
            var nodes = WorldMapUi.Ours(_state.Shown);

            _state.Sel = nodes[row];
            _state.SelMap = PickMap(WorldMapUi.Region(_state.Shown)!.Nodes[nodes[row]]);
        }
        else if (combo == 3) { _type = row; Run(); }
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

        var input = new GameObject();
        input.Add(new Rect(new Rectangle(Lay.Get("sInX", 15), Lay.Get("sInY", 44),
            Lay.Get("sInW", 152), Lay.Get("sInH", 22))));
        input.Add(new Button { Kind = ButtonKind.Click, OnClick = FocusSearch });
        input.Add(new WorldMapSearchText { Caret = _caret });
        box.AddChild(input);

        var kind = new GameObject();
        kind.Add(new Rect(new Rectangle(Lay.Get("sTypeX", 15), Lay.Get("sTypeY", 74), Lay.Get("sTypeW", 152), 20)));
        kind.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Combo(3) });
        kind.Add(new WorldMapCombo { Text = () => TypeName(_type) });
        box.AddChild(kind);

        AddHits(box);

        // The magnifier sits on the field, so it is a child of it: right edge,
        // centred, and it moves with the field.
        var go = new GameObject();
        int size = Lay.Get("goSize", 14);
        go.Add(new Rect(new Rectangle(Lay.Get("sInW", 152) - Lay.Get("goInset", 18), (Lay.Get("sInH", 22) - size) / 2, size, size)));
        go.Add(new Button { Kind = ButtonKind.Click, OnClick = Run });
        go.Add(new Image("ui/wm_s_go"));
        input.AddChild(go);

        box.AddChild(MapWindowParts.Close(() => { _state.Search = false; _state.OpenCombo = -1; _caret.Focused = false; Dirty = true; },
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
            o.Add(new WorldMapSearchRow { Name = hit.Name, Icon = hit.Icon, Ink = hit.Ink });
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
        string q = _caret.Text.Trim().ToLowerInvariant();
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
        _caret.Focused = false;
        Switch(hit.Region);
        _state.Sel = hit.Node;
        _state.SelMap = hit.MapId;
        Dirty = true;
    }

    // The typing of the name field: Enter searches, Escape leaves, Backspace
    // deletes and a letter goes in.
    public void UpdateKeys(KeyboardState kb)
    {
        foreach (var key in kb.GetPressedKeys())
        {
            if (_down.Contains(key)) continue;
            if (key == Keys.Enter) { Run(); continue; }
            if (key == Keys.Escape) { _caret.Focused = false; continue; }
            if (key == Keys.Back) { _caret.Backspace(); continue; }
            char c = CharOf(key);
            if (c == '\0') continue;
            _caret.Type(c);
        }
        _down.Clear();
        foreach (var key in kb.GetPressedKeys()) _down.Add(key);
    }

    private static char CharOf(Keys key)
    {
        if (key >= Keys.A && key <= Keys.Z) return (char)('a' + (key - Keys.A));
        if (key >= Keys.D0 && key <= Keys.D9) return (char)('0' + (key - Keys.D0));
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
        return key == Keys.Space ? ' ' : '\0';
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
