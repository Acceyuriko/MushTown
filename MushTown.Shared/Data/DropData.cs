namespace MushTown.Shared;

// One mob's drop table (drops/{mob}.json, repo data): a meso range that
// always pays, plus items the server rolls independently on each kill.
public sealed class DropData
{
    public int MobId { get; set; }
    public int MesoMin { get; set; }
    public int MesoMax { get; set; }
    public List<DropItem> Items { get; set; } = new();
}

public sealed class DropItem
{
    public int Item { get; set; }
    public double Chance { get; set; } // 0..1, rolled per entry (a 10% red + 4% blue rarely pays both)
    public int Count { get; set; }
}
