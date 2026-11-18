namespace MushTown.Shared;

// The always-on stat records of the Fire/Poison skills that only add numbers, read from the WZ table
// at max level (a skill always stands at max level). Magic Armor adds DEF; MP Boost adds max MP (a rate
// and a part per character level), one attack speed stage (WZ actionSpeed -1 = stage +1) and the crit
// rate of the worn weapon (WZ psdWT: a wand gives cr 5, the staff has no entry).
public static class WzSkillStats
{
    public static IEnumerable<StatModifier> Modifiers(IReadOnlyDictionary<int, WzSkillData> skills, int level, int weaponItemId)
    {
        var armor = skills[WzSkillData.MagicArmor];
        yield return new(StatKey.DEF, StatValueType.FLAT, armor.Value("pddX", armor.MaxLevel), StatSource.Skill, armor.Name);
        var boost = skills[WzSkillData.MpBoost];
        yield return new(StatKey.MAX_MP, StatValueType.FLAT, boost.Value("lv2mmp", boost.MaxLevel) * level, StatSource.Skill, boost.Name);
        yield return new(StatKey.MAX_MP, StatValueType.PERCENT, boost.Value("mmpR", boost.MaxLevel), StatSource.Skill, boost.Name);
        yield return new(StatKey.ATTACK_SPEED, StatValueType.FLAT, -boost.Value("actionSpeed", boost.MaxLevel), StatSource.Skill, boost.Name);
        if (boost.WeaponBonus.TryGetValue(weaponItemId / 10000 - 100, out var bonus))
            yield return new(StatKey.CRIT_RATE, StatValueType.FLAT, bonus["cr"][boost.MaxLevel - 1], StatSource.Skill, boost.Name);
    }
}
