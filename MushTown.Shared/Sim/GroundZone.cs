namespace MushTown.Shared;

// A box fixed in the world (its origin is where it was cast) that lives for a while and is judged on every world tick once it is up (Poison Mist). A zone may
// also spread (Creeping Toxin's pool): it starts as a line at OriginX and its box widens to FullLeft..FullRight in steps (see Spread). It knows
// no skill and no damage: the world asks Step whether the zone is up in this step, and then judges the mobs in the box. JustStarted is true for the step in
// which it started (the world does what the cast itself does there). WaitMs is the time before the zone starts (the body action's attack frame).
// Held directly by MobWorld, never added to an object (like Mob).
public sealed class GroundZone : Component
{
    public int Uid;
    public int Map;
    public int Attacker; // the player whose cast it is
    public float Left;
    public float Right;
    public float FullLeft; // the box the zone ends up with (the same as Left..Right when it does not spread)
    public float FullRight;
    public float OriginX; // where a spreading zone starts (the totem of Creeping Toxin)
    public int StepMs; // a spreading zone: the time between two steps of the widening (0: it does not spread)
    public int StepPx; // a spreading zone: how far each edge moves in one step
    public int FirstMs; // a spreading zone: the age at which the first step is made (before that the box is a line at OriginX)
    public float Top;
    public float Bottom;
    public float OriginY; // the feet of the caster at the cast: the origin of the box
    public int LifeMs; // the whole time it stays, once started
    public int WaitMs; // time left before it starts
    public int AgeMs; // time it has run
    public bool JustStarted; // set by Step: this step is the one in which the zone started
    private bool _began;

    public bool Over => WaitMs <= 0 && AgeMs >= LifeMs;
    public int MsLeft => Math.Max(0, LifeMs - AgeMs);

    // A mob stands in the zone when its feet x is inside and its body [y - bodyH, y] touches the box height. Edges count as inside. The height rule is the
    // same as WzSkillData.BodyOverlapsY.
    public bool Contains(float x, float y, int bodyH) => x >= Left && x <= Right && y >= Top && y - bodyH <= Bottom;

    // The same test on the box of the cast hit of a spreading zone (Creeping Toxin): the WZ box lt..rb (only its x edges are given) round OriginX, where the totem stands, at the same height.
    public bool ContainsCastBox(float x, float y, int bodyH, float ltX, float rbX) => x >= OriginX + ltX && x <= OriginX + rbX && y >= Top && y - bodyH <= Bottom;

    // One edge of a spreading zone after ageMs, in steps (the server and the clients draw the same widening): it stays at the origin until firstMs, then it is stepPx from the origin,
    // and stepPx further after every stepMs, until it is at the full edge.
    public static float Spread(float origin, float full, int ageMs, int firstMs, int stepMs, int stepPx)
    {
        if (stepMs <= 0) return full;
        if (ageMs < firstMs) return origin;
        float far = ((ageMs - firstMs) / stepMs + 1) * (float)stepPx;
        return origin + Math.Sign(full - origin) * Math.Min(Math.Abs(full - origin), far);
    }

    // Runs ms and returns whether the zone is up in this step (started, and not at or after its end).
    public bool Step(int ms)
    {
        JustStarted = false;
        if (Over) return false;
        if (WaitMs > 0)
        {
            int wait = Math.Min(WaitMs, ms);
            WaitMs -= wait;
            ms -= wait;
            if (WaitMs > 0 || ms == 0) return false;
        }
        JustStarted = !_began;
        _began = true;
        AgeMs = Math.Min(LifeMs, AgeMs + ms);
        if (StepMs > 0)
        {
            Left = Spread(OriginX, FullLeft, AgeMs, FirstMs, StepMs, StepPx);
            Right = Spread(OriginX, FullRight, AgeMs, FirstMs, StepMs, StepPx);
        }
        return AgeMs < LifeMs;
    }
}
