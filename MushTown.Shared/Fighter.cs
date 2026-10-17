namespace MushTown.Shared;

// Combat clocks: the swing lock, skill cooldowns, live buffs, combo orbs,
// and the Self Recovery timer. Moved from the Client in S3, behavior kept.
// Held directly like SkillCost (never added to an object): the Client calls
// Tick at frame-top, the exact old spot. Riding the world step instead would
// sample every gate one phase late (the S2 facing/lock lesson), so Tick stays
// manual and Update stays a no-op.
public sealed class Fighter : Component
{
    private const int RegenTickMs = 10000; // Self Recovery pays out every 10s, in combat too

    public float AttackLock; // ms of swing left: movement frozen until it hits 0
    public Dictionary<int, float> Cooldowns = new(); // ms left per cooling skill (clamped, never removed)
    public Dictionary<int, float> Buffs = new(); // ms left per live buff (removed on expiry)
    public int Orbs; // combo orbs held (Combo Attack live only)

    // Outputs, fresh every tick: the Client drains them right after.
    public List<int> ExpiredBuffs = new(); // buffs that ran out: EndBuff each, in order
    public bool RegenDue; // the recovery timer fired: pay out both pools

    private float _regenMs = RegenTickMs; // ms to the next Self Recovery tick

    public void Tick(float dtMs)
    {
        AttackLock = Math.Max(0, AttackLock - dtMs);
        foreach (int id in Cooldowns.Keys.ToList())
            Cooldowns[id] = Math.Max(0, Cooldowns[id] - dtMs);
        ExpiredBuffs.Clear();
        foreach (int id in Buffs.Keys.ToList()) // buffs tick down, then leave (loops and stats go with them)
        {
            Buffs[id] -= dtMs;
            if (Buffs[id] <= 0)
            {
                Buffs.Remove(id);
                ExpiredBuffs.Add(id);
            }
        }
        RegenDue = false;
        _regenMs -= dtMs;
        if (_regenMs <= 0)
        {
            _regenMs += RegenTickMs;
            RegenDue = true;
        }
    }
}
