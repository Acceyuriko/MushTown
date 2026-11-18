namespace MushTown.Shared;

// One skill read straight from the WZ skill node (the Fire/Poison line): the numbers of every level
// (the common formulas, already worked out), the rectangles, the flags, and the tooltip text at max
// level. This is a table of its own: the warrior SkillData is a hand-made feel table and is not
// extended. The active skills are not cast yet; of the passives only Magic Armor and MP Boost take effect.
public sealed class WzSkillData
{
    // 200 Magician.
    public const int EnergyBolt = 2001008;
    public const int Teleport = 2001009;
    public const int ManaWave = 2001011;
    public const int MagicGuard = 2000002;
    public const int MagicArmor = 2000010;
    public const int MpBoost = 2000006;
    public const int ElementalWeakness = 2000007;

    public int SkillId { get; set; }
    public string Name { get; set; } = "";
    public int MaxLevel { get; set; }
    public int Kind { get; set; } // SkillData.KindActive / KindBuff / KindPassive
    public string ElemAttr { get; set; } = ""; // f = fire, s = poison, "" = none
    public bool Invisible { get; set; }        // not listed in the skill window
    public string Icon { get; set; } = "";
    public string Disabled { get; set; } = "";
    public List<string> Desc { get; set; } = new();   // String.wz desc
    public List<string> Effect { get; set; } = new(); // String.wz h, the numbers filled at max level
    public List<string> Actions { get; set; } = new(); // action/0.. names
    public Dictionary<string, string> Info { get; set; } = new(); // info/*: flags and types as written
    // Every number field of common: index 0 is level 1.
    public Dictionary<string, double[]> Common { get; set; } = new();
    // The vector fields of common (lt, rb): x, y.
    public Dictionary<string, int[]> Vectors { get; set; } = new();
    // The nodes this skill owns (the fire wall of Ignite, the blast of Creeping Toxin...): same table, no art.
    public List<WzSkillData> Children { get; set; } = new();

    public double Value(string field, int level) => Common[field][level - 1];
}
