namespace MushTown.Shared;

// What one slot click did (the client maps this to sound + doll rebuild).
public enum HandAct
{
    None, // empty cell, mismatch, or a guarded drink: silent, nothing changed
    Moved, // pick, drop, or return: click
    Wore, // the worn set changed: click + doll rebuild
    Drank, // drank the held potion: silent (legacy drinks made no sound)
    Used, // cash-tab item: the client runs its Utility effect
}

// One item in the hand (MapleStory pick-up/drop, not press-drag).
// The positional model moves the whole cell, so pick/return never changes
// a bag count. A double-click on the source cell drinks or wears the item.
public sealed class CarryHand
{
    public const double DoubleMs = 500; // two clicks inside this on the same cell = use
    public string? Kind; // null = empty hands; else use/equip/cash/dec
    public int Id;
    public int Cell = -1; // source cell
    private string _lastKind = "";
    private int _lastId;
    private int _lastCell = -1;
    private double _lastMs = -DoubleMs * 2;

    public bool Empty => Kind == null;

    // The held cell still holds the held item. External bag changes (a sale,
    // a quick-slot drink, a reward) can invalidate the preview.
    public bool StillThere(CharaSlot chara) =>
        Kind != null && Inventory.SlotId(chara, Kind, Cell) == Id;

    // One slot click. hpPct feeds the shared drink guard (buffed max HP).
    public HandAct Click(CharaSlot chara, string tab, int id, IReadOnlyDictionary<int, ItemData> defs, int hpPct, double nowMs, int index)
    {
        if (Kind == null)
        {
            if (id == 0 || Inventory.SlotId(chara, tab, index) != id) return HandAct.None;
            Kind = tab;
            Id = id;
            Cell = index;
            Arm(tab, id, index, nowMs);
            return HandAct.Moved;
        }
        if (Kind != tab) { Disarm(); return HandAct.None; }
        if (!StillThere(chara))
        {
            // Something else removed or moved the cell (sell, drink, reward,
            // pickup): drop the stale preview and treat this as a fresh click.
            Return();
            return Click(chara, tab, id, defs, hpPct, nowMs, index);
        }
        if (Cell == index)
        {
            if (_lastKind == tab && _lastId == Id && _lastCell == index && nowMs - _lastMs <= DoubleMs)
                return DoubleClickHeld(chara, defs, hpPct);
            Kind = null;
            Cell = -1;
            Disarm();
            return HandAct.Moved;
        }
        if (!Inventory.Move(chara, tab, Cell, index)) return HandAct.None;
        Kind = null;
        Cell = -1;
        Disarm();
        return HandAct.Moved;
    }

    // Esc, right click, close, save: the hand is a cell preview, so clearing
    // it is lossless.
    public void Return()
    {
        Kind = null;
        Cell = -1;
    }

    private HandAct DoubleClickHeld(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs, int hpPct)
    {
        HandAct act = Kind switch
        {
            "use" => DrinkHeld(chara, defs, hpPct),
            "equip" => WearHeld(chara, defs),
            "dec" => Progression.WearCash(chara, Id, defs) ? HandAct.Wore : HandAct.None,
            "cash" => HandAct.Used,
            _ => HandAct.None,
        };
        if (act == HandAct.None) return act;
        Kind = null;
        Cell = -1;
        Disarm();
        return act;
    }

    private HandAct DrinkHeld(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs, int hpPct)
    {
        var item = defs[Id];
        Progression.UsePotion(chara, Id, item.Hp, item.Mp, hpPct);
        return HandAct.Drank;
    }

    private HandAct WearHeld(CharaSlot chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        if (!Progression.WearEquip(chara, Id, defs)) return HandAct.None;
        return HandAct.Wore;
    }

    private void Arm(string cell, int id, int index, double nowMs)
    {
        _lastKind = cell;
        _lastId = id;
        _lastCell = index;
        _lastMs = nowMs;
    }

    private void Disarm() { _lastMs = -DoubleMs * 2; _lastCell = -1; }
}
