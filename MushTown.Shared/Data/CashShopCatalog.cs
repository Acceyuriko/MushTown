namespace MushTown.Shared;

// The cash shop item list (Export/lists/cashshop.json -> assets/cashshop/catalog.json).
// Prices are meso; Count is how many pieces one purchase gives.
public sealed class CashShopCatalog
{
    public List<CashShopItem> Items { get; set; } = new();
    public List<CashShopLook> Hairs { get; set; } = new();
    public List<CashShopLook> Faces { get; set; } = new();

    // The visual sub-tab index of each item, or -1 when Group/Tab is unknown.
    public static int TabIndex(string group, string tab) => (group, tab) switch
    {
        ("Utility", "Cube") => 0,
        ("Utility", "Scroll") => 1,
        ("Utility", "Convenience") => 2,
        ("Utility", "Pets") => 3,
        ("Utility", "Pet Equip") => 4,
        ("Utility", "Pet Skills") => 5,
        ("Utility", "Pet Food") => 6,
        ("Utility", "Social") => 7,
        ("Utility", "Atmos. Effects") => 8,
        ("Utility", "Add Slots") => 9,
        ("Fashion", "Weapons") => 0,
        ("Fashion", "Hats") => 1,
        ("Fashion", "Outfits") => 2,
        ("Fashion", "Tops") => 3,
        ("Fashion", "Bottoms") => 4,
        ("Fashion", "Cape") => 5,
        ("Fashion", "Shoes") => 6,
        ("Fashion", "Gloves") => 7,
        ("Fashion", "Accessories") => 8,
        ("Fashion", "Effects") => 9,
        _ => -1,
    };

    // The four fields the exporter and the client both prove.
    public bool ValidateShape(out string error)
    {
        foreach (var item in Items)
        {
            if (item.Price <= 0) { error = $"cashshop item {item.Id} has a bad price {item.Price}"; return false; }
            if (item.Count <= 0) { error = $"cashshop item {item.Id} has a bad count {item.Count}"; return false; }
            if (item.Group is not ("Utility" or "Fashion"))
            {
                error = $"cashshop item {item.Id} has an unknown group {item.Group}";
                return false;
            }
            if (TabIndex(item.Group, item.Tab) < 0)
            {
                error = $"cashshop item {item.Id} has an unknown tab {item.Tab}";
                return false;
            }
        }
        if (!ValidateLooks(Hairs, "hair", out error)) return false;
        if (!ValidateLooks(Faces, "face", out error)) return false;
        error = "";
        return true;
    }

    // A look list is optional in a test catalog, but when present it must
    // have one free default and only the other rows can be sold.
    private static bool ValidateLooks(List<CashShopLook> looks, string kind, out string error)
    {
        int defaults = 0;
        int freeNonDefaults = 0;
        foreach (var look in looks)
        {
            if (look.ItemId <= 0) { error = $"cashshop {kind} {look.ItemId} has a bad item id"; return false; }
            if (look.Cosmetic <= 0) { error = $"cashshop {kind} {look.ItemId} has a bad cosmetic {look.Cosmetic}"; return false; }
            if (look.Name == "") { error = $"cashshop {kind} {look.ItemId} has no name"; return false; }
            if (look.Price < 0) { error = $"cashshop {kind} {look.ItemId} has a bad price {look.Price}"; return false; }
            if (look.Default)
            {
                defaults++;
                if (look.Price != 0) { error = $"cashshop default {kind} {look.ItemId} must be free"; return false; }
            }
            else if (look.Price == 0)
            {
                freeNonDefaults++;
            }
        }
        if (looks.Count > 0 && defaults != 1)
        {
            error = $"cashshop {kind} list has {defaults} defaults, want exactly one";
            return false;
        }
        if (freeNonDefaults > 1)
        {
            error = $"cashshop {kind} list has {freeNonDefaults} free non-default rows, want at most one free non-default row";
            return false;
        }
        error = "";
        return true;
    }

    // Boot proves every row before the shop window can be opened.
    public bool Validate(IReadOnlyDictionary<int, ItemData> items, out string error)
    {
        if (!ValidateShape(out error)) return false;
        foreach (var item in Items)
            if (!items.ContainsKey(item.Id))
            {
                error = $"cashshop item {item.Id} has no exported ItemData";
                return false;
            }
        foreach (var look in Hairs.Concat(Faces))
            if (!items.ContainsKey(look.ItemId))
            {
                error = $"cashshop {look.Name} {look.ItemId} has no exported ItemData";
                return false;
            }
        error = "";
        return true;
    }
}

public sealed class CashShopItem
{
    public int Id { get; set; }
    public long Price { get; set; }
    public int Count { get; set; }
    public string Group { get; set; } = "";
    public string Tab { get; set; } = "";
}

public sealed class CashShopLook
{
    public int ItemId { get; set; }
    public int Cosmetic { get; set; }
    public string Name { get; set; } = "";
    public long Price { get; set; }
    public bool Default { get; set; }
}
