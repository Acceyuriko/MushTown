using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The speech bubble over the player: the official MapleStory balloon
// (UI/ChatBalloon.img/0) with the message the player sent. The bubble
// hangs over the head, so it moves with the character.
public partial class MushTownGame
{
    // Every number of the bubble lives in layouts/panel-balloon.json.
    internal static readonly PanelLayout _balloonLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-balloon.json"));

    // The corner art is 6x6: that is the border of the balloon.
    private const int BalloonBorder = 6;

    // The message in the bubble and the clock it goes away at.
    private string _balloonText = "";
    private double _balloonUntil;

    // The player sent a message: the bubble shows it for a few seconds.
    internal void ShowBalloon(string text)
    {
        _balloonText = text;
        _balloonUntil = _timeMs + _balloonLay.Get("lifeMs", 4000);
    }

    // The balloon art (the exporter writes assets/ui/balloon_*.png).
    internal void LoadBalloonArt()
    {
        foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
            LoadTexture($"ui/balloon_{piece}.png");
        LoadTexture("ui/balloon_arrow.png");
    }

    // The bubble over the head: the tail, the frame and the message.
    internal void DrawBalloon(UiGfx gfx)
    {
        string text = BubbleText();
        if (text == "") return;

        int w = _balloonLay.Get("w", 150);
        int maxLines = _balloonLay.Get("maxLines", 3);
        int lineH = _balloonLay.Get("lineH", 15);
        int padX = _balloonLay.Get("padX", 8);
        int padY = _balloonLay.Get("padY", 6);
        int inner = w - 2 * (BalloonBorder + padX);

        var lines = BubbleLines(gfx, text, inner, maxLines);
        int h = 2 * (BalloonBorder + padY) + lines.Count * lineH;

        // the bubble hangs over the head of the character
        int feetX = (int)Math.Floor(_mover.X) - _camera.X;
        int feetY = (int)Math.Floor(_mover.Y) - _camera.Y;
        int bottom = feetY - _balloonLay.Get("aboveFeet", 80);
        int x = Math.Clamp(feetX - w / 2, 4, GameWidth - w - 4); // it stays on the screen
        int y = Math.Max(bottom - h, 4);

        // the tail first: the frame edge covers its top. Its tip lands under
        // the character, the wide end under the bubble.
        gfx.Sb.Draw(gfx.Tex("ui/balloon_arrow.png"),
            new Vector2(x + w / 2 - 1, bottom - _balloonLay.Get("arrowUp", 2)), Color.White);
        NineSlice.Draw(gfx, "balloon", new Rectangle(x, y, w, h));

        // the message, centered in the bubble
        int ty = y + BalloonBorder + padY;
        foreach (string line in lines)
        {
            var t = gfx.Ui.GetSmall(line);
            gfx.Sb.Draw(t, new Vector2(x + (w - t.Width) / 2, ty), Color.Black);
            ty += lineH;
        }
    }

    // The message the bubble shows: the one the player sent. A shot never
    // types, so a shot named *balloon* shows a sample line instead.
    private string BubbleText()
    {
        if (_balloonText != "")
        {
            if (_timeMs <= _balloonUntil) return _balloonText;
            _balloonText = ""; // the bubble is gone
        }
        if (_shotPath != null && Path.GetFileNameWithoutExtension(_shotPath).Contains("balloon"))
            return "Hi! I am going to Southperry now, see you soon!";
        return "";
    }

    // The message on its lines: wrapped to the bubble width and cut short
    // when it needs more lines than the bubble has.
    private static List<string> BubbleLines(UiGfx gfx, string text, int width, int maxLines)
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
