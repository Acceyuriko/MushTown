namespace MushTown.Shared;

// One equipment set (assets/set/<id>.json, exported from Etc.wz SetItemInfo.img):
// its parts, and the effect lines each piece count adds.
public sealed class SetData
{
    public int Id { get; set; }
    public string Name { get; set; } = ""; // WZ setItemName
    public List<SetPart> Parts { get; set; } = new();
    public List<SetTier> Tiers { get; set; } = new(); // by piece count, each with only its own lines
}

// One slot of the set. A part with several ids is a "choose one" part (the
// weapons of the 8th Magician Set): wearing any of them counts as one piece.
public sealed class SetPart
{
    public string Slot { get; set; } = ""; // WZ islot
    public string Label { get; set; } = ""; // the slot name the tooltip prints in brackets
    public string Name { get; set; } = ""; // the item name, or for a choose-one part the WZ Desc line
    public List<int> Ids { get; set; } = new();
}

public sealed class SetTier
{
    public int Count { get; set; } // pieces needed
    public List<SetEffect> Effects { get; set; } = new();
}

// One line: the WZ field name (incPDD, nbdR, ...) and its number. The option
// 60023 of the 5 and 6 piece tiers is read from ItemOption.img at export and
// stored as ignoreTargetDEF.
public sealed class SetEffect
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
}

public static class SetRules
{
    public static bool PartWorn(SetPart part, IReadOnlyCollection<int>? worn) =>
        worn != null && part.Ids.Any(worn.Contains);

    public static int WornCount(SetData set, IReadOnlyCollection<int>? worn) =>
        set.Parts.Count(p => PartWorn(p, worn));

    public static bool TierActive(SetData set, SetTier tier, IReadOnlyCollection<int>? worn) =>
        WornCount(set, worn) >= tier.Count;

    // The stat records of every set effect that is on: each tier whose piece count is reached adds its own lines
    // (the tiers add up). Wearing a piece off drops the count, so the tiers above it are gone on the next refresh.
    public static IEnumerable<StatModifier> Modifiers(IReadOnlyDictionary<int, SetData> sets, IReadOnlyCollection<int>? worn) =>
        sets.Values.SelectMany(set => set.Tiers
            .Where(tier => TierActive(set, tier, worn))
            .SelectMany(tier => tier.Effects.SelectMany(Records)));

    // incMDD, incACC and incEVA have no stat key: the official game removed those stats (research acc-eva-mdd),
    // so they stay tooltip text. Ignore defense is a multiplier: two 10% lines make 1 - 0.9 x 0.9.
    private static StatModifier[] Records(SetEffect e)
    {
        StatModifier Rec(StatKey key, StatValueType type, double value) => new(key, type, value, StatSource.Set);
        double v = e.Value;
        return e.Key switch
        {
            "incPAD" => new[] { Rec(StatKey.ATT, StatValueType.FLAT, v) },
            "incMAD" => new[] { Rec(StatKey.MATT, StatValueType.FLAT, v) },
            "incPDD" => new[] { Rec(StatKey.DEF, StatValueType.FLAT, v) },
            "incINT" => new[] { Rec(StatKey.INT, StatValueType.FLAT, v) },
            "incAllStat" => new[]
            {
                Rec(StatKey.STR, StatValueType.FLAT, v), Rec(StatKey.DEX, StatValueType.FLAT, v),
                Rec(StatKey.INT, StatValueType.FLAT, v), Rec(StatKey.LUK, StatValueType.FLAT, v),
            },
            "incMHPr" => new[] { Rec(StatKey.MAX_HP, StatValueType.PERCENT, v) },
            "incMMPr" => new[] { Rec(StatKey.MAX_MP, StatValueType.PERCENT, v) },
            "nbdR" => new[] { Rec(StatKey.NORMAL_PCT, StatValueType.FLAT, v) },
            "ignoreTargetDEF" => new[] { Rec(StatKey.IED_PCT, StatValueType.MULTIPLIER, 1.0 - v / 100.0) },
            "incMDD" or "incACC" or "incEVA" => Array.Empty<StatModifier>(),
            _ => throw new InvalidDataException($"set effect {e.Key} is not supported"), // the exporter refuses it too
        };
    }

    // The set of an item, or null when it belongs to none. An item whose set
    // is not in the table is a stale export.
    public static SetData? Of(ItemData item, IReadOnlyDictionary<int, SetData> sets) =>
        item.SetId == 0 ? null : sets[item.SetId];

    // The tooltip words of the WZ effect fields. A field not listed here is
    // not a supported set effect: the exporter and the tooltip both refuse it.
    private static readonly Dictionary<string, (string Label, bool Percent, bool Plus)> Fields = new()
    {
        ["incPAD"] = ("Attack", false, false),
        ["incMAD"] = ("Magic Attack", false, false),
        ["incPDD"] = ("Defense", false, false),
        ["incMDD"] = ("Magic Defense", false, false),
        ["incACC"] = ("Accuracy", false, false),
        ["incEVA"] = ("Avoidability", false, false),
        ["incINT"] = ("INT", false, false),
        ["incAllStat"] = ("All Stats", false, false),
        ["incMHPr"] = ("Max HP", true, false),
        ["incMMPr"] = ("Max MP", true, false),
        ["nbdR"] = ("Damage vs. Normal Monsters", true, true),
        ["ignoreTargetDEF"] = ("Ignore Enemy Defense", true, true),
    };

    public static bool IsKnown(string key) => Fields.ContainsKey(key);

    public static string Line(SetEffect e)
    {
        var (label, percent, plus) = Fields[e.Key];
        return $"{label} : {(plus ? "+" : "")}{e.Value}{(percent ? "%" : "")}";
    }
}
