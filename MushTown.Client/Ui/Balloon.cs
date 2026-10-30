using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The speech bubble over the player: the official MapleStory balloon
// (UI/ChatBalloon.img/0) with the message the player sent. It hangs over the
// head, so it reads the feet every frame. The lines are wrapped once per
// message: the wrap and the text art are the slow parts.
public sealed class Balloon : Component, IDrawable
{
    private const int Border = 6; // the corner art is 6x6: that is the border of the balloon

    private readonly Func<string> _text; // the message, "" when there is none
    private readonly Func<Point> _feet;  // the character's feet on screen
    private readonly PanelLayout _lay;   // every number of the bubble (layouts/panel-balloon.json)

    private string _wrapped = "";
    private int _inner;
    private int _maxLines;
    private List<string> _lines = new();

    public Balloon(Func<string> text, Func<Point> feet, PanelLayout lay)
    {
        _text = text;
        _feet = feet;
        _lay = lay;
    }

    public void Draw(UiGfx gfx)
    {
        string text = _text();
        if (text == "") return;

        int w = _lay.Get("w", 130);
        int maxLines = _lay.Get("maxLines", 3);
        int lineH = _lay.Get("lineH", 15);
        int padX = _lay.Get("padX", 8);
        int padY = _lay.Get("padY", 6);
        int inner = w - 2 * (Border + padX);

        if (text != _wrapped || inner != _inner || maxLines != _maxLines) // the lines change only when the message or the size does
        {
            _wrapped = text;
            _inner = inner;
            _maxLines = maxLines;
            _lines = Wrap(gfx, text, inner, maxLines);
        }
        var lines = _lines;
        int h = 2 * (Border + padY) + lines.Count * lineH;

        // the bubble hangs over the head of the character
        var feet = _feet();
        int bottom = feet.Y - _lay.Get("aboveFeet", 74);
        int x = Math.Clamp(feet.X - w / 2, 4, MushTownGame.GameWidth - w - 4); // it stays on the screen
        int y = Math.Max(bottom - h, 4);

        // the tail first: the frame edge covers its top. Its tip lands under
        // the character, the wide end under the bubble.
        gfx.Sb.Draw(gfx.Tex("ui/balloon_arrow.png"),
            new Vector2(x + w / 2 - 1, bottom - _lay.Get("arrowUp", 2)), Color.White);
        NineSlice.Draw(gfx, "balloon", new Rectangle(x, y, w, h));

        // the message, centered in the bubble
        int ty = y + Border + padY;
        foreach (string line in lines)
        {
            var t = gfx.Ui.GetSmall(line);
            gfx.Sb.Draw(t, new Vector2(x + (w - t.Width) / 2, ty), Color.Black);
            ty += lineH;
        }
    }

    // The message on its lines: wrapped to the bubble width and cut short
    // when it needs more lines than the bubble has.
    private static List<string> Wrap(UiGfx gfx, string text, int width, int maxLines)
    {
        var lines = gfx.Ui.Wrap(text, width);
        if (lines.Count <= maxLines) return lines;
        var kept = lines.GetRange(0, maxLines);
        kept[maxLines - 1] = Trim(gfx, kept[maxLines - 1] + "…", width);
        return kept;
    }

    // The text that fits the bubble width.
    private static string Trim(UiGfx gfx, string text, int width)
    {
        while (text.Length > 0 && gfx.Ui.GetSmall(text).Width > width) text = text[..^1];
        return text;
    }
}
