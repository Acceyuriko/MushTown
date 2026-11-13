namespace MushTown.Shared;

// The 27 keys every character-stat source can modify. The order is the
// canonical order used by snapshots and the stat panel.
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
    BUFF_DURATION_PCT,
    CDR_PCT,
    COOLDOWN_SKIP_PCT,
    DROP_PCT,
    MESO_PCT,
    EXP_PCT,
    SPEED,
    JUMP,
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

// One modifier record. Source is for tracing only; the aggregator never
// branches on the source name.
public readonly record struct StatModifier(StatKey Key, StatValueType Type, double Value, string Source);

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

    public static StatSnapshot Empty { get; } = new();

    private StatSnapshot() => Array.Fill(_multiplier, 1.0);

    internal StatSnapshot(double[] flat, double[] percent, double[] finalFlat, double[] multiplier)
    {
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

    // The fully aggregated value of one key.
    public double Value(StatKey key)
    {
        int i = (int)key;
        double value = Math.Floor(_flat[i] * (1.0 + _percent[i] / 100.0)) + _finalFlat[i];
        return value * _multiplier[i];
    }

    public long IntValue(StatKey key) => (long)Math.Floor(Value(key));

    // Raw value for movement stats: SPEED/JUMP must not lose a fraction.
    public double DoubleValue(StatKey key)
    {
        int i = (int)key;
        return (_flat[i] * (1.0 + _percent[i] / 100.0) + _finalFlat[i]) * _multiplier[i];
    }
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

        foreach (var m in modifiers)
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
        return new StatSnapshot(flat, percent, finalFlat, multiplier);
    }
}
