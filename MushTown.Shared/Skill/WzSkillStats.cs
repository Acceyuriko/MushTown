namespace MushTown.Shared;

// The always-on stat records of the Fire/Poison skills that only add numbers, read from the WZ table
// at max level (a skill always stands at max level). Magic Armor adds DEF; MP Boost adds max MP (a rate
// and a part per character level), one attack speed stage (WZ actionSpeed -1 = stage +1) and the crit
// rate of the worn weapon (WZ psdWT: a wand gives cr 5, the staff has no entry). Teleport adds speed: WZ psdSpeed
// is percent points of the base speed (like the equipment incSpeed, a PERCENT record on SPEED) and speedMax is
// percent points added to the SPEED cap (140% base). Magic Guard and
// Elemental Weakness add no stat: the hurt path and the hit judge read their numbers from here.
// The 2nd job passives (210.img): High Wisdom adds INT (WZ intX); Agile Magic adds INT (intX) and attack speed (WZ
// psdWeaponBooster actionSpeed -2 = stage +2, exported as boosterActionSpeed); Spell Mastery adds magic attack (x), crit rate
// (cr) and mastery (WZ mastery, percent points on top of the weapon's own mastery: unverified, WZ does not say whether it adds
// or replaces). MP Eater and Elemental Drain add no stat: the hit judge reads their numbers (MpEater, ElementalDrain).
// The 3rd job passives (211.img): Magic Critical adds crit rate (cr) and crit damage (criticaldamage). Elemental Decrease adds final
// damage (mdR: a MULTIPLIER on FINAL_DAMAGE_PCT) and takes u percent points off the enemy's elemental resistance (WZ: "Enemy Elemental
// Resistance: -u%"; the same stat as ignore elemental resistance). Element Amplification adds MP cost (costmpR) and damage % (damR)
// to the magic skills: the hidden keys MP_COST_PCT and MAGIC_DAMAGE_PCT, read by SkillCost.Pay and by the Build. Burning Magic adds
// the DoT time (x) and the final damage on a hit against a mob with a DoT (z): the hidden keys DOT_TIME_PCT and DOT_FINAL_PCT, read by
// the Build. Teleport Boost adds distance to Teleport (TeleportReach).
public static class WzSkillStats
{
    // Magic Guard at max level: the share of a touch, in percent, that MP pays (WZ x = 15 + 7 * level).
    public static int MagicGuardPct(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var guard = skills[WzSkillData.MagicGuard];
        return (int)guard.Value("x", guard.MaxLevel);
    }

    // Elemental Weakness: the chance, in percent, that one of our hits marks the mob (WZ prop).
    public static int WeaknessProp(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var weak = skills[WzSkillData.ElementalWeakness];
        return (int)weak.Value("prop", weak.MaxLevel);
    }

    // Empirical Knowledge: the chance, in percent, that one of our hits marks the mob (WZ prop). The other numbers (stack cap,
    // time, damage % and DEF-ignored % per stack) are read by the server's MobWorld from the same skill node.
    public static int EmpiricalKnowledgeProp(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var mark = skills[WzSkillData.EmpiricalKnowledge];
        return (int)mark.Value("prop", mark.MaxLevel);
    }

    // MP Eater at max level: the chance (WZ prop), the share of the mob's max MP absorbed from a normal mob (WZ x) and from a boss
    // (WZ y), in percent; the amount absorbed goes to the caster.
    public static (int prop, int pct, int bossPct) MpEater(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var eater = skills[WzSkillData.MpEater];
        return ((int)eater.Value("prop", eater.MaxLevel), (int)eater.Value("x", eater.MaxLevel), (int)eater.Value("y", eater.MaxLevel));
    }

    // Elemental Drain at max level: the final damage percent for each damage-over-time effect on the mobs near the caster (WZ x) and the most
    // effects that count (WZ y). The WZ text counts nearby enemies with a DoT (range 800); MobWorld.DrainLayers counts the
    // DoT skills on the mobs near the caster (a circle of radius 800), one layer per distinct skill.
    public static (int pct, int max) ElementalDrain(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var drain = skills[WzSkillData.ElementalDrain];
        return ((int)drain.Value("x", drain.MaxLevel), (int)drain.Value("y", drain.MaxLevel));
    }

    // Meditation while its buff runs: magic attack (WZ indieMad = 10 + x) at max level, a Buff record. The buff lasts time seconds
    // (WZ time = 40 + 10 * x, read by the caster). The party part (lt/rb) is not used: the buff is for the caster only.
    public static StatModifier Meditation(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var meditation = skills[WzSkillData.Meditation];
        return new(StatKey.MATT, StatValueType.FLAT, meditation.Value("indieMad", meditation.MaxLevel), StatSource.Buff, meditation.Name);
    }

    // Teleport's horizontal and vertical distance at max level, with Teleport Boost's added (WZ x and y of both).
    public static (int horizontal, int vertical) TeleportReach(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var teleport = skills[WzSkillData.Teleport];
        var boost = skills[WzSkillData.TeleportBoost];
        return ((int)(teleport.Value("x", teleport.MaxLevel) + boost.Value("x", boost.MaxLevel)),
            (int)(teleport.Value("y", teleport.MaxLevel) + boost.Value("y", boost.MaxLevel)));
    }

    public static IEnumerable<StatModifier> Modifiers(IReadOnlyDictionary<int, WzSkillData> skills, int level, int weaponItemId)
    {
        var armor = skills[WzSkillData.MagicArmor];
        yield return new(StatKey.DEF, StatValueType.FLAT, armor.Value("pddX", armor.MaxLevel), StatSource.Skill, armor.Name);
        var boost = skills[WzSkillData.MpBoost];
        yield return new(StatKey.MAX_MP, StatValueType.FLAT, boost.Value("lv2mmp", boost.MaxLevel) * level, StatSource.Skill, boost.Name);
        yield return new(StatKey.MAX_MP, StatValueType.PERCENT, boost.Value("mmpR", boost.MaxLevel), StatSource.Skill, boost.Name);
        yield return new(StatKey.ATTACK_SPEED, StatValueType.FLAT, -boost.Value("actionSpeed", boost.MaxLevel), StatSource.Skill, boost.Name);
        var teleport = skills[WzSkillData.Teleport];
        yield return new(StatKey.SPEED, StatValueType.PERCENT, teleport.Value("psdSpeed", teleport.MaxLevel), StatSource.Skill, teleport.Name);
        yield return new(StatKey.SPEED_MAX, StatValueType.FLAT, teleport.Value("speedMax", teleport.MaxLevel), StatSource.Skill, teleport.Name);
        var wisdom = skills[WzSkillData.HighWisdom];
        yield return new(StatKey.INT, StatValueType.FLAT, wisdom.Value("intX", wisdom.MaxLevel), StatSource.Skill, wisdom.Name);
        var agile = skills[WzSkillData.AgileMagic];
        yield return new(StatKey.INT, StatValueType.FLAT, agile.Value("intX", agile.MaxLevel), StatSource.Skill, agile.Name);
        yield return new(StatKey.ATTACK_SPEED, StatValueType.FLAT, -agile.Value("boosterActionSpeed", agile.MaxLevel), StatSource.Skill, agile.Name);
        var spell = skills[WzSkillData.SpellMastery];
        yield return new(StatKey.MATT, StatValueType.FLAT, spell.Value("x", spell.MaxLevel), StatSource.Skill, spell.Name);
        yield return new(StatKey.CRIT_RATE, StatValueType.FLAT, spell.Value("cr", spell.MaxLevel), StatSource.Skill, spell.Name);
        yield return new(StatKey.MASTERY, StatValueType.FLAT, spell.Value("mastery", spell.MaxLevel), StatSource.Skill, spell.Name);
        var critical = skills[WzSkillData.MagicCritical];
        yield return new(StatKey.CRIT_RATE, StatValueType.FLAT, critical.Value("cr", critical.MaxLevel), StatSource.Skill, critical.Name);
        yield return new(StatKey.CRIT_DAMAGE, StatValueType.FLAT, critical.Value("criticaldamage", critical.MaxLevel), StatSource.Skill, critical.Name);
        var decrease = skills[WzSkillData.ElementalDecrease];
        yield return new(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.0 + decrease.Value("mdR", decrease.MaxLevel) / 100.0, StatSource.Skill, decrease.Name);
        yield return new(StatKey.IGNORE_ELEMENT_RESISTANCE_PCT, StatValueType.FLAT, decrease.Value("u", decrease.MaxLevel), StatSource.Skill, decrease.Name);
        var amplify = skills[WzSkillData.ElementAmplification];
        yield return new(StatKey.MP_COST_PCT, StatValueType.FLAT, amplify.Value("costmpR", amplify.MaxLevel), StatSource.Skill, amplify.Name);
        yield return new(StatKey.MAGIC_DAMAGE_PCT, StatValueType.FLAT, amplify.Value("damR", amplify.MaxLevel), StatSource.Skill, amplify.Name);
        var burning = skills[WzSkillData.BurningMagic];
        yield return new(StatKey.DOT_TIME_PCT, StatValueType.FLAT, burning.Value("x", burning.MaxLevel), StatSource.Skill, burning.Name);
        yield return new(StatKey.DOT_FINAL_PCT, StatValueType.FLAT, burning.Value("z", burning.MaxLevel), StatSource.Skill, burning.Name);
        if (boost.WeaponBonus.TryGetValue(weaponItemId / 10000 - 100, out var bonus))
            yield return new(StatKey.CRIT_RATE, StatValueType.FLAT, bonus["cr"][boost.MaxLevel - 1], StatSource.Skill, boost.Name);
    }
}
