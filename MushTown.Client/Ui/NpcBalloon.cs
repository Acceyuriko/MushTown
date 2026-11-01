using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The bubble one NPC wears while it talks: its own clock, its own line, drawn
// over its own head. The NPC's quest mark has the NPC while it shows, so the
// quest balloon and the chat balloon never appear together.
public sealed class NpcBalloon : Component, IDrawable
{
    public required List<string> Lines;           // the lines the NPC can say (String d0, d1, ...)
    public required NpcQuestMark Mark;            // the quest balloon of the same NPC
    public required Func<string, string> Text;    // strips the markup (#b, #p<id>#, ...)
    public required PanelLayout Lay;              // layouts/panel-balloon.json
    public required Func<BalloonStyleData> Style; // the plain balloon (0): the NPC bubble reuses it
    public required Func<Point> Head;             // the NPC's head on the screen
    public required Func<bool> Busy;              // true: the talk panel or the shop is open

    private double _now;  // ms since the map loaded
    private double _hide; // when the line goes away
    private double _next; // when the next line comes
    private string _line = "";
    private bool _started;

    private bool Quiet => Mark.Showing || Busy();

    public override void Update(float dt)
    {
        _now += dt * 1000;
        if (!_started) { _started = true; _next = _now + Roll(GapMin, GapMax); } // the first line waits like the rest
        if (Quiet)
        {
            _line = "";
            _next = _now + Roll(GapMin, GapMax); // quiet for a while, not a burst when it lifts
            return;
        }
        if (_now >= _next) Say();
    }

    // Start a line now (the shot poses call this: a shot never runs Update).
    public void Say(int index = -1) // -1 picks at random; a shot passes 0 so the picture repeats
    {
        _hide = _now + Roll(LifeMin, LifeMax);
        _next = _hide + Roll(GapMin, GapMax);
        _line = Text(Lines[index < 0 ? Random.Shared.Next(Lines.Count) : index]);
    }

    public void Draw(UiGfx gfx)
    {
        if (_line == "" || _now >= _hide || Quiet) return;

        var lay = Lay;
        var st = Style();
        int w = lay.Get("npcW", 150);
        int padX = lay.Get("npcPadX", 8);
        int padY = lay.Get("npcPadY", 6);
        int lineH = lay.Get("npcLineH", 16);
        int px = lay.Get("npcFontSize", 14);
        var lines = gfx.Ui.Wrap(_line, w - 2 * (st.Border + padX), px);
        int h = 2 * (st.Border + padY) + lines.Count * lineH;

        // it hangs over the head of the NPC. No screen check: the camera clips
        // a bubble that hangs off the edge, the same way it clips the NPC.
        var head = Head();
        int x = head.X - w / 2;
        // the tail tip touches the head: the arrow hangs below the frame by its
        // own art height, so the frame sits that much higher
        var arrow = gfx.Tex($"ui/balloon_{st.Style}_arrow.png");
        int y = head.Y - h - st.ArrowY - arrow.Height - lay.Get("npcAboveHead", 2);
        BalloonDraw.Draw(gfx, st, x, y, w, h, lines, px, lineH, padY, lay.Get("npcClr", -13354916));
    }

    // a layout typo (min above max) must not crash the game every frame
    private static int Roll(int a, int b) => Random.Shared.Next(Math.Min(a, b), Math.Max(a, b) + 1);

    private int GapMin => Lay.Get("npcSayGapMinMs", 3000);
    private int GapMax => Lay.Get("npcSayGapMaxMs", 5000);
    private int LifeMin => Lay.Get("npcSayLifeMinMs", 8000);
    private int LifeMax => Lay.Get("npcSayLifeMaxMs", 20000);
}
