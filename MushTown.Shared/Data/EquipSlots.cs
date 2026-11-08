namespace MushTown.Shared;

// The items the equipment window draws: the worn gear on the Equipment page
// and the worn cash items on the Outfits page. The slot name picks the box.
public static class EquipSlots
{
    // The window boxes one gear WZ slot fills. Add a row for a new slot and
    // its box art; the window itself stays the same.
    private static readonly Dictionary<string, string[]> Boxes = new()
    {
        ["Wp"] = new[] { "weapon" },
        ["Ma"] = new[] { "top", "bottom" },
    };

    // The worn gear: a weapon fills Weapon, a longcoat fills Top and Bottom.
    public static List<(string Slot, int Id)> Worn(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        var list = new List<(string, int)>();
        foreach (int id in chara.Equips ?? new List<int>())
            Add(list, chara, id, defs, cash: false);
        return list;
    }

    // The worn cash items: the current hair and face coupons fill Hair and
    // Face, a weapon fills Weapon, a longcoat fills Top and Bottom.
    public static List<(string Slot, int Id)> Cash(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        var list = new List<(string, int)>();
        foreach (int id in chara.CashWorn ?? new List<int>())
            Add(list, chara, id, defs, cash: true);
        return list;
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
