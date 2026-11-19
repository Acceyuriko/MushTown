using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The world objects step themselves: a mob plays its anim and ends its own
// life, a portal loops, a damage number ages out, a skill effect plays once
// (or wraps while a buff holds it), and the tree skips whatever is already
// dead.
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
    public void NumberTick_DiesAtTheEndOfItsLife()
    {
        var n = new GameObject().Add(new FloatingNumber { Text = "12" });
        n.Update(0.5f); // 500ms: still rising
        Assert.IsFalse(n.Owner.Dead);
        n.Update(0.3f); // 800ms: the last frame counts as over
        Assert.IsTrue(n.Owner.Dead);
        Assert.AreEqual(800f, n.Age, 0.01f);
    }

    // A number of a multi-hit cast waits for its turn: not there before DelayMs, there from it, over DelayMs + LifeMs later.
    [TestMethod]
    public void NumberDelay_WaitsThenShowsThenDies()
    {
        var n = new GameObject().Add(new FloatingNumber { Text = "12", DelayMs = 200 });
        Assert.IsFalse(n.Visible);
        n.Update(0.1f); // 100 ms
        Assert.IsFalse(n.Visible);
        n.Update(0.1f); // 200 ms: its turn
        Assert.IsTrue(n.Visible);
        n.Update(0.7f); // 900 ms: 700 ms of its own life
        Assert.IsFalse(n.Owner.Dead);
        n.Update(0.1f); // 1000 ms = DelayMs + LifeMs
        Assert.IsTrue(n.Owner.Dead);
    }

    // Two numbers of one cast, the upper one AppearStepMs later: at the same moment the lower one has faded more.
    [TestMethod]
    public void NumberFade_GoesFromTheBottomUp()
    {
        var lower = new GameObject().Add(new FloatingNumber { Text = "1", DelayMs = 0 });
        var upper = new GameObject().Add(new FloatingNumber { Text = "2", DelayMs = FloatingNumber.AppearStepMs });
        lower.Update(0.6f); // 600 ms: the lower one is 600 ms into its life (past 70% of 800), the upper one 500 ms (before it)
        upper.Update(0.6f);
        Assert.AreEqual(255, upper.Alpha); // the upper one is still full
        Assert.IsTrue(lower.Alpha < upper.Alpha);
        Assert.IsTrue(lower.Rise > upper.Rise);
    }

    [TestMethod]
    public void NumberReset_BringsANumberBack()
    {
        var n = new GameObject().Add(new FloatingNumber { Text = "12", Age = 900, Skin = 3 });
        n.Owner.Dead = true;
        n.Reset();
        Assert.AreEqual(0f, n.Age);
        Assert.AreEqual(0, n.Skin);
        Assert.IsFalse(n.Owner.Dead);
        n.Update(0.1f); // it ticks again
        Assert.AreEqual(100f, n.Age, 0.01f);
    }

    [TestMethod]
    public void PoolSweepCollectDead_ReturnsTheNumberToThePool()
    {
        var world = new GameObject();
        var pool = new Stack<FloatingNumber>();
        var live = new List<FloatingNumber>();
        var a = new GameObject().Add(new FloatingNumber { Text = "1" });
        var b = new GameObject().Add(new FloatingNumber { Text = "2" });
        world.AddChild(a.Owner);
        world.AddChild(b.Owner);
        live.Add(a);
        live.Add(b);
        a.Owner.Dead = true;
        PoolSweep.CollectDead(live, world, pool);
        Assert.AreEqual(1, world.Children.Count);
        Assert.AreEqual(1, live.Count);
        Assert.AreSame(a, pool.Pop());
        Assert.AreSame(b, live[0]);
    }

    [TestMethod]
    public void PoolSweepDropAll_ClearsTheTreeAndTheList()
    {
        var world = new GameObject();
        var pool = new Stack<FloatingNumber>();
        var live = new List<FloatingNumber>();
        var a = new GameObject().Add(new FloatingNumber { Text = "1" });
        var b = new GameObject().Add(new FloatingNumber { Text = "2" });
        world.AddChild(a.Owner);
        world.AddChild(b.Owner);
        live.Add(a);
        live.Add(b);
        PoolSweep.DropAll(live, world, pool);
        Assert.AreEqual(0, world.Children.Count);
        Assert.AreEqual(0, live.Count);
        Assert.AreEqual(2, pool.Count);
    }

    [TestMethod]
    public void FxTick_PlaysOnceAndDies()
    {
        var f = new GameObject().Add(new SkillFx { Frames = new List<string> { "a", "b" } });
        f.Update(0.05f); // 50ms: the first frame still shows
        Assert.AreEqual(0, f.Frame);
        f.Update(0.05f); // 100ms: next frame
        Assert.AreEqual(1, f.Frame);
        Assert.IsFalse(f.Owner.Dead);
        f.Update(0.1f); // past the last frame: done
        Assert.IsTrue(f.Owner.Dead);
        Assert.AreEqual(2, f.Frame); // one past the end: the collect must run before the draw
        f.Reset(); // a pooled effect comes back clean
        Assert.IsFalse(f.Owner.Dead);
        Assert.AreEqual(0, f.Frame);
    }

    [TestMethod]
    public void FxTick_LoopWrapsForever()
    {
        var f = new GameObject().Add(new SkillFx { Loop = true, Frames = new List<string> { "a", "b" } });
        f.Update(0.1f);
        Assert.AreEqual(1, f.Frame);
        f.Update(0.1f); // the second frame wraps back to the first
        Assert.AreEqual(0, f.Frame);
        Assert.IsFalse(f.Owner.Dead);
    }

    [TestMethod]
    public void FxTick_LoopRidesThePlayer()
    {
        var rider = new PlayerMover { X = 100f, Y = 200f };
        var f = new GameObject().Add(new SkillFx { Loop = true, Frames = new List<string> { "a" }, Rider = rider });
        f.Update(0.1f);
        Assert.AreEqual(100f, f.X);
        Assert.AreEqual(200f - SkillFx.CastDy, f.Y);
        rider.X = 150f; // it follows every frame, not just the first
        f.Update(0.1f);
        Assert.AreEqual(150f, f.X);
        f.Reset(); // the pool hands a stripped loop back as a one-shot: no rider, no wrap
        Assert.IsFalse(f.Loop);
        Assert.IsNull(f.Rider);
        Assert.AreEqual(0f, f.Timer, 0.001f);
    }

    [TestMethod]
    public void FxStripLoops_TakesOnlyThatBuffOutOfTheTree()
    {
        var world = new GameObject();
        var pool = new Stack<SkillFx>();
        var live = new List<SkillFx>();
        var a = new GameObject().Add(new SkillFx { Skill = 1, Loop = true, Frames = new List<string> { "a" } });
        var b = new GameObject().Add(new SkillFx { Skill = 2, Loop = true, Frames = new List<string> { "a" } });
        var cast = new GameObject().Add(new SkillFx { Skill = 1, Frames = new List<string> { "a" } });
        foreach (var f in new[] { a, b, cast })
        {
            world.AddChild(f.Owner);
            live.Add(f);
        }
        SkillFx.StripLoops(live, world, pool, 1);
        Assert.AreEqual(2, world.Children.Count); // the node leaves the tree, or the next spawn adds it twice
        Assert.AreEqual(2, live.Count); // the cast of the same skill keeps playing
        Assert.AreSame(a, pool.Pop());
        Assert.AreSame(b, live[0]);
        Assert.AreSame(cast, live[1]);
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

    [TestMethod]
    public void PeerArt_NewFromLookTable_KeepsLookBeforeState()
    {
        var look = new AppearanceEvent(7, 4, 30020, 20000,
            new[] { 1701000, 1040001 }, new[] { 1302007, 1040015 });
        var p = new PeerArt(7, new Dictionary<int, AppearanceEvent> { [7] = look });
        Assert.AreEqual(7, p.Id);
        Assert.AreEqual(4, p.Skin);
        Assert.AreEqual(30020, p.Hair);
        Assert.AreEqual(20000, p.Face);
        CollectionAssert.AreEqual(new[] { 1701000, 1040001 }, p.CashWorn);
        CollectionAssert.AreEqual(new[] { 1302007, 1040015 }, p.Equips);
    }

    [TestMethod]
    public void AddGearParts_UnknownId_AddsNothing()
    {
        var parts = new List<CharaPart>();
        MushTownGame.AddGearParts(parts, 999999, "stand1", 0, new Dictionary<(string, string), CharaData>());
        Assert.AreEqual(0, parts.Count);
    }
}
