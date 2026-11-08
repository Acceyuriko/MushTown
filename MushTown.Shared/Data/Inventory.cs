namespace MushTown.Shared;

// Bag slots: the dictionaries keep itemId -> count, the order lists keep
// one item id per visible cell. Both stay in step through this class.
public static class Inventory
{
    public const int Capacity = 128;

    private static readonly string[] Tabs = { "use", "equip", "cash", "dec" };

    // Starts a new character's bags and order lists.
    public static void Ensure(CharaSlot chara)
    {
        foreach (string tab in Tabs) EnsureTab(chara, tab);
    }

    // A new character gets all four free coupons once: the current gender's
    // pair goes on the doll, the other pair goes to the decoration bag.
    public static void GiveCashDefaults(CharaSlot chara)
    {
        int hairCoupon = chara.Female ? ItemData.HairCouponFemale : ItemData.HairCouponMale;
        int faceCoupon = chara.Female ? ItemData.FaceCouponFemale : ItemData.FaceCouponMale;
        int otherHair = chara.Female ? ItemData.HairCouponMale : ItemData.HairCouponFemale;
        int otherFace = chara.Female ? ItemData.FaceCouponMale : ItemData.FaceCouponFemale;
        chara.CashWorn = new List<int> { hairCoupon, faceCoupon };
        chara.DecBag = new Dictionary<int, int> { [otherHair] = 1, [otherFace] = 1 };
        chara.Hair = chara.Female ? ItemData.DefaultHairFemale : ItemData.DefaultHairMale;
        chara.Face = chara.Female ? ItemData.DefaultFaceFemale : ItemData.DefaultFaceMale;
    }

    private static Dictionary<int, int> Bag(CharaSlot chara, string tab) => tab switch
    {
        "use" => chara.Use ??= new Dictionary<int, int>(),
        "equip" => chara.EquipBag ??= new Dictionary<int, int>(),
        "cash" => chara.CashBag ??= new Dictionary<int, int>(),
        "dec" => chara.DecBag ??= new Dictionary<int, int>(),
        _ => throw new ArgumentOutOfRangeException(nameof(tab)),
    };

    private static List<int> Order(CharaSlot chara, string tab) => tab switch
    {
        "use" => chara.UseOrder ??= new List<int>(),
        "equip" => chara.EquipOrder ??= new List<int>(),
        "cash" => chara.CashOrder ??= new List<int>(),
        "dec" => chara.DecOrder ??= new List<int>(),
        _ => throw new ArgumentOutOfRangeException(nameof(tab)),
    };

    public static int SlotId(CharaSlot chara, string tab, int cell) => Order(chara, tab)[cell];

    public static int Count(CharaSlot chara, string tab, int id) =>
        Bag(chara, tab).GetValueOrDefault(id);

    // Occupied cells of one tab, for the used/total line.
    public static int Used(CharaSlot chara, string tab) =>
        Order(chara, tab).Count(id => id > 0);

    // Adds a count to one bag. False when the tab has no room; a stackable
    // item already in the bag takes no new cell.
    public static bool Add(CharaSlot chara, string tab, int id, int count)
    {
        EnsureTab(chara, tab);
        var bag = Bag(chara, tab);
        bool stack = tab != "equip";
        int cells = stack ? (bag.GetValueOrDefault(id) > 0 ? 0 : 1) : count;
        if (Used(chara, tab) + cells > Capacity) return false;
        bag[id] = bag.GetValueOrDefault(id) + count;
        var order = Order(chara, tab);
        for (int n = 0; n < cells; n++)
            order[order.IndexOf(0)] = id;
        return true;
    }

    // Checks room before a caller charges money or takes a drop.
    public static bool CanAdd(CharaSlot chara, string tab, int id, int count)
    {
        EnsureTab(chara, tab);
        bool stack = tab != "equip";
        int cells = stack ? (Bag(chara, tab).GetValueOrDefault(id) > 0 ? 0 : 1) : count;
        return Used(chara, tab) + cells <= Capacity;
    }

    // Checks a whole reward list in one pass: distinct new stack ids need one
    // cell each, equipment copies need one cell each.
    public static bool CanAddAll(CharaSlot chara, string tab, IReadOnlyList<(int Id, int Count)> items)
    {
        EnsureTab(chara, tab);
        var bag = Bag(chara, tab);
        int cells = tab == "equip"
            ? items.Sum(i => i.Count)
            : items.Where(i => bag.GetValueOrDefault(i.Id) <= 0).Select(i => i.Id).Distinct().Count();
        return Used(chara, tab) + cells <= Capacity;
    }

    // Empty target moves, same id joins, different id swaps the two cells.
    // Equipment copies never join: each copy has its own cell.
    public static bool Move(CharaSlot chara, string tab, int from, int to)
    {
        var order = Order(chara, tab);
        if (from == to) return false;
        int fromId = order[from];
        int toId = order[to];
        if (fromId == 0) return false;
        if (toId == 0)
        {
            order[to] = fromId;
            order[from] = 0;
        }
        else if (toId == fromId)
        {
            if (tab == "equip") return false;
            order[from] = 0;
        }
        else
        {
            order[from] = toId;
            order[to] = fromId;
        }
        return true;
    }

    // Sort the current tab by type, then item id, from the first cell.
    // A stackable item fills one cell; equipment fills one cell per copy.
    public static void Sort(CharaSlot chara, string tab, IReadOnlyDictionary<int, ItemData> defs)
    {
        EnsureTab(chara, tab);
        var bag = Bag(chara, tab);
        var order = Order(chara, tab);
        var ids = new List<int>();
        foreach (var (id, count) in bag)
        {
            int cells = tab == "equip" ? count : 1;
            for (int n = 0; n < cells && ids.Count < Capacity; n++) ids.Add(id);
        }
        ids.Sort((a, b) =>
        {
            int ta = TypeRank(defs, a);
            int tb = TypeRank(defs, b);
            return ta != tb ? ta.CompareTo(tb) : a.CompareTo(b);
        });
        for (int i = 0; i < Capacity; i++) order[i] = i < ids.Count ? ids[i] : 0;
    }

    // Remove a count from one bag; a stackable cell goes at zero, and an
    // equipment copy gives up its own cell.
    public static bool Remove(CharaSlot chara, string tab, int id, int count)
    {
        EnsureTab(chara, tab);
        var bag = Bag(chara, tab);
        if (!bag.TryGetValue(id, out int have) || have < count) return false;
        if (have == count) bag.Remove(id);
        else bag[id] = have - count;
        var order = Order(chara, tab);
        if (!bag.ContainsKey(id))
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i] == id) order[i] = 0;
        }
        else if (tab == "equip")
        {
            int left = count;
            for (int i = order.Count - 1; i >= 0 && left > 0; i--)
                if (order[i] == id) { order[i] = 0; left--; }
        }
        return true;
    }

    private static int TypeRank(IReadOnlyDictionary<int, ItemData> defs, int id)
    {
        if (!defs.TryGetValue(id, out var data)) return 0;
        return data.Slot switch
        {
            "Wp" => 0,
            "Ma" => 1,
            _ => 2,
        };
    }

    // Pad the order to 128 cells and give every bag item its cell. A tab
    // that already has the cells keeps them.
    private static void EnsureTab(CharaSlot chara, string tab)
    {
        var order = Order(chara, tab);
        var bag = Bag(chara, tab);
        while (order.Count < Capacity) order.Add(0);
        if (order.Count > Capacity) order.RemoveRange(Capacity, order.Count - Capacity);
        for (int i = 0; i < order.Count; i++)
            if (order[i] > 0 && bag.GetValueOrDefault(order[i]) <= 0) order[i] = 0;
        bool stack = tab != "equip";
        var placed = new Dictionary<int, int>();
        foreach (int id in order)
            if (id > 0) placed[id] = placed.GetValueOrDefault(id) + 1;
        foreach (var (id, count) in bag)
        {
            if (count <= 0) continue;
            int need = (stack ? 1 : count) - placed.GetValueOrDefault(id);
            while (need-- > 0)
            {
                int empty = order.IndexOf(0);
                if (empty < 0) break;
                order[empty] = id;
            }
        }
    }

}
