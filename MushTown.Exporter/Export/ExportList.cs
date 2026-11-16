using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Exporter;

// Loads the export ID lists (tracked data: Export/lists/*.json). A missing
// or bad file fails loud: a silent default list would export the wrong game.
public static class ExportList
{
    public static int[] Load(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Export", "lists", $"{name}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing export list: {path}", path);
        return JsonSerializer.Deserialize<int[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"export list {path} holds no int array");
    }

    // The damage skins to export: skin id and the coupon that gives it.
    // Written out by hand: the WZ damageSkinID field is missing on some coupons.
    public static DamageSkinRow[] LoadDamageSkins()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Export", "lists", "damageskins.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing export list: {path}", path);
        return JsonSerializer.Deserialize<DamageSkinRow[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"export list {path} holds no damage skin rows");
    }

    public static CashShopCatalog LoadCashShop()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Export", "lists", "cashshop.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing export list: {path}", path);
        return JsonSerializer.Deserialize<CashShopCatalog>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"export list {path} holds no cash shop catalog");
    }
}

public sealed record DamageSkinRow(int Skin, int Item);
