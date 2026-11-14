namespace MushTown.Shared;

// One mob's live drop table (S9): thin object over the def row (same
// shape as SkillCost/SkillDamage). Roll pays the meso pile plus each
// paying item; the world places them (VR clamp + landing + ids stay
// server-side with the other bookkeeping).
public sealed class DropTable : Component
{
    public DropData Def { get; }
    public DropTable(DropData def) { Def = def; } // dumb ctor

    // One kill's payouts, in table order: (kind, item id, count, meso amount).
    // The world lays them out as one row; the roll only decides what pays.
    public List<(int kind, int id, int count, long amount)> Roll(Random rng)
    {
        var list = new List<(int kind, int id, int count, long amount)>();
        long span = Def.MesoMax - Def.MesoMin + 1;
        long amount = Def.MesoMin + (span <= int.MaxValue
            ? rng.Next((int)span)
            : rng.NextInt64(span));
        list.Add((DropKind.Mesos, 0, 1, amount));
        foreach (var d in Def.Items)
        {
            if (rng.NextDouble() >= d.Chance) continue;
            list.Add((DropKind.Item, d.Item, d.Count, 0));
        }
        return list;
    }
}
