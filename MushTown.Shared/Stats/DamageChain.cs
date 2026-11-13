namespace MushTown.Shared;

// The element carried by an attack. Mob ElemAttr uses the same letters:
// P physical, F fire, I ice, L lightning, S poison, H holy, D dark.
public enum AttackElement
{
    Physical = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3,
    Poison = 4,
    Holy = 5,
    Dark = 6,
}

// The result of every step, kept separate so tests can pin each multiplier.
public readonly record struct DamageResult(
    long StatValue,
    long MinActual,
    long MaxActual,
    long MinPanel,
    long MaxPanel,
    double MinSkill,
    double MaxSkill,
    double CritMultiplier,
    double DefenseMultiplier,
    double ElementMultiplier,
    double LevelMultiplier,
    long TotalDamage);

// Pure 8-step damage chain. PanelRange reads the stat snapshot directly;
// the production chain reads Build and shares Panel() with the panel.
public static class DamageChain
{
    // Steps 1, 3 and 4 live here so the panel and the Build chain cannot drift.
    private static long StatValue(long main, long sub) => 4L * main + sub;

    // Step 2: floor(base attack * (1 + attack% / 100)) + final attack.
    public static long TotalAttack(StatSnapshot stats, bool magic = false)
    {
        StatKey key = magic ? StatKey.MATT : StatKey.ATT;
        StatKey pctKey = magic ? StatKey.MATT_PCT : StatKey.ATT_PCT;
        double pct = stats.Percent(key) + stats.Percent(pctKey);
        return (long)Math.Floor(stats.Flat(key) * (1.0 + pct / 100.0))
            + (long)Math.Floor(stats.FinalFlat(key));
    }

    public static double FinalDamageMultiplier(StatSnapshot stats) =>
        stats.Multiplier(StatKey.FINAL_DAMAGE_PCT) * (1.0 + Points(stats, StatKey.FINAL_DAMAGE_PCT) / 100.0);

    // Step 3 truncates instead of rounding, matching the old panel.
    private static (long MinActual, long MaxActual, long MinPanel, long MaxPanel) Panel(
        long statValue, long attack, int dmgPct, double finalMultiplier, WeaponProfile weapon)
    {
        double damage = dmgPct / 100.0;
        long maxActual = (long)(weapon.Coefficient * statValue * attack / 100.0);
        long minActual = (long)(maxActual * weapon.MasteryPct / 100.0);
        long maxPanel = (long)Math.Floor(maxActual * (1.0 + damage) * finalMultiplier);
        long minPanel = (long)Math.Floor(1.0 + minActual * (1.0 + damage) * finalMultiplier);
        return (minActual, maxActual, minPanel, maxPanel);
    }

    // Step 4: panel range, including damage% and final damage%.
    public static (long min, long max) PanelRange(StatSnapshot stats, JobProfile job)
    {
        var panel = Panel(
            StatValue(stats.IntValue(job.Main), stats.IntValue(job.Sub)),
            TotalAttack(stats),
            (int)stats.IntValue(StatKey.DAMAGE_PCT),
            FinalDamageMultiplier(stats),
            StatTables.Weapon(job.Weapon));
        return (panel.MinPanel, panel.MaxPanel);
    }

    // Step 7 multipliers.
    public static double CritMultiplier(bool crit, double critDamagePct) =>
        crit ? 1.0 + (50.0 + critDamagePct) / 100.0 : 1.0;

    public static double DefenseMultiplier(double mobDefPct, double iedMultiplier) =>
        1.0 - mobDefPct / 100.0 * iedMultiplier;

    public static double IedMultiplier(StatSnapshot stats) =>
        IedMultiplier(stats.Multiplier(StatKey.IED_PCT), Points(stats, StatKey.IED_PCT));

    public static double IedMultiplier(double multiplier, double flatPct) =>
        multiplier * (1.0 - flatPct / 100.0);

    private static double Points(StatSnapshot stats, StatKey key) =>
        Math.Floor(stats.Flat(key) * (1.0 + stats.Percent(key) / 100.0)) + stats.FinalFlat(key);

    public static double LevelMultiplier(int attackerLevel, int mobLevel)
    {
        int d = attackerLevel - mobLevel;
        if (d >= 5) return 1.20;
        if (d == 4) return 1.18;
        if (d == 3) return 1.16;
        if (d == 2) return 1.14;
        if (d == 1) return 1.12;
        if (d == 0) return 1.10;
        if (d == -1) return 1.0584;
        if (d == -2) return 1.007;
        if (d == -3) return 0.9672;
        if (d == -4) return 0.918;
        if (d == -5) return 0.88;
        if (d == -6) return 0.85;
        if (d >= -10) return 0.85 - 0.025 * (-6 - d); // -7..-10: 0.825 -> 0.75
        if (d >= -20) return 0.75 - 0.025 * (-10 - d); // -11..-20: 0.725 -> 0.50
        if (d <= -40) return 0.0;
        return 0.50 - 0.025 * (-20 - d); // -21..-39: 0.475 -> 0.025
    }

    // The production chain. Build carries the snapshot values; the target's
    // level and ElemAttr are passed separately because they are not attacker
    // stats. Step 8 keeps one hit per call; TotalDamage is the production
    // return value used by Damage.
    public static DamageResult Calculate(Build build, double roll01, double crit01, double mobDef,
        int mobLevel, string mobElemAttr = "", AttackElement element = AttackElement.Physical,
        int skillPct = 100, bool boss = false)
    {
        var weapon = StatTables.Weapon((WeaponType)build.WeaponType);
        long statValue = StatValue(build.Str, build.Dex);
        var panel = Panel(statValue, build.WeaponAtk, build.DmgPct,
            1.0 + build.FinalDamagePct / 100.0, weapon);
        var (minActual, maxActual, minPanel, maxPanel) = panel;
        double damage = build.DmgPct;
        double target = 1.0 + (damage + (boss ? build.BossPct : build.NormalPct)) / 100.0;
        double minSkill = minPanel / (1.0 + damage / 100.0) * skillPct / 100.0 * target;
        double maxSkill = maxPanel / (1.0 + damage / 100.0) * skillPct / 100.0 * target;
        double actual = minSkill + (maxSkill - minSkill) * Math.Clamp(roll01, 0.0, 1.0);
        bool crit = crit01 * 100.0 < Math.Clamp(build.CritPct, 0.0, 100.0);
        double critMult = CritMultiplier(crit, build.CritDamage);
        double defense = DefenseMultiplier(mobDef * 100.0, 1.0 - build.IedPct / 100.0);
        double level = mobLevel <= 0 ? 1.0 : LevelMultiplier(build.Level, mobLevel);
        double elemMult = string.IsNullOrEmpty(mobElemAttr) ? 1.0 : MobElemAttr.Multiplier(mobElemAttr, element);
        double after = actual * critMult * defense * elemMult * level;
        long perHit = Math.Max(1L, (long)Math.Floor(after));
        return new DamageResult(statValue, minActual, maxActual,
            minPanel, maxPanel, minSkill, maxSkill, critMult, defense, elemMult, level, perHit);
    }
}
