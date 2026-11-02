using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The map window data the exporter wrote: assets/ui/mapui.json.
internal static class MapUi
{
    private static MapUiData? _data;
    private static readonly Dictionary<int, MapNameUi> _names = new();
    private static readonly Dictionary<int, WorldRegionUi> _regions = new();
    private static readonly MapNameUi NoName = new();

    // The json loads once. The lookups are dictionaries: the windows ask
    // for a name and a region every frame.
    public static MapUiData Data
    {
        get
        {
            if (_data == null)
            {
                _data = MushTownGame.LoadJson<MapUiData>(Path.Combine(MushTownGame.AssetsDir, "ui", "mapui.json"));
                foreach (var n in _data.Maps) _names[n.Id] = n;
                foreach (var r in _data.World.Regions)
                    foreach (int id in r.Maps) _regions[id] = r; // a map in two regions takes the last one
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

    // The region the map sits in (null when the map is not on the world map).
    public static WorldRegionUi? Region(int mapId)
    {
        _ = Data;
        return _regions.GetValueOrDefault(mapId);
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

// The world map window: the whole world, with the regions. Click a region to
// see the maps of that region; a click on a map picks it and Go travels.
public sealed class WorldMapWindow : UiWindow
{
    private static readonly Color HoverBlue = new(123, 204, 224); // the official hover blue
    private readonly MushTownGame _game;
    private WorldRegionUi? _picked; // the region the player clicked
    private WorldRegionUi? _shown;  // the region the list was built for (the picked one, or the one we stand in)
    // The regions with a map of ours: their dots are the live ones.
    private readonly HashSet<WorldRegionUi> _live = new();
    // The maps of the shown region: the official name wrapped over the
    // lines it needs, and the Y of every line inside the list box.
    private readonly List<(MapNameUi Map, string Text, int Y)> _lines = new(); // Y is local to the list box
    // The list scrolls like the official panel: the first line on screen,
    // the lines that do not fit and the wheel that moves the list.
    private int _scroll;    // the first line on screen
    private int _hidden;    // the lines that do not fit
    private int _wheel;     // the wheel value of the last frame
    private bool _toEnd;    // the shot mode wants the end of the list
    // The rows of the list: their buttons, their maps and the box inside the
    // list (local to the window, like every other rect it makes).
    // The picked map travels only when the Go button is pressed.
    private readonly List<(Button Btn, MapNameUi Map, Rectangle Box)> _rows = new();
    private MapNameUi? _sel;

    public WorldMapWindow(MushTownGame game) : base(HotkeyAction.WorldMap) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));

    public override Rectangle Rect() => new(0, 0, _lay.Get("ww", 852), _lay.Get("wh", 561));
    public override Rectangle TitleRect() => new(0, 0, Rect().Width, _lay.Get("titleH", 30));
    public override Point LayoutPos() => new(_lay.Get("wx", 250), _lay.Get("wy", 30));

    public override void Build()
    {
        _game.LoadTexture("ui/wm_frame.png");
        _game.LoadTexture("ui/wm_scroll_base.png");
        _game.LoadTexture("ui/wm_scroll_thumb.png");
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture("ui/map_x" + state);
        _game.LoadTexture(MapUi.Data.World.Image);

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it
        root.Add(new Picture("ui/wm_frame.png"));
        root.Add(new Picture(MapUi.Data.World.Image, _lay.Get("worldX", 198), _lay.Get("worldY", 75)));
        // the regions: a dot each, with a click box on it
        _live.Clear();
        var maps = new Dictionary<WorldRegionUi, List<MapNameUi>>();
        int clickW = _lay.Get("dotClickW", 17), clickH = _lay.Get("dotClickH", 17);
        foreach (var region in MapUi.Data.World.Regions)
        {
            var list = MapsOf(region);
            if (list.Count == 0) continue; // no map of ours there: nothing to list
            maps[region] = list;
            _live.Add(region);
            var spot = RegionSpot(region);
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(spot.X - (clickW - 1) / 2, spot.Y - (clickH - 1) / 2, clickW, clickH)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Pick(region) });
            root.AddChild(o);
        }
        // the maps of the shown region: click one to pick it
        _lines.Clear();
        var here = MapUi.Region(_game.GameMap.MapId);
        if (here != null && _picked != null && _picked != here) _picked = null; // the player left that region
        var listRegion = _picked ?? here;
        if (listRegion != _shown) { _scroll = 0; _sel = null; } // a new list starts at the top with nothing picked
        if (_sel?.Id == _game.GameMap.MapId) _sel = null; // the row of the map we stand on is gone: nothing to pick
        _shown = listRegion;
        _hidden = 0;
        _rows.Clear();
        if (_shown != null && maps.TryGetValue(_shown, out var shown))
            BuildList(root, shown);
        root.Add(new Invoke(gfx => Draw(gfx, root.Get<Rect>()!.Screen)));
        var go = new GameObject();
        go.Add(new Rect(GoRect()));
        go.Add(new Button { Kind = ButtonKind.Silent, OnClick = GoPicked }); // the travel plays the portal sound
        root.AddChild(go);
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MapWindowParts.Close(() => Show = false, new Rectangle(_lay.Get("wcloseX", 829), _lay.Get("wcloseY", 12), 11, 11)));
        Host.AddChild(root);
    }

    // The map list of the shown region: every name on the lines it needs, and
    // a click box on the part of the name that is on screen (clicking it picks
    // the map, the Go button travels).
    private void BuildList(GameObject root, List<MapNameUi> shown)
    {
        bool toEnd = _toEnd;
        _toEnd = false;
        var box = ListRect();
        int listW = box.Width, listStep = _lay.Get("listStep", 20);
        int listX = box.X, listY = box.Y;
        int visible = box.Height / listStep;
        // every map of the region: the name on the lines it needs
        var names = new List<(MapNameUi Map, List<string> Lines, int Start)>();
        int total = 0;
        foreach (var map in shown)
        {
            var lines = _game.WrapText(map.Name, listW);
            names.Add((map, lines, total));
            total += lines.Count;
        }
        _hidden = Math.Max(0, total - visible);
        _scroll = toEnd ? _hidden : Math.Clamp(_scroll, 0, _hidden);
        foreach (var (map, lines, start) in names)
        {
            if (start + lines.Count <= _scroll || start >= _scroll + visible) continue; // off screen
            for (int i = 0; i < lines.Count; i++)
            {
                int line = start + i;
                if (line < _scroll || line >= _scroll + visible) continue; // this line is off screen
                _lines.Add((map, lines[i], (line - _scroll) * listStep)); // local to the list box
            }
            if (map.Id == _game.GameMap.MapId) continue; // the map we stand on: nowhere to go
            var o = new GameObject();
            // The click box is only the part of the name that is on screen:
            // a name that is half scrolled away must not catch the clicks
            // of the row above it.
            int top = listY + Math.Max(0, start - _scroll) * listStep;
            int bottom = listY + Math.Min(visible, start + lines.Count - _scroll) * listStep;
            var rowBox = new Rectangle(listX, top, listW, bottom - top);
            var pick = new Button { Kind = ButtonKind.Click, OnClick = () => _sel = map }; // a click picks, Go travels
            o.Add(new Rect(rowBox));
            o.Add(pick);
            _rows.Add((pick, map, rowBox));
            root.AddChild(o);
        }
    }

    // The maps of a region that this game has. A map without an official name
    // is skipped: an empty row would be a button with nothing to read.
    private static List<MapNameUi> MapsOf(WorldRegionUi region)
    {
        var list = new List<MapNameUi>();
        foreach (var map in MapUi.Data.Maps)
            if (map.Name != "" && region.Maps.Contains(map.Id)) list.Add(map);
        return list;
    }

    // Where a region's dot sits on the map art (local to the window).
    private Point RegionSpot(WorldRegionUi region) => new(
        _lay.Get("worldX", 198) + region.X,
        _lay.Get("worldY", 75) + region.Y);

    // The player picks a region: the map list shows its maps.
    private void Pick(WorldRegionUi region)
    {
        _picked = region;
        Dirty = true; // the list of maps changes
    }

    // The shot mode scrolls the list to the end (the wheel is the way in the game).
    public void ScrollToEnd()
    {
        _toEnd = true;
        Dirty = true;
    }

    // The Go button: the picked map travels only when it is pressed.
    private Rectangle GoRect() => new(_lay.Get("goX", 16), _lay.Get("goY", 420),
        _lay.Get("goW", 64), _lay.Get("goH", 22));

    private void GoPicked()
    {
        if (_sel == null) return;
        _game.TravelTo(_sel.Id);
        _sel = null;
        Show = false;
    }

    // The map changed: rebuild the list (the map we stand on has no button).
    public void OnMapChanged() => Dirty = true;

    // The region marks on the world map and the maps of the shown region.
    private void Draw(UiGfx gfx, Rectangle screen)
    {
        var r = screen;
        int mapId = _game.GameMap.MapId;
        var here = MapUi.Region(mapId);
        int dotW = _lay.Get("dotW", 7), dotH = _lay.Get("dotH", 7);
        foreach (var region in MapUi.Data.World.Regions)
        {
            var ink = region == here ? new Color(255, 220, 0)
                : region == _shown ? Color.White
                : _live.Contains(region) ? new Color(120, 160, 200) : new Color(80, 95, 110);
            var spot = RegionSpot(region);
            spot.X += screen.X;
            spot.Y += screen.Y;
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(spot.X - (dotW - 1) / 2, spot.Y - (dotH - 1) / 2, dotW, dotH), ink);
        }
        if (_shown == null) return;
        // A pick keeps the blue frame until another row is picked, and the MAP INFO
        // line always names what Go will do. The row under the mouse only tints, so
        // the two can be seen at the same time.
        MapNameUi? hovered = null;
        foreach (var (btn, map, _) in _rows)
            if (btn == gfx.HoverButton) hovered = map;
        var info = _sel ?? hovered;
        foreach (var (_, map, box) in _rows)
        {
            var row = box;
            row.Offset(screen.Location);
            if (map == _sel) Border.Draw(gfx, row, HoverBlue);
            else if (map == hovered) gfx.Sb.Draw(gfx.Pixel, row, new Color(123, 204, 224, 50));
        }
        // The info line keeps one line: a long name is cut at a word and marked,
        // the framed row right below it carries the whole of it.
        var title = _shown.Title;
        if (info != null)
        {
            var lines = _game.WrapText(info.Name, _lay.Get("listW", 146));
            title = lines.Count > 1 ? lines[0] + "…" : lines[0];
        }
        gfx.Sb.Draw(gfx.Ui.GetSmall(title), new Vector2(r.X + _lay.Get("infoX", 20), r.Y + _lay.Get("infoY", 51)),
            info != null ? new Color(255, 220, 0) : Color.White);
        // The Go button: grey until a row is picked.
        var go = GoRect();
        go.Offset(screen.Location);
        Border.Draw(gfx, go, _sel != null ? HoverBlue : new Color(90, 100, 110));
        var goText = gfx.Ui.GetSmall("GO");
        gfx.Sb.Draw(goText, new Vector2(go.X + (go.Width - goText.Width) / 2, go.Y + (go.Height - goText.Height) / 2),
            _sel != null ? Color.White : new Color(120, 130, 140));
        var list = ListRect();
        list.Offset(screen.Location);
        foreach (var (map, text, y) in _lines)
        {
            var ink = map.Id == mapId ? new Color(255, 220, 0) : Color.White;
            gfx.Sb.Draw(gfx.Ui.GetSmall(text), new Vector2(list.X, list.Y + y), ink);
        }
        if (_hidden > 0) ScrollBar(gfx, screen);
    }

    // The wheel scrolls the list (the official panel scrolls it too). Runs
    // every frame before the windows sync, so the rebuild consumes the new
    // scroll and the rows and the bar always agree; while the window is
    // closed it only keeps the baseline fresh, so a reopen cannot replay
    // the notches of a scroll that happened while the map was away.
    public void UpdateWheel()
    {
        var ms = Mouse.GetState();
        int delta = ms.ScrollWheelValue - _wheel;
        _wheel = ms.ScrollWheelValue; // every frame, so the next delta is a step
        if (!Show || delta == 0 || _hidden == 0) return;
        var (gx, gy) = _game.MouseGamePos(ms);
        var list = ListRect();
        list.Offset(ScreenBox().Location);
        if (!list.Contains(gx, gy)) return;
        int steps = delta / 120; // one notch is 120, like the chat log
        if (steps == 0) return;
        // The wheel up (delta > 0) shows the earlier names, so the first
        // visible line moves up: the scroll value counts down.
        int want = Math.Clamp(_scroll - steps * _lay.Get("wheelStep", 3), 0, _hidden);
        if (want == _scroll) return;
        _scroll = want;
        Dirty = true; // the list moves
    }

    // The box the list sits in: the names and the rows that fit.
    private Rectangle ListRect()
    {
        return new Rectangle(_lay.Get("listX", 16), _lay.Get("listY", 75),
            _lay.Get("listW", 146), _lay.Get("listH", 340));
    }

    // The official scroll bar of the list: the track and the cursor on it.
    // The track spans the rows of the list, so it uses the list numbers.
    // The thumb is the official art at its own size (the texture size).
    private void ScrollBar(UiGfx gfx, Rectangle screen)
    {
        var box = ListRect();
        box.Offset(screen.Location);
        int x = screen.X + _lay.Get("scrollX", 165);
        int top = box.Y;
        int len = box.Height;
        var thumb = gfx.Tex("ui/wm_scroll_thumb.png");
        var track = gfx.Tex("ui/wm_scroll_base.png");
        gfx.Sb.Draw(track, new Rectangle(x, top, track.Width, len), Color.White);
        int y = top + (len - thumb.Height) * _scroll / _hidden;
        gfx.Sb.Draw(thumb, new Rectangle(x, y, thumb.Width, thumb.Height), Color.White);
    }
}
