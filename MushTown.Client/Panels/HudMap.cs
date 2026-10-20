using Microsoft.Xna.Framework;
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
                    foreach (int id in r.Maps) _regions[id] = r;
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

// The parts both map windows have: the title strip that drags the window
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

// The minimap window: the map around the player, big.
public sealed class MinimapWindow : UiWindow
{
    private readonly MushTownGame _game;
    public MinimapWindow(MushTownGame game) : base(HotkeyAction.Minimap) { _game = game; }

    // Every number of this window lives in layouts/panel-map.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 420), _lay.Get("h", 320));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("insetT", 70));
    public override Point LayoutPos() => new(_lay.Get("x", 470), _lay.Get("y", 40));

    public override void Build()
    {
        foreach (string file in MapUi.Data.Frame) _game.LoadTexture("ui/" + file);
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture("ui/map_x" + state);
        _game.LoadTexture(_game.GameMap.MiniMap.Image);

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Invoke(Draw));
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MapWindowParts.Close(this, new Rectangle(Pos.X + _lay.Get("closeX", 396), Pos.Y + _lay.Get("closeY", 8), 11, 11)));
        Scene.Add(root);
    }

    // The map in the well, the dots, and the official frame around it.
    private void Draw(UiGfx gfx)
    {
        var r = Rect();
        int left = _lay.Get("insetL", 7), top = _lay.Get("insetT", 70);
        int right = _lay.Get("insetR", 7), bottom = _lay.Get("insetB", 10);
        var well = new Rectangle(r.X + left, r.Y + top, r.Width - left - right, r.Height - top - bottom);

        var map = _game.GameMap;
        var tex = gfx.Tex(map.MiniMap.Image);
        float s = Math.Min((float)well.Width / tex.Width, (float)well.Height / tex.Height);
        int mw = (int)(tex.Width * s), mh = (int)(tex.Height * s);
        int mx = well.X + (well.Width - mw) / 2, my = well.Y + (well.Height - mh) / 2;

        // The white fill of the well goes under the map, the frame slices
        // over it. The json lists the pieces in this order: the fill
        // first, then nw, n, ne, w, e, sw, s, se.
        var files = MapUi.Data.Frame;
        gfx.Sb.Draw(gfx.Tex("ui/" + files[0]), well, Color.White);
        gfx.Sb.Draw(tex, new Rectangle(mx, my, mw, mh), Color.White);
        MapDots.Draw(gfx, map, _game.PlayerX, _game.PlayerY, mx, my, mw, mh);
        for (int i = 1; i < files.Count; i++)
        {
            var box = i switch
            {
                1 => new Rectangle(r.X, r.Y, 44, 76),                                 // nw
                2 => new Rectangle(r.X + 44, r.Y, r.Width - 59, 70),                  // n
                3 => new Rectangle(r.X + r.Width - 15, r.Y, 15, 76),                 // ne
                4 => new Rectangle(r.X, r.Y + 76, 9, r.Height - 92),                  // w
                5 => new Rectangle(r.X + r.Width - 9, r.Y + 76, 9, r.Height - 92),    // e
                6 => new Rectangle(r.X, r.Y + r.Height - 16, 15, 16),                // sw
                7 => new Rectangle(r.X + 15, r.Y + r.Height - 10, r.Width - 30, 10), // s
                _ => new Rectangle(r.X + r.Width - 15, r.Y + r.Height - 16, 15, 16), // se
            };
            gfx.Sb.Draw(gfx.Tex("ui/" + files[i]), box, Color.White);
        }

        // The header: a small map picture in the frame's own white square,
        // then the street name and the map name (WZ vectors).
        float ps = Math.Min(36f / tex.Width, 36f / tex.Height);
        int pw = (int)(tex.Width * ps), ph = (int)(tex.Height * ps);
        gfx.Sb.Draw(tex, new Rectangle(r.X + 7 + (36 - pw) / 2, r.Y + 30 + (36 - ph) / 2, pw, ph), Color.White);
        var name = MapUi.Name(map.MapId);
        gfx.Sb.Draw(gfx.Ui.GetSmall(name.Street), new Vector2(r.X + _lay.Get("streetX", 50), r.Y + _lay.Get("streetY", 30)), new Color(212, 225, 229));
        gfx.Sb.Draw(gfx.Ui.GetSmall(name.Name), new Vector2(r.X + _lay.Get("nameX", 50), r.Y + _lay.Get("nameY", 48)), Color.White);
    }
}

// The world map window: the whole world, with the regions.
public sealed class WorldMapWindow : UiWindow
{
    private readonly MushTownGame _game;
    public WorldMapWindow(MushTownGame game) : base(HotkeyAction.WorldMap) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("ww", 852), _lay.Get("wh", 561));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 30));
    public override Point LayoutPos() => new(_lay.Get("wx", 250), _lay.Get("wy", 30));

    public override void Build()
    {
        _game.LoadTexture("ui/wm_frame.png");
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture("ui/map_x" + state);
        _game.LoadTexture(MapUi.Data.World.Image);

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new Picture("ui/wm_frame.png"));
        root.Add(new Picture(MapUi.Data.World.Image, _lay.Get("worldX", 198), _lay.Get("worldY", 75)));
        root.Add(new Invoke(Draw));
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MapWindowParts.Close(this, new Rectangle(Pos.X + _lay.Get("wcloseX", 829), Pos.Y + _lay.Get("wcloseY", 12), 11, 11)));
        Scene.Add(root);
    }

    // The region marks on the world map and the current region in MAP INFO.
    private void Draw(UiGfx gfx)
    {
        var r = Rect();
        int wx = r.X + _lay.Get("worldX", 198), wy = r.Y + _lay.Get("worldY", 75);
        int mapId = _game.GameMap.MapId;
        var current = MapUi.Region(mapId);
        foreach (var region in MapUi.Data.World.Regions)
        {
            bool here = region == current;
            var ink = here ? new Color(255, 220, 0) : new Color(120, 160, 200);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(wx + region.X - 3, wy + region.Y - 3, 7, 7), ink);
        }
        if (current != null)
        {
            var name = MapUi.Name(mapId);
            gfx.Sb.Draw(gfx.Ui.GetSmall(current.Title), new Vector2(r.X + _lay.Get("infoX", 14), r.Y + _lay.Get("infoY", 40)), Color.White);
            gfx.Sb.Draw(gfx.Ui.GetSmall(name.Name), new Vector2(r.X + _lay.Get("infoX", 14), r.Y + _lay.Get("infoY", 40) + 16), new Color(212, 225, 229));
        }
    }
}
