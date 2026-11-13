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

public readonly record struct JobProfile(StatKey Main, StatKey Sub, WeaponType Weapon);

public readonly record struct WeaponProfile(double Coefficient, double MasteryPct);

// Coefficients follow character-stats.md §3.2. Unlisted weapon types use the
// fallback coefficient.
public static class StatTables
{
    public const int Hero = 110;
    public const int FirePoisonMage = 210;

    public static JobProfile Job(int jobId) => jobId switch
    {
        Hero => new(StatKey.STR, StatKey.DEX, WeaponType.HeroTwoHand),
        FirePoisonMage => new(StatKey.INT, StatKey.LUK, WeaponType.ShortStaff),
        _ => new(StatKey.STR, StatKey.DEX, WeaponType.Default),
    };

    public static WeaponProfile Weapon(WeaponType type) => type switch
    {
        WeaponType.OneHandSword => new(1.24, 20),
        WeaponType.HeroTwoHand => new(1.44, 20),
        WeaponType.TwoHandSword => new(1.34, 20),
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
