using MushTown.Shared;

namespace MushTown.Server;

// Basic attack damage, v1. Pure function, one step per line, following the
// bwiki damage guide (https://maplewiki.net/index.php?title=伤害计算&action=raw):
// stats value -> panel range -> roll -> defense -> global -> min 1.
// W10 passes the real attacker stats in (W11 skills).
public static class Damage
{
    // One knob for the whole game feel; v1 leaves the formula untouched.
    private const double GlobalMult = 1.0;

    // roll01 blends min->max (v1 rolls uniform; official MS rolls per damage line).
    // def is the fraction of damage the mob blocks (0-1).
    public static int Calc(double roll01, double def, int str, int dex, int weaponAtk)
    {
        (int minRange, int maxRange) = Progression.PanelRange(str, dex, weaponAtk); // 实际面板
        double roll = minRange + (maxRange - minRange) * roll01;
        double guarded = roll * (1 - def); // 防御 (v1: nothing ignores it)
        return Math.Max(1, (int)(guarded * GlobalMult)); // every hit lands for >= 1
    }
}
