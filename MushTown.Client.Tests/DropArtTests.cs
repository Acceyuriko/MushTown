using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The drop view owns its birth / rest / take timing.
[TestClass]
public class DropArtTests
{
    [TestMethod]
    public void DropArt_SpinsWhileFallingThenRests()
    {
        var o = new GameObject();
        var d = o.Add(new DropArt { State = DropState.Falling, FromX = 100, FromY = 100, X = 100, Y = 100 });

        d.Update(0.2f);
        Assert.AreEqual(DropState.Falling, d.State);
        Assert.IsTrue(d.Rotation > 0);

        d.Update(0.2f);
        Assert.AreEqual(DropState.Resting, d.State);
        Assert.AreEqual(0f, d.Rotation);

        d.Update(0.5f);
        Assert.AreEqual(DropState.Resting, d.State); // rest never ends itself
    }

    [TestMethod]
    public void DropArt_TakeEndsAfter200ms()
    {
        var o = new GameObject();
        var d = o.Add(new DropArt { State = DropState.Taken, SpawnMs = 0 });
        d.Update(0.19f);
        Assert.IsFalse(o.Dead);
        d.Update(0.01f);
        Assert.IsTrue(o.Dead);
    }
}
