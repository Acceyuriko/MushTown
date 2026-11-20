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

    // The same for a WZ skill at the level lv (0 = not learned): mpCon of the level, and the cooltime
    // when the skill has one (Teleport's 480 ms: the user decided). A buff pays its MP only.
    // mpCostPct (Element Amplification, percent points) raises the MP of a damage skill only (a buff, Teleport and Mana Wave cost what
    // they cost); the raised cost is rounded down (unverified).
    public bool Pay(CharaSlot? chara, WzSkillData skill, int lv, IDictionary<int, float> cooldowns, int mpCostPct = 0)
    {
        if (chara == null || lv <= 0) return false;
        cooldowns.TryGetValue(skill.SkillId, out float left);
        int mp = (int)skill.Value("mpCon", lv);
        if (skill.Common.ContainsKey("damage")) mp = mp * (100 + mpCostPct) / 100;
        if (chara.Mp < mp || left > 0) return false;
        chara.Mp -= mp;
        if (skill.Kind != SkillData.KindBuff) cooldowns[skill.SkillId] = CooldownMs(skill, lv); // a shield (Elemental Adaptation) starts its cooldown when its layers run out (Fighter.TryBlockStatus)
        return true;
    }

    // WZ cooltime is seconds, Teleport's teleportCooltime is milliseconds; no cooldown is 0.
    public static float CooldownMs(WzSkillData skill, int lv) =>
        skill.Common.ContainsKey("cooltime") ? (float)skill.Value("cooltime", lv) * 1000f
        : skill.Common.ContainsKey("teleportCooltime") ? (float)skill.Value("teleportCooltime", lv) : 0f;
}

public sealed class SkillDamage : Component
{
    private readonly IReadOnlyDictionary<int, SkillData> _defs;
    public SkillDamage(IReadOnlyDictionary<int, SkillData> defs) { _defs = defs; } // dumb ctor
    // Cast-time damage pct: the skill's level curve plus the combo orbs' damage (each orb pays Combo Attack's rate at its level), then Combo
    // Synergy's FINAL damage (WZ: "Final Damage increases by damR% for each combo orb"): the orbs times its rate make one final damage %, which
    // multiplies the whole pct (a final damage zone multiplies the hit, and the skill percent multiplies the hit the same way; the cast message
    // carries a whole number, so the result is rounded to one).
    public int Pct(int skill, int lv, int orbs, int comboLv, int synLv)
    {
        int pct = Progression.SkillDamage(_defs[skill], lv);
        if (orbs > 0)
        {
            pct += orbs * _defs[SkillData.ComboAttack].OrbDmg * comboLv;
            pct = (int)Math.Round(pct * (1.0 + orbs * _defs[SkillData.ComboSynergy].SynDmg * synLv / 100.0));
        }
        return pct;
    }
}
