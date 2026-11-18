namespace MushTown.Shared;

// One equipment set (assets/set/<id>.json, exported from Etc.wz SetItemInfo.img):
// its parts, and the effect lines each piece count adds. Only displayed so far.
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
