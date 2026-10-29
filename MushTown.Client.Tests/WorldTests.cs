using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The world objects step themselves: a mob plays its anim and ends its own
// life, a portal loops, and the tree skips whatever is already dead.
[TestClass]
public class WorldTests
{
    private sealed class Count : Component
    {
        public int Ticks;
        public override void Update(float dt) => Ticks++;
    }

    // A mob file with one action per name, one frame each unless told otherwise.
    private static MobData Data(params (string Name, int Frames)[] actions)
    {
        var data = new MobData();
        foreach (var (name, count) in actions)
        {
            var a = new MobAction { Name = name };
            for (int i = 0; i < count; i++) a.Frames.Add(new MobFrame { Image = $"{name}{i}", Delay = 100 });
            data.Actions.Add(a);
        }
        return data;
    }

    private static MobArt Mob(MobData data, string action)
    {
        var o = new GameObject();
        var m = o.Add(new MobArt { Data = data, Mob = 1 });
        m.SetAction(action);
        return m;
    }

    [TestMethod]
    public void MobTick_AdvancesOnTheFrameDelay()
    {
        var m = Mob(Data(("stand", 2), ("hit1", 1), ("die1", 1)), "stand");
        m.Update(0.099f); // 99ms: not yet
        Assert.AreEqual(0, m.Frame);
        m.Update(0.002f); // 101ms: the first frame is spent
        Assert.AreEqual(1, m.Frame);
        m.Update(0.1f); // the second frame loops back to the first
        Assert.AreEqual(0, m.Frame);
        Assert.AreEqual("stand", m.Action);
    }

    [TestMethod]
    public void MobTick_FlinchReturnsToIdle()
    {
        var m = Mob(Data(("stand", 2), ("hit1", 1), ("die1", 1)), "hit1");
        m.Update(0.1f);
        Assert.AreEqual("stand", m.Action);
        Assert.AreEqual(0, m.Frame);
        Assert.AreEqual(2, m.Frames.Count); // the frames moved with the name: the draw reads this list
        Assert.IsFalse(m.Owner.Dead);
    }

    [TestMethod]
    public void MobTick_DeathMarksTheOwnerDeadOnTheLastFrame()
    {
        var m = Mob(Data(("stand", 2), ("hit1", 1), ("die1", 2)), "die1");
        m.Update(0.1f); // the first death frame is spent: not over yet
        Assert.IsFalse(m.Owner.Dead);
        Assert.AreEqual(1, m.Frame);
        m.Update(0.1f); // the last one ends the life
        Assert.IsTrue(m.Owner.Dead);
        Assert.AreEqual(2, m.Frame); // Frame == Frames.Count: the corpse has to be collected before the draw
    }

    [TestMethod]
    public void MobTick_NoStandFallsBackToMove()
    {
        var m = Mob(Data(("move", 1), ("hit1", 1), ("die1", 1)), "hit1");
        m.Update(0.1f);
        Assert.AreEqual("move", m.Action);
    }

    [TestMethod]
    public void CollectDead_DropsTheBodyAndTheEntry()
    {
        var world = new GameObject();
        var mobs = new Dictionary<int, MobArt>();
        var live = Mob(Data(("stand", 1), ("hit1", 1), ("die1", 1)), "stand");
        world.AddChild(live.Owner);
        mobs[1] = live;
        var dying = Mob(Data(("stand", 1), ("hit1", 1), ("die1", 1)), "die1");
        world.AddChild(dying.Owner);
        mobs[2] = dying;
        dying.Update(0.1f); // the death anim is one frame: it ends here
        Assert.IsTrue(dying.Owner.Dead);
        MobArt.CollectDead(mobs, world);
        Assert.AreEqual(1, world.Children.Count);
        Assert.IsTrue(mobs.ContainsKey(1));
        Assert.IsFalse(mobs.ContainsKey(2));
    }

    [TestMethod]
    public void TreeUpdate_SkipsADeadNodeAndItsChildren()
    {
        var dead = new GameObject { Dead = true };
        var tick = dead.Add(new Count());
        var child = new GameObject();
        var childTick = child.Add(new Count());
        dead.AddChild(child);
        TreeUpdate.UpdateObject(dead, 0.1f);
        Assert.AreEqual(0, tick.Ticks);
        Assert.AreEqual(0, childTick.Ticks);
    }

    [TestMethod]
    public void PortalTick_LoopsItsFrames()
    {
        var portal = new PortalItem { Frames = { new PortalFrame(), new PortalFrame() } };
        var o = new GameObject();
        var p = o.Add(new PortalComp { Portal = portal, Delays = new List<int> { 100, 100 } });
        p.Update(0.1f);
        Assert.AreEqual(1, p.Frame);
        p.Update(0.1f); // the second frame wraps back to the first
        Assert.AreEqual(0, p.Frame);
    }
}
