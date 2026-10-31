using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The bubble over an NPC: the official balloon (the same nine pieces as the
// player's plain one) with the line the NPC says when the player walks up. The
// tail hangs in the middle under the bubble, not at the side, and the text is
// the NPC font and color, not the player balloon's.
public sealed class NpcBalloon : Component, IDrawable
{
    private readonly Func<(string text, Point head)> _speech; // "" when no NPC is talking
    private readonly PanelLayout _lay;                        // layouts/panel-balloon.json
    private readonly BalloonStyleData _style;                 // the plain balloon (0): the NPC bubble reuses it

    public NpcBalloon(Func<(string text, Point head)> speech, PanelLayout lay, BalloonStyleData style)
    {
        _speech = speech;
        _lay = lay;
        _style = style;
    }

    public void Draw(UiGfx gfx)
    {
        var (text, head) = _speech();
        if (text == "") return;

        var st = _style;
        int w = _lay.Get("npcW", 150);
        int padX = _lay.Get("npcPadX", 8);
        int padY = _lay.Get("npcPadY", 6);
        int lineH = _lay.Get("npcLineH", 16);
        int px = _lay.Get("npcFontSize", 14);
        var lines = gfx.Ui.Wrap(text, w - 2 * (st.Border + padX), px);
        int h = 2 * (st.Border + padY) + lines.Count * lineH;

        // it hangs over the head of the NPC and stays on the screen
        int bottom = head.Y - _lay.Get("npcAboveHead", 44);
        int x = Math.Clamp(head.X - w / 2, 4, MushTownGame.GameWidth - w - 4);
        int y = Math.Max(bottom - h, 4);

        // the frame first, then the tail over its bottom edge: the two read as one
        // shape (drawn the other way the frame's dark outline cuts the tail's neck).
        // It hangs from the box that was drawn, so a bubble pushed down by the
        // screen edge keeps its tail.
        BalloonDraw.Draw(gfx, st, x, y, w, h, lines, px, lineH, padY, _lay.Get("npcClr", -13354916));
    }
}
