namespace MushTown.Shared;

// The keys every character-stat source can modify. The order is the
// canonical order used by snapshots and the stat panel. The first 28 keys
// are the original contract; new keys are only ever added after JUMP.
public enum StatKey
{
    STR,
    DEX,
    INT,
    LUK,
    MAX_HP,
    MAX_MP,
    DEF,
    ATT,
    MATT,
    ATT_PCT,
    MATT_PCT,
    DAMAGE_PCT,
    BOSS_PCT,
    NORMAL_PCT,
    FINAL_DAMAGE_PCT,
    CRIT_RATE,
    CRIT_DAMAGE,
    IED_PCT,
    STATUS_RESISTANCE,
    KNOCKBACK_RESISTANCE,
    BUFF_DURATION_PCT,
    CDR_PCT,
    COOLDOWN_SKIP_PCT,
    DROP_PCT,
    MESO_PCT,
    EXP_PCT,
    SPEED,
    JUMP,
    IGNORE_ELEMENT_RESISTANCE_PCT,
    STATUS_DAMAGE_PCT,
    SUMMON_DURATION_PCT,
    STAR_FORCE,
    ARCANE_POWER,
    SACRED_POWER,
    ATTACK_SPEED,
    CDR_SECONDS,
    // Hidden keys, no stat panel row. DAMAGE_TAKEN takes one MULTIPLIER per
    // source (0.82 = 18% less damage taken); ELEMENT_RESISTANCE is summed.
    DAMAGE_TAKEN,
    ELEMENT_RESISTANCE,
    // Hidden key: percent points added to the SPEED cap (WZ speedMax; the base cap is 140%).
    SPEED_MAX,
}

// How one source contributes to a key. FLAT first, then PERCENT, then
// FINAL_FLAT, then MULTIPLIER.
public enum StatValueType
{
    FLAT,
    PERCENT,
    FINAL_FLAT,
    MULTIPLIER,
}

// Where a modifier comes from: a fixed list, in the order the hover shows the
// groups. Base (level, AP, start values, the movement base) is never shown, only
// the extra on top of it.
public enum StatSource
{
    Base,
    Equipment,
    Set, // the piece-count effects of an equipment set
    Skill, // class skills, Heroic and Link
    Buff,
    UnionMember,
    UnionAttacker,
    HyperStat,
    Trait,
}

// One modifier record. Label names the single source (the hover lists it for
// the keys whose sources multiply); the aggregator never branches on it.
public readonly record struct StatModifier(StatKey Key, StatValueType Type, double Value, StatSource Source, string Label = "");

// One immutable character-stat snapshot. Values are kept as the four
// aggregation buckets; Value() applies them in the official order:
// floor(flat * (1 + percent / 100)) + finalFlat, then * multiplier.
public sealed class StatSnapshot
{
    public static readonly StatKey[] AllKeys = Enum.GetValues<StatKey>();

    private readonly double[] _flat = new double[AllKeys.Length];
    private readonly double[] _percent = new double[AllKeys.Length];
    private readonly double[] _finalFlat = new double[AllKeys.Length];
    private readonly double[] _multiplier = new double[AllKeys.Length];

    // Every modifier the snapshot was built from, for the source breakdown.
    public IReadOnlyList<StatModifier> Modifiers { get; } = Array.Empty<StatModifier>();

    public static StatSnapshot Empty { get; } = new();

    private StatSnapshot() => Array.Fill(_multiplier, 1.0);

    internal StatSnapshot(double[] flat, double[] percent, double[] finalFlat, double[] multiplier, IReadOnlyList<StatModifier> modifiers)
    {
        Modifiers = modifiers;
        _flat = flat;
        _percent = percent;
        _finalFlat = finalFlat;
        _multiplier = multiplier;
    }

    // One key's summed fixed value, before percentage.
    public double Flat(StatKey key) => _flat[(int)key];

    // One key's summed percentage points. PERCENT sources are added together
    // before they touch the fixed-value sum.
    public double Percent(StatKey key) => _percent[(int)key];

    // One key's summed final fixed value: added after percentage.
    public double FinalFlat(StatKey key) => _finalFlat[(int)key];

    // Product of MULTIPLIER sources. No source means 1.0 (the neutral value).
    public double Multiplier(StatKey key) => _multiplier[(int)key];

    // The value before the MULTIPLIER zone, capped. The multiplier zone is a
    // separate calculation: Heroic's Meso x6 must not show in the panel
    // number, so the panel reads this for the keys that have such a zone.
    public double Additive(StatKey key)
    {
        int i = (int)key;
        return Cap(key, Math.Floor(_flat[i] * (100.0 + _percent[i]) / 100.0) + _finalFlat[i]);
    }

    // Like Additive but not rounded down: the percentage rows (damage, boss
    // damage, EXP, ...) keep their decimals for the panel. The formula keys
    // keep reading Additive.
    public double Exact(StatKey key)
    {
        int i = (int)key;
        return Cap(key, _flat[i] * (100.0 + _percent[i]) / 100.0 + _finalFlat[i]);
    }

    // The fully aggregated value of one key.
    public double Value(StatKey key) => Additive(key) * _multiplier[(int)key];

    public long IntValue(StatKey key) => (long)Math.Floor(Value(key));

    // Raw value for movement stats: SPEED/JUMP must not lose a fraction.
    public double DoubleValue(StatKey key)
    {
        int i = (int)key;
        return Cap(key, _flat[i] * (1.0 + _percent[i] / 100.0) + _finalFlat[i]) * _multiplier[i];
    }

    // SPEED / JUMP are stored as pixel values; 100% is the base value.
    public double SpeedPercent => DoubleValue(StatKey.SPEED) / PlayerMover.WalkSpeed * 100.0;

    public double JumpPercent => DoubleValue(StatKey.JUMP) / PlayerMover.JumpV0 * 100.0;

    // The caps of research/stat-formulas-decisions.md rule 12. IED needs
    // none: its sources multiply, so it cannot pass 100%. JUMP is a negative
    // take-off velocity, so its cap is a maximum magnitude.
    private double Cap(StatKey key, double value) => key switch
    {
        StatKey.MAX_HP or StatKey.MAX_MP => Math.Min(value, 500_000),
        StatKey.CRIT_RATE => Math.Min(value, 100),
        StatKey.SPEED => Math.Min(value, PlayerMover.WalkSpeed * (140 + _flat[(int)StatKey.SPEED_MAX]) / 100),
        StatKey.JUMP => Math.Max(value, PlayerMover.JumpV0 * 1.23),
        _ => value,
    };
}

// Pure aggregator: four ordered buckets, no source-system logic.
public static class StatAggregator
{
    public static StatSnapshot Aggregate(IEnumerable<StatModifier> modifiers)
    {
        int n = StatSnapshot.AllKeys.Length;
        var flat = new double[n];
        var percent = new double[n];
        var finalFlat = new double[n];
        var multiplier = new double[n];
        Array.Fill(multiplier, 1.0);
        var all = modifiers.ToList();

        foreach (var m in all)
        {
            int i = (int)m.Key;
            switch (m.Type)
            {
                case StatValueType.FLAT:
                    flat[i] += m.Value;
                    break;
                case StatValueType.PERCENT:
                    percent[i] += m.Value;
                    break;
                case StatValueType.FINAL_FLAT:
                    finalFlat[i] += m.Value;
                    break;
                case StatValueType.MULTIPLIER:
                    multiplier[i] *= m.Value;
                    break;
            }
        }
        return new StatSnapshot(flat, percent, finalFlat, multiplier, all);
    }
}
