namespace MushTown.Shared;

// The four ground meso pictures (Item/Special/0900.img, iconRaw frame 0), one per amount
// tier: 1-49, 50-99, 100-999, 1000+. The exporter, the server (drop spacing) and the client
// (drawing) all read this one table.
public static class MesoArt
{
    public static readonly int[] Ids = { 9000000, 9000001, 9000002, 9000003 };
    public static readonly (int X, int Y)[] Origins = { (-5, 28), (-5, 28), (-1, 32), (0, 32) }; // the WZ origin of each picture
    public static readonly int[] Widths = { 23, 23, 31, 32 }; // picture width, px

    public static int Tier(long amount) =>
        amount >= 1000 ? 3 : amount >= 100 ? 2 : amount >= 50 ? 1 : 0;

    // The exported picture of a tier, under assets/.
    public static string Image(int tier) => $"item/meso_{Ids[tier]:D8}.png";
}
