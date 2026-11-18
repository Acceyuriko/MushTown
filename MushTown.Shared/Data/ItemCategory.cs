namespace MushTown.Shared;

// The "Category" line of an equipment tooltip: the weapon group by the id
// (WZ has no text for it), or the slot name of every other gear.
public static class ItemCategory
{
    private static readonly Dictionary<int, string> Weapons = new()
    {
        [130] = "One-Handed Sword",
        [137] = "Wand",
        [138] = "Staff (One-Handed Weapon)",
        [144] = "Pole Arm (Two-Handed Weapon)",
        [170] = "Cash Weapon",
    };

    // The slot names: the set tooltip prints them in brackets too.
    public static readonly Dictionary<string, string> Slots = new()
    {
        ["Wp"] = "Weapon",
        ["Cp"] = "Hat",
        ["Ma"] = "Top",
        ["MaPn"] = "Overall",
        ["Pn"] = "Bottom",
        ["So"] = "Shoes",
        ["Gv"] = "Gloves",
        ["Sr"] = "Cape",
        ["Si"] = "Shield",
        ["Af"] = "Face Accessory",
        ["Ay"] = "Eye Decoration",
        ["Ae"] = "Earrings",
        ["Ri"] = "Ring",
        ["Pe"] = "Pendant",
        ["Be"] = "Belt",
        ["Sh"] = "Shoulder Accessory",
        ["Me"] = "Medal",
        ["Po"] = "Pocket Item",
        ["Ba"] = "Badge",
    };

    // The attack speed words of a weapon. The numbers are the WZ attackSpeed;
    // the words are not in the WZ (the client string table is not shipped),
    // they follow the English client list. A speed not listed throws.
    private static readonly Dictionary<int, string> Speeds = new()
    {
        [2] = "Fastest",
        [3] = "Faster",
        [4] = "Fast",
        [5] = "Fast",
        [6] = "Normal",
        [7] = "Slow",
        [8] = "Slow",
        [9] = "Slower",
    };

    public static string Speed(int attackSpeed) => $"{Speeds[attackSpeed]} ({attackSpeed})";

    // Null when the id or slot is not known (the exporter then refuses the item).
    public static string? Of(int id, string slot) =>
        slot == "Wp" ? Weapons.GetValueOrDefault(id / 10000) : Slots.GetValueOrDefault(slot);
}
