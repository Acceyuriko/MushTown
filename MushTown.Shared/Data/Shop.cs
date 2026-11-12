namespace MushTown.Shared;

// Buy/sell math (per-keeper stocks live in shops/): buys at WZ price,
// sells back anything in the bags at half (min 1). Worn gear never sells
// (take it off first: the sell list shows bags only).
public static class Shop
{
    public static long SellPrice(long price) => price <= 0 ? 0 : Math.Max(1, price / 2); // a 0-price item (cash gear) sells for nothing

    public static bool Buy(CharaSlot chara, int id, long price, ItemData def, int count = 1)
    {
        long total = price * count;
        if (chara.Mesos < total) return false;
        // the item goes to the bag it belongs to: equips to the equip bag,
        // everything else to the use bag (the sell list looks there).
        string tab = def.Slot != null ? "equip" : "use";
        if (!Inventory.Add(chara, tab, id, count)) return false; // full bag: no charge
        chara.Mesos -= total;
        return true;
    }

    public static bool Sell(CharaSlot chara, int id, ItemData def, int count = 1)
    {
        long gain = SellPrice(def.Price) * count;
        if (count <= 0) return false;
        if (!Inventory.Remove(chara, def.Slot != null ? "equip" : "use", id, count)) return false;
        chara.Mesos += gain;
        return true;
    }
}
