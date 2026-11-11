namespace MushTown.Shared;

// Buying cash shop items and the two Utility effects. Every method checks the
// whole price before it changes the character.
public static class CashShop
{
    public static bool Buy(CharaSlot chara, int id, int price, ItemData def, int count, bool decorative)
    {
        long total = (long)price * count;
        string tab = decorative ? "dec" : "cash";
        if (count <= 0 || total > chara.Mesos || !Inventory.CanAdd(chara, tab, id, count)) return false;
        chara.Mesos -= (int)total;
        return Inventory.Add(chara, tab, id, count);
    }

    public static bool BuyHair(CharaSlot chara, int id, int price) => BuyPack(chara, id, price);

    public static bool BuyFace(CharaSlot chara, int id, int price) => BuyPack(chara, id, price);

    private static bool BuyPack(CharaSlot chara, int id, int price)
    {
        if (price > chara.Mesos || !Inventory.CanAdd(chara, "dec", id, 1)) return false;
        chara.Mesos -= price;
        return Inventory.Add(chara, "dec", id, 1);
    }

    // Clears every learned skill and gives its levels back as SP. Returns
    // false when there is nothing to reset.
    public static bool ResetSkills(CharaSlot chara)
    {
        int points = 0;
        foreach (int lv in chara.SkillLv.Values) points += lv * Progression.SkillSpCost;
        if (points == 0) return false;
        chara.SkillLv.Clear();
        chara.Sp += points;
        return true;
    }

    // Renames one hero when the name follows the creation rules and is not
    // used by another hero in any save.
    public static bool Rename(SaveStore store, int save, int slot, string name)
    {
        if (save < 0 || save >= SaveStore.MaxSaves || slot < 0 || slot >= SaveStore.MaxHeroes) return false;
        if (name.Length is < 1 or > 12 || !name.All(SaveStore.IsIdChar)) return false;
        var target = store.Saves[save]?.Slots[slot];
        if (target == null) return false;
        if (target.Name == name) return false;
        foreach (var data in store.Saves)
        {
            if (data == null) continue;
            foreach (var hero in data.Slots)
                if (hero != null && hero != target && hero.Name == name) return false;
        }
        target.Name = name;
        return true;
    }
}
