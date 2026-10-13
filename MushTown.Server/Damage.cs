namespace MushTown.Server;

// Basic attack damage, v1. Pure function, one step per line, following the
// bwiki damage guide (https://maplewiki.net/index.php?title=伤害计算&action=raw):
// stats value -> panel range -> roll -> defense -> global -> min 1.
// V1 plays a fixed level-1 beginner (W9 passes real stats in, W10 skills).
public static class Damage
{
    // One knob for the whole game feel; v1 leaves the formula untouched.
    private const double GlobalMult = 1.0;

    // Polearm weapon multiplier. bwiki's weapon table page is gone (template
    // 404), so 4.6 is a v1 guess, kept visible here until verified.
    private const double PolearmMult = 4.6;

    // Melee base mastery: bwiki 熟练度 floor 20% (no beginner skill raises it).
    private const double MeleeMastery = 0.20;

    // The v1 attacker: 4/4 beginner with the 01442000 polearm (32 = its WZ incPAD).
    private const int BeginnerStr = 4;
    private const int BeginnerDex = 4;
    private const int BeginnerWeaponAtk = 32;

    // roll01 blends min->max (v1 rolls uniform; official MS rolls per damage line).
    // def is the fraction of damage the mob blocks (0-1).
    public static int Calc(double roll01, double def)
    {
        int statsValue = BeginnerStr * 4 + BeginnerDex; // warrior: STR main, DEX sub
        int maxRange = (int)(PolearmMult * statsValue * BeginnerWeaponAtk / 100); // 实际最大面板
        int minRange = (int)(maxRange * MeleeMastery); // 实际最小面板
        double roll = minRange + (maxRange - minRange) * roll01;
        double guarded = roll * (1 - def); // 防御 (v1: nothing ignores it)
        return Math.Max(1, (int)(guarded * GlobalMult)); // every hit lands for >= 1
    }
}
