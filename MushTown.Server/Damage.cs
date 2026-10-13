using MushTown.Shared;

namespace MushTown.Server;

// Basic attack and skill damage, v1. One shape, following the bwiki damage
// guide (https://maplewiki.net/index.php?title=伤害计算&action=raw):
// stats value -> panel range -> roll -> skill percent -> defense -> global -> min 1.
// A basic swing is a 100% roll.
public static class Damage
{
    // One knob for the whole game feel; v1 leaves the formula untouched.
    private const double GlobalMult = 1.0;

    // roll01 blends min->max (v1 rolls uniform; official MS rolls per damage line).
    // def is the fraction of damage the mob blocks (0-1).
    public static int Calc(double roll01, double def, int str, int dex, int weaponAtk) =>
        SkillCalc(roll01, def, str, dex, weaponAtk, 100);

    // Skill hit: the panel roll times the skill's damage percent, then the
    // same defense/global/min-1 tail. v1 rolls one line (official MS rolls
    // per attack line; multi-line display is polish).
    public static int SkillCalc(double roll01, double def, int str, int dex, int weaponAtk, int damagePct)
    {
        (int minRange, int maxRange) = Progression.PanelRange(str, dex, weaponAtk);
        double roll = minRange + (maxRange - minRange) * roll01;
        double skilled = roll * damagePct / 100; // 技能倍率
        double guarded = skilled * (1 - def);
        return Math.Max(1, (int)(guarded * GlobalMult)); // every hit lands for >= 1
    }
}
