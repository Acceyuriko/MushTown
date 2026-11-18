namespace MushTown.Shared;

// The Combat Power of the stat panel (research/character-stats.md section 4, the wiki Combat Power page):
//   CP = floor(0.01 x (weapon coefficient / the job's highest weapon coefficient) x (4 x main + sub)
//        x (1 + damage% + boss%) x (1 + final damage%) / prod(1 + skill final damage%)
//        x (1.35 + crit damage%) x adjusted attack)
// A mage reads magic attack where the others read attack. It is only shown: it never goes into the damage.
// Left out on purpose (the page leaves them out too): ignore defense, ignore element resistance, level gap, map factor.
// Two simplifications, until the systems they need exist:
//  - Adjusted attack = the panel attack. The reference weapon correction ("panel attack - the weapon's total attack
//    + the attack of a same-grade reference bow") needs the star force, which is not built: the correction is 0.
//  - The skill final damage divisor is 1: the numbers a skill carries for its own cast (its damage%, its final damage)
//    are not in the snapshot, so there is no skill final damage to divide out.
// The page says the stats that skills give are not counted. We read it (the user's reading) as: the numbers a skill
// carries for its own cast are not counted. A passive skill adds its stats straight to the panel, so they are counted:
// the CP reads the full snapshot, every source (Skill, Buff, Equipment, Union, Hyper, Trait).
// Consumable sources: no consumable source has a real value yet; decide when they are built.
public static class CombatPower
{
    public const double CritBase = 1.35;

    // job: the current class and worn weapon profile; maxCoefficient: the highest weapon coefficient of the class.
    public static long Calculate(StatSnapshot stats, JobProfile job, double maxCoefficient)
    {
        double weaponRatio = StatTables.Weapon(job.Weapon).Coefficient / maxCoefficient;
        long statValue = 4 * stats.IntValue(job.Main) + stats.IntValue(job.Sub);
        double damageZone = 1.0 + (stats.Value(StatKey.DAMAGE_PCT) + stats.Value(StatKey.BOSS_PCT)) / 100.0;
        double finalZone = DamageChain.FinalDamageMultiplier(stats); // no skill final damage to divide out yet
        double critZone = CritBase + stats.Value(StatKey.CRIT_DAMAGE) / 100.0;
        long attack = DamageChain.TotalAttack(stats, job.Main == StatKey.INT);
        return (long)Math.Floor(0.01 * weaponRatio * statValue * damageZone * finalZone * critZone * attack + 1e-6); // 1e-6: 11070 must not land on 11069.99999
    }
}
