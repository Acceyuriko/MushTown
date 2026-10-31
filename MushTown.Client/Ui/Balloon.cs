using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The speech bubble over the player: the official MapleStory balloon
// (UI/ChatBalloon.img/<style>) with the message the player sent. The style
// comes from the worn chat ring (info/chatBalloon; 0 is the plain balloon).
// It hangs over the head, so it reads the feet every frame. The lines are
// wrapped once per message: the wrap and the text art are the slow parts.
public sealed class Balloon : Component, IDrawable
{
    private readonly Func<string> _text;   // the message, "" when there is none
    private readonly Func<Point> _feet;    // the character's feet on screen
    private readonly PanelLayout _lay;     // every number of the bubble (layouts/panel-balloon.json)
    private readonly Func<BalloonStyleData> _style; // the worn ring's balloon style

    private string _wrapped = "";
    private int _inner;
    private int _maxLines;
    private int _px;
    private List<string> _lines = new();

    public Balloon(Func<string> text, Func<Point> feet, PanelLayout lay, Func<BalloonStyleData> style)
    {
        _text = text;
        _feet = feet;
        _lay = lay;
        _style = style;
    }

    public void Draw(UiGfx gfx)
    {
        string text = _text();
        if (text == "") return;

        var st = _style();
        int w = _lay.Get("w", 130);
        int maxLines = _lay.Get("maxLines", 3);
        int lineH = _lay.Get("lineH", 15);
        int padX = _lay.Get("padX", 8);
        int padY = _lay.Get("padY", 6);
        int px = _lay.Get("fontSize", 12);
        int inner = w - 2 * (st.Border + padX);

        if (text != _wrapped || inner != _inner || maxLines != _maxLines || px != _px) // the lines change only when the message or the size does
        {
            _wrapped = text;
            _inner = inner;
            _maxLines = maxLines;
            _px = px;
            _lines = Wrap(gfx, text, inner, maxLines, px);
        }
        var lines = _lines;
        int h = 2 * (st.Border + padY) + lines.Count * lineH;

        // the bubble hangs over the head of the character
        var feet = _feet();
        int bottom = feet.Y - _lay.Get("aboveFeet", 74);
        int x = Math.Clamp(feet.X - w / 2, 4, MushTownGame.GameWidth - w - 4); // it stays on the screen
        int y = Math.Max(bottom - h, 4);

        // the message, centered in the bubble, in the color of the style
        BalloonDraw.Draw(gfx, st, x, y, w, h, lines, px, lineH, padY, st.Color);
    }

    // The message on its lines: wrapped to the bubble width and cut short
    // when it needs more lines than the bubble has.
    private static List<string> Wrap(UiGfx gfx, string text, int width, int maxLines, int px)
    {
        var lines = gfx.Ui.Wrap(text, width, px);
        if (lines.Count <= maxLines) return lines;
        var kept = lines.GetRange(0, maxLines);
        kept[maxLines - 1] = Trim(gfx, kept[maxLines - 1] + "…", width, px);
        return kept;
    }

    // The text that fits the bubble width.
    private static string Trim(UiGfx gfx, string text, int width, int px)
    {
        while (text.Length > 0 && gfx.Ui.GetSized(text, px).Width > width) text = text[..^1];
        return text;
    }
}
