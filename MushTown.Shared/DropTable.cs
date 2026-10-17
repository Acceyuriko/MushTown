namespace MushTown.Shared;

// One mob's live drop table (S9): thin object over the def row (same
// shape as SkillCost/SkillDamage). Roll pays the meso pile plus each
// paying item with its slot offset; the world places them (VR clamp +
// landing + ids stay server-side with the other bookkeeping).
public sealed class DropTable : Component
{
    public DropData Def { get; }
    public DropTable(DropData def) { Def = def; } // dumb ctor

    // One kill's payouts: (kind, id, count, dx from the corpse). Meso
    // always pays at -7; paying items compact into 14px slots from +7,
    // so a coat with no potion lands at +7, not the old fixed +21.
    public List<(int kind, int id, int count, float dx)> Roll(Random rng)
    {
        var list = new List<(int kind, int id, int count, float dx)>();
        list.Add((DropKind.Mesos, Def.MesoMin + rng.Next(Def.MesoMax - Def.MesoMin + 1), 1, -7));
        int slot = 0;
        foreach (var d in Def.Items)
        {
            if (rng.NextDouble() >= d.Chance) continue;
            list.Add((DropKind.Item, d.Item, d.Count, 7 + 14 * slot));
            slot++;
        }
        return list;
    }
}
