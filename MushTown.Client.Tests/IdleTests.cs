using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The frame picker of an idle animation: the world map tip plays an NPC or a
// monster with it, so the tip must walk the frames and start over.
[TestClass]
public class IdleTests
{
    [TestMethod]
    public void At_WalksTheDelaysAndLoops()
    {
        int[] delays = { 1200, 120, 120 }; // 1440 ms a round: the first frame holds almost all of it
        Assert.AreEqual(0, Idle.At(delays, 0));
        Assert.AreEqual(0, Idle.At(delays, 1199));
        Assert.AreEqual(1, Idle.At(delays, 1200));
        Assert.AreEqual(2, Idle.At(delays, 1320));
        Assert.AreEqual(0, Idle.At(delays, 1440)); // the next round
        Assert.AreEqual(1, Idle.At(delays, 1440 + 1200));
    }

    [TestMethod]
    public void At_OneFrameStaysOnTheFirst()
    {
        Assert.AreEqual(0, Idle.At(new[] { 100 }, 5000));
    }

    [TestMethod]
    public void PingPongAt_MirrorsTheMiddleFrame()
    {
        int[] delays = { 120, 120, 120 };
        Assert.AreEqual(0, Idle.PingPongAt(delays, 0));
        Assert.AreEqual(0, Idle.PingPongAt(delays, 119));
        Assert.AreEqual(1, Idle.PingPongAt(delays, 120));
        Assert.AreEqual(2, Idle.PingPongAt(delays, 240));
        Assert.AreEqual(1, Idle.PingPongAt(delays, 360));
        Assert.AreEqual(0, Idle.PingPongAt(delays, 480));
    }

    [TestMethod]
    public void PingPongAt_TwoFramesLoops()
    {
        int[] delays = { 100, 100 };
        Assert.AreEqual(0, Idle.PingPongAt(delays, 200));
        Assert.AreEqual(0, Idle.PingPongAt(delays, 400));
    }

    [TestMethod]
    public void Frame_PingPongsOnlyStand1()
    {
        int[] delays = { 100, 100, 100 };
        Assert.AreEqual(0, Idle.Frame("stand1", delays, 0));
        Assert.AreEqual(1, Idle.Frame("stand1", delays, 100));
        Assert.AreEqual(2, Idle.Frame("stand1", delays, 200));
        Assert.AreEqual(1, Idle.Frame("stand1", delays, 300));
        Assert.AreEqual(0, Idle.Frame("stand1", delays, 400));

        Assert.AreEqual(0, Idle.Frame("walk1", delays, 0));
        Assert.AreEqual(1, Idle.Frame("walk1", delays, 100));
        Assert.AreEqual(2, Idle.Frame("walk1", delays, 200));
        Assert.AreEqual(0, Idle.Frame("walk1", delays, 300));
        Assert.AreEqual(1, Idle.Frame("walk1", delays, 400));
        Assert.AreEqual(2, Idle.Frame("walk1", delays, 500));
    }

    // A stand with a single picture cannot animate: the mob crawls with move
    // instead (the snail). Two or more stand frames play as stand.
    [TestMethod]
    public void Idle_StandNeedsMoreThanOneFrame()
    {
        Assert.AreEqual("move", Mob(("stand", 1), ("move", 3)).Idle);
        Assert.AreEqual("stand", Mob(("stand", 2), ("move", 3)).Idle);
        Assert.AreEqual("move", Mob(("move", 4)).Idle);
    }

    // A lag spike must not slow the animation: one tick may cross frames.
    [TestMethod]
    public void Update_WalksSeveralFramesOnALongTick()
    {
        var mob = new MobArt { Data = Mob(("stand", 3)), Mob = 1 };
        mob.SetAction("stand");
        mob.Update(0.25f); // 250 ms of 100 ms frames: two frames and 50 ms left
        Assert.AreEqual(2, mob.Frame);
        Assert.AreEqual(50f, mob.Timer, 0.001f);
    }

    private static MobData Mob(params (string Name, int Frames)[] actions)
    {
        var data = new MobData();
        foreach (var (name, count) in actions)
        {
            var action = new MobAction { Name = name };
            for (int i = 0; i < count; i++) action.Frames.Add(new MobFrame { Image = "x.png", Delay = 100 });
            data.Actions.Add(action);
        }
        return data;
    }
}
