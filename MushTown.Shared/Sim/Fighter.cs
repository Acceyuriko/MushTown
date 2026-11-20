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
    public float AttackScale = 1f; // the time scale the lock was set with (StatTables.DelayScale): the swing frames run by it
    public Dictionary<int, float> Cooldowns = new(); // ms left per cooling skill (clamped, never removed)
    public Dictionary<int, float> Buffs = new(); // ms left per live buff (removed on expiry)
    public int Orbs; // combo orbs held (Combo Attack live only)
    public int StatusShield; // Elemental Adaptation's layers left: each blocks one abnormal status (no timer, only the cast gives them)

    // Outputs, fresh every tick: the Client drains them right after.
    public List<int> ExpiredBuffs = new(); // buffs that ran out: EndBuff each, in order
    public bool RegenDue; // the recovery timer fired: pay out both pools

    private float _regenMs = RegenTickMs; // ms to the next Self Recovery tick

    // Starts a swing or a cast: the lock is the base time x the scale (stage 4 = 1.0).
    public void StartAttack(float baseMs, float scale)
    {
        AttackLock = baseMs * scale;
        AttackScale = scale;
    }

    // A new session or character: no cooldown, no buff, no orb, no shield layer.
    public void Reset()
    {
        Cooldowns.Clear();
        Buffs.Clear();
        Orbs = 0;
        StatusShield = 0;
    }

    // The entry a status asks before it is put on the player: a layer left is spent and the status is blocked (true); no layer, not blocked.
    // No caller yet: no status exists (see IStatusEffect).
    public bool TryBlockStatus()
    {
        if (StatusShield <= 0) return false;
        StatusShield--;
        return true;
    }

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
