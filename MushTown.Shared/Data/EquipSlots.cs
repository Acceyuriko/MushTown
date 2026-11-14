namespace MushTown.Shared;

// The items the equipment window draws: the worn gear on the Equipment page
// and the worn cash items on the Outfits page. The slot name picks the box.
public static class EquipSlots
{
    // The window boxes one gear WZ slot fills. Add a row here and its label
    // art; DecoBoxes reuses this table and only adds its own hair/face/skin
    // boxes. The codes are the WZ islot values: "Cp" hat, "Gv" gloves, "Sr"
    // cape, "Si" shield, "Af" face accessory, "Ay" eye accessory, "Ae"
    // earring, "Ri" ring, "Pe" pendant, "Be" belt, "Sh" shoulder, "Me" medal,
    // "Po" pocket, "Ba" badge. "Ma" is a top, "Pn" is pants, "MaPn" is an
    // overall: it shows in Top and locks Bottom.
    private static readonly Dictionary<string, string[]> Boxes = new()
    {
        ["Wp"] = new[] { "weapon" },
        ["Ma"] = new[] { "top" },
        ["MaPn"] = new[] { "top" },
        ["Pn"] = new[] { "bottom" },
        ["So"] = new[] { "shoes" },
        ["Cp"] = new[] { "hat" },
        ["Gv"] = new[] { "gloves" },
        ["Sr"] = new[] { "cape" },
        ["Si"] = new[] { "secondary" },
        ["Af"] = new[] { "face_acc" },
        ["Ay"] = new[] { "eye_acc" },
        ["Ae"] = new[] { "earrings" },
        ["Ri"] = new[] { "ring1" },
        ["Pe"] = new[] { "pendant1" },
        ["Be"] = new[] { "belt" },
        ["Sh"] = new[] { "shoulder" },
        ["Me"] = new[] { "medal" },
        ["Po"] = new[] { "pocket" },
        ["Ba"] = new[] { "badge" },
    };

    // The one slot table: item validation and the exporter use these keys.
    // Equipment-window display does not hardcode Wp/Ma; attack and nametag
    // logic still have their own special cases.
    public static IReadOnlySet<string> KnownSlots { get; } = new HashSet<string>(Boxes.Keys);

    // A gear item can be dropped on this window box. An overall fills Top
    // only, so it cannot be dropped on Bottom.
    public static bool Accepts(string box, int id, IReadOnlyDictionary<int, ItemData> defs) =>
        defs.TryGetValue(id, out var data)
        && data.Slot != null
        && Boxes.TryGetValue(data.Slot, out var boxes)
        && Array.IndexOf(boxes, box) >= 0;

    // The worn gear: every known slot fills its box; an overall fills Top and
    // locks Bottom so no pants can be worn under it.
    public static List<(string Slot, int Id)> Worn(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        var list = new List<(string, int)>();
        foreach (int id in chara.Equips ?? new List<int>())
            Add(list, chara, id, defs, cash: false);
        return list;
    }

    // The worn cash items: the current hair and face coupons fill Hair and
    // Face, a weapon fills Weapon, an overall fills Top and locks Bottom.
    public static List<(string Slot, int Id)> Cash(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        var list = new List<(string, int)>();
        foreach (int id in chara.CashWorn ?? new List<int>())
            Add(list, chara, id, defs, cash: true);
        return list;
    }

    // An overall leaves the Bottom box unusable: the window draws it red.
    public static bool BottomLocked(List<int>? worn, IReadOnlyDictionary<int, ItemData> defs)
    {
        if (worn == null) return false;
        foreach (int id in worn)
            if (defs.TryGetValue(id, out var data) && data.Slot == "MaPn")
                return true;
        return false;
    }

    private static void Add(List<(string, int)> list, CharaSlot chara, int id, IReadOnlyDictionary<int, ItemData> defs, bool cash)
    {
        if (!defs.TryGetValue(id, out var data)) return; // a save may carry an item this build does not export
        if (cash && data.Cosmetic != 0)
        {
            if (data.Cosmetic == chara.Hair) list.Add(("hair", id));
            else if (data.Cosmetic == chara.Face) list.Add(("face", id));
            return;
        }
        var slot = data.Slot;
        if (slot == null || !Boxes.TryGetValue(slot, out var boxes)) return;
        foreach (string box in boxes) list.Add((box, id));
    }
}
