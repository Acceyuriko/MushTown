namespace MushTown.Shared;

// A box fixed in the world (its origin is where it was cast) that lives for a while and goes off once each period for what stands in it (Poison Mist). It knows no skill
// and no damage: the world asks Step how many periods came due and judges the mobs in the box for each one. The first period
// goes off the moment the zone starts, the next ones every PeriodMs after, and none at or after the end. WaitMs is the time before
// the zone starts (the body action's attack frame). Held directly by MobWorld, never added to an object (like Mob).
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
    public int PeriodMs;
    public int LifeMs; // the whole time it stays, once started
    public int WaitMs; // time left before it starts
    public int AgeMs; // time it has run
    private int _nextMs; // the age of the next period

    public bool Over => WaitMs <= 0 && AgeMs >= LifeMs;
    public int MsLeft => Math.Max(0, LifeMs - AgeMs);

    // A mob stands in the zone when its feet x is inside and its body [y - bodyH, y] touches the box height. Edges count as inside. The height rule is the
    // same as WzSkillData.BodyOverlapsY.
    public bool Contains(float x, float y, int bodyH) => x >= Left && x <= Right && y >= Top && y - bodyH <= Bottom;

    // Runs ms and returns how many periods came due in it.
    public int Step(int ms)
    {
        if (Over) return 0;
        if (WaitMs > 0)
        {
            int wait = Math.Min(WaitMs, ms);
            WaitMs -= wait;
            ms -= wait;
            if (WaitMs > 0 || ms == 0) return 0;
        }
        AgeMs = Math.Min(LifeMs, AgeMs + ms);
        int due = 0;
        while (_nextMs <= AgeMs && _nextMs < LifeMs)
        {
            due++;
            _nextMs += PeriodMs;
        }
        return due;
    }

    // The hook for a detonation (Mist Eruption, not made yet): the zone ends now.
    public void Detonate()
    {
        WaitMs = 0;
        AgeMs = LifeMs;
    }
}
