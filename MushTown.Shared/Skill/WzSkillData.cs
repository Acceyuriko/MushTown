namespace MushTown.Shared;

// One skill read straight from the WZ skill node (the Fire/Poison line): the numbers of every level
// (the common formulas, already worked out), the rectangles, the flags, and the tooltip text at max
// level. This is a table of its own: the warrior SkillData is a hand-made feel table and is not
// extended. Energy Bolt is cast (the rectangle, the segments, the art); Magic Guard, Elemental Weakness,
// Magic Armor and MP Boost take effect; Teleport and Mana Wave are not cast yet.
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
    public List<string> CastFrames { get; set; } = new(); // the cast effect art (png paths), only for a damage skill
    public List<string> HitFrames { get; set; } = new();  // the hit spark art (hit/0), only for a damage skill
    public Dictionary<string, string> Info { get; set; } = new(); // info/*: flags and types as written
    // Every number field of common: index 0 is level 1.
    public Dictionary<string, double[]> Common { get; set; } = new();
    // The vector fields of common (lt, rb): x, y.
    public Dictionary<string, int[]> Vectors { get; set; } = new();
    // psdWT: the passive bonus by worn weapon. Key = the weapon code (the item id / 10000 - 100: a wand is
    // 137, code 37), value = the fields of that weapon's bonus (cr = crit rate), every level like Common.
    public Dictionary<int, Dictionary<string, double[]>> WeaponBonus { get; set; } = new();
    // The nodes this skill owns (the fire wall of Ignite, the blast of Creeping Toxin...): same table, no art.
    public List<WzSkillData> Children { get; set; } = new();

    public double Value(string field, int level) => Common[field][level - 1];

    // The element of the attack: "" is none (it hits like a physical attack), f fire, s poison. Any other
    // letter has no case yet (the next jobs add theirs): it fails loud.
    public AttackElement Element() => ElemAttr switch
    {
        "" => AttackElement.Physical,
        "f" => AttackElement.Fire,
        "s" => AttackElement.Poison,
        _ => throw new InvalidDataException($"skill {SkillId} has the element {ElemAttr}, not handled yet"),
    };

    // The WZ box lt..rb: is the point (dx, dy) from the box origin inside it? The box is written for a caster
    // that faces left (the raw art faces left), so a caster that faces right mirrors x. Edges count as inside.
    public bool InRect(float dx, float dy, bool facingRight)
    {
        if (facingRight) dx = -dx;
        var lt = Vectors["lt"];
        var rb = Vectors["rb"];
        return dx >= lt[0] && dx <= rb[0] && dy >= lt[1] && dy <= rb[1];
    }
}
