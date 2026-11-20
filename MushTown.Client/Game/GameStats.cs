using MushTown.Shared;

namespace MushTown.Client;

// The client's only character-stat collection point. Every source becomes a
// StatModifier; the Shared aggregator turns those records into one snapshot
// per frame, and the stat panel, movement and damage all read that snapshot.
public partial class MushTownGame
{
    private StatSnapshot _stats = StatSnapshot.Empty;
    private bool _hasWeapon = true; // a worn weapon; the default passives give ATT too, so the snapshot cannot tell

    // CLI sessions have no saved character: the old beginner values, but
    // movement must still work (`--host` / `--join` never load a save).
    public static StatSnapshot BeginnerSnapshot() => StatAggregator.Aggregate(new[]
    {
        new StatModifier(StatKey.STR, StatValueType.FLAT, 4, StatSource.Base),
        new StatModifier(StatKey.DEX, StatValueType.FLAT, 4, StatSource.Base),
        new StatModifier(StatKey.INT, StatValueType.FLAT, 4, StatSource.Base),
        new StatModifier(StatKey.LUK, StatValueType.FLAT, 4, StatSource.Base),
        new StatModifier(StatKey.MAX_HP, StatValueType.FLAT, Progression.MaxHp(1), StatSource.Base),
        new StatModifier(StatKey.MAX_MP, StatValueType.FLAT, Progression.MaxMp(1), StatSource.Base),
        new StatModifier(StatKey.ATT, StatValueType.FLAT, Progression.WeaponAtk, StatSource.Base),
        new StatModifier(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, StatSource.Base),
        new StatModifier(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0, StatSource.Base),
    });

    // Hyper Stats are not built yet (T-20261114000518). Every key they will
    // touch already has a neutral record of its own group, so the hover lists
    // the group as soon as a level gives it a value.
    private static readonly StatModifier[] HyperStats = Array.ConvertAll(new (StatKey Key, StatValueType Type)[]
    {
        (StatKey.STR, StatValueType.FINAL_FLAT), (StatKey.DEX, StatValueType.FINAL_FLAT),
        (StatKey.INT, StatValueType.FINAL_FLAT), (StatKey.LUK, StatValueType.FINAL_FLAT),
        (StatKey.MAX_HP, StatValueType.PERCENT), (StatKey.MAX_MP, StatValueType.PERCENT),
        (StatKey.ATT, StatValueType.FINAL_FLAT), (StatKey.MATT, StatValueType.FINAL_FLAT),
        (StatKey.CRIT_RATE, StatValueType.FLAT), (StatKey.CRIT_DAMAGE, StatValueType.FLAT),
        (StatKey.IED_PCT, StatValueType.MULTIPLIER),
        (StatKey.DAMAGE_PCT, StatValueType.FLAT), (StatKey.BOSS_PCT, StatValueType.FLAT), (StatKey.NORMAL_PCT, StatValueType.FLAT),
        (StatKey.STATUS_RESISTANCE, StatValueType.FLAT), (StatKey.EXP_PCT, StatValueType.FLAT), (StatKey.ARCANE_POWER, StatValueType.FLAT),
    }, slot => new StatModifier(slot.Key, slot.Type, slot.Type == StatValueType.MULTIPLIER ? 1.0 : 0.0, StatSource.HyperStat, "Hyper Stat"));

    private void RefreshStats()
    {
        _weaponType = DetectWeaponType();
        var mods = new List<StatModifier>();
        var chara = _save?.Slots[_slot];
        if (chara == null)
        {
            _stats = BeginnerSnapshot();
            _hasWeapon = true;
            return;
        }

        mods.Add(new(StatKey.STR, StatValueType.FLAT, chara.Str, StatSource.Base));
        mods.Add(new(StatKey.DEX, StatValueType.FLAT, chara.Dex, StatSource.Base));
        mods.Add(new(StatKey.INT, StatValueType.FLAT, chara.Int, StatSource.Base));
        mods.Add(new(StatKey.LUK, StatValueType.FLAT, chara.Luk, StatSource.Base));
        mods.AddRange(StatTables.Vitals(chara.Job, chara.Level)); // base vitals and the job skills that scale with the level
        if (chara.Job == StatTables.FirePoisonMage) mods.AddRange(WzSkillStats.Modifiers(_wzSkills, chara.Level, WornWeapon(chara)?.ItemId ?? 0)); // Magic Armor, MP Boost (and its wand crit)

        var worn = ItemData.EquipBonus(chara.Equips, _items);
        _hasWeapon = worn.weapon > 0;
        if (_hasWeapon)
        {
            mods.Add(new(StatKey.ATT, StatValueType.FLAT, worn.weapon, StatSource.Equipment));
            mods.Add(new(StatKey.ATT, StatValueType.FLAT,
                _skills[SkillData.GuardianArmor].Atk * SkillLv(SkillData.GuardianArmor), StatSource.Skill));
        }
        // A mage has the fixed stage; the others take the stage of the worn weapon (no weapon: no stage).
        if (StatTables.AttackSpeed(CurrentJob().Main == StatKey.INT, WornWeapon(chara)?.AttackSpeed ?? 0) is { } speed)
            mods.Add(speed);
        mods.AddRange(ItemData.EquipModifiers(chara.Equips, _items));
        mods.AddRange(SetRules.Modifiers(_sets, chara.Equips));

        mods.AddRange(DefaultPassives.All);
        mods.Add(DefaultPassives.MapleWarrior(StatKey.STR, chara.Str));
        mods.Add(DefaultPassives.MapleWarrior(StatKey.DEX, chara.Dex));
        mods.Add(DefaultPassives.MapleWarrior(StatKey.INT, chara.Int));
        mods.Add(DefaultPassives.MapleWarrior(StatKey.LUK, chara.Luk));
        mods.AddRange(HyperStats);

        int training = SkillLv(SkillData.PhysicalTraining);
        int agile = SkillLv(SkillData.AgileArms);
        mods.Add(new(StatKey.STR, StatValueType.FLAT, _skills[SkillData.PhysicalTraining].Str * training, StatSource.Skill));
        mods.Add(new(StatKey.DEX, StatValueType.FLAT, _skills[SkillData.PhysicalTraining].Dex * training, StatSource.Skill));
        mods.Add(new(StatKey.STR, StatValueType.FLAT, _skills[SkillData.AgileArms].Str * agile, StatSource.Skill));

        mods.Add(_skills[SkillData.WeaponMastery].FinalDamageModifier(SkillLv(SkillData.WeaponMastery))); // WZ: "Final Damage": its own zone
        mods.Add(new(StatKey.CRIT_RATE, StatValueType.FLAT,
            _skills[SkillData.WeaponMastery].CritPct * SkillLv(SkillData.WeaponMastery)
            + _skills[SkillData.ChanceAttack].CritPct * SkillLv(SkillData.ChanceAttack), StatSource.Skill));

        // Iron Body is a buff while it runs: its DEF is its own Buff record. Its HP% and Warrior Mastery's HP
        // are always on and come from StatTables.Vitals (WZ numbers), not from the skill data.
        bool ironBody = _fighter.Buffs.ContainsKey(SkillData.IronBody);
        mods.Add(new(StatKey.MAX_HP, StatValueType.PERCENT,
            _skills[SkillData.Endure].HpPct * SkillLv(SkillData.Endure), StatSource.Skill));

        mods.Add(new(StatKey.DEF, StatValueType.FLAT,
            ironBody ? _skills[SkillData.IronBody].DefPct * SkillLv(SkillData.IronBody) : 0, StatSource.Buff));
        mods.Add(new(StatKey.DEF, StatValueType.FLAT,
            _skills[SkillData.GuardianArmor].DefPct * SkillLv(SkillData.GuardianArmor)
            + _skills[SkillData.Endure].DefPct * SkillLv(SkillData.Endure), StatSource.Skill));

        int movePct = _skills[SkillData.WarriorMastery].MovePct * SkillLv(SkillData.WarriorMastery);
        mods.Add(new(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, StatSource.Base));
        mods.Add(new(StatKey.SPEED, StatValueType.PERCENT, movePct, StatSource.Skill));
        mods.Add(new(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0, StatSource.Base));
        mods.Add(new(StatKey.JUMP, StatValueType.PERCENT, movePct, StatSource.Skill));

        if (_fighter.Buffs.ContainsKey(SkillData.SpiritBlade))
            mods.Add(new(StatKey.ATT_PCT, StatValueType.PERCENT,
                _skills[SkillData.SpiritBlade].AtkPct * SkillLv(SkillData.SpiritBlade), StatSource.Buff));

        // The stat part of Defense reads the final STR, DEX and LUK, so it is added after a first pass.
        var total = StatAggregator.Aggregate(mods);
        mods.Add(new(StatKey.DEF, StatValueType.FLAT,
            StatTables.BaseDefense(total.IntValue(StatKey.STR), total.IntValue(StatKey.DEX), total.IntValue(StatKey.LUK)), StatSource.Base));
        _stats = StatAggregator.Aggregate(mods);
    }

    private WeaponType _weaponType = WeaponType.Default;

    private WeaponType DetectWeaponType() => WornWeapon(_save?.Slots[_slot]) is { } weapon
        ? StatTables.WeaponTypeOf(weapon.ItemId)
        : WeaponType.Default;

    // The worn real weapon (a cash weapon is only a look), or null.
    private ItemData? WornWeapon(CharaSlot? chara)
    {
        ItemData? weapon = null;
        if (chara?.Equips == null) return null;
        foreach (int id in chara.Equips)
            if (_items.TryGetValue(id, out var item) && item.Slot == "Wp" && !item.Cash)
                weapon = item;
        return weapon;
    }

    // The current class and weapon profile. A saved character with no
    // recognised weapon falls back to the job's table row.
    private JobProfile CurrentJob()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return new JobProfile(StatKey.STR, StatKey.DEX, WeaponType.Default);
        var job = StatTables.Job(chara.Job);
        if (_weaponType == WeaponType.Default) return job;
        var weapon = _weaponType;
        if (job.Weapon == WeaponType.HeroTwoHand && weapon == WeaponType.TwoHandSword)
            weapon = WeaponType.HeroTwoHand;
        return job with { Weapon = weapon };
    }

    // The Build that goes to the local/server judge. All values come from the
    // one snapshot. CLI has no character, so it always uses Beginner and
    // CurrentJob() is Default there.
    private Build CurrentBuild()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return Progression.Beginner;
        int finalAttack = _skills[SkillData.FinalAttack].ProcPct * SkillLv(SkillData.FinalAttack);
        var job = CurrentJob(); // the class picks the main and sub stat and physical or magic attack
        bool mage = chara.Job == StatTables.FirePoisonMage;
        var mpEater = mage ? WzSkillStats.MpEater(_wzSkills) : default;
        var drain = mage ? WzSkillStats.ElementalDrain(_wzSkills) : default;
        // Empirical Knowledge is a permanent default passive (every job), so its chance applies whenever the skill table has it.
        int markProp = _wzSkills.ContainsKey(WzSkillData.EmpiricalKnowledge) ? WzSkillStats.EmpiricalKnowledgeProp(_wzSkills) : 0;
        return new Build(
            _stats.IntValue(job.Main),
            _stats.IntValue(job.Sub),
            DamageChain.TotalAttack(_stats, job.Main == StatKey.INT),
            (int)_stats.IntValue(StatKey.DAMAGE_PCT),
            (int)_stats.IntValue(StatKey.CRIT_RATE),
            finalAttack,
            (int)_stats.IntValue(StatKey.BOSS_PCT),
            (int)_stats.IntValue(StatKey.NORMAL_PCT),
            (int)Math.Round((DamageChain.FinalDamageMultiplier(_stats) - 1) * 100),
            (int)_stats.IntValue(StatKey.CRIT_DAMAGE),
            (int)Math.Round((1 - DamageChain.IedMultiplier(_stats)) * 100),
            chara.Level,
            (int)job.Weapon,
            mage ? WzSkillStats.WeaknessProp(_wzSkills) : 0, // Elemental Weakness: the Fire/Poison Mage marks mobs
            (int)_stats.IntValue(StatKey.MASTERY),
            mpEater.prop, mpEater.pct, mpEater.bossPct, // MP Eater
            drain.pct, drain.max, // Elemental Drain
            markProp, // Empirical Knowledge
            (int)_stats.IntValue(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT), // Elemental Decrease, Insight
            (int)_stats.IntValue(StatKey.MAGIC_DAMAGE_PCT), // Element Amplification
            (int)_stats.IntValue(StatKey.DOT_TIME_PCT), // Burning Magic
            (int)_stats.IntValue(StatKey.DOT_FINAL_PCT));
    }

    private void ClampPoolsToMax()
    {
        if (_save?.Slots[_slot] is CharaSlot chara) Progression.ClampPools(chara, MaxHpNow(), MaxMpNow());
    }

    private long MaxHpNow() => _stats.IntValue(StatKey.MAX_HP);
    private long MaxMpNow() => _stats.IntValue(StatKey.MAX_MP);
    private double OwnDef() => _stats.Value(StatKey.DEF);
    private int OwnKnockbackResistance() => (int)_stats.IntValue(StatKey.KNOCKBACK_RESISTANCE);
    private float OwnWalk() => (float)_stats.DoubleValue(StatKey.SPEED);
    private float OwnJump() => (float)_stats.DoubleValue(StatKey.JUMP);
    private bool HasWeapon() => _hasWeapon;

    private void PushBuild()
    {
        RefreshStats(); // buff edges change the snapshot outside the frame start
        _net?.SendBuild(CurrentBuild());
    }
}
