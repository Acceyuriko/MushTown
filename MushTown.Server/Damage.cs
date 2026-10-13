using MushTown.Shared;

namespace MushTown.Server;

// Basic attack and skill damage, v1. One shape, following the bwiki damage
// guide (https://maplewiki.net/index.php?title=伤害计算&action=raw):
// stats value -> panel range -> roll -> skill percent -> defense -> crit ->
// global -> min 1. A basic swing is a 100% roll. Crits (Weapon/Chance
// Attack feed the rate) pay 150% per hit line, official-flavored.
public static class Damage
{
    // One knob for the whole game feel; v1 leaves the formula untouched.
    private const double GlobalMult = 1.0;
    private const double CritMult = 1.5;

    // roll01 blends min->max (v1 rolls uniform; official MS rolls per damage line).
    // def is the fraction of damage the mob blocks (0-1). dmgPct adds +damage%
    // (Weapon Mastery); crit01 < critPct/100 crits.
    public static int Calc(double roll01, double crit01, double def, Build build) =>
        SkillCalc(roll01, crit01, def, build, 100);

    // Skill hit: the cast-time pct (skill level and combo orbs folded in by
    // the caster) plus the attacker's +damage%, then the same
    // defense/crit/global/min-1 tail. v1 rolls one line (official MS rolls
    // per attack line; multi-line display is polish).
    public static int SkillCalc(double roll01, double crit01, double def, Build build, int pct)
    {
        (int minRange, int maxRange) = Progression.PanelRange(build.Str, build.Dex, build.WeaponAtk);
        double roll = minRange + (maxRange - minRange) * roll01;
        double skilled = roll * (pct + build.DmgPct) / 100; // 技能倍率
        double guarded = skilled * (1 - def);
        double critted = crit01 * 100 < build.CritPct ? guarded * CritMult : guarded;
        return Math.Max(1, (int)(critted * GlobalMult)); // every hit lands for >= 1
    }
}
