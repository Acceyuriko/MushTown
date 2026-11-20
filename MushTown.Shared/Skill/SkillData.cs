namespace MushTown.Shared;

// One skill. Kinds: active (cast for damage), buff (cast for a timed self
// effect), passive (always on once learned). v1 scaling rules, uniform:
// active damage lerps DmgLo->DmgHi over lv 1..max; every other column is
// per skill level (value = column x lv), except BuffMs/OrbMax (flat).
// MP and cooldown never scale (v1). Modern-client gaps, all v1 mapped:
// Iron Body/Guardian/Endure keep +MaxHP%/+attack and add DEF% (W13 mob
// damage); Weapon Mastery/Chance Attack feed the crit stat; Warrior Mastery
// moves both run and jump with one column.
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
    // jump %, flat weapon attack (Guardian v1), DEF% (mob touch), regen per
    // 10s tick, Final Attack proc %, Combo Synergy (+damage% per orb, +gain chance%).
    public int Str { get; set; }
    public int Dex { get; set; }
    public int DmgPct { get; set; } // Weapon Mastery: WZ says "Final Damage: +pdR%", so this is a FINAL damage % per level (FinalDamageModifier), not a damage % point
    public int CritPct { get; set; }
    public int MovePct { get; set; }
    public int Atk { get; set; }
    public int DefPct { get; set; }
    public int RegenHp { get; set; }
    public int RegenMp { get; set; }
    public int ProcPct { get; set; }
    public int SynDmg { get; set; } // Combo Synergy: WZ says "Final Damage increases by damR% for each combo orb": a FINAL damage % per orb per level (SkillDamage.Pct multiplies it in)
    public int SynChance { get; set; }
    public List<string> Desc { get; set; } = new(); // String.wz desc, one entry per line (the tooltip)
    public string Icon { get; set; } = "";
    public string Disabled { get; set; } = "";
    public List<string> Effect { get; set; } = new();
    public List<string> Hit { get; set; } = new();
    public List<string> Affected { get; set; } = new(); // buff loop frames (buffs with art only)

    // Weapon Mastery's final damage at a skill level: a MULTIPLIER record on FINAL_DAMAGE_PCT (1 + DmgPct x lv %), like every final damage source.
    public StatModifier FinalDamageModifier(int lv) =>
        new(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.0 + DmgPct * lv / 100.0, StatSource.Skill);
}
