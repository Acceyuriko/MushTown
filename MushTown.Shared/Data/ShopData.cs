namespace MushTown.Shared;

// One keeper's stock (shops/{npc}.json, repo data): the ids its buy list sells.
public sealed class ShopData
{
    public int Npc { get; set; }
    public List<int> Stock { get; set; } = new();
}
