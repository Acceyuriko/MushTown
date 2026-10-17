namespace MushTown.Shared;

// Live skill casting (probe P1): thin objects over the def table. Cost and
// damage read defs live by id; the math stays in Progression (tested there).
// Authority unchanged: net mode still sends the cast to the server, which
// judges with its own random; local mode runs its own world.
public sealed class SkillCost : Component
{
    private readonly IReadOnlyDictionary<int, SkillData> _defs;
    public SkillCost(IReadOnlyDictionary<int, SkillData> defs) { _defs = defs; } // dumb ctor
    public bool Pay(CharaSlot? chara, int skill, int lv, IDictionary<int, float> cooldowns)
    {
        if (chara == null) return false;
        var def = _defs[skill];
        cooldowns.TryGetValue(skill, out float left);
        if (!Progression.CanCast(chara, def, lv, left)) return false;
        chara.Mp -= def.Mp;
        cooldowns[skill] = def.CooldownMs;
        return true;
    }
}

public sealed class SkillDamage : Component
{
    private readonly IReadOnlyDictionary<int, SkillData> _defs;
    public SkillDamage(IReadOnlyDictionary<int, SkillData> defs) { _defs = defs; } // dumb ctor
    // Cast-time damage pct: the skill's level curve plus combo orbs (each orb
    // pays Combo Attack's rate plus Combo Synergy's, both at their levels).
    public int Pct(int skill, int lv, int orbs, int comboLv, int synLv)
    {
        int pct = Progression.SkillDamage(_defs[skill], lv);
        if (orbs > 0)
            pct += orbs * (_defs[SkillData.ComboAttack].OrbDmg * comboLv
                + _defs[SkillData.ComboSynergy].SynDmg * synLv);
        return pct;
    }
}
