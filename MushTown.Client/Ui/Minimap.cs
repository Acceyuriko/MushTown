using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The one layout file every part of the minimap reads.
internal static class MinimapLay
{
    public static readonly PanelLayout Lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-minimap.json"));
}

// The window state: which layout, which panels are open, which filter rows are
// off. It outlives a rebuild, so the buttons can be clicked without losing it.
public sealed class MinimapState
{
    public bool Big;
    public bool NpcList;
    public bool Filter;

    private readonly HashSet<string> _off = new();

    public bool On(string row) => !_off.Contains(row);

    public void Toggle(string row)
    {
        if (!_off.Add(row)) _off.Remove(row);
    }
}

// The map picture and the dots on it. The picture is drawn at its own size and
// centred in the well, the way the reference client does it (UIMiniMap.cs: the
// canvas is never scaled, only shifted by (rect - canvas) / 2). The dots ride
// the same offset, so they land on the art pixel the map data points at.
public sealed class MinimapArt : Component, IDrawable
{
    public required MushTownGame Game { get; init; }
    public required MinimapState State { get; init; }

    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        var map = Game.GameMap;
        var tex = gfx.Tex(map.MiniMap.Image);
        int ox = box.X + (box.Width - tex.Width) / 2;
        int oy = box.Y + (box.Height - tex.Height) / 2;
        // The well is black behind the picture (UIMiniMap.cs: a black fill of
        // 70%, then the canvas centred), so a small map shows black edges.
        gfx.Sb.Draw(gfx.Pixel, box, new Color(0, 0, 0, 178));
        gfx.Sb.Draw(tex, new Vector2(ox, oy), Color.White);

        // A dot: the world point to a pixel of the art, then the icon is put on
        // that pixel by its bottom edge, so an NPC and the player on the same
        // ground share one line whatever the icon height is.
        void Dot(float wx, float wy, string icon)
        {
            var (ax, ay) = map.MiniMap.ToDot(wx, wy, tex.Width, tex.Height);
            if (ax < 0 || ay < 0 || ax >= tex.Width || ay >= tex.Height) return; // outside the art
            var dot = gfx.Tex($"ui/mini_dot_{icon}.png");
            gfx.Sb.Draw(dot, new Vector2(ox + ax - dot.Width / 2, oy + ay - dot.Height + 1), Color.White);
        }

        // The portals first, then the NPCs, then the player on top, so the
        // player is never hidden under another mark.
        foreach (var p in map.Portals) if (p.Pt == PortalRules.Visible) Dot(p.X, p.Y, "portal");
        if (State.On("npcAll") && State.On("npc0"))
            foreach (var n in map.Npcs)
            {
                int mark = Game.NpcQuestIcon(n.NpcId);
                Dot(n.X, n.Cy, mark == 0 ? "npcstart" : mark == 2 ? "npcend" : "npc");
            }
        if (State.On("pcAll") && State.On("pc0")) Dot(Game.PlayerX, Game.PlayerY, "user");
    }
}

// The large layout header: the map mark and the two names over the frame. The
// frame art leaves a white slot for the mark; a map without one leaves it empty.
public sealed class MinimapHeader : Component, IDrawable
{
    public required MushTownGame Game { get; init; }

    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        var map = Game.GameMap;
        int slot = MinimapLay.Lay.Get("markW", 38);
        var at = new Rectangle(box.X + MinimapLay.Lay.Get("markX", 6), box.Y + MinimapLay.Lay.Get("markY", 28), slot, slot);
        if (map.MiniMap.Mark != "")
        {
            var mark = gfx.Tex($"ui/map_mark_{map.MiniMap.Mark}.png");
            // Six of the 382 marks are wider than the slot: they start at its
            // left and top edge instead of centring 1 or 2 pixels outside.
            gfx.Sb.Draw(mark, new Rectangle(at.X + Math.Max(0, (slot - mark.Width) / 2), at.Y + Math.Max(0, (slot - mark.Height) / 2), mark.Width, mark.Height), Color.White);
        }
        var name = MapUi.Name(map.MapId);
        Text(gfx, name.Street, box.X + MinimapLay.Lay.Get("streetX", 50), box.Y + MinimapLay.Lay.Get("streetY", 30), new Color(212, 225, 229));
        Text(gfx, name.Name, box.X + MinimapLay.Lay.Get("nameX", 50), box.Y + MinimapLay.Lay.Get("nameY", 48), Color.White);
    }

    private static void Text(UiGfx gfx, string text, int x, int y, Color ink)
    {
        if (text == "") return;
        var t = gfx.Ui.GetSmall(text);
        gfx.Sb.Draw(t, new Vector2(x, y), ink);
    }
}

// The list of this map's NPCs (the person button): the official window art,
// which carries the NPC label, plus one row per NPC.
public sealed class MinimapNpcList : Component, IDrawable
{
    public required MushTownGame Game { get; init; }

    // How many rows fit in the panel. There is no scrolling: the rest is cut.
    public static int RowCount =>
        (MinimapLay.Lay.Get("listH", 286) - MinimapLay.Lay.Get("listTop", 30)) / MinimapLay.Lay.Get("listRowH", 18);

    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex("ui/npclist_bg.png"), box, Color.White);

        // The rows: the dot, then the name, in the box the window defines.
        var dot = gfx.Tex("ui/npclist_dot.png");
        var npcs = Game.GameMap.Npcs;
        for (int i = 0; i < Math.Min(npcs.Count, RowCount); i++)
        {
            var n = npcs[i];
            int y = box.Y + RowY(i);
            gfx.Sb.Draw(dot, new Vector2(box.X + MinimapLay.Lay.Get("listIconX", 11), y + MinimapLay.Lay.Get("listIconDy", 4)), Color.White);
            gfx.Sb.Draw(gfx.Ui.GetSmall(Game.NpcName(n.NpcId)), new Vector2(box.X + MinimapLay.Lay.Get("listTextX", 30), y + MinimapLay.Lay.Get("listTextDy", 3)), Color.White);
        }
    }

    // Where a row sits inside the panel: the window places the hover boxes
    // with the same rule.
    public static int RowY(int i) => MinimapLay.Lay.Get("listTop", 30) + i * MinimapLay.Lay.Get("listRowH", 18);
}

// The filter panel (the funnel button): the official art already draws the
// labels and the row icons, so this only adds the checkboxes and the names.
public sealed class MinimapFilter : Component, IDrawable
{
    public required MinimapState State { get; init; }
    private static int FilterTextPx => MinimapLay.Lay.Get("filterTextPx", 9);

    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex("ui/mini_filter_bg.png"), box, Color.White);
        foreach (var row in Rows)
        {
            // The official rect is 14x16 and the art is 11x11, so the box is
            // centred in it; the name is centred on the same line. The two
            // offsets let the layout file nudge a row.
            var check = gfx.Tex(State.On(row.Row) ? "ui/check_1.png" : "ui/check_0.png");
            gfx.Sb.Draw(check, new Vector2(box.X + row.CheckX + MinimapLay.Lay.Get("filterCheckDx", 1), box.Y + row.CheckY + MinimapLay.Lay.Get("filterCheckDy", 2)), Color.White);
            if (row.Name == "") continue; // the section box carries the title already
            gfx.Sb.Draw(gfx.Ui.GetSized(row.Name, FilterTextPx), new Vector2(box.X + row.X + MinimapLay.Lay.Get("filterTextDx", 0), box.Y + row.Y + MinimapLay.Lay.Get("filterTextDy", 3)), new Color(226, 232, 238));
        }
    }

    // The rows the exporter wrote from the official window: the name, where the
    // name sits and where the checkbox sits.
    public static readonly IReadOnlyList<MinimapFilterRow> Rows =
        MushTownGame.LoadJson<List<MinimapFilterRow>>(Path.Combine(MushTownGame.AssetsDir, "ui", "mini_filter.json"));
}
