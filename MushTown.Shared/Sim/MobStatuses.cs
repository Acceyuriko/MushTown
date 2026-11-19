namespace MushTown.Shared;

// What a status does to a mob. One entry per kind: the next kinds (damage over time, stun, slow) add
// their own name here and read their own number from the list.
public enum MobStatusKind
{
    ElementalWeakness, // value = the extra magic damage taken, in percent
}

// The statuses on one mob: a kind, its number and the time left. Adding a kind that is already on the
// mob replaces its number and restarts its time (it refreshes, it does not stack). The mob steps it
// on every AI tick; a dead mob is gone with its list.
// The damage-over-time effects (DoT) are kept apart, one entry per SKILL that put one on: the same skill again only refreshes
// its time, another skill is another effect (the user's rule). Only the count is read now (Elemental Drain); the
// DoT skills add their damage numbers with their own tickets.
public sealed class MobStatuses
{
    private readonly Dictionary<MobStatusKind, (double value, int msLeft)> _list = new();

    private readonly Dictionary<int, int> _dots = new(); // skill id -> ms left

    public void Add(MobStatusKind kind, double value, int ms) => _list[kind] = (value, ms);

    public void AddDot(int skill, int ms) => _dots[skill] = ms;

    // How many different skills have a DoT on the mob.
    public int DotCount => _dots.Count;

    // The number of the kind, 0 when the mob does not have it.
    public double Value(MobStatusKind kind) => _list.TryGetValue(kind, out var s) ? s.value : 0;

    // The damage % a magic hit gets from this mob's statuses (0 = none).
    public double MagicExtraDamagePct => Value(MobStatusKind.ElementalWeakness);

    public void Step(int ms)
    {
        if (_dots.Count > 0)
        {
            foreach (int skill in _dots.Keys.ToList())
            {
                if (_dots[skill] <= ms) _dots.Remove(skill);
                else _dots[skill] -= ms;
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
