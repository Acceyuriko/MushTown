namespace MushTown.Shared;

// Weapon categories used by the damage chain. Default is the fallback for
// every weapon/job not in the table yet.
public enum WeaponType
{
    Default = 0,
    OneHandSword = 1,
    HeroTwoHand = 2,
    TwoHandSword = 3,
    ShortStaff = 4,
    Staff = 5,
    OneHandBlunt = 6,
    OneHandAxe = 7,
}

// The vital fields are design values, not v271 official ones (WZ has no base table; .dsh/research/hp-mp.md
// section 1, MapleStory Classic fixed gains): max HP = 50 + HpPerLevel x (level - 1) + HpBonus, max MP =
// 5 + MpPerLevel x (level - 1) + MpBonus. The bonus is the job advancement gifts, folded in. The defaults are
// the Hero row, so every job without a row of its own plays the Hero numbers.
public readonly record struct JobProfile(StatKey Main, StatKey Sub, WeaponType Weapon,
    int HpPerLevel = 28, int MpPerLevel = 12, int HpBonus = 700, int MpBonus = 300);

// CritPct is the crit rate the weapon type brings by itself: a wand has 5% (user 2026-11-18). The other
// types are 0 until they are confirmed.
public readonly record struct WeaponProfile(double Coefficient, double MasteryPct, double CritPct = 0);

// Coefficients follow character-stats.md §3.2. Unlisted weapon types use the
// fallback coefficient.
public static class StatTables
{
    public const int Hero = 110;
    public const int FirePoisonMage = 210;

    // The stat part of Defense (research/stat-formulas.md, StrategyWiki Defense):
    // Defense = [1.5 x STR + 0.4 x (DEX + LUK) + added DEF] x (1 + DEF% / 100). The added DEF and the DEF%
    // are the other DEF records of the snapshot; this is the base record built from the final STR, DEX, LUK.
    public static double BaseDefense(long str, long dex, long luk) => 1.5 * str + 0.4 * (dex + luk);

    public static JobProfile Job(int jobId) => jobId switch
    {
        Hero => new(StatKey.STR, StatKey.DEX, WeaponType.HeroTwoHand),
        FirePoisonMage => new(StatKey.INT, StatKey.LUK, WeaponType.ShortStaff, 16, 22, 300, 700),
        _ => new(StatKey.STR, StatKey.DEX, WeaponType.Default),
    };

    // The max HP / MP records of a job at a level: the base, plus the max-level job skills that raise it.
    // The skill numbers are WZ's (hp-mp.md section 3): Warrior Mastery lv2mhp 20 and Iron Body mhpR 20 at
    // level 20; MP Boost lv2mmp 20 + 5 x 20 and mmpR 20 at level 20. lv2mhp / lv2mmp are per character
    // level, so they are one FLAT record of coefficient x level. A job without a row plays the Hero's skills.
    public static IEnumerable<StatModifier> Vitals(int jobId, int level)
    {
        yield return new(StatKey.MAX_HP, StatValueType.FLAT, Progression.MaxHp(level, jobId), StatSource.Base);
        yield return new(StatKey.MAX_MP, StatValueType.FLAT, Progression.MaxMp(level, jobId), StatSource.Base);
        if (jobId == FirePoisonMage)
        {
            yield return new(StatKey.MAX_MP, StatValueType.FLAT, 120.0 * level, StatSource.Skill, "MP Boost");
            yield return new(StatKey.MAX_MP, StatValueType.PERCENT, 20, StatSource.Skill, "MP Boost");
        }
        else
        {
            yield return new(StatKey.MAX_HP, StatValueType.FLAT, 20.0 * level, StatSource.Skill, "Warrior Mastery");
            yield return new(StatKey.MAX_HP, StatValueType.PERCENT, 20, StatSource.Skill, "Iron Body");
        }
    }

    // The attack speed stage of the panel (1 slowest .. 10 fastest). A weapon's
    // WZ attackSpeed counts the other way round (2 fastest .. 9 slowest), so the
    // stage is 10 minus it: a sword with 4 is stage 6, a wand with 6 is stage 4.
    // A mage always has the fixed stage 4.
    public const int MageAttackSpeed = 4;
    public static int AttackSpeedStage(int wzAttackSpeed) => 10 - wzAttackSpeed;

    // The time scale of a swing or a cast by the panel stage (StrategyWiki MapleStory/Formulas, Attack Speed; not
    // verified in the game): the WZ delays are written for stage 4, so the time is x (20 - stage) / 16, the stage kept in
    // 1..10 (the WZ panel text: +1 is the slowest, +10 the fastest; no soft cap). A stage of 0 (no weapon) counts as 1.
    // Stage 4 is 1.0, stage 8 is 0.75, stage 10 is 0.625.
    public static double DelayScale(double stage) => (20.0 - Math.Clamp(stage, 1.0, 10.0)) / 16.0;

    // The attack speed record of a character: a mage has the fixed stage, the others take the
    // stage of the worn weapon (WZ value 0 or no weapon: no record).
    public static StatModifier? AttackSpeed(bool mage, int weaponWzAttackSpeed) =>
        mage ? new(StatKey.ATTACK_SPEED, StatValueType.FLAT, MageAttackSpeed, StatSource.Base)
        : weaponWzAttackSpeed > 0 ? new(StatKey.ATTACK_SPEED, StatValueType.FLAT, AttackSpeedStage(weaponWzAttackSpeed), StatSource.Equipment)
        : null;

    // The highest weapon coefficient of a job, for the combat power: the job row's own weapon is its best one.
    // That holds for the Hero (two-hand 1.44) and the Fire/Poison Mage (wand and staff 1.20, its only
    // coefficient). Another job gets its own rule when it has a job table row.
    public static double MaxWeaponCoefficient(int jobId) => Weapon(Job(jobId).Weapon).Coefficient;

    public static WeaponProfile Weapon(WeaponType type) => type switch
    {
        WeaponType.OneHandSword => new(1.24, 20),
        WeaponType.HeroTwoHand => new(1.44, 20),
        WeaponType.TwoHandSword => new(1.34, 20),
        WeaponType.ShortStaff => new(1.20, 20, 5),
        _ => new(1.20, 20),
    };

    // Item-id families used by the weapon lookup. The current export only has
    // 130 (swords) and 144 (Mop); unmapped ids fall back to Default.
    public static WeaponType WeaponTypeOf(int itemId) => (itemId / 10000) switch
    {
        130 => WeaponType.OneHandSword,
        131 => WeaponType.OneHandAxe,
        132 => WeaponType.OneHandBlunt,
        137 => WeaponType.ShortStaff,
        138 => WeaponType.Staff,
        140 or 141 => WeaponType.TwoHandSword,
        _ => WeaponType.Default,
    };
}
