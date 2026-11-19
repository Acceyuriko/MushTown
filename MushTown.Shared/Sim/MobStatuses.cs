namespace MushTown.Shared;

// What a status does to a mob. One entry per kind: the next kinds (stun, slow) add
// their own name here and read their own number from the list.
public enum MobStatusKind
{
    ElementalWeakness, // value = the extra magic damage taken, in percent
}

// One damage-over-time effect (DoT) on a mob: the numbers are taken from the caster at the cast (the Build snapshot, the
// damage % of one tick, the element), so the ticks do not change when the caster later changes gear or buffs.
// The statuses own the clock (MsLeft, NextMs); the server reads the ticks and does the damage.
// On purpose: if the caster leaves the game, its DoT on a mob runs to its end anyway; a mob it poisons to death gives no EXP to anyone
// (the kill is credited to an Attacker that is gone).
public sealed class Dot
{
    public int Skill;
    public int Attacker;
    public Build Build;
    public int Pct; // the damage % of one tick (WZ dot)
    public AttackElement Element;
    public Random Rng = null!;
    public int IntervalMs;
    public int MsLeft; // the time left (WZ dotTime at the cast)
    public int NextMs; // ms to the next tick

    public Dot Clone() => (Dot)MemberwiseClone(); // one cast's numbers for each mob it hits (the clock is the mob's own)
}

// The statuses on one mob: a kind, its number and the time left. Adding a kind that is already on the
// mob replaces its number and restarts its time (it refreshes, it does not stack). The mob steps it
// on every AI tick; a dead mob is gone with its list.
// The DoTs are kept apart, one entry per SKILL that put one on: the same skill again only refreshes
// its time (and takes the new cast's numbers), another skill is another effect (the user's rule). DotCount is the number of
// skills, for Elemental Drain. A tick that came due while stepping waits in TakeDue until the server does its damage.
public sealed class MobStatuses
{
    private readonly Dictionary<MobStatusKind, (double value, int msLeft)> _list = new();

    private readonly Dictionary<int, Dot> _dots = new(); // skill id -> its DoT

    private readonly List<Dot> _due = new();

    public void Add(MobStatusKind kind, double value, int ms) => _list[kind] = (value, ms);

    // The first tick comes one interval after the cast. A refresh (the skill is on the mob already) takes the new numbers and time but keeps the
    // beat of the next tick: a skill cast more often than its interval must not push the tick away for ever.
    public void AddDot(Dot dot)
    {
        dot.NextMs = _dots.TryGetValue(dot.Skill, out var old) ? old.NextMs : dot.IntervalMs;
        _dots[dot.Skill] = dot;
    }

    // How many different skills have a DoT on the mob.
    public int DotCount => _dots.Count;

    // The ticks that came due in the steps since the last call, oldest first (a dot that ran out in the same step still gives its last tick).
    public IReadOnlyList<Dot> TakeDue()
    {
        if (_due.Count == 0) return Array.Empty<Dot>(); // most mobs have no DoT: no allocation on their AI steps
        var due = new List<Dot>(_due);
        _due.Clear();
        return due;
    }

    // The number of the kind, 0 when the mob does not have it.
    public double Value(MobStatusKind kind) => _list.TryGetValue(kind, out var s) ? s.value : 0;

    // The damage % a magic hit gets from this mob's statuses (0 = none).
    public double MagicExtraDamagePct => Value(MobStatusKind.ElementalWeakness);

    public void Step(int ms)
    {
        if (_dots.Count > 0)
        {
            foreach (var dot in _dots.Values.ToList())
            {
                int run = Math.Min(ms, dot.MsLeft);
                dot.MsLeft -= run;
                dot.NextMs -= run;
                for (; dot.NextMs <= 0; dot.NextMs += dot.IntervalMs) _due.Add(dot);
                if (dot.MsLeft == 0) _dots.Remove(dot.Skill);
            }
        }
        if (_list.Count == 0) return;
        foreach (var kind in _list.Keys.ToList())
        {
            int left = _list[kind].msLeft - ms;
            if (left <= 0) _list.Remove(kind);
            else _list[kind] = (_list[kind].value, left);
        }
    }
}
