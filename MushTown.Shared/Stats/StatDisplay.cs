using System.Globalization;

namespace MushTown.Shared;

// One line of the hover source list: the label, the number, which key it
// came from, and whether the number is a PERCENT bucket (a share of the
// fixed values) instead of points of the key itself.
public readonly record struct StatSourceLine(string Label, double Value, StatKey Key, bool Bucket, string? Text = null);

public enum StatFormat
{
    Count, // an integer game value with thousands marks
    Pct0, // a whole percent
    Pct2, // a percent with two decimals
}

// What the stat panel shows for each of its 34 rows (28 on page 1; the bottom
// block has a second page, rows 28..33), computed from the one snapshot. The
// row order is the one of HudStat.StatRows. The snapshot keeps the full
// decimals; the rounding happens only here, in one place: percentages round
// half away from zero at the digits shown, integer stats show the floored
// game value. Row 6 (the damage range) has no key and cannot be passed to
// Number, Text or Sources: the panel builds it from the class and the weapon.
public static class StatDisplay
{
    public const int CooldownRow = 16;
    public const int FinalDamageRow = 8;
    public const int IedRow = 10;
    public const int StatusResistanceRow = 29;

    private readonly record struct Row(StatFormat Format, StatKey[] Keys);

    private static Row R(StatFormat format, params StatKey[] keys) => new(format, keys);

    private static readonly Row[] Rows =
    {
        R(StatFormat.Count, StatKey.MAX_HP), R(StatFormat.Count, StatKey.MAX_MP), // 0..5: HP MP / STR DEX / INT LUK
        R(StatFormat.Count, StatKey.STR), R(StatFormat.Count, StatKey.DEX),
        R(StatFormat.Count, StatKey.INT), R(StatFormat.Count, StatKey.LUK),
        R(StatFormat.Count), // 6 damage range: no key
        R(StatFormat.Pct2, StatKey.DAMAGE_PCT), // 7
        R(StatFormat.Pct2, StatKey.FINAL_DAMAGE_PCT), // 8
        R(StatFormat.Pct2, StatKey.BOSS_PCT), // 9
        R(StatFormat.Pct2, StatKey.IED_PCT), // 10
        R(StatFormat.Pct2, StatKey.NORMAL_PCT), // 11
        R(StatFormat.Count, StatKey.ATT, StatKey.ATT_PCT), // 12
        R(StatFormat.Pct0, StatKey.CRIT_RATE), // 13
        R(StatFormat.Count, StatKey.MATT, StatKey.MATT_PCT), // 14
        R(StatFormat.Pct2, StatKey.CRIT_DAMAGE), // 15
        R(StatFormat.Pct0, StatKey.CDR_PCT, StatKey.CDR_SECONDS), // 16: shown as "X sec/Y%"
        R(StatFormat.Pct0, StatKey.BUFF_DURATION_PCT), // 17
        R(StatFormat.Pct2, StatKey.COOLDOWN_SKIP_PCT), // 18
        R(StatFormat.Pct2, StatKey.IGNORE_ELEMENT_RESISTANCE_PCT), // 19
        R(StatFormat.Pct2, StatKey.STATUS_DAMAGE_PCT), // 20
        R(StatFormat.Pct0, StatKey.SUMMON_DURATION_PCT), // 21
        R(StatFormat.Pct0, StatKey.MESO_PCT), // 22 .. 27: the bottom block, page 1
        R(StatFormat.Count, StatKey.STAR_FORCE),
        R(StatFormat.Pct0, StatKey.DROP_PCT),
        R(StatFormat.Count, StatKey.ARCANE_POWER),
        R(StatFormat.Pct2, StatKey.EXP_PCT),
        R(StatFormat.Count, StatKey.SACRED_POWER),
        R(StatFormat.Count, StatKey.DEF), // 28 .. 33: the bottom block, page 2
        R(StatFormat.Pct0, StatKey.STATUS_RESISTANCE),
        R(StatFormat.Pct0, StatKey.SPEED),
        R(StatFormat.Pct0, StatKey.JUMP),
        R(StatFormat.Pct0, StatKey.KNOCKBACK_RESISTANCE),
        R(StatFormat.Count, StatKey.ATTACK_SPEED),
    };

    // The row's number as the panel shows it. The snapshot holds multipliers
    // for final damage and ignore defense (the panel shows the multiplier minus
    // 100%, or 100% minus what is left), the sums of points for damage and the
    // like (already the panel number), and pixels for speed and jump.
    public static double Number(StatSnapshot s, int row) => row switch
    {
        FinalDamageRow => (DamageChain.FinalDamageMultiplier(s) - 1.0) * 100.0,
        IedRow => (1.0 - DamageChain.IedMultiplier(s)) * 100.0,
        12 => DamageChain.TotalAttack(s),
        14 => DamageChain.TotalAttack(s, magic: true),
        30 => s.SpeedPercent,
        31 => s.JumpPercent,
        _ => Rows[row].Format == StatFormat.Count // the multiplier zone (Heroic's Meso x6) is not part of the panel number
            ? s.Additive(Rows[row].Keys[0]) // an integer game value: rounded down
            : s.Exact(Rows[row].Keys[0]), // a percentage: keeps its decimals, only the display rounds
    };

    // The row's text, for every row but the damage range (it needs the class
    // and the weapon). HP and MP give the maximum; the panel adds the current value.
    public static string Text(StatSnapshot s, int row) => row == CooldownRow
        ? $"{Plain(s.Exact(StatKey.CDR_SECONDS))} sec/{Format(StatFormat.Pct0, s.Exact(StatKey.CDR_PCT))}"
        : Format(Rows[row].Format, Number(s, row));

    public static string Format(StatFormat format, double value) => format switch
    {
        StatFormat.Pct2 => Round(value, 2).ToString("0.00", CultureInfo.InvariantCulture) + "%",
        StatFormat.Pct0 => Round(value, 0).ToString("#,##0", CultureInfo.InvariantCulture) + "%",
        _ => Math.Floor(value).ToString("#,##0", CultureInfo.InvariantCulture),
    };

    // One source line of the hover, in the format of its row.
    public static string LineText(int row, StatSourceLine line)
    {
        if (line.Text != null) return line.Text;
        if (line.Bucket) return Plain(line.Value) + "%";
        if (row == CooldownRow && line.Key == StatKey.CDR_SECONDS) return Plain(line.Value) + " sec";
        return Format(Rows[row].Format, line.Value);
    }

    // The share of an abnormal status duration the Status Resistance cuts
    // (research/stat-formulas.md: floor(28 * log10(resistance) + 1), 0 at 0).
    public static int StatusReductionPct(double resistance) =>
        resistance <= 0 ? 0 : (int)Math.Floor(28.0 * Math.Log10(resistance) + 1.0);

    public static string SourceName(StatSource source) => source switch
    {
        StatSource.Equipment or StatSource.Set => "Equipment", // a set effect is listed with the equipment
        StatSource.Skill => "Skill",
        StatSource.Buff => "Buff",
        StatSource.UnionMember => "Union Member",
        StatSource.UnionAttacker => "Union Attacker",
        StatSource.HyperStat => "Hyper Stat",
        StatSource.Trait => "Trait",
        _ => "Base",
    };

    // The hover source list of a row. Rows whose sources multiply (final
    // damage, ignore defense) list one line per source; the others list one
    // line per source group that is not 0, in the group order.
    public static List<StatSourceLine> Sources(StatSnapshot s, int row) => row switch
    {
        FinalDamageRow => Multiplied(s, StatKey.FINAL_DAMAGE_PCT, m => (m - 1.0) * 100.0),
        IedRow => Multiplied(s, StatKey.IED_PCT, m => (1.0 - m) * 100.0),
        _ => Grouped(s, Rows[row].Keys),
    };

    // One line per source; the equipment records (the set effects too) are one line, every value listed
    // ("10.00%,10.00%"), the line's value is the product of them.
    private static List<StatSourceLine> Multiplied(StatSnapshot s, StatKey key, Func<double, double> toPoints)
    {
        var records = s.Modifiers
            .Where(m => m.Key == key && m.Type == StatValueType.MULTIPLIER && m.Source != StatSource.Base && toPoints(m.Value) != 0.0)
            .ToList();
        var lines = new List<StatSourceLine>();
        var gear = records.Where(m => m.Source is StatSource.Equipment or StatSource.Set).ToList();
        if (gear.Count > 0)
            lines.Add(new StatSourceLine(SourceName(StatSource.Equipment), toPoints(gear.Aggregate(1.0, (product, m) => product * m.Value)), key, false,
                string.Join(",", gear.Select(m => Format(StatFormat.Pct2, toPoints(m.Value))))));
        lines.AddRange(records
            .Where(m => m.Source is not (StatSource.Equipment or StatSource.Set))
            .OrderBy(m => m.Source)
            .Select(m => new StatSourceLine(m.Label != "" ? m.Label : SourceName(m.Source), toPoints(m.Value), key, false)));
        return lines;
    }

    private static List<StatSourceLine> Grouped(StatSnapshot s, StatKey[] keys)
    {
        var lines = new List<StatSourceLine>();
        foreach (var source in Enum.GetValues<StatSource>())
        {
            if (source is StatSource.Base or StatSource.Set) continue; // a set effect counts as equipment
            foreach (var key in keys)
            {
                double points = 0, bucket = 0;
                foreach (var m in s.Modifiers)
                {
                    if (m.Key != key || (m.Source != source && !(source == StatSource.Equipment && m.Source == StatSource.Set))) continue;
                    if (m.Type is StatValueType.FLAT or StatValueType.FINAL_FLAT) points += m.Value;
                    else if (m.Type == StatValueType.PERCENT) bucket += m.Value;
                }
                if (points != 0.0) lines.Add(new StatSourceLine(SourceName(source), points, key, false));
                if (bucket != 0.0) lines.Add(new StatSourceLine(SourceName(source), bucket, key, true));
            }
        }
        return lines;
    }

    private static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static string Plain(double value) => Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
