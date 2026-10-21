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

// The minimap window: the official panel (Minimap.img/Mini) with the map
// of the current map, the player and the portals.
public sealed class MinimapWindow : UiWindow
{
    private readonly MushTownGame _game;
    public MinimapWindow(MushTownGame game) : base(HotkeyAction.Minimap) { _game = game; Show = true; } // the minimap is always on screen

    // Esc does not close the minimap: it is always on screen.
    public override void OnClosed() => Show = true;

    // Every number of this window lives in layouts/panel-map.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 180), _lay.Get("h", 180));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("insetT", 48));
    public override Point LayoutPos() => new(_lay.Get("x", 0), _lay.Get("y", 0));

    public override void Build()
    {
        _game.LoadTexture("ui/mini_bg.png");
        _game.LoadTexture(_game.GameMap.MiniMap.Image);

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Invoke(Draw));
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        Scene.Add(root);
    }

    // The official panel, the map in it, the dots and the names.
    private void Draw(UiGfx gfx)
    {
        var r = Rect();
        int left = _lay.Get("insetL", 8), top = _lay.Get("insetT", 48);
        int right = _lay.Get("insetR", 8), bottom = _lay.Get("insetB", 8);
        var well = new Rectangle(r.X + left, r.Y + top, r.Width - left - right, r.Height - top - bottom);

        gfx.Sb.Draw(gfx.Tex("ui/mini_bg.png"), r, Color.White);
        var map = _game.GameMap;
        var tex = gfx.Tex(map.MiniMap.Image);
        float s = Math.Min((float)well.Width / tex.Width, (float)well.Height / tex.Height);
        int mw = (int)(tex.Width * s), mh = (int)(tex.Height * s);
        int mx = well.X + (well.Width - mw) / 2, my = well.Y + (well.Height - mh) / 2;
        gfx.Sb.Draw(tex, new Rectangle(mx, my, mw, mh), Color.White);
        MapDots.Draw(gfx, map, _game.PlayerX, _game.PlayerY, mx, my, mw, mh);

        // The street name and the map name (the official names).
        var name = MapUi.Name(map.MapId);
        gfx.Sb.Draw(gfx.Ui.GetSmall(name.Street), new Vector2(r.X + _lay.Get("streetX", 10), r.Y + _lay.Get("streetY", 12)), new Color(212, 225, 229));
        gfx.Sb.Draw(gfx.Ui.GetSmall(name.Name), new Vector2(r.X + _lay.Get("nameX", 10), r.Y + _lay.Get("nameY", 30)), Color.White);
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
