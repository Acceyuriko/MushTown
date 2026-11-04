using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The one layout file every part of the world map reads.
internal static class MapLay
{
    public static readonly PanelLayout Lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-map.json"));
}

// The world map data the exporter wrote: assets/ui/worldmap.json. Every frame
// asks for the shown map and its names, so it loads once.
internal static class WorldMapUi
{
    private static WorldMapData? _data;
    private static readonly Dictionary<string, WorldMapRegion> _maps = new();
    private static readonly Dictionary<int, string> _names = new();
    // The maps we exported: only these carry NPCs, monsters and a destination.
    private static readonly Dictionary<int, MapData> _files = new(); // every map file read, by id

    public static WorldMapData Data
    {
        get
        {
            if (_data == null)
            {
                _data = MushTownGame.LoadJson<WorldMapData>(Path.Combine(MushTownGame.AssetsDir, "ui", "worldmap.json"));
                foreach (var m in _data.Maps) _maps[m.Name] = m;
                foreach (var n in _data.Names) _names[n.Id] = n.Name;
            }
            return _data;
        }
    }

    public static WorldMapRegion? Region(string name)
    {
        _ = Data;
        return _maps.GetValueOrDefault(name);
    }

    // The official name of a map id, ours or not (the json carries the names of
    // every node the exported world maps show).
    public static string Name(int id)
    {
        _ = Data;
        if (_names.TryGetValue(id, out string? name)) return name;
        return MapUi.Name(id).Name;
    }

    // The regions the first combo lists: the links of the root map that point at a
    // world map we exported, with the name the link carries. Built once, because
    // a combo asks for it every frame.
    private static List<(string Text, string Target)>? _regions;

    public static List<(string Text, string Target)> Regions() => _regions ??= BuildRegions();

    private static List<(string Text, string Target)> BuildRegions()
    {
        var list = new List<(string, string)>();
        foreach (var link in Region("WorldMap")!.Links)
            if (Region(link.Target) != null && link.ToolTip != "") list.Add((link.ToolTip, link.Target));
        return list;
    }

    // The nodes of a region that stand for a map we have: the third combo.
    public static List<int> Ours(string region)
    {
        var list = new List<int>();
        var map = Region(region)!;
        for (int i = 0; i < map.Nodes.Count; i++)
            if (map.Nodes[i].Ours) list.Add(i);
        return list;
    }

    // The NPCs and monsters one map shows: the left column lists them and the
    // window loads their pictures. Same query, so the two cannot drift.
    public static List<(int Kind, int Id)> LivesOf(int mapId)
    {
        var file = MapFile(mapId);
        return file.Npcs.Select(n => (Kind: 0, Id: n.NpcId))
            .Concat(file.Life.Select(l => (Kind: 1, Id: l.MobId)))
            .Distinct().ToList();
    }

    // The maps we exported that hold this NPC or monster, by name. The tip asks
    // on every frame of a hover, so the answer stays.
    private static readonly Dictionary<(int, int), List<string>> _holds = new();

    public static List<string> Holds(int kind, int id)
    {
        if (_holds.TryGetValue((kind, id), out var hit)) return hit;
        var list = new List<string>();
        foreach (var map in MapUi.Data.Maps)
        {
            var file = MapFile(map.Id);
            bool here = kind == 0 ? file.Npcs.Any(n => n.NpcId == id) : file.Life.Any(l => l.MobId == id);
            if (here) list.Add(map.Name);
        }
        _holds[(kind, id)] = list;
        return list;
    }

    // The map the left column describes: the picked one, else the map we stand
    // on when it is on this world map. The header box names it and the window
    // builds its NPC and monster rows from it.
    public static int ShownMapId(WorldMapState state, int here)
    {
        if (state.Sel >= 0) return state.SelMap;
        foreach (var node in Region(state.Shown)!.Nodes)
            if (node.Maps.Contains(here)) return here;
        return 0;
    }

    // The icon of one node type: every type a node uses has its picture exported.
    public static WorldMapIcon Icon(int type) => _data!.Icons.First(i => i.Type == type);
    public static WorldMapIcon Pin => _data!.Pin;
    public static WorldMapIcon Sel => _data!.Sel;

    // The maps we exported: only these carry NPCs, monsters and a destination.
    private static bool Has(int id) => MapUi.Data.Maps.Any(m => m.Id == id);

    // The map a node travels to: the one we stand on when it is there, else the
    // first of ours. The tip of the node and the click both read this answer.
    public static int PickMap(WorldMapNode node, int here)
    {
        if (node.Maps.Contains(here) && Has(here)) return here;
        return node.Maps.FirstOrDefault(Has);
    }

    public static MapData MapFile(int id)
    {
        if (_files.TryGetValue(id, out MapData? hit)) return hit;
        _files[id] = MushTownGame.LoadJson<MapData>(Path.Combine(MushTownGame.AssetsDir, "maps", $"{id}.json"));
        return _files[id];
    }
}

// The window state: which world map is on screen and which node is picked. It
// outlives a rebuild, so the buttons and the map switch keep their place.
public sealed class WorldMapState
{
    public string Shown = "WorldMap";
    public int Sel = -1;      // index into the shown region's nodes
    public int SelMap;        // the map the picked node travels to, 0 = none
    public int OpenCombo = -1; // which combo box lists its choices, -1 = none
    public int Hover = -1;     // the link under the mouse, -1 = none
    public int HoverNode = -1; // the node under the mouse, -1 = none
    public bool Search;       // the search window is open
}

// The map picture with its nodes: the official base art, one icon per MapList
// entry, the red pin on the map we stand on and the blue arrow on the picked
// node.
public sealed class WorldMapArt : Component, IDrawable
{
    public required MushTownGame Game { get; init; }
    public required WorldMapState State { get; init; }

    public void Draw(UiGfx gfx)
    {
        // The object's own box already sits on the map area, so nothing here adds
        // the window offset again: the node click boxes use the same origin.
        var box = Owner.Get<Rect>()!.Screen;
        var region = WorldMapUi.Region(State.Shown)!;
        State.HoverNode = -1; // the node marks fill this in on the same frame
        gfx.Sb.Draw(gfx.Tex(region.Image), new Vector2(box.X, box.Y), Color.White);

        // The links: the base art already carries every block, so the picture a
        // link points at is the highlight plate, drawn only under the mouse. The
        // plate is a block-shaped picture, so a plain box would light up two
        // blocks where they touch: the pixel decides. The topmost wins. This one
        // answer feeds the plate, the tooltip and the click, so they cannot
        // disagree.
        // Only the object the mouse is actually on lights a block: a node stands
        // on top of the links, so hovering a node must not light the block under it.
        State.Hover = gfx.Hovered == Owner ? LinkAt(gfx, region, box) : -1;
        if (State.Hover >= 0)
        {
            var link = region.Links[State.Hover];
            gfx.Sb.Draw(gfx.Tex($"ui/{link.Image}"), new Vector2(box.X + link.X, box.Y + link.Y), Color.White);
        }

        // The nodes: the icon's origin is the point the WZ gives, and it is not
        // the middle of the picture, so the art hangs off it.
        int now = Game.GameMap.MapId;
        for (int i = 0; i < region.Nodes.Count; i++)
        {
            var node = region.Nodes[i];
            var icon = node.Type >= 0 ? WorldMapUi.Icon(node.Type) : null;
            int px = box.X + region.OriginX + node.X;
            int py = box.Y + region.OriginY + node.Y;
            if (icon != null) gfx.Sb.Draw(gfx.Tex($"ui/wm_node_{node.Type}.png"), new Vector2(px - icon.OX, py - icon.OY), Color.White);
            if (node.Maps.Contains(now))
            {
                var pin = gfx.Tex("ui/wm_cur.png");
                // The pin bobs: the official art has eight frames with the same
                // picture and four origins, so the bob is the y offset.
                int bob = (int)(gfx.Time / 130) % 8;
                gfx.Sb.Draw(pin, new Vector2(px - WorldMapUi.Pin.OX, py - WorldMapUi.Pin.OY - (bob < 4 ? bob : 7 - bob)), Color.White);
            }
            if (i != State.Sel) continue;
            var arrow = gfx.Tex("ui/wm_select.png");
            gfx.Sb.Draw(arrow, new Vector2(px - WorldMapUi.Sel.OX, py - WorldMapUi.Sel.OY), Color.White);
        }
    }

    private static int LinkAt(UiGfx gfx, WorldMapRegion region, Rectangle box)
    {
        for (int i = region.Links.Count - 1; i >= 0; i--)
        {
            var link = region.Links[i];
            var r = new Rectangle(box.X + link.X, box.Y + link.Y, link.W, link.H);
            if (Hit(gfx, link.Image, r, gfx.Mouse)) return i;
        }
        return -1;
    }

    // Whether the mouse is on a part of the link plate that is actually drawn.
    // The plate's see-through pixels do not count, so two blocks that touch do
    // not both light up.
    private static readonly Dictionary<string, (int W, bool[] Mask)> _masks = new();

    private static bool Hit(UiGfx gfx, string image, Rectangle r, Point mouse)
    {
        if (!r.Contains(mouse)) return false;
        if (!_masks.TryGetValue(image, out var hit))
        {
            var tex = gfx.Tex($"ui/{image}");
            var px = new Color[tex.Width * tex.Height];
            tex.GetData(px);
            var mask = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) mask[i] = px[i].A > 200;
            hit = (tex.Width, mask);
            _masks[image] = hit;
        }
        return hit.Mask[(mouse.Y - r.Y) * hit.W + (mouse.X - r.X)];
    }
}

// The left column: the map name in the header box. The NPC and monster rows are
// objects of their own (the window builds them).
public sealed class WorldMapInfo : Component, IDrawable
{
    public required MushTownGame Game { get; init; }
    public required WorldMapState State { get; init; }

    internal static readonly Color NpcInk = new(0x66, 0xFF, 0xFF);
    internal static readonly Color MobInk = new(0xFF, 0x00, 0x66);

    // The header box and the body box of the left column, and the name of the
    // map they describe. The NPC and monster rows are objects of their own (the
    // window builds them), so this only paints the column.
    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex("ui/wm_info_top.png"), new Vector2(box.X + MapLay.Lay.Get("infoX", 7), box.Y + MapLay.Lay.Get("infoY", 28)), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/wm_info_body.png"), new Vector2(box.X + MapLay.Lay.Get("infoX", 7), box.Y + MapLay.Lay.Get("infoBodyY", 97)), Color.White);

        // What the panel describes: the picked map, or the picked node when we
        // have no map for it (then there is no life list). With no pick at all
        // there is nothing to name.
        int mapId = WorldMapUi.ShownMapId(State, Game.GameMap.MapId);
        string title = mapId != 0 ? WorldMapUi.Name(mapId)
            : State.Sel >= 0 ? WorldMapUi.Region(State.Shown)!.Nodes[State.Sel].Name : "";
        if (title == "") return;
        gfx.Sb.Draw(Text.Fit(gfx, title, MapLay.Lay.Get("nameW", 120)), new Vector2(box.X + MapLay.Lay.Get("nameX", 20), box.Y + MapLay.Lay.Get("nameY", 51)), Color.White);
    }
}

// The data shapes of ui/worldmap.json (PascalCase, like every other exported
// json: the loader is case sensitive).
public sealed class WorldMapData
{
    public List<WorldMapRegion> Maps { get; set; } = new();
    public List<WorldMapIcon> Icons { get; set; } = new();
    public WorldMapIcon Pin { get; set; } = new();
    public WorldMapIcon Sel { get; set; } = new();
    public List<WorldMapName> Names { get; set; } = new();
}

public sealed class WorldMapIcon
{
    public int Type { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int OX { get; set; }
    public int OY { get; set; }
}

public sealed class WorldMapName
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class WorldMapRegion
{
    public string Name { get; set; } = "";
    public string Image { get; set; } = "";
    public int W { get; set; }
    public int H { get; set; }
    // The base art's origin: the centre of the picture. A node's X/Y is measured
    // from there; a link's X/Y is measured from the art's top left corner.
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public string Parent { get; set; } = "";
    public List<WorldMapNode> Nodes { get; set; } = new();
    public List<WorldMapLink> Links { get; set; } = new();
}

public sealed class WorldMapNode
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Type { get; set; }
    public List<int> Maps { get; set; } = new();
    public string Name { get; set; } = "";
    public bool Ours { get; set; }
}

public sealed class WorldMapLink
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public string ToolTip { get; set; } = "";
    public string Target { get; set; } = "";
    public string Image { get; set; } = "";
}

// One combo box: the official three pieces at their own sizes (the left cap, the
// stretched middle, the right cap that carries the arrow). The open list is a
// second ComboBox whose Rows are set: one row per choice, the row under the
// mouse in the mouse over pieces. The text of the box and of its rows is a Text
// component of the same object, drawn over the pieces.
public sealed class ComboBox : Component, IDrawable
{
    public IReadOnlyList<string>? Rows { get; init; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        if (Rows == null)
        {
            string state = gfx.Hovered != Owner ? "n" : gfx.Pressed ? "p" : "h";
            Row(gfx, r, state, arrow: true);
            return;
        }
        // The row under the mouse wears the mouse over pieces, like the box.
        int rowH = MapLay.Lay.Get("comboRowH", 20);
        int at = gfx.Mouse.Y - r.Y;
        int over = gfx.Mouse.X >= r.X && gfx.Mouse.X < r.Right && at >= 0 && at < Rows.Count * rowH ? at / rowH : -1;
        for (int i = 0; i < Rows.Count; i++)
            Row(gfx, new Rectangle(r.X, r.Y + i * rowH, r.Width, rowH), i == over ? "h" : "n", arrow: false,
                i == over ? ListOver : Color.White);
    }

    // The pieces only change their arrow between states, and a list row has no
    // arrow: a light blue wash is what makes the row under the mouse visible.
    private static readonly Color ListOver = new(203, 226, 255);

    // One row of the pieces. A list row has no arrow, so its right end is the left
    // cap mirrored: the row keeps the edge the whole box has.
    private static void Row(UiGfx gfx, Rectangle r, string state, bool arrow, Color? tint = null)
    {
        var ink = tint ?? Color.White;
        var l = gfx.Tex($"ui/wm_combo_{state}0.png");
        var m = gfx.Tex($"ui/wm_combo_{state}1.png");
        var t = gfx.Tex($"ui/wm_combo_{state}2.png");
        int end = arrow ? t.Width : l.Width;
        gfx.Sb.Draw(l, new Vector2(r.X, r.Y), ink);
        gfx.Sb.Draw(m, new Rectangle(r.X + l.Width, r.Y, r.Width - l.Width - end, m.Height), ink);
        if (arrow) gfx.Sb.Draw(t, new Vector2(r.Right - t.Width, r.Y), ink);
        else gfx.Sb.Draw(l, new Vector2(r.Right - l.Width, r.Y), null, ink, 0f, Vector2.Zero, 1f, SpriteEffects.FlipHorizontally, 0f);
    }
}

// One row of the search results: the icon and the highlight plate under the
// mouse. The name is a Text component of the same object.
public sealed class WorldMapSearchRow : Component, IDrawable
{
    public required string Icon { get; init; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        if (gfx.Hovered == Owner) gfx.Sb.Draw(gfx.Tex("ui/wm_s_row.png"), new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(gfx.Tex(Icon), new Vector2(r.X, r.Y), Color.White);
    }
}

// The node the mouse rests on says its index: the tooltip reads the same answer
// the click does, so a node never names itself while another one is hovered.
public sealed class WorldMapNodeMark : Component, IDrawable
{
    public required WorldMapState State { get; init; }
    public required int Index { get; init; }

    public void Draw(UiGfx gfx)
    {
        if (gfx.Hovered == Owner) State.HoverNode = Index;
    }
}

// The tooltip of the official window: a block names itself, a node lists the
// monsters and NPCs of its map. The grey rule and the two bullets are the art
// the WZ carries for this tip, so the pieces are not invented here. The tip of
// an NPC or monster row is a LifeTip, drawn by the tip layer of the HUD.
public sealed class WorldMapTip : Component, IDrawable
{
    public required MushTownGame Game { get; init; }
    public required WorldMapState State { get; init; }

    private readonly List<(Texture2D Art, string Text)> _rows = new();

    public void Draw(UiGfx gfx)
    {
        if (State.HoverNode >= 0) DrawNode(gfx);
        else if (State.Hover >= 0) DrawLink(gfx);
    }

    private void DrawLink(UiGfx gfx)
    {
        string text = WorldMapUi.Region(State.Shown)!.Links[State.Hover].ToolTip;
        if (text == "") return;
        int room = MapLay.Lay.Get("tipW", 200);
        var t = Text.Fit(gfx, text, room);
        int pad = MapLay.Lay.Get("tipPad", 7);
        // The frame's two ends are 14 and 15 tall, so it cannot be shorter than that.
        int h = Math.Max(t.Height + pad * 2, gfx.Tex("ui/tip_nw.png").Height + gfx.Tex("ui/tip_s.png").Height);
        var box = Anchor.At(gfx, t.Width + pad * 2, h);
        NineSlice.Draw(gfx, "tip", box);
        Text.Draw(gfx.Sb, gfx.Ui, t, new Rectangle(box.X + pad, box.Y, t.Width, box.Height), Color.White);
    }

    // A node: the name of the map over the grey rule, then one row per monster
    // and per NPC. A node we have no map for only names itself.
    private void DrawNode(UiGfx gfx)
    {
        var region = WorldMapUi.Region(State.Shown)!;
        var node = region.Nodes[State.HoverNode];
        string name = node.Name;
        _rows.Clear();
        int id = WorldMapUi.PickMap(node, Game.GameMap.MapId);
        if (id != 0)
        {
            name = WorldMapUi.Name(id);
            var file = WorldMapUi.MapFile(id);
            foreach (int mob in file.Life.Select(l => l.MobId).Distinct()) _rows.Add((gfx.Tex("ui/wm_tip_mob.png"), Game.MobName(mob)));
            foreach (int npc in file.Npcs.Select(n => n.NpcId).Distinct()) _rows.Add((gfx.Tex("ui/wm_tip_npc.png"), Game.NpcName(npc)));
        }

        int pad = MapLay.Lay.Get("tipPad", 7);
        int rowH = MapLay.Lay.Get("tipRowH", 14);
        int col = MapLay.Lay.Get("tipBullet", 14); // the 9 px bullet and the gap before the name
        int room = MapLay.Lay.Get("tipW", 200);
        var title = Text.Fit(gfx, name, room);
        int inner = title.Width;
        foreach (var (_, text) in _rows) inner = Math.Max(inner, gfx.Ui.GetSmall(text).Width + col);
        inner = Math.Min(inner, room);

        var box = LifeTip.Head(gfx, Anchor, title, inner, _rows.Count * rowH, out int y);
        foreach (var (art, text) in _rows)
        {
            gfx.Sb.Draw(art, new Vector2(box.X + pad, y + (rowH - art.Height) / 2), Color.White);
            Text.Draw(gfx.Sb, gfx.Ui, Text.Fit(gfx, text, box.Width - pad * 2 - col),
                new Rectangle(box.X + pad + col, y, box.Width - pad * 2 - col, rowH), Color.White);
            y += rowH;
        }
    }

    // The box of this window's tips lands where every other tip does: the tip
    // layer owns the anchor and this tip asks its own object for it.
    private TipAnchor Anchor => Owner.Get<TipAnchor>()!;
}
