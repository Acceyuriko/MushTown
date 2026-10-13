namespace MushTown.Shared;

// One skill. v1 numbers are feel-tuned (the client holds no skill stats),
// documented here so W12 reuses the same table shape:
//   Slash Blast: req 1, 5 MP, 3s CD, 150% damage, 6 mobs, 160px ring, 600ms lock.
//   War Leap: req 3, 3 MP, 5s CD, air-only, once per airtime (damage fields unused).
public sealed class SkillData
{
    public const int SlashBlast = 1001005;
    public const int WarLeap = 1001008;

    public int SkillId { get; set; }
    public string Name { get; set; } = "";
    public int ReqLevel { get; set; }
    public int Mp { get; set; }
    public int CooldownMs { get; set; }
    public int DamagePct { get; set; }
    public int MobCount { get; set; }
    public int Range { get; set; }
    public int CastLockMs { get; set; }
    public string Icon { get; set; } = "";
    public string Disabled { get; set; } = "";
    public List<string> Effect { get; set; } = new();
    public List<string> Hit { get; set; } = new();
}
