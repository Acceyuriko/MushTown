namespace MushTown.Shared;

// Buy/sell math (per-keeper stocks live in shops/): buys at WZ price,
// sells back anything in the bags at half (min 1); 0-price cash gear sells
// for nothing. Worn gear never sells
// (take it off first: the sell list shows bags only). The caller passes the
// bag tab: cash and decoration gear do not live in the equip/use bags.
public static class Shop
{
    public static long SellPrice(long price) => price <= 0 ? 0 : Math.Max(1, price / 2); // a 0-price item (cash gear) sells for nothing

    public static string BagFor(ItemData def) =>
        def.Cash ? (def.Slot != null || def.Cosmetic != 0 ? "dec" : "cash")
            : def.Slot != null ? "equip" : "use";

    public static bool Buy(CharaSlot chara, int id, long price, string tab, int count = 1)
    {
        long total = price * count;
        if (chara.Mesos < total) return false;
        if (!Inventory.Add(chara, tab, id, count)) return false; // full bag: no charge
        chara.Mesos -= total;
        return true;
    }

    public static bool Sell(CharaSlot chara, int id, long price, string tab, int count = 1)
    {
        if (count <= 0) return false;
        long gain = SellPrice(price) * count;
        if (!Inventory.Remove(chara, tab, id, count)) return false;
        chara.Mesos += gain;
        return true;
    }
}
