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

    // Every NPC and monster of the maps we exported, once each: the left column
    // can show any of them, so the window loads their pictures at once.
    private static List<(int Kind, int Id)>? _lives;

    public static List<(int Kind, int Id)> Lives() => _lives ??= BuildLives();

    private static List<(int Kind, int Id)> BuildLives()
    {
        var seen = new HashSet<(int, int)>();
        var list = new List<(int Kind, int Id)>();
        foreach (var map in MapUi.Data.Maps)
        {
            var file = MapFile(map.Id);
            foreach (var npc in file.Npcs)
                if (seen.Add((0, npc.NpcId))) list.Add((0, npc.NpcId));
            foreach (var life in file.Life)
                if (seen.Add((1, life.MobId))) list.Add((1, life.MobId));
        }
        return list;
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
    public (int Kind, int Id)? HoverLife; // the NPC or monster row under the mouse (kind 0 = NPC, 1 = monster)
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

// The left column: the map name in the header box, the level range, then the
// NPCs and monsters of that map. Only our maps have the two lists.
public sealed class WorldMapInfo : Component, IDrawable
{
    public required MushTownGame Game { get; init; }
    public required WorldMapState State { get; init; }

    internal static readonly Color NpcInk = new(0x66, 0xFF, 0xFF);
    internal static readonly Color MobInk = new(0xFF, 0x00, 0x66);

    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        State.HoverLife = null; // the rows below fill this in on the same frame
        gfx.Sb.Draw(gfx.Tex("ui/wm_info_top.png"), new Vector2(box.X + MapLay.Lay.Get("infoX", 7), box.Y + MapLay.Lay.Get("infoY", 28)), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/wm_info_body.png"), new Vector2(box.X + MapLay.Lay.Get("infoX", 7), box.Y + MapLay.Lay.Get("infoBodyY", 97)), Color.White);

        // What the panel describes: the picked map, or the picked node when we
        // have no map for it (then there is no life list). With no pick at all
        // there is nothing to name.
        int mapId = ShownMapId();
        string title = mapId != 0 ? WorldMapUi.Name(mapId)
            : State.Sel >= 0 ? WorldMapUi.Region(State.Shown)!.Nodes[State.Sel].Name : "";
        if (title == "") return;
        gfx.Sb.Draw(WorldMapInfo.Fit(gfx, title, MapLay.Lay.Get("nameW", 120)), new Vector2(box.X + MapLay.Lay.Get("nameX", 20), box.Y + MapLay.Lay.Get("nameY", 51)), Color.White);
        if (mapId == 0) return;

        var file = WorldMapUi.MapFile(mapId);
        int y = MapLay.Lay.Get("lifeY", 145);
        int x = MapLay.Lay.Get("lifeX", 16);
        int step = MapLay.Lay.Get("lifeStep", 20);
        int rows = MapLay.Lay.Get("lifeRows", 14);
        int used = 0;
        var npcIcon = gfx.Tex("ui/wm_life_npc.png");
        var mobIcon = gfx.Tex("ui/wm_life_mob.png");
        // The same kind appears once: a map holds eight snails but lists one.
        foreach (int id in file.Npcs.Select(n => n.NpcId).Distinct())
        {
            if (used++ >= rows) return;
            gfx.Sb.Draw(npcIcon, new Vector2(box.X + x, box.Y + y), Color.White);
            gfx.Sb.Draw(WorldMapInfo.Fit(gfx, Game.NpcName(id), 130), new Vector2(box.X + x + 22, box.Y + y + 2), NpcInk);
            Mark(gfx, box, x, y, step, 0, id);
            y += step;
        }
        foreach (int id in file.Life.Select(l => l.MobId).Distinct())
        {
            if (used++ >= rows) return;
            gfx.Sb.Draw(mobIcon, new Vector2(box.X + x, box.Y + y), Color.White);
            gfx.Sb.Draw(WorldMapInfo.Fit(gfx, Game.MobName(id), 130), new Vector2(box.X + x + 22, box.Y + y + 2), MobInk);
            Mark(gfx, box, x, y, step, 1, id);
            y += step;
        }
    }

    // The row under the mouse says which NPC or monster it shows: a row carries
    // no click, so the picture of it is the whole answer.
    private void Mark(UiGfx gfx, Rectangle box, int x, int y, int step, int kind, int id)
    {
        if (gfx.Hovered != Owner) return;
        if (!new Rectangle(box.X + x, box.Y + y, MapLay.Lay.Get("lifeW", 152), step).Contains(gfx.Mouse)) return;
        State.HoverLife = (kind, id);
    }

    // A name longer than the row is cut with two dots, so it stays inside the box.
    // The text is measured bare and the dots are added once: baking a texture per
    // cut would fill the small-text cache with strings nobody draws.
    public static Texture2D Fit(UiGfx gfx, string text, int width)
    {
        if (gfx.Ui.GetSmall(text).Width <= width) return gfx.Ui.GetSmall(text);
        while (text.Length > 0 && gfx.Ui.GetSmall(text + "..").Width > width) text = text[..^1];
        return gfx.Ui.GetSmall(text + "..");
    }

    // The map the left column describes: the picked one, else the map we stand
    // on when it is on this world map.
    private int ShownMapId()
    {
        if (State.Sel >= 0) return State.SelMap;
        foreach (var node in WorldMapUi.Region(State.Shown)!.Nodes)
            if (node.Maps.Contains(Game.GameMap.MapId)) return Game.GameMap.MapId;
        return 0;
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
// stretched middle, the right cap that carries the arrow) and the current choice
// written on it. The open list is a second ComboBox whose Rows are set: one row
// per choice, the row under the mouse in the mouse over pieces.
public sealed class ComboBox : Component, IDrawable
{
    public Func<string> Text { get; init; } = () => "";
    public IReadOnlyList<string>? Rows { get; init; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        if (Rows == null)
        {
            string state = gfx.Hovered != Owner ? "n" : gfx.Pressed ? "p" : "h";
            Row(gfx, r, Text(), state, arrow: true);
            return;
        }
        // The row under the mouse wears the mouse over pieces, like the box.
        int rowH = MapLay.Lay.Get("comboRowH", 20);
        int at = gfx.Mouse.Y - r.Y;
        int over = gfx.Mouse.X >= r.X && gfx.Mouse.X < r.Right && at >= 0 && at < Rows.Count * rowH ? at / rowH : -1;
        for (int i = 0; i < Rows.Count; i++)
            Row(gfx, new Rectangle(r.X, r.Y + i * rowH, r.Width, rowH), Rows[i], i == over ? "h" : "n", arrow: false);
    }

    // One row of the pieces. A list row has no arrow, so its right end is the left
    // cap mirrored: the row keeps the edge the whole box has.
    private static void Row(UiGfx gfx, Rectangle r, string text, string state, bool arrow)
    {
        var l = gfx.Tex($"ui/wm_combo_{state}0.png");
        var m = gfx.Tex($"ui/wm_combo_{state}1.png");
        var t = gfx.Tex($"ui/wm_combo_{state}2.png");
        int end = arrow ? t.Width : l.Width;
        gfx.Sb.Draw(l, new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(m, new Rectangle(r.X + l.Width, r.Y, r.Width - l.Width - end, m.Height), Color.White);
        if (arrow) gfx.Sb.Draw(t, new Vector2(r.Right - t.Width, r.Y), Color.White);
        else gfx.Sb.Draw(l, new Vector2(r.Right - l.Width, r.Y), null, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.FlipHorizontally, 0f);
        gfx.Sb.Draw(WorldMapInfo.Fit(gfx, text, r.Width - 6 - end - 2), new Vector2(r.X + 6, r.Y + 3), Color.Black);
    }
}

// One row of the search results: the icon, the name, and the highlight plate
// under the mouse.
public sealed class WorldMapSearchRow : Component, IDrawable
{
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required Color Ink { get; init; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        if (gfx.Hovered == Owner) gfx.Sb.Draw(gfx.Tex("ui/wm_s_row.png"), new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(gfx.Tex(Icon), new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(WorldMapInfo.Fit(gfx, Name, r.Width - 22), new Vector2(r.X + 22, r.Y + 2), Ink);
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
// the WZ carries for this tip, so the pieces are not invented here.
public sealed class WorldMapTip : Component, IDrawable
{
    public required MushTownGame Game { get; init; }
    public required WorldMapState State { get; init; }

    private readonly List<(Texture2D Art, string Text)> _rows = new();

    public void Draw(UiGfx gfx)
    {
        if (State.HoverLife is { } life) DrawLife(gfx, life);
        else if (State.HoverNode >= 0) DrawNode(gfx);
        else if (State.Hover >= 0) DrawLink(gfx);
    }

    // A row of the left column: the name, the picture of the NPC or monster and
    // the maps of ours that hold it.
    private void DrawLife(UiGfx gfx, (int Kind, int Id) life)
    {
        string name = life.Kind == 0 ? Game.NpcName(life.Id) : Game.MobName(life.Id);
        var art = gfx.Tex(life.Kind == 0 ? $"npc/{life.Id}.png" : Game.MobIdleFrame(life.Id));
        var maps = WorldMapUi.Holds(life.Kind, life.Id);

        int pad = MapLay.Lay.Get("tipPad", 7);
        int rowH = MapLay.Lay.Get("tipRowH", 14);
        int room = MapLay.Lay.Get("tipW", 200); // the names are cut here: the box follows the mouse
        int artGap = MapLay.Lay.Get("tipArtGap", 2);
        var title = WorldMapInfo.Fit(gfx, name, room);
        int inner = Math.Max(title.Width, art.Width);
        foreach (string map in maps) inner = Math.Max(inner, gfx.Ui.GetSmall(map).Width);
        inner = Math.Max(Math.Min(inner, room), art.Width); // the picture is never cut

        var box = Head(gfx, title, inner, art.Height + artGap + maps.Count * rowH, out int y);
        gfx.Sb.Draw(art, new Vector2(box.X + pad, y), Color.White);
        y += art.Height + artGap;
        foreach (string map in maps)
        {
            gfx.Sb.Draw(WorldMapInfo.Fit(gfx, map, box.Width - pad * 2), new Vector2(box.X + pad, y), Color.White);
            y += rowH;
        }
    }

    private void DrawLink(UiGfx gfx)
    {
        string text = WorldMapUi.Region(State.Shown)!.Links[State.Hover].ToolTip;
        if (text == "") return;
        var t = gfx.Ui.GetSmall(text);
        int pad = MapLay.Lay.Get("tipPad", 7);
        // The frame's two ends are 14 and 15 tall, so it cannot be shorter than that.
        int h = Math.Max(t.Height + pad * 2, gfx.Tex("ui/tip_nw.png").Height + gfx.Tex("ui/tip_s.png").Height);
        var box = Place(gfx, t.Width + pad * 2, h);
        NineSlice.Draw(gfx, "tip", box);
        gfx.Sb.Draw(t, new Vector2(box.X + pad, box.Y + (box.Height - t.Height) / 2), Color.White);
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
        int textDy = MapLay.Lay.Get("tipTextDy", 1);
        var title = gfx.Ui.GetSmall(name);
        int inner = title.Width;
        foreach (var (_, text) in _rows) inner = Math.Max(inner, gfx.Ui.GetSmall(text).Width + col);

        var box = Head(gfx, title, inner, _rows.Count * rowH, out int y);
        foreach (var (art, text) in _rows)
        {
            gfx.Sb.Draw(art, new Vector2(box.X + pad, y + (rowH - art.Height) / 2), Color.White);
            gfx.Sb.Draw(WorldMapInfo.Fit(gfx, text, box.Width - pad * 2 - col), new Vector2(box.X + pad + col, y + textDy), Color.White);
            y += rowH;
        }
    }

    // The head every tip of this window shares: the box, the name and the grey
    // rule under it. The body starts at y and is bodyH tall.
    private static Rectangle Head(UiGfx gfx, Texture2D title, int inner, int bodyH, out int y)
    {
        int pad = MapLay.Lay.Get("tipPad", 7);
        int gap = MapLay.Lay.Get("tipGap", 4); // above and below the rule
        var line = gfx.Tex("ui/wm_tip_line.png");
        var box = Place(gfx, inner + pad * 2, pad + title.Height + gap + line.Height + gap + bodyH + pad);
        NineSlice.Draw(gfx, "tip", box);
        y = box.Y + pad;
        gfx.Sb.Draw(title, new Vector2(box.X + pad, y), Color.White);
        y += title.Height + gap;
        gfx.Sb.Draw(line, new Rectangle(box.X + pad, y, box.Width - pad * 2, line.Height), Color.White);
        y += line.Height + gap;
        return box;
    }

    // The tip follows the mouse and stays on screen: to its right, held inside
    // the screen edge, and level with the cursor.
    private static Rectangle Place(UiGfx gfx, int w, int h)
    {
        var m = gfx.Mouse;
        int x = Math.Clamp(m.X + MapLay.Lay.Get("tipDX", 16), 0, Math.Max(0, MushTownGame.GameWidth - w));
        int y = Math.Clamp(m.Y - h / 2, 0, Math.Max(0, MushTownGame.GameHeight - h));
        return new Rectangle(x, y, w, h);
    }
}
