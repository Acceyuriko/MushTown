using System.Globalization;

namespace MushTown.Shared;

// The effect line of the skill tooltip: the numbers of one skill at one level,
// read with the same column rules SkillData documents (every column but the
// buff time and the orb cap is per level).
public static class SkillText
{
    public static string Effect(SkillData skill, int lv)
    {
        var parts = new List<string>();
        void Add(string label, int value, string unit = "")
        {
            if (value != 0) parts.Add($"{label}: +{value}{unit}");
        }
        if (skill.Kind != SkillData.KindPassive)
        {
            parts.Add($"MP Cost: {skill.Mp}");
            parts.Add($"Cooldown: {Seconds(skill.CooldownMs)} sec");
        }
        if (skill.Kind == SkillData.KindActive)
        {
            parts.Add($"Damage: {Progression.SkillDamage(skill, lv)}%");
            parts.Add($"Max Targets: {skill.MobCount}");
        }
        if (skill.Kind == SkillData.KindBuff) parts.Add($"Duration: {Seconds(skill.BuffMs)} sec");
        Add("Attack", skill.AtkPct * lv, "%");
        Add("Max HP", skill.HpPct * lv, "%");
        Add("DEF", skill.DefPct * lv, "%");
        Add("Damage", skill.DmgPct * lv, "%");
        Add("Critical Rate", skill.CritPct * lv, "%");
        Add("Movement Speed and Jump", skill.MovePct * lv, "%");
        Add("STR", skill.Str * lv);
        Add("DEX", skill.Dex * lv);
        Add("Attack Power", skill.Atk * lv);
        Add("Orb Chance", (skill.OrbChance + skill.SynChance) * lv, "%");
        if (skill.OrbMax > 0) parts.Add($"Max Orbs: {skill.OrbMax}");
        Add("Damage per Orb", (skill.OrbDmg + skill.SynDmg) * lv, "%");
        Add("Final Attack Chance", skill.ProcPct * lv, "%");
        if (skill.RegenHp > 0) parts.Add($"HP Recovery: {skill.RegenHp * lv} per 10 sec");
        if (skill.RegenMp > 0) parts.Add($"MP Recovery: {skill.RegenMp * lv} per 10 sec");
        return string.Join(", ", parts);
    }

    private static string Seconds(int ms) => (ms / 1000.0).ToString("0.##", CultureInfo.InvariantCulture);
}
