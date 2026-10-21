namespace MushTown.Shared;

// Buy/sell math (per-keeper stocks live in shops/): buys at WZ price,
// sells back anything in the bags at half (min 1). Worn gear never sells
// (take it off first: the sell list shows bags only).
public static class Shop
{
    public static int SellPrice(int price) => Math.Max(1, price / 2);

    public static bool Buy(CharaSlot chara, int id, int price, ItemData def)
    {
        if (chara.Mesos < price) return false;
        chara.Mesos -= price;
        // the item goes to the bag it belongs to: equips to the equip bag,
        // everything else to the use bag (the sell list looks there).
        if (def.Slot != null)
        {
            chara.EquipBag ??= new Dictionary<int, int>();
            chara.EquipBag[id] = chara.EquipBag.GetValueOrDefault(id) + 1;
        }
        else
        {
            chara.Use ??= new Dictionary<int, int>();
            chara.Use[id] = chara.Use.GetValueOrDefault(id) + 1;
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
