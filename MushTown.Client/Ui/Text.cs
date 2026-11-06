using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Where a line sits in the box of its object: across and down.
public enum TextLine { Top, Middle, Bottom }
public enum TextAlign { Left, Center, Right }

// One line of small text in the box of its object: a name, a menu option, a
// label. The line is centred on the cap band of the font, so every row of the
// same height reads level and no caller adds an offset of its own.
public sealed class Text : Component, IDrawable
{
    public required Func<string> Value { get; init; }
    public Color Ink { get; init; } = Color.White;
    public int X { get; init; } // from the left edge of the box
    public int W { get; init; } // 0 = the rest of the box
    public TextLine Line { get; init; } = TextLine.Middle;
    public TextAlign Align { get; init; } = TextAlign.Left;

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        int boxW = W > 0 ? W : r.Width - X;
        var t = Fit(gfx, Value(), boxW);
        Draw(gfx.Sb, gfx.Ui, t, new Rectangle(r.X + X, r.Y, boxW, r.Height), Ink, Line, Align);
    }

    // One line of small text in a box: the component above and the world's name
    // tags (which draw into the world batch, not a UiGfx) both come through here,
    // so the two axes are laid out in one place.
    public static void Draw(SpriteBatch sb, UiText ui, Texture2D t, Rectangle box, Color ink,
        TextLine line = TextLine.Middle, TextAlign align = TextAlign.Left, InkBand? band = null)
    {
        var inkBand = band ?? ui.SmallInk;
        int x = align switch
        {
            TextAlign.Center => box.X + (box.Width - t.Width) / 2,
            TextAlign.Right => box.Right - t.Width,
            _ => box.X,
        };
        int top = line switch
        {
            TextLine.Top => box.Y,
            TextLine.Bottom => box.Bottom - inkBand.Height,
            _ => box.Y + (box.Height - inkBand.Height) / 2,
        };
        // The baked picture keeps an empty row above the caps: pull it up.
        sb.Draw(t, new Vector2(x, top - inkBand.Top), ink);
    }

    // A string longer than its room is cut with two dots, so it stays in the
    // box. The caller measures the bare string first; the cut candidates go
    // through the small-text cache like every other string.
    public static Texture2D Fit(UiGfx gfx, string text, int width)
    {
        if (gfx.Ui.GetSmall(text).Width <= width) return gfx.Ui.GetSmall(text);
        while (text.Length > 0 && gfx.Ui.GetSmall(text + "..").Width > width) text = text[..^1];
        return gfx.Ui.GetSmall(text + "..");
    }
}
