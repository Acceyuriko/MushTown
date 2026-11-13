using MushTown.Shared;

namespace MushTown.Client;

// The client's only character-stat collection point. Every source becomes a
// StatModifier; the Shared aggregator turns those records into one snapshot
// per frame, and the stat panel, movement and damage all read that snapshot.
public partial class MushTownGame
{
    private StatSnapshot _stats = StatSnapshot.Empty;

    // CLI sessions have no saved character: the old beginner values, but
    // movement must still work (`--host` / `--join` never load a save).
    public static StatSnapshot BeginnerSnapshot() => StatAggregator.Aggregate(new[]
    {
        new StatModifier(StatKey.STR, StatValueType.FLAT, 4, "beginner"),
        new StatModifier(StatKey.DEX, StatValueType.FLAT, 4, "beginner"),
        new StatModifier(StatKey.INT, StatValueType.FLAT, 4, "beginner"),
        new StatModifier(StatKey.LUK, StatValueType.FLAT, 4, "beginner"),
        new StatModifier(StatKey.MAX_HP, StatValueType.FLAT, Progression.MaxHp(1), "beginner"),
        new StatModifier(StatKey.MAX_MP, StatValueType.FLAT, Progression.MaxMp(1), "beginner"),
        new StatModifier(StatKey.ATT, StatValueType.FLAT, Progression.WeaponAtk, "beginner"),
        new StatModifier(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, "base"),
        new StatModifier(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0, "base"),
    });

    private void RefreshStats()
    {
        _weaponType = DetectWeaponType();
        var mods = new List<StatModifier>();
        var chara = _save?.Slots[_slot];
        if (chara == null)
        {
            _stats = BeginnerSnapshot();
            return;
        }

        mods.Add(new(StatKey.STR, StatValueType.FLAT, chara.Str, "character"));
        mods.Add(new(StatKey.DEX, StatValueType.FLAT, chara.Dex, "character"));
        mods.Add(new(StatKey.INT, StatValueType.FLAT, chara.Int, "character"));
        mods.Add(new(StatKey.LUK, StatValueType.FLAT, chara.Luk, "character"));
        mods.Add(new(StatKey.MAX_HP, StatValueType.FLAT, Progression.MaxHp(chara.Level), "level"));
        mods.Add(new(StatKey.MAX_MP, StatValueType.FLAT, Progression.MaxMp(chara.Level), "level"));

        var worn = ItemData.EquipBonus(chara.Equips, _items);
        if (worn.weapon > 0)
            mods.Add(new(StatKey.ATT, StatValueType.FLAT,
                worn.weapon + _skills[SkillData.GuardianArmor].Atk * SkillLv(SkillData.GuardianArmor), "equipment"));
        mods.Add(new(StatKey.DEF, StatValueType.FLAT, worn.def, "equipment"));
        mods.Add(new(StatKey.STR, StatValueType.FLAT, worn.str, "equipment"));
        mods.Add(new(StatKey.DEX, StatValueType.FLAT, worn.dex, "equipment"));

        int training = SkillLv(SkillData.PhysicalTraining);
        int agile = SkillLv(SkillData.AgileArms);
        mods.Add(new(StatKey.STR, StatValueType.FLAT, _skills[SkillData.PhysicalTraining].Str * training, "skill"));
        mods.Add(new(StatKey.DEX, StatValueType.FLAT, _skills[SkillData.PhysicalTraining].Dex * training, "skill"));
        mods.Add(new(StatKey.STR, StatValueType.FLAT, _skills[SkillData.AgileArms].Str * agile, "skill"));

        mods.Add(new(StatKey.DAMAGE_PCT, StatValueType.FLAT,
            _skills[SkillData.WeaponMastery].DmgPct * SkillLv(SkillData.WeaponMastery), "skill"));
        mods.Add(new(StatKey.CRIT_RATE, StatValueType.FLAT,
            _skills[SkillData.WeaponMastery].CritPct * SkillLv(SkillData.WeaponMastery)
            + _skills[SkillData.ChanceAttack].CritPct * SkillLv(SkillData.ChanceAttack), "skill"));

        int hpPct =
            (_fighter.Buffs.ContainsKey(SkillData.IronBody) ? _skills[SkillData.IronBody].HpPct * SkillLv(SkillData.IronBody) : 0)
            + _skills[SkillData.WarriorMastery].HpPct * SkillLv(SkillData.WarriorMastery)
            + _skills[SkillData.Endure].HpPct * SkillLv(SkillData.Endure);
        if (hpPct != 0)
            mods.Add(new(StatKey.MAX_HP, StatValueType.PERCENT, hpPct, "skill/buff"));

        int defPct =
            (_fighter.Buffs.ContainsKey(SkillData.IronBody) ? _skills[SkillData.IronBody].DefPct * SkillLv(SkillData.IronBody) : 0)
            + _skills[SkillData.GuardianArmor].DefPct * SkillLv(SkillData.GuardianArmor)
            + _skills[SkillData.Endure].DefPct * SkillLv(SkillData.Endure);
        if (defPct != 0)
            mods.Add(new(StatKey.DEF, StatValueType.FLAT, defPct, "skill"));

        int movePct = _skills[SkillData.WarriorMastery].MovePct * SkillLv(SkillData.WarriorMastery);
        mods.Add(new(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, "base"));
        mods.Add(new(StatKey.SPEED, StatValueType.PERCENT, movePct, "skill"));
        mods.Add(new(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0, "base"));
        mods.Add(new(StatKey.JUMP, StatValueType.PERCENT, movePct, "skill"));

        if (_fighter.Buffs.ContainsKey(SkillData.SpiritBlade))
            mods.Add(new(StatKey.ATT_PCT, StatValueType.PERCENT,
                _skills[SkillData.SpiritBlade].AtkPct * SkillLv(SkillData.SpiritBlade), "buff"));

        _stats = StatAggregator.Aggregate(mods);
    }

    private WeaponType _weaponType = WeaponType.Default;

    private WeaponType DetectWeaponType()
    {
        var chara = _save?.Slots[_slot];
        if (chara?.Equips == null) return WeaponType.Default;
        var type = WeaponType.Default;
        foreach (int id in chara.Equips)
            if (_items.TryGetValue(id, out var item) && item.Slot == "Wp" && !item.Cash)
                type = StatTables.WeaponTypeOf(id);
        return type;
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
        return new Build(
            _stats.IntValue(StatKey.STR),
            _stats.IntValue(StatKey.DEX),
            DamageChain.TotalAttack(_stats),
            (int)_stats.IntValue(StatKey.DAMAGE_PCT),
            (int)_stats.IntValue(StatKey.CRIT_RATE),
            finalAttack,
            (int)_stats.IntValue(StatKey.BOSS_PCT),
            (int)_stats.IntValue(StatKey.NORMAL_PCT),
            (int)Math.Round((DamageChain.FinalDamageMultiplier(_stats) - 1) * 100),
            (int)_stats.IntValue(StatKey.CRIT_DAMAGE),
            (int)Math.Round((1 - DamageChain.IedMultiplier(_stats)) * 100),
            chara.Level,
            (int)CurrentJob().Weapon);
    }

    private long MaxHpNow() => _stats.IntValue(StatKey.MAX_HP);
    private long MaxMpNow() => _stats.IntValue(StatKey.MAX_MP);
    private int OwnDef() => (int)_stats.IntValue(StatKey.DEF);
    private float OwnWalk() => (float)_stats.DoubleValue(StatKey.SPEED);
    private float OwnJump() => (float)_stats.DoubleValue(StatKey.JUMP);
    private bool HasWeapon() => _stats.IntValue(StatKey.ATT) > 0;

    private void PushBuild()
    {
        RefreshStats(); // buff edges change the snapshot outside the frame start
        _net?.SendBuild(CurrentBuild());
    }
}
