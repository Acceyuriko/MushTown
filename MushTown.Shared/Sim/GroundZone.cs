namespace MushTown.Shared;

// A box fixed in the world (its origin is where it was cast) that lives for a while and is judged on every world tick once it is up (Poison Mist). It knows
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
        return AgeMs < LifeMs;
    }

    // The hook for a detonation (Mist Eruption, not made yet): the zone ends now.
    public void Detonate()
    {
        WaitMs = 0;
        AgeMs = LifeMs;
    }
}
