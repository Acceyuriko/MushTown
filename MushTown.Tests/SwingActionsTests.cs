using MushTown.Shared;

namespace MushTown.Tests;

// The swing component: a pool, and a roll that picks one of it.
[TestClass]
public class SwingActionsTests
{
    private static readonly string[] Pool = { "swingO1", "swingO2", "swingO3" };

    [TestMethod]
    public void Roll_ReachesEveryActionOfThePool()
    {
        var swing = new SwingActions(new Random(1));
        var seen = new HashSet<string>();
        for (int i = 0; i < 200; i++) seen.Add(swing.Roll(Pool));
        CollectionAssert.AreEquivalent(Pool, seen.ToArray());
    }

    [TestMethod]
    public void Roll_SameSeedGivesTheSameRolls()
    {
        var a = new SwingActions(new Random(42));
        var b = new SwingActions(new Random(42));
        for (int i = 0; i < 20; i++) Assert.AreEqual(a.Roll(Pool), b.Roll(Pool));
    }

    [TestMethod]
    public void Roll_KeepsThePoolAndTheCurrentAction()
    {
        var swing = new SwingActions(new Random(1));
        string rolled = swing.Roll(Pool);
        Assert.AreEqual(rolled, swing.Current);
        CollectionAssert.AreEqual(Pool, swing.Pool.ToArray());
    }

    [TestMethod]
    public void Roll_EmptyPoolThrows()
    {
        var swing = new SwingActions(new Random(1));
        Assert.ThrowsException<InvalidOperationException>(() => swing.Roll(Array.Empty<string>()));
    }

    [TestMethod]
    public void WandPool_HoldsTheThreeSwingsAndEachIsAnExportedAction()
    {
        CollectionAssert.AreEqual(Pool, Progression.SwingPools[6]);
        foreach (string action in Pool) Assert.IsTrue(Net.Actions.Contains(action));
    }
}
