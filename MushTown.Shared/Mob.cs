namespace MushTown.Shared;

// One live mob: body state plus the movement AI (chase/wander, edges,
// falls, touch). Moved from the Server in S5, behavior kept. Held
// directly by MobWorld (never added to an object): the world calls Step
// on its own tick, which carries players the Update pass cannot pass.
public sealed class Mob : Component
{
    private const int TouchX = 35; // touch box half-size (mob feet vs player feet)
    private const int TouchY = 45;
    private const int BossTouchX = 60; // Mushmom is ~120px wide: the small box would miss edge touches
    private const int BossTouchY = 60;
    private const int ChaseDy = 150; // chase only reaches nearby platforms
    private const int TouchCdMs = 1000; // one touch per mob per second max
    private const float FallV = 700f; // airborne mobs fall at the client MaxFall

    public int Uid;
    public int MobId;
    public int Map;
    public float X;
    public float Y;
    public float HomeX; // spawn feet: graves re-plant here, void falls land here
    public float HomeY;
    public bool FacingRight;
    public float TouchMs; // ms until this mob may touch again
    public int Hp;
    public int MaxHp;
    public double Def;
    public int Exp;
    public int Touch;
    public int Speed;
    public int Aggro;
    public bool Boss;

    // One AI step: cool the touch down, then walk the line or fall off it.
    public void Step(int ms, MapData map, List<(int id, int map, float x, float y)> players)
    {
        if (TouchMs > 0) TouchMs = Math.Max(0, TouchMs - ms);
        float dt = ms / 1000f;
        if (SceneMath.StepTo(map.Footholds, (int)Math.Floor(X), (int)Math.Floor(Y)) == null)
            Fall(map, dt); // off the line (pushed off, spawn offset): fall
        else
            Walk(map, players, dt);
    }

    // Touch test for one player: inside the box and cooled down. Sets the
    // cooldown on a hit; the caller builds the Hurt event.
    public bool TryTouch(float px, float py)
    {
        if (TouchMs > 0) return false;
        int tx = Boss ? BossTouchX : TouchX, ty = Boss ? BossTouchY : TouchY;
        if (Math.Abs(px - X) > tx || Math.Abs(py - Y) > ty) return false;
        TouchMs = TouchCdMs;
        return true;
    }

    // One grounded step: chase the nearest in-range player, else keep the
    // wander line. Pixel-by-pixel like the client (one stride would skip
    // slope joints): a lost edge or a VR wall turns the mob around. A clean
    // walk keeps the float stride (16ms local ticks move 0.64px: truncating
    // to ints would freeze them); only an edge stop plants int feet.
    private void Walk(MapData map, List<(int id, int map, float x, float y)> players, float dt)
    {
        float best = float.MaxValue;
        foreach (var p in players)
        {
            if (p.map != Map) continue;
            float dx = Math.Abs(p.x - X);
            if (dx > Aggro || Math.Abs(p.y - Y) > ChaseDy || dx >= best) continue;
            best = dx;
            if (p.x != X) FacingRight = p.x > X;
        }
        float rawX = X + (FacingRight ? 1 : -1) * Speed * dt;
        float nx = Math.Clamp(rawX, map.Info.VrLeft, map.Info.VrRight);
        if (nx != rawX)
        {
            FacingRight = !FacingRight; // VR wall: turn in place, next tick walks away
            return;
        }
        int x1 = (int)Math.Floor(nx);
        int stepX = Math.Sign(x1 - (int)Math.Floor(X));
        int gx = (int)Math.Floor(X);
        bool stopped = false;
        while (true)
        {
            var step = SceneMath.StepTo(map.Footholds, gx, (int)Math.Floor(Y));
            if (step == null)
            {
                FacingRight = !FacingRight; // walked off the edge: turn around
                stopped = true;
                break;
            }
            Y = SceneMath.FootholdY(step, gx);
            X = gx;
            if (gx == x1) break;
            gx += stepX;
        }
        if (!stopped) X = nx;
    }

    // One airborne step: pushed-off mobs fall straight down, land on the
    // first ground below, or pop home when the void eats them.
    private void Fall(MapData map, float dt)
    {
        float ny = Y + FallV * dt;
        int ix = (int)Math.Floor(X);
        var g = SceneMath.GroundBelow(map.Footholds, ix, (int)Math.Ceiling(Y));
        if (g != null && ny >= SceneMath.FootholdY(g, ix))
        {
            Y = SceneMath.FootholdY(g, ix);
            return;
        }
        Y = ny;
        if (Y >= map.Info.VrBottom)
        {
            X = HomeX; // fell out of the world: pop home
            Y = HomeY;
        }
    }
}
