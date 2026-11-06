namespace MushTown.Shared;

// Buy/sell math (per-keeper stocks live in shops/): buys at WZ price,
// sells back anything in the bags at half (min 1). Worn gear never sells
// (take it off first: the sell list shows bags only).
public static class Shop
{
    public static int SellPrice(int price) => price <= 0 ? 0 : Math.Max(1, price / 2); // a 0-price item (cash gear) sells for nothing

    public static bool Buy(CharaSlot chara, int id, int price, ItemData def, int count = 1)
    {
        long total = (long)price * count;
        if (chara.Mesos < total) return false;
        chara.Mesos -= (int)total;
        // the item goes to the bag it belongs to: equips to the equip bag,
        // everything else to the use bag (the sell list looks there).
        if (def.Slot != null)
        {
            chara.EquipBag ??= new Dictionary<int, int>();
            chara.EquipBag[id] = chara.EquipBag.GetValueOrDefault(id) + count;
        }
        else
        {
            chara.Use ??= new Dictionary<int, int>();
            chara.Use[id] = chara.Use.GetValueOrDefault(id) + count;
        }
        return true;
    }

    public static bool Sell(CharaSlot chara, int id, ItemData def)
    {
        var bag = def.Slot != null ? chara.EquipBag : chara.Use;
        if (bag == null || !bag.TryGetValue(id, out int count) || count <= 0) return false;
        if (count == 1) bag.Remove(id);
        else bag[id] = count - 1;
        chara.Mesos += SellPrice(def.Price);
        return true;
    }
}
