using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

[TestClass]
public class GameStatsTests
{
    [TestMethod]
    public void BeginnerSnapshot_KeepsCliMovement()
    {
        var stats = MushTownGame.BeginnerSnapshot();

        Assert.AreEqual(PlayerMover.WalkSpeed, stats.DoubleValue(StatKey.SPEED), 1e-9);
        Assert.AreEqual(PlayerMover.JumpV0, stats.DoubleValue(StatKey.JUMP), 1e-9);
        Assert.AreEqual(140.0, stats.DoubleValue(StatKey.SPEED), 1e-9);
        Assert.AreEqual(-520.0, stats.DoubleValue(StatKey.JUMP), 1e-9);
    }
}
