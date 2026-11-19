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

    // MP Eater at max level: the chance (WZ prop), the share of OUR max MP that a proc gives back (WZ x) and the share against a boss
    // (WZ y), in percent. Unverified: the WZ text says "x% of Max MP" and "y% of the boss's Max MP"; the mobs of this game have
    // no MP, so the share is taken of the caster's max MP.
    public static (int prop, int pct, int bossPct) MpEater(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var eater = skills[WzSkillData.MpEater];
        return ((int)eater.Value("prop", eater.MaxLevel), (int)eater.Value("x", eater.MaxLevel), (int)eater.Value("y", eater.MaxLevel));
    }

    // Elemental Drain at max level: the final damage percent for each damage-over-time effect on the target (WZ x) and the most
    // effects that count (WZ y). The WZ text counts nearby enemies with a DoT (range 800); here it counts the DoTs on the
    // target itself (the ticket's rule).
    public static (int pct, int max) ElementalDrain(IReadOnlyDictionary<int, WzSkillData> skills)
    {
        var drain = skills[WzSkillData.ElementalDrain];
        return ((int)drain.Value("x", drain.MaxLevel), (int)drain.Value("y", drain.MaxLevel));
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
        if (boost.WeaponBonus.TryGetValue(weaponItemId / 10000 - 100, out var bonus))
            yield return new(StatKey.CRIT_RATE, StatValueType.FLAT, bonus["cr"][boost.MaxLevel - 1], StatSource.Skill, boost.Name);
    }
}
