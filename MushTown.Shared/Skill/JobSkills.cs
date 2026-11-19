namespace MushTown.Shared;

// Which skills a job owns: the job pages I to V of the skill window. A character uses only
// its own job's skills (window, quick bar, macros, casting, passives). The default passives
// (Heroic, Link, Union, Traits) are not job skills and are not listed here.
public static class JobSkills
{
    private static readonly int[][] Hero =
    {
        new[] { SkillData.SlashBlast, SkillData.IronBody, SkillData.WarLeap, SkillData.WarriorMastery, SkillData.LeapAttack, SkillData.GuardianArmor },
        new[] { SkillData.Brandish, SkillData.WeaponMastery, SkillData.SpiritBlade, SkillData.ComboAttack, SkillData.FlashBlade, SkillData.AgileArms, SkillData.FinalAttack, SkillData.PhysicalTraining },
        new[] { SkillData.IntrepidSlash, SkillData.BeamBlade, SkillData.Rush, SkillData.ScarringSword, SkillData.ComboSynergy, SkillData.SelfRecovery, SkillData.ChanceAttack, SkillData.Endure },
        Array.Empty<int>(), // IV: no skills yet
        Array.Empty<int>(), // V: no skills yet
    };

    // Fire/Poison: the 1st job (200.img, the visible ones: Elemental Weakness is invisible in WZ) and the 2nd job
    // (210.img: the passives, then Meditation and Flame Orb; Poison Breath and Ignite follow with their own tickets). Pages III to V follow with their own jobs' tickets.
    private static readonly int[][] FirePoison =
    {
        new[] { WzSkillData.EnergyBolt, WzSkillData.Teleport, WzSkillData.ManaWave, WzSkillData.MagicGuard, WzSkillData.MagicArmor, WzSkillData.MpBoost },
        new[] { WzSkillData.MpEater, WzSkillData.SpellMastery, WzSkillData.HighWisdom, WzSkillData.ElementalDrain, WzSkillData.AgileMagic, WzSkillData.Meditation, WzSkillData.FlameOrb },
        Array.Empty<int>(), // III
        Array.Empty<int>(), // IV
        Array.Empty<int>(), // V
    };

    private static readonly int[][] None = { Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() };

    // The skill ids of pages I to V. A job with no skills yet has five empty pages.
    public static int[][] Pages(int job) => job == StatTables.Hero ? Hero : job == StatTables.FirePoisonMage ? FirePoison : None;

    public static bool Has(int job, int skillId) => Pages(job).Any(page => page.Contains(skillId));

    // Every skill of the job stands at its max level (no skill points); a skill of another job is not learned.
    public static int Level(int job, SkillData skill) => Has(job, skill.SkillId) ? skill.MaxLevel : 0;
}
