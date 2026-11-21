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
    private const int InfinityTickMs = 5000; // Infinity restores HP/MP every 5s (WZ x = 5)
    private const int ArcaneAimTimeMs = 5000; // Arcane Aim's stacks all leave after 5s without a hit (WZ time = 5)

    public float AttackLock; // ms of swing left: movement frozen until it hits 0
    public float AttackScale = 1f; // the time scale the lock was set with (StatTables.DelayScale): the swing frames run by it
    public Dictionary<int, float> Cooldowns = new(); // ms left per cooling skill (clamped, never removed)
    public Dictionary<int, float> Buffs = new(); // ms left per live buff (removed on expiry)
    public int Orbs; // combo orbs held (Combo Attack live only)
    public int StatusShield; // Elemental Adaptation's layers left: each blocks one abnormal status (no timer, only the cast gives them; the last one spent starts the cooldown)
    public int ArcaneStacks; // Arcane Aim's stacks on the player: each adds damage % (0 when the skill is not learned or the time ran out)

    // Outputs, fresh every tick: the Client drains them right after.
    public List<int> ExpiredBuffs = new(); // buffs that ran out: EndBuff each, in order
    public bool RegenDue; // the recovery timer fired: pay out both pools
    public bool InfinityDue; // Infinity's 5s clock fired: restore 10% of each pool

    private float _regenMs = RegenTickMs; // ms to the next Self Recovery tick
    private float _infinityMs = InfinityTickMs; // ms to the next Infinity recovery
    private float _arcaneMs; // ms left on the Arcane Aim stacks (0 = none)

    // Arcane Aim: a hit adds a stack (up to max) and restarts the 5s clock (a hit at the cap only restarts the clock).
    public void AddArcaneStack(int max)
    {
        if (ArcaneStacks < max) ArcaneStacks++;
        _arcaneMs = ArcaneAimTimeMs;
    }

    // Starts a swing or a cast: the lock is the base time x the scale (stage 4 = 1.0).
    public void StartAttack(float baseMs, float scale)
    {
        AttackLock = baseMs * scale;
        AttackScale = scale;
    }

    // A new session or character: no cooldown, no buff, no orb, no shield layer, no Arcane Aim stack.
    public void Reset()
    {
        Cooldowns.Clear();
        Buffs.Clear();
        Orbs = 0;
        StatusShield = 0;
        ArcaneStacks = 0;
        _arcaneMs = 0;
    }

    // Elemental Adaptation's cast: refused while layers remain, else the usual pay (MP, level, cooldown check) and WZ y layers. The cast
    // starts no cooldown: it starts when the last layer is spent.
    public bool TryCastShield(SkillCost cost, CharaSlot? chara, WzSkillData wz, int lv)
    {
        if (StatusShield > 0 || !cost.Pay(chara, wz, lv, Cooldowns)) return false;
        StatusShield = (int)wz.Value("y", lv);
        return true;
    }

    // The entry a status asks before it is put on the player: a layer left and WZ x percent of the max MP in the pool (rounded down) make it
    // blocked (true): the MP is paid and a layer is spent; the last layer starts the cooldown. No layer, or not enough MP (WZ does not say
    // what happens then: unverified): not blocked, nothing is paid. No caller yet: no status exists (see IStatusEffect).
    public bool TryBlockStatus(CharaSlot chara, WzSkillData wz, int lv, int maxMp)
    {
        if (StatusShield <= 0) return false;
        int mp = maxMp * (int)wz.Value("x", lv) / 100;
        if (chara.Mp < mp) return false;
        chara.Mp -= mp;
        if (--StatusShield == 0) Cooldowns[wz.SkillId] = SkillCost.CooldownMs(wz, lv);
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
        InfinityDue = false;
        _infinityMs -= dtMs;
        if (_infinityMs <= 0)
        {
            _infinityMs += InfinityTickMs;
            InfinityDue = true;
        }
        if (ArcaneStacks > 0)
        {
            _arcaneMs -= dtMs;
            if (_arcaneMs <= 0) ArcaneStacks = 0;
        }
    }
}
