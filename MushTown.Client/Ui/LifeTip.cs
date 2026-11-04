using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The tip of one NPC or monster: the name, its stand picture playing, and the
// maps of ours that hold it, one green line each. The left column of the world
// map and the NPC list of the minimap show the same tip, so they share it. The
// stand itself is the row's LifeStand, the same component the NPC in the map
// carries: one animation, one place. The tip layer draws it for whichever row
// the mouse is on.
public sealed class LifeTip : Component
{
    private LifeStand Stand => Owner.Get<LifeStand>()!;

    // The maps we have that hold this life. The official writes them green.
    private static readonly Color MapInk = new(0x00, 0xFF, 0x00);

    public void DrawAt(UiGfx gfx, TipAnchor anchor)
    {
        string name = Stand.Name;
        var idle = Stand.Art;
        var art = gfx.Tex(Stand.ImageAt(gfx.Time));
        var maps = WorldMapUi.Holds(Stand.Kind, Stand.Id);

        int pad = MapLay.Lay.Get("tipPad", 7);
        int rowH = MapLay.Lay.Get("tipRowH", 14);
        int room = MapLay.Lay.Get("tipW", 200); // the names are cut here: the box follows the mouse
        int artGap = MapLay.Lay.Get("tipArtGap", 2);
        var title = Text.Fit(gfx, name, room);
        // The box keeps the room of the widest and tallest frame, so it does not
        // jump while the animation plays.
        int wide = 0, tall = 0;
        foreach (string image in idle.Images)
        {
            var frame = gfx.Tex(image);
            wide = Math.Max(wide, frame.Width);
            tall = Math.Max(tall, frame.Height);
        }
        int inner = Math.Max(title.Width, wide);
        foreach (string map in maps) inner = Math.Max(inner, gfx.Ui.GetSmall("- " + map).Width);
        inner = Math.Max(Math.Min(inner, room), wide); // the picture is never cut

        var box = Head(gfx, anchor, title, inner, tall + artGap + maps.Count * rowH, out int y);
        gfx.Sb.Draw(art, new Vector2(box.X + pad, y + tall - art.Height), Color.White); // the feet stay put
        y += tall + artGap;
        foreach (string map in maps)
        {
            Text.Draw(gfx.Sb, gfx.Ui, Text.Fit(gfx, "- " + map, box.Width - pad * 2),
                new Rectangle(box.X + pad, y, box.Width - pad * 2, rowH), MapInk);
            y += rowH;
        }
    }

    // The head every tip of the world map shares: the box, the name and the grey
    // rule under it. The body starts at y and is bodyH tall.
    public static Rectangle Head(UiGfx gfx, TipAnchor anchor, Texture2D title, int inner, int bodyH, out int y)
    {
        int pad = MapLay.Lay.Get("tipPad", 7);
        int gap = MapLay.Lay.Get("tipGap", 4); // above and below the rule
        var line = gfx.Tex("ui/wm_tip_line.png");
        var box = anchor.At(gfx, inner + pad * 2, pad + title.Height + gap + line.Height + gap + bodyH + pad);
        NineSlice.Draw(gfx, "tip", box);
        y = box.Y + pad;
        Text.Draw(gfx.Sb, gfx.Ui, title, new Rectangle(box.X + pad, y, title.Width, title.Height), Color.White);
        y += title.Height + gap;
        gfx.Sb.Draw(line, new Rectangle(box.X + pad, y, box.Width - pad * 2, line.Height), Color.White);
        y += line.Height + gap;
        return box;
    }
}
