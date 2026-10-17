using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class DropTableTests
{
    private static DropTable Table(int mesoMin, int mesoMax, params (int item, double chance, int count)[] items)
    {
        var table = new DropData { MobId = 100000, MesoMin = mesoMin, MesoMax = mesoMax };
        foreach (var (item, chance, count) in items)
            table.Items.Add(new DropItem { Item = item, Chance = chance, Count = count });
        return new DropTable(table);
    }

    // Meso always pays exactly once, in range, at -7 with count 1 (the
    // amount rides the id). A flat range pays exactly.
    [TestMethod]
    public void Roll_MesoAlwaysPaysInRange()
    {
        var table = Table(10, 30);
        for (int seed = 0; seed < 200; seed++)
        {
            var first = table.Roll(new Random(seed))[0];
            Assert.AreEqual(DropKind.Mesos, first.kind);
            Assert.IsTrue(first.id >= 10 && first.id <= 30);
            Assert.AreEqual(1, first.count);
            Assert.AreEqual(-7f, first.dx);
        }
        Assert.AreEqual(150, Table(150, 150).Roll(new Random(0))[0].id);
    }

    // No items: meso alone (a boss purse with no potion rows).
    [TestMethod]
    public void Roll_EmptyItems_PaysMesoOnly()
    {
        var payouts = Table(10, 30).Roll(new Random(0));
        Assert.AreEqual(1, payouts.Count);
        Assert.AreEqual(DropKind.Mesos, payouts[0].kind);
    }

    // Chance 1 always pays, in file order, compacting into 14px slots
    // from +7 (NextDouble never reaches 1).
    [TestMethod]
    public void Roll_ChanceOne_PaysAllInSlotOrder()
    {
        var payouts = Table(10, 30, (2000000, 1, 2), (2000003, 1, 1)).Roll(new Random(0));
        Assert.AreEqual(3, payouts.Count);
        Assert.AreEqual((DropKind.Item, 2000000, 2, 7f), payouts[1]);
        Assert.AreEqual((DropKind.Item, 2000003, 1, 21f), payouts[2]);
    }

    // Chance 0 never pays (NextDouble starts at 0), and the miss compacts:
    // the paying entry behind it still lands at +7.
    [TestMethod]
    public void Roll_ChanceZero_SkipsAndCompacts()
    {
        var payouts = Table(10, 30, (2000000, 0, 1), (1442004, 1, 1)).Roll(new Random(0));
        Assert.AreEqual(2, payouts.Count);
        Assert.AreEqual((DropKind.Item, 1442004, 1, 7f), payouts[1]);
    }
}
