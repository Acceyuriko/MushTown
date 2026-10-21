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

// The channel bar (HUD, bottom right): the official CHANGE CHANNEL panel
// with the world name, the channel count and the channel list. The game
// plays on one channel, so the list shows them all and marks the one in use.
public static class ChannelBar
{
    public const int CurrentChannel = 0; // the game plays on the first channel

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));

    // The bar box: the art is 436x67, the layout file gives the spot.
    public static Rectangle Rect() => new(_lay.Get("chX", 930), _lay.Get("chY", 616), 436, 67);

    // One channel slot in the list strip.
    private static Rectangle Slot(int i) => new(Rect().X + _lay.Get("chSlotX", 14) + i * _lay.Get("chSlotPitch", 13),
        Rect().Y + _lay.Get("chSlotY", 54), _lay.Get("chSlotW", 11), _lay.Get("chSlotH", 12));

    // The bar tree: the panel art. The game adds it to the HUD scene
    // (MushTown.Client/Panels/HudBar.cs). The channel slots are not
    // buttons: the game plays on one channel, so there is nothing to click.
    public static GameObject Build(MushTownGame game)
    {
        game.LoadTexture(MapUi.Data.Channel.Image);
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new Picture(MapUi.Data.Channel.Image));
        root.Add(new Invoke(Draw));
        return root;
    }

    // The world name, the channel count and the channel list.
    private static void Draw(UiGfx gfx)
    {
        var r = Rect();
        var channel = MapUi.Data.Channel;
        int textY = r.Y + _lay.Get("chTextY", 30);
        gfx.Sb.Draw(gfx.Ui.GetSmall(channel.World.ToUpperInvariant() + " WORLD"), new Vector2(r.X + _lay.Get("chTextX", 14), textY), new Color(255, 200, 90));
        gfx.Sb.Draw(gfx.Ui.GetSmall($"[{CurrentChannel + 1}/{channel.Channels}]"), new Vector2(r.X + _lay.Get("chCountX", 356), textY), Color.White);
        for (int i = 0; i < channel.Channels; i++)
        {
            var s = Slot(i);
            if (i == CurrentChannel) gfx.Sb.Draw(gfx.Pixel, s, new Color(255, 220, 0));
            else gfx.Sb.Draw(gfx.Pixel, new Rectangle(s.X + 4, s.Y, 2, s.Height), new Color(68, 136, 187));
        }
    }
}
