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

    // The X in the corner: it closes the window.
    public static GameObject Close(UiWindow win, Rectangle rect)
    {
        var o = new GameObject();
        o.Add(new Rect(rect));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => win.Show = false });
        o.Add(new Image("ui/map_x"));
        return o;
    }
}

// The nine piece frame of the minimap window (the official
// UIMap.img/MiniMap/MinMap). The pieces keep their size, the edges and the
// center stretch.
public sealed class MinimapFrame : Component, IDrawable
{
    public void Draw(UiGfx gfx) => NineSlice.Draw(gfx, "mini_frame", Owner.Get<Rect>()!.Box);
}

// The minimap window: the official v1.2 frame with the map of the current
// map, the player, the portals, the row of five buttons and the zoom pair.
public sealed class MinimapWindow : UiWindow
{
    private readonly MushTownGame _game;
    private int _zoom = 1; // the zoom level of the map art (1 = the whole map)

    // The frame sizes come from the art: the side border is 9 wide, the top
    // bar 30 tall and the bottom bar 10 tall. The buttons are 21x21.
    private const int BorderX = 9, TopH = 30, BottomH = 10, BtnSize = 21;
    private const int MaxZoom = 4;

    public MinimapWindow(MushTownGame game) : base(HotkeyAction.Minimap) { _game = game; Show = true; } // the minimap is always on screen

    // Esc does not close the minimap: it is always on screen.
    public override void OnClosed() => Show = true;

    // Every number of this window lives in layouts/panel-minimap.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-minimap.json"));

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 185), _lay.Get("h", 160));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, TopH);
    public override Point LayoutPos() => new(_lay.Get("x", 0), _lay.Get("y", 0));

    // The map area inside the frame.
    private Rectangle Well() => new(Pos.X + BorderX, Pos.Y + TopH,
        Rect().Width - BorderX * 2, Rect().Height - TopH - BottomH);

    public override void Build()
    {
        foreach (string piece in new[] { "nw", "n", "ne", "w", "e", "sw", "s", "se", "c" })
            _game.LoadTexture($"ui/mini_frame_{piece}.png");
        foreach (string name in new[] { "town", "map", "navi", "npc", "filter", "zoomout", "zoomin" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png", "_d.png" })
                _game.LoadTexture($"ui/mini_btn_{name}{state}");
        _game.LoadTexture(_game.GameMap.MiniMap.Image);

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Invoke(Draw));  // the map, the dots and the names
        root.Add(new MinimapFrame()); // the frame on top of the map
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        AddRow(root);
        AddZoom(root);
        Scene.Add(root);
    }

    // The row of five buttons on the top bar: the house, the book, the
    // pin, the person and the funnel. The book opens the world map. The
    // other four have no job yet, so they show the official disabled art
    // and cannot be clicked.
    private void AddRow(GameObject root)
    {
        string[] names = { "town", "map", "navi", "npc", "filter" };
        for (int i = 0; i < names.Length; i++)
        {
            var b = new GameObject();
            b.Add(new Rect(new Rectangle(Pos.X + _lay.Get("rowX", 61) + i * _lay.Get("rowPitch", 22),
                Pos.Y + _lay.Get("rowY", 4), BtnSize, BtnSize)));
            if (names[i] == "map")
            {
                b.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _game.ShowWindow(HotkeyAction.WorldMap) });
                b.Add(new Image("ui/mini_btn_map"));
            }
            else b.Add(new Picture($"ui/mini_btn_{names[i]}_d.png"));
            root.AddChild(b);
        }
    }

    // The zoom pair on the top bar: the minus and the plus.
    private void AddZoom(GameObject root)
    {
        int x = Pos.X + _lay.Get("zoomX", 15), y = Pos.Y + _lay.Get("zoomY", 4);
        int pitch = _lay.Get("zoomPitch", 22);
        var minus = new GameObject();
        minus.Add(new Rect(new Rectangle(x, y, BtnSize, BtnSize)));
        minus.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _zoom = Math.Max(1, _zoom - 1) });
        minus.Add(new Image("ui/mini_btn_zoomout"));
        root.AddChild(minus);
        var plus = new GameObject();
        plus.Add(new Rect(new Rectangle(x + pitch, y, BtnSize, BtnSize)));
        plus.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _zoom = Math.Min(MaxZoom, _zoom + 1) });
        plus.Add(new Image("ui/mini_btn_zoomin"));
        root.AddChild(plus);
    }

    // The part of the map art we show: the whole art at zoom 1, a window
    // around the player when zoomed in.
    private Rectangle Source(int artW, int artH)
    {
        int w = Math.Max(1, artW / _zoom), h = Math.Max(1, artH / _zoom);
        var mini = _game.GameMap.MiniMap;
        int px = (int)((_game.PlayerX + mini.CenterX) / (float)mini.Width * artW);
        int py = (int)((_game.PlayerY + mini.CenterY) / (float)mini.Height * artH);
        return new Rectangle(Math.Clamp(px - w / 2, 0, artW - w), Math.Clamp(py - h / 2, 0, artH - h), w, h);
    }

    // The map art in the well, the dots and the names.
    private void Draw(UiGfx gfx)
    {
        var well = Well();
        var map = _game.GameMap;
        var tex = gfx.Tex(map.MiniMap.Image);
        var src = Source(tex.Width, tex.Height);
        float s = Math.Min((float)well.Width / src.Width, (float)well.Height / src.Height);
        var dest = new Rectangle(well.X + (int)((well.Width - src.Width * s) / 2),
            well.Y + (int)((well.Height - src.Height * s) / 2), (int)(src.Width * s), (int)(src.Height * s));
        gfx.Sb.Draw(tex, dest, src, Color.White);

        // A dot on the map: the world point to a pixel in the shown art.
        void Dot(float wx, float wy, Color ink)
        {
            var (ax, ay) = map.MiniMap.ToDot(wx, wy, tex.Width, tex.Height);
            if (ax < src.X || ay < src.Y || ax >= src.Right || ay >= src.Bottom) return; // outside the shown part
            int x = dest.X + (int)((ax - src.X) * s), y = dest.Y + (int)((ay - src.Y) * s);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(x - 1, y - 1, 3, 3), ink);
        }
        foreach (var p in map.Portals) Dot(p.X, p.Y, new Color(80, 160, 255));
        Dot(_game.PlayerX, _game.PlayerY, new Color(255, 220, 0));

        // The street name and the map name (the official names) over the map.
        var name = MapUi.Name(map.MapId);
        Text(gfx, name.Street, well.X + _lay.Get("streetX", 8), well.Y + _lay.Get("streetY", 6), new Color(212, 225, 229));
        Text(gfx, name.Name, well.X + _lay.Get("nameX", 8), well.Y + _lay.Get("nameY", 22), Color.White);
    }

    // The name text with a dark shadow, so it reads over the map art.
    private static void Text(UiGfx gfx, string text, int x, int y, Color ink)
    {
        if (text == "") return;
        var t = gfx.Ui.GetSmall(text);
        gfx.Sb.Draw(t, new Vector2(x + 1, y + 1), new Color(0, 0, 0, 160));
        gfx.Sb.Draw(t, new Vector2(x, y), ink);
    }
}

// The world map window: the whole world, with the regions. Click a region to
// see the maps of that region, click a map to travel there.
public sealed class WorldMapWindow : UiWindow
{
    private readonly MushTownGame _game;
    private WorldRegionUi? _picked; // the region the player clicked
    private WorldRegionUi? _shown;  // the region the list was built for (the picked one, or the one we stand in)
    // The regions with a map of ours: their dots are the live ones.
    private readonly HashSet<WorldRegionUi> _live = new();
    // The maps of the shown region: the official name wrapped over the
    // lines it needs, and the spot of every line on the screen.
    private readonly List<(MapNameUi Map, string Text, int Y)> _lines = new();
    // The list scrolls like the official panel: the first line on screen,
    // the lines that do not fit and the wheel that moves the list.
    private int _scroll;    // the first line on screen
    private int _hidden;    // the lines that do not fit
    private int _wheel;     // the wheel value of the last frame
    private bool _toEnd;    // the shot mode wants the end of the list

    public WorldMapWindow(MushTownGame game) : base(HotkeyAction.WorldMap) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("ww", 852), _lay.Get("wh", 561));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 30));
    public override Point LayoutPos() => new(_lay.Get("wx", 250), _lay.Get("wy", 30));

    public override void Build()
    {
        _game.LoadTexture("ui/wm_frame.png");
        _game.LoadTexture("ui/wm_scroll_base.png");
        _game.LoadTexture("ui/wm_scroll_thumb.png");
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture("ui/map_x" + state);
        _game.LoadTexture(MapUi.Data.World.Image);

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
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
        // the maps of the shown region: click one to travel there
        _lines.Clear();
        var here = MapUi.Region(_game.GameMap.MapId);
        if (here != null && _picked != null && _picked != here) _picked = null; // the player left that region
        var listRegion = _picked ?? here;
        if (listRegion != _shown) _scroll = 0; // a new list starts at the top
        _shown = listRegion;
        _hidden = 0;
        if (_shown != null && maps.TryGetValue(_shown, out var shown))
            BuildList(root, shown);
        root.Add(new Invoke(Draw));
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MapWindowParts.Close(this, new Rectangle(Pos.X + _lay.Get("wcloseX", 829), Pos.Y + _lay.Get("wcloseY", 12), 11, 11)));
        Scene.Add(root);
    }

    // The map list of the shown region: every name on the lines it needs, and
    // a click box on the part of the name that is on screen (clicking it
    // travels there).
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
                _lines.Add((map, lines[i], listY + (line - _scroll) * listStep));
            }
            if (map.Id == _game.GameMap.MapId) continue; // the map we stand on: nowhere to go
            var o = new GameObject();
            // The click box is only the part of the name that is on screen:
            // a name that is half scrolled away must not catch the clicks
            // of the row above it.
            int top = listY + Math.Max(0, start - _scroll) * listStep;
            int bottom = listY + Math.Min(visible, start + lines.Count - _scroll) * listStep;
            o.Add(new Rect(new Rectangle(listX, top, listW, bottom - top)));
            o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => { _game.TravelTo(map.Id); Show = false; } });
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

    // Where a region's dot sits on the map art.
    private Point RegionSpot(WorldRegionUi region) => new(
        Rect().X + _lay.Get("worldX", 198) + region.X,
        Rect().Y + _lay.Get("worldY", 75) + region.Y);

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

    // The map changed: rebuild the list (the map we stand on has no button).
    public void OnMapChanged() => Dirty = true;

    // The region marks on the world map and the maps of the shown region.
    private void Draw(UiGfx gfx)
    {
        var r = Rect();
        int mapId = _game.GameMap.MapId;
        var here = MapUi.Region(mapId);
        int dotW = _lay.Get("dotW", 7), dotH = _lay.Get("dotH", 7);
        foreach (var region in MapUi.Data.World.Regions)
        {
            var ink = region == here ? new Color(255, 220, 0)
                : region == _shown ? Color.White
                : _live.Contains(region) ? new Color(120, 160, 200) : new Color(80, 95, 110);
            var spot = RegionSpot(region);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(spot.X - (dotW - 1) / 2, spot.Y - (dotH - 1) / 2, dotW, dotH), ink);
        }
        if (_shown == null) return;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_shown.Title), new Vector2(r.X + _lay.Get("infoX", 20), r.Y + _lay.Get("infoY", 51)), Color.White);
        var list = ListRect();
        foreach (var (map, text, y) in _lines)
        {
            var ink = map.Id == mapId ? new Color(255, 220, 0) : Color.White;
            gfx.Sb.Draw(gfx.Ui.GetSmall(text), new Vector2(list.X, y), ink);
        }
        if (_hidden > 0) ScrollBar(gfx);
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
        if (!ListRect().Contains(gx, gy)) return;
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
        var r = Rect();
        return new Rectangle(r.X + _lay.Get("listX", 16), r.Y + _lay.Get("listY", 75),
            _lay.Get("listW", 146), _lay.Get("listH", 340));
    }

    // The official scroll bar of the list: the track and the cursor on it.
    // The track spans the rows of the list, so it uses the list numbers.
    // The thumb is the official art at its own size (the texture size).
    private void ScrollBar(UiGfx gfx)
    {
        var box = ListRect();
        int x = Rect().X + _lay.Get("scrollX", 165);
        int top = box.Y;
        int len = box.Height;
        var thumb = gfx.Tex("ui/wm_scroll_thumb.png");
        var track = gfx.Tex("ui/wm_scroll_base.png");
        gfx.Sb.Draw(track, new Rectangle(x, top, track.Width, len), Color.White);
        int y = top + (len - thumb.Height) * _scroll / _hidden;
        gfx.Sb.Draw(thumb, new Rectangle(x, y, thumb.Width, thumb.Height), Color.White);
    }
}
