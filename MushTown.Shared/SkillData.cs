namespace MushTown.Shared;

// One skill. Kinds: active (cast for damage), buff (cast for a timed self
// effect), passive (always on once learned). v1 scaling rules, uniform:
// active damage lerps DmgLo->DmgHi over lv 1..max; every other column is
// per skill level (value = column x lv), except BuffMs/OrbMax (flat).
// MP and cooldown never scale (v1). Modern-client gaps, all v1 mapped:
// Iron Body/Guardian/Endure lose DEF-flavored parts (no mob damage until
// W13) and keep +MaxHP%/+attack; Weapon Mastery/Chance Attack feed the
// crit stat; Warrior Mastery moves both run and jump with one column.
public sealed class SkillData
{
    // 100 Warrior.
    public const int SlashBlast = 1001005;
    public const int WarLeap = 1001008;
    public const int LeapAttack = 1001010;
    public const int IronBody = 1000003;
    public const int WarriorMastery = 1000009;
    public const int GuardianArmor = 1000007;
    // 110 Fighter.
    public const int Brandish = 1101011;
    public const int FlashBlade = 1101014;
    public const int ComboAttack = 1100013;
    public const int SpiritBlade = 1101006;
    public const int WeaponMastery = 1100000;
    public const int AgileArms = 1100015;
    public const int FinalAttack = 1100002;
    public const int PhysicalTraining = 1100009;
    // 111 Crusader.
    public const int IntrepidSlash = 1111010;
    public const int BeamBlade = 1111016;
    public const int Rush = 1111012;
    public const int ScarringSword = 1111003;
    public const int ComboSynergy = 1110013;
    public const int SelfRecovery = 1110000;
    public const int ChanceAttack = 1110009;
    public const int Endure = 1110011;

    public const int KindActive = 0;
    public const int KindBuff = 1;
    public const int KindPassive = 2;

    public int SkillId { get; set; }
    public string Name { get; set; } = "";
    public int ReqLevel { get; set; }
    public int MaxLevel { get; set; }
    public int Kind { get; set; }
    public int Mp { get; set; }
    public int CooldownMs { get; set; }
    // Active: damage pct at lv 1 and at max, mob cap, ring px, cast lock ms.
    public int DmgLo { get; set; }
    public int DmgHi { get; set; }
    public int MobCount { get; set; }
    public int Range { get; set; }
    public int CastLockMs { get; set; }
    // Buff: duration ms (flat), +attack% / +maxHP% per lv, orb parts (Combo:
    // gain chance% per lv, cap flat, +damage% per orb per lv... Synergy adds).
    public int BuffMs { get; set; }
    public int AtkPct { get; set; }
    public int HpPct { get; set; }
    public int OrbChance { get; set; }
    public int OrbMax { get; set; }
    public int OrbDmg { get; set; }
    // Passive, all per skill level: stats, +damage%, crit%, +maxHP%, run and
    // jump %, flat weapon attack (Guardian v1), regen per 10s tick, Final
    // Attack proc %, Combo Synergy (+damage% per orb, +gain chance%).
    public int Str { get; set; }
    public int Dex { get; set; }
    public int DmgPct { get; set; }
    public int CritPct { get; set; }
    public int MovePct { get; set; }
    public int Atk { get; set; }
    public int RegenHp { get; set; }
    public int RegenMp { get; set; }
    public int ProcPct { get; set; }
    public int SynDmg { get; set; }
    public int SynChance { get; set; }
    public string Icon { get; set; } = "";
    public string Disabled { get; set; } = "";
    public List<string> Effect { get; set; } = new();
    public List<string> Hit { get; set; } = new();
    public List<string> Affected { get; set; } = new(); // buff loop frames (buffs with art only)
}
