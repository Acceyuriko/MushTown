namespace MushTown.Shared;

// What one slot click did (the client maps this to sound + rebuild).
public enum HandAct
{
    None, // empty cell, mismatch, or a guarded drink: silent, nothing changed
    Moved, // pick, drop, or return: click
    Wore, // the worn set changed: click + doll rebuild
    Drank, // drank the held potion: silent (legacy drinks made no sound)
}

// One item in the hand (MapleStory pick-up/drop, not press-drag).
// Take-1 rule: every pick moves exactly one piece out of the bags,
// so Esc always has a lossless home. Roster model: there are no bag
// positions, so a drop merges into the pool no matter which cell was
// clicked; only wear needs the worn cell (swap lands back in hand).
public sealed class CarryHand
{
    public const double DoubleMs = 500; // two clicks inside this on the same cell = use
    public string? Kind; // null = empty hands; else use/bag/worn (matches Slot.ItemKind)
    public int Id;
    private string _lastKind = "";
    private int _lastId;
    private double _lastMs = -DoubleMs * 2;

    public bool Empty => Kind == null;

    // One slot click. hpPct feeds the shared drink guard (buffed max HP).
    public HandAct Click(CharaSlot chara, string cell, int id, IReadOnlyDictionary<int, ItemData> defs, int hpPct, double nowMs)
    {
        if (Kind == null)
        {
            if (cell == "use" && TakeUse(chara, id)) { Arm(cell, id, nowMs); Kind = "use"; Id = id; return HandAct.Moved; }
            if (cell == "bag" && TakeBag(chara, id)) { Arm(cell, id, nowMs); Kind = "bag"; Id = id; return HandAct.Moved; }
            if (cell == "worn" && chara.Equips?.Remove(id) == true) { Arm(cell, id, nowMs); Kind = "worn"; Id = id; return HandAct.Wore; }
            return HandAct.None;
        }
        if (cell == "use" && Kind == "use" && id == Id && cell == _lastKind && id == _lastId && nowMs - _lastMs <= DoubleMs)
        {
            PoolUse(chara, Id); // drink through the shared rule (full HP = silent no-op)
            Kind = null;
            Disarm(); // eaten: a third click starts over
            var item = defs[id];
            Progression.UsePotion(chara, id, item.Hp, item.Mp, hpPct);
            return HandAct.Drank;
        }
        if (Kind == "use")
        {
            if (cell != "use") { Disarm(); return HandAct.None; } // potions land on USE cells only; a click elsewhere breaks the double
            PoolUse(chara, Id);
            Kind = null;
            return HandAct.Moved;
        }
        if (cell == "worn") return WearHeld(chara, defs);
        if (cell == "bag")
        {
            PoolBag(chara, Id);
            Kind = null;
            return HandAct.Moved;
        }
        Disarm(); // a click elsewhere breaks the double
        return HandAct.None; // equips land on EQUIP cells only
    }

    // Esc, close, save: the held piece rejoins the bags. Worn-origin
    // pieces land in the bag too, never back on the doll (the slot
    // may hold someone else by now; the bag never says no).
    public void Return(CharaSlot chara)
    {
        if (Kind == "use") PoolUse(chara, Id);
        else if (Kind != null) PoolBag(chara, Id);
        Kind = null;
    }

    // Wears the held piece through the shared rule: stock it, wear it,
    // then lift the swapped piece back into the hand. Always lands
    // (the stock step feeds WearEquip exactly what it asks for).
    private HandAct WearHeld(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        int id = Id;
        var def = defs[id]; // loud on unknown (same rule as WearEquip)
        int old = -1;
        if (chara.Equips != null)
        {
            int at = chara.Equips.FindIndex(w => defs.TryGetValue(w, out var wd) && wd.Slot == def.Slot);
            if (at >= 0) old = chara.Equips[at];
        }
        chara.EquipBag ??= new Dictionary<int, int>();
        chara.EquipBag[id] = chara.EquipBag.GetValueOrDefault(id) + 1;
        Progression.WearEquip(chara, id, defs);
        if (old < 0) Kind = null;
        else
        {
            chara.EquipBag[old]--;
            if (chara.EquipBag[old] == 0) chara.EquipBag.Remove(old);
            Kind = "worn";
            Id = old;
        }
        return HandAct.Wore;
    }

    private void Arm(string cell, int id, double nowMs) { _lastKind = cell; _lastId = id; _lastMs = nowMs; }

    private void Disarm() { _lastMs = -DoubleMs * 2; }

    private static bool TakeUse(CharaSlot chara, int id)
    {
        var use = chara.Use;
        if (use == null || !use.TryGetValue(id, out int n) || n <= 0) return false;
        if (n == 1) use.Remove(id);
        else use[id] = n - 1;
        return true;
    }

    private static bool TakeBag(CharaSlot chara, int id)
    {
        var bag = chara.EquipBag;
        if (bag == null || !bag.TryGetValue(id, out int n) || n <= 0) return false;
        if (n == 1) bag.Remove(id);
        else bag[id] = n - 1;
        return true;
    }

    private static void PoolUse(CharaSlot chara, int id)
    {
        chara.Use ??= new Dictionary<int, int>();
        chara.Use[id] = chara.Use.GetValueOrDefault(id) + 1;
    }

    private static void PoolBag(CharaSlot chara, int id)
    {
        chara.EquipBag ??= new Dictionary<int, int>();
        chara.EquipBag[id] = chara.EquipBag.GetValueOrDefault(id) + 1;
    }
}
