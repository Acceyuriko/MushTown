using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The one way a speech balloon is drawn: the nine-piece frame of the style, the
// tail under its middle and the lines centred inside. The player balloon and the
// NPC balloon differ only in what they hand in (size, font, color, style).
internal static class BalloonDraw
{
    public static void Draw(UiGfx gfx, BalloonStyleData st, int x, int y, int w, int h,
        List<string> lines, int px, int lineH, int padY, int clr)
    {
        var ink = new Color((clr >> 16) & 255, (clr >> 8) & 255, clr & 255);
        // the frame first, then the tail over its bottom border: the two read as
        // one shape (the other way round the frame's outline cuts the tail's neck)
        NineSlice.Draw(gfx, $"balloon_{st.Style}", new Rectangle(x, y, w, h));
        gfx.Sb.Draw(gfx.Tex($"ui/balloon_{st.Style}_arrow.png"),
            new Vector2(x + w / 2 + st.ArrowX, y + h + st.ArrowY), Color.White);

        int ty = y + st.Border + padY;
        foreach (string line in lines)
        {
            var t = gfx.Ui.GetSized(line, px);
            gfx.Sb.Draw(t, new Vector2(x + (w - t.Width) / 2, ty), ink);
            ty += lineH;
        }
    }
}
