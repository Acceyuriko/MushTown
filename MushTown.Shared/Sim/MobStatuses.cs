namespace MushTown.Shared;

// What a status does to a mob. One entry per kind: the next kinds (stun, slow) add
// their own name here and read their own number from the list.
public enum MobStatusKind
{
    ElementalWeakness, // value = the extra magic damage taken, in percent
    Dot, // damage over time: kept in the DoT list (Dot, AddDot), never in the kind list; here so Has(Dot) answers like the others
    Stun, // no skill puts these four on a mob yet: the names are here so Burning Magic already counts them (WZ: stun, freeze, darkness, paralysis)
    Freeze,
    Darkness,
    Paralysis,
}

// One damage-over-time effect (DoT) on a mob: the numbers are taken from the caster at the cast (the Build snapshot, the
// damage % of one tick, the element), so the ticks do not change when the caster later changes gear or buffs.
// The statuses own the clock (MsLeft, NextMs); the server reads the ticks and does the damage.
// A DoT belongs to its source (Attacker): the kill is credited to that source, and when the source disconnects or leaves the map
// the server removes its DoTs (MobWorld.CancelDoTs).
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
// The DoTs are kept apart by SOURCE AND SKILL: the same source's same skill again only refreshes its time (and takes the new
// cast's numbers); another source, or another skill, is another independent effect (the user's rule). DotCount is the number of
// distinct skills, for Elemental Drain (one layer per skill, whatever the number of sources). A tick that came due while stepping
// waits in TakeDue until the server does its damage.
// The Empirical Knowledge marks are kept apart by SOURCE only: each attacker has its own stacks and its own clock on this mob,
// and only that attacker's client shows them (the server tells only the owner). A hit adds a stack (up to the skill's max) and
// restarts the time; a hit with no proc only refreshes the time of the stacks already there; the time running out drops them all.
public sealed class MobStatuses
{
    private readonly Dictionary<MobStatusKind, (double value, int msLeft)> _list = new();

    private readonly Dictionary<(int Attacker, int Skill), Dot> _dots = new(); // (source, skill) -> its DoT

    private readonly List<Dot> _due = new();

    private readonly Dictionary<int, (int stacks, int msLeft)> _marks = new(); // source -> its Empirical Knowledge stacks + time left

    private readonly List<(int attacker, int stacks)> _markChanges = new(); // stack changes to report to the owner

    public void Add(MobStatusKind kind, double value, int ms) => _list[kind] = (value, ms);

    // The first tick comes one interval after the cast. A refresh (the same source's same skill is on the mob already) takes the new
    // numbers and time but keeps the beat of the next tick: a skill cast more often than its interval must not push the tick away for ever.
    public void AddDot(Dot dot)
    {
        var key = (dot.Attacker, dot.Skill);
        dot.NextMs = _dots.TryGetValue(key, out var old) ? old.NextMs : dot.IntervalMs;
        _dots[key] = dot;
    }

    // How many distinct DoT skills are on the mob (one layer per skill, whatever the number of sources), for Elemental Drain.
    public int DotCount => _dots.Keys.Select(k => k.Skill).Distinct().Count();

    // Whether any source's DoT of this skill is on the mob (for the layer count of a DoT that is about to land).
    public bool HasDotSkill(int skill) => _dots.Keys.Any(k => k.Skill == skill);

    // The stacks this attacker will have after one more hit: a proc adds one up to the cap, a hit with no proc keeps them (HitMark does the same).
    public int StacksAfterHit(int attacker, int maxStacks, bool proc) =>
        proc ? Math.Min(MarkStacks(attacker) + 1, maxStacks) : MarkStacks(attacker);

    // Whether the mob has this status now. A DoT is any DoT, from any source: it lives in the DoT list (AddDot), so never Add(Dot, ...).
    public bool Has(MobStatusKind kind) => kind == MobStatusKind.Dot ? _dots.Count > 0 : _list.ContainsKey(kind);

    // The statuses Burning Magic counts (WZ: damage over time, stun, freeze, darkness, paralysis).
    private static readonly MobStatusKind[] BurningKinds =
        { MobStatusKind.Dot, MobStatusKind.Stun, MobStatusKind.Freeze, MobStatusKind.Darkness, MobStatusKind.Paralysis };

    // The one place that says whether a hit on this mob gets Burning Magic's final damage: the mob has any of those statuses.
    public bool HasBurningTarget
    {
        get
        {
            foreach (var kind in BurningKinds)
                if (Has(kind)) return true;
            return false;
        }
    }

    // The DoTs of one source leave with it (its disconnect, map change or cash-shop entry): no tick of a source
    // that is no longer here. Nothing else changes.
    public void RemoveDotsOf(int attacker)
    {
        if (_dots.Count == 0) return;
        foreach (var key in _dots.Keys.Where(k => k.Attacker == attacker).ToList())
            _dots.Remove(key);
    }

    // The ticks that came due in the steps since the last call, oldest first (a dot that ran out in the same step still gives its last tick).
    public IReadOnlyList<Dot> TakeDue()
    {
        if (_due.Count == 0) return Array.Empty<Dot>(); // most mobs have no DoT: no allocation on their AI steps
        var due = new List<Dot>(_due);
        _due.Clear();
        return due;
    }

    // The Empirical Knowledge stacks this attacker has on the mob (0 = none).
    public int MarkStacks(int attacker) => _marks.TryGetValue(attacker, out var m) ? m.stacks : 0;

    // One hit of the attacker on this mob: a proc adds a stack (capped at maxStacks) and restarts the time; a hit with no proc
    // only refreshes the time of the stacks already there (the ticket: hitting again refreshes). A stack change is reported to the
    // owner (see TakeMarkChanges).
    public void HitMark(int attacker, int maxStacks, int ms, bool proc)
    {
        if (proc)
        {
            if (_marks.TryGetValue(attacker, out var m))
            {
                int stacks = Math.Min(m.stacks + 1, maxStacks);
                _marks[attacker] = (stacks, ms);
                if (stacks != m.stacks) _markChanges.Add((attacker, stacks));
            }
            else
            {
                _marks[attacker] = (1, ms);
                _markChanges.Add((attacker, 1));
            }
        }
        else if (_marks.TryGetValue(attacker, out var have))
            _marks[attacker] = (have.stacks, ms);
    }

    // Set the stacks outright (shot staging and tests that want a fixed stack): 0 removes.
    public void SetMark(int attacker, int stacks, int ms)
    {
        if (stacks <= 0)
        {
            if (_marks.Remove(attacker)) _markChanges.Add((attacker, 0));
            return;
        }
        _marks[attacker] = (stacks, ms);
        _markChanges.Add((attacker, stacks));
    }

    // The marks of one source leave with it (its disconnect, map change or cash-shop entry), like its DoTs.
    public void RemoveMarksOf(int attacker)
    {
        if (_marks.Remove(attacker)) _markChanges.Add((attacker, 0));
    }

    // The stack changes since the last call, to send to the owner's own client (nobody else sees these marks).
    public IReadOnlyList<(int attacker, int stacks)> TakeMarkChanges()
    {
        if (_markChanges.Count == 0) return Array.Empty<(int, int)>(); // most steps change no mark
        var changes = new List<(int, int)>(_markChanges);
        _markChanges.Clear();
        return changes;
    }

    // The number of the kind, 0 when the mob does not have it.
    public double Value(MobStatusKind kind) => _list.TryGetValue(kind, out var s) ? s.value : 0;

    // The damage % a magic hit gets from this mob's statuses (0 = none).
    public double MagicExtraDamagePct => Value(MobStatusKind.ElementalWeakness);

    public void Step(int ms)
    {
        if (_dots.Count > 0)
        {
            foreach (var (key, dot) in _dots.ToList())
            {
                int run = Math.Min(ms, dot.MsLeft);
                dot.MsLeft -= run;
                dot.NextMs -= run;
                for (; dot.NextMs <= 0; dot.NextMs += dot.IntervalMs) _due.Add(dot);
                if (dot.MsLeft == 0) _dots.Remove(key);
            }
        }
        if (_marks.Count > 0)
        {
            foreach (int attacker in _marks.Keys.ToList())
            {
                var m = _marks[attacker];
                int left = m.msLeft - ms;
                if (left <= 0) { _marks.Remove(attacker); _markChanges.Add((attacker, 0)); }
                else _marks[attacker] = (m.stacks, left);
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
