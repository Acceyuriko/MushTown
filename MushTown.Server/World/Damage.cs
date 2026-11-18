using MushTown.Shared;

namespace MushTown.Server;

// Thin server adapter over the Shared 8-step chain. Damage rules live in
// MushTown.Shared/DamageChain.cs; MobWorld.Strike/SkillStrike call these
// production wrappers.
public static class Damage
{
    // Basic swing: a 100% skill roll. mobLevel <= 0 means no level-advantage
    // multiplier; tests pass it explicitly.
    public static DamageResult CalcResult(double roll01, double crit01, double def, Build build, int mobLevel,
        string mobElemAttr = "", AttackElement element = AttackElement.Physical, bool boss = false) =>
        DamageChain.Calculate(build, roll01, crit01, def, mobLevel, mobElemAttr, element, 100, boss);

    public static long Calc(double roll01, double crit01, double def, Build build, int mobLevel,
        string mobElemAttr = "", AttackElement element = AttackElement.Physical, bool boss = false) =>
        CalcResult(roll01, crit01, def, build, mobLevel, mobElemAttr, element, boss).TotalDamage;

    // Damaging skill: pct is the cast-time skill percentage (level and orbs
    // already folded in by the caster). extraDmgPct is the damage % the mob's statuses add to a magic hit.
    public static DamageResult SkillResult(double roll01, double crit01, double def, Build build, int pct,
        int mobLevel, string mobElemAttr = "", AttackElement element = AttackElement.Physical,
        bool boss = false, double extraDmgPct = 0) =>
        DamageChain.Calculate(build, roll01, crit01, def, mobLevel, mobElemAttr, element, pct, boss, 0, extraDmgPct);

    public static long SkillCalc(double roll01, double crit01, double def, Build build, int pct,
        int mobLevel, string mobElemAttr = "", AttackElement element = AttackElement.Physical,
        bool boss = false, double extraDmgPct = 0) =>
        SkillResult(roll01, crit01, def, build, pct, mobLevel, mobElemAttr, element, boss, extraDmgPct).TotalDamage;
}
