using MushTown.Shared;

namespace MushTown.Tests;

// The air moves of one airtime (Mana Wave): a boost and a slow fall, each once per airtime, reset on the ground or a ladder.
// The numbers here are the ones the Client reads from WZ: boost y 1200, slow fall time 5 s, u 25 and v 95 (units unverified).
[TestClass]
public sealed class AirMovementTests
{
    private const float Dt = 1f / 60f;
    private const float BoostSpeed = 1200f;
    private const float FloatSeconds = 5f;
    private const float FloatFall = 95f;
    private const float FloatPull = 25f;

    private static MapData Map() => new()
    {
        Info = new MapInfo { VrLeft = 0, VrTop = -2000, VrRight = 1000, VrBottom = 800 },
        Footholds = new List<FootholdItem> { new() { X1 = 0, Y1 = 600, X2 = 1000, Y2 = 600 } },
        Ladders = new List<LadderItem> { new() { X = 500, Y1 = 300, Y2 = 600, Ladder = true } },
    };

    private static (GameObject world, AirMovement air, PlayerMover mover) Rig()
    {
        var map = Map();
        var world = new GameObject();
        var air = new AirMovement();
        var mover = new PlayerMover { Map = map, X = 100, Y = 600 };
        mover.Ground = SceneMath.GroundBelow(map.Footholds, 100, 600);
        var player = new GameObject();
        player.Add(air); // before the mover, as the Client adds them
        player.Add(mover);
        world.AddChild(player);
        return (world, air, mover);
    }

    private static PlayerInput Keys(bool jumpHeld = false, bool up = false) =>
        new() { Walk = PlayerMover.WalkSpeed, Jump = PlayerMover.JumpV0, JumpHeld = jumpHeld, Up = up };

    private static void Run(GameObject world, PlayerMover mover, PlayerInput input, int frames)
    {
        mover.Input = input;
        for (int i = 0; i < frames; i++) TreeUpdate.UpdateObject(world, Dt);
    }

    // Peak height of the boost: speed^2 / 2 / gravity = 400 px (the plain jump is 75).
    [TestMethod]
    public void Boost_OnTheGround_ShootsUp_ToFourHundredPixels()
    {
        var (world, air, mover) = Rig();
        Assert.IsTrue(air.CanBoost);
        air.Boost(BoostSpeed);
        Assert.IsNull(mover.Ground);
        float top = mover.Y;
        mover.Input = Keys();
        for (int i = 0; i < 120 && mover.Ground == null; i++)
        {
            TreeUpdate.UpdateObject(world, Dt);
            top = Math.Min(top, mover.Y);
        }
        Assert.AreEqual(600 - 400, top, 15f);
        Assert.IsNotNull(mover.Ground); // and it came back down
    }

    [TestMethod]
    public void Boost_OncePerAirtime_ResetsOnTheGround()
    {
        var (world, air, mover) = Rig();
        air.Boost(BoostSpeed);
        Assert.IsTrue(air.BoostUsed);
        Assert.IsFalse(air.CanBoost); // in the air, used
        Run(world, mover, Keys(), 120); // lands
        Assert.IsNotNull(mover.Ground);
        Assert.IsTrue(air.CanBoost); // a new airtime
        Assert.IsFalse(air.BoostUsed);
    }

    // The case "Jump pressed in the air also triggers it": after a plain jump the boost is still there, once.
    [TestMethod]
    public void Boost_InTheAir_AfterAPlainJump_ThenNoSecondOne()
    {
        var (world, air, mover) = Rig();
        Run(world, mover, Keys(jumpHeld: true), 1); // the mover's own jump
        Assert.IsNull(mover.Ground);
        Run(world, mover, Keys(), 6);
        Assert.IsTrue(air.CanBoost);
        float vy = mover.Vy;
        air.Boost(BoostSpeed);
        Assert.AreEqual(-BoostSpeed, mover.Vy);
        Assert.IsTrue(mover.Vy < vy);
        Assert.IsFalse(air.CanBoost);
    }

    [TestMethod]
    public void Boost_NotOnALadder()
    {
        var (_, air, mover) = Rig();
        mover.Ground = null;
        mover.Climb = Map().Ladders[0];
        Assert.IsFalse(air.CanBoost);
    }

    // The slow fall starts from rest, speeds up slowly (u px/s^2: slow at first, faster later) and stops speeding up at v.
    [TestMethod]
    public void Float_StartsFromRest_SpeedsUpSlowly_AndStopsAtTheCap()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = -2000;
        mover.Vy = 700; // a fast fall before: lost when the slow fall starts
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Assert.AreEqual(0f, mover.Vy);
        float[] speed = new float[5];
        float[] drop = new float[5];
        float start = mover.Y;
        for (int s = 0; s < 5; s++)
        {
            Run(world, mover, Keys(jumpHeld: true), 60);
            speed[s] = mover.Vy;
            drop[s] = mover.Y - start;
        }
        Assert.AreEqual(FloatPull, speed[0], 1f); // 1 s in: u
        Assert.AreEqual(2 * FloatPull, speed[1], 1f);
        Assert.AreEqual(3 * FloatPull, speed[2], 1f);
        Assert.AreEqual(FloatFall, speed[3], 0.5f); // 4 s in: 100 would be due, the cap holds it at v
        Assert.AreEqual(FloatFall, speed[4], 0.5f);
        Assert.AreEqual(FloatPull / 2, drop[0], 1f); // the first second falls 12.5 px
        Assert.IsTrue(drop[1] - drop[0] > drop[0] * 2); // each later second falls more than the one before, until the cap
        Assert.IsTrue(drop[2] - drop[1] > drop[1] - drop[0]);
        Assert.IsTrue(drop[3] - drop[2] > drop[2] - drop[1]);
        Assert.IsTrue(air.Floating);
    }

    // Let go of Jump: the fall speeds up again, and it cannot start again in the same airtime.
    [TestMethod]
    public void Float_EndsWhenJumpIsReleased_AndOnlyOncePerAirtime()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 0;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Run(world, mover, Keys(jumpHeld: true), 30);
        Run(world, mover, Keys(jumpHeld: false), 30);
        Assert.IsFalse(air.Floating);
        Assert.IsTrue(mover.Vy > FloatFall * 2); // falling freely again
        Assert.AreEqual(FloatEnd.Released, air.EndedBy);
        Assert.IsFalse(air.CanFloat); // used
        Run(world, mover, Keys(jumpHeld: true), 5);
        Assert.IsFalse(air.Floating); // holding Jump again does nothing without a new start
    }

    // The time limit: 5 seconds, then the cap is gone even with Jump held.
    [TestMethod]
    public void Float_EndsAfterItsTime()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = -1000;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Run(world, mover, Keys(jumpHeld: true), 4 * 60);
        Assert.IsTrue(air.Floating);
        Run(world, mover, Keys(jumpHeld: true), 2 * 60);
        Assert.IsFalse(air.Floating);
        Assert.AreEqual(FloatEnd.TimedOut, air.EndedBy);
        Assert.IsTrue(mover.Vy > FloatFall * 2);
    }

    [TestMethod]
    public void Float_NotOnTheGround_AndResetsOnLanding()
    {
        var (world, air, mover) = Rig();
        Assert.IsFalse(air.CanFloat); // on the ground
        mover.Ground = null;
        mover.Y = 500;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        mover.Input = Keys(jumpHeld: true);
        for (int i = 0; i < 400 && mover.Ground == null; i++) TreeUpdate.UpdateObject(world, Dt); // 100 px at the slow pull (about 2.8 s): lands
        Assert.IsNotNull(mover.Ground);
        Assert.IsTrue(air.CanBoost); // the next airtime is fresh (the Client asks before the step)
        Assert.IsFalse(air.Floating);
        Assert.AreEqual(FloatEnd.Landed, air.EndedBy);
        Assert.IsFalse(air.FloatUsed);
        Run(world, mover, Keys(), 1);
        Assert.AreEqual(PlayerMover.MaxFall, mover.FallCap);
    }

    // Boost and slow fall are two moves: using one does not use the other.
    [TestMethod]
    public void BoostAndFloat_AreCountedApart()
    {
        var (world, air, mover) = Rig();
        air.Boost(BoostSpeed);
        Run(world, mover, Keys(jumpHeld: true), 5);
        Assert.IsTrue(air.CanFloat);
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Assert.IsFalse(air.CanBoost);
        Assert.IsFalse(air.CanFloat);
    }

    // A ladder starts a new airtime too.
    [TestMethod]
    public void Ladder_ResetsBoostAndFloat()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 400;
        air.Boost(BoostSpeed);
        air.Float(FloatSeconds, FloatFall, FloatPull);
        mover.Climb = Map().Ladders[0];
        mover.Vy = 0;
        Assert.IsFalse(air.CanBoost); // on the rope: no boost, but the counters are fresh
        mover.Climb = null;
        Assert.IsTrue(air.CanBoost);
        Assert.IsTrue(air.CanFloat);
    }

    // The poses (WZ Character 00002000.img): the boost is alert for 30 ms (two steps) and then the jump pose; the slow fall is fly.
    [TestMethod]
    public void Poses_BoostIsAlertForTwoSteps_SlowFallIsFly_ThenJump()
    {
        var (world, air, mover) = Rig();
        air.Boost(BoostSpeed);
        var shown = new List<string>();
        mover.Input = Keys();
        for (int i = 0; i < 4; i++)
        {
            TreeUpdate.UpdateObject(world, Dt);
            shown.Add(mover.Action);
        }
        CollectionAssert.AreEqual(new[] { "alert", "alert", "jump", "jump" }, shown);
        mover.Vy = 0;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Run(world, mover, Keys(jumpHeld: true), 3);
        Assert.AreEqual("fly", mover.Action);
        Run(world, mover, Keys(jumpHeld: false), 1);
        Assert.AreEqual("jump", mover.Action);
    }

    // The bubble of the slow fall on the player object; the owner switches Active as the Client does (after each step, from the air state).
    private static void RunWithBubble(GameObject world, AirMovement air, PlayerMover mover, FloatBubble bubble, int frames, bool held)
    {
        for (int i = 0; i < frames; i++)
        {
            mover.Input = Keys(jumpHeld: held);
            TreeUpdate.UpdateObject(world, Dt);
            bubble.Active = air.Floating;
        }
    }

    // Start: the clock starts at 0 and runs while it floats. Release: Ended is set once, the clock stops.
    [TestMethod]
    public void Bubble_PlaysWhileFloating_AndEndsOnceOnRelease()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = -2000;
        var bubble = mover.Owner.Add(new FloatBubble());
        Assert.IsFalse(bubble.Active);
        air.Float(FloatSeconds, FloatFall, FloatPull);
        bubble.Active = air.Floating;
        Assert.IsTrue(bubble.Active);
        Assert.AreEqual(0f, bubble.Ms);
        RunWithBubble(world, air, mover, bubble, 30, true); // half a second
        Assert.AreEqual(500f, bubble.Ms, 40f);
        Assert.IsFalse(bubble.Ended);
        RunWithBubble(world, air, mover, bubble, 1, false);
        Assert.IsFalse(bubble.Active);
        Assert.IsTrue(bubble.TakeEnded());
        Assert.IsFalse(bubble.TakeEnded()); // once
        Assert.AreEqual(FloatEnd.Released, air.EndedBy);
        float ms = bubble.Ms;
        RunWithBubble(world, air, mover, bubble, 10, false);
        Assert.AreEqual(ms, bubble.Ms); // the clock stopped
    }

    // Timeout: held through the 5 s limit, the bubble ends after 5 s, not before.
    [TestMethod]
    public void Bubble_EndsAtTheFiveSecondLimit()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = -2000;
        var bubble = mover.Owner.Add(new FloatBubble());
        air.Float(FloatSeconds, FloatFall, FloatPull);
        bubble.Active = air.Floating;
        RunWithBubble(world, air, mover, bubble, 4 * 60 + 50, true); // 4.83 s
        Assert.IsTrue(bubble.Active);
        Assert.IsFalse(bubble.Ended);
        RunWithBubble(world, air, mover, bubble, 20, true); // 5.17 s
        Assert.IsFalse(bubble.Active);
        Assert.IsTrue(bubble.TakeEnded());
        Assert.AreEqual(FloatEnd.TimedOut, air.EndedBy);
    }

    // A landing ends the bubble too, but the reason says Landed: the Client plays no end effect for it.
    [TestMethod]
    public void Bubble_Landing_EndsWithTheLandedReason()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 560;
        var bubble = mover.Owner.Add(new FloatBubble());
        air.Float(FloatSeconds, FloatFall, FloatPull);
        bubble.Active = air.Floating;
        for (int i = 0; i < 300 && mover.Ground == null; i++) RunWithBubble(world, air, mover, bubble, 1, true); // 40 px at the slow pull: about 1.8 s
        Assert.IsNotNull(mover.Ground);
        RunWithBubble(world, air, mover, bubble, 1, false); // the next step sees the ground
        Assert.IsFalse(bubble.Active);
        Assert.IsTrue(bubble.TakeEnded());
        Assert.AreEqual(FloatEnd.Landed, air.EndedBy);
    }

    // A boost during a slow fall ends it: no longer floating, the pose and the pull are back to the plain air. The reason
    // stays None (no release and no time limit ended it).
    [TestMethod]
    public void Boost_DuringASlowFall_EndsIt_WithNoReason()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 0;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Run(world, mover, Keys(jumpHeld: true), 10);
        Assert.IsTrue(air.Floating);
        air.Boost(BoostSpeed);
        Assert.IsFalse(air.Floating);
        Assert.AreEqual(FloatEnd.None, air.EndedBy);
        Assert.AreEqual(-BoostSpeed, mover.Vy);
        Run(world, mover, Keys(jumpHeld: true), 1);
        Assert.AreEqual("alert", mover.Action); // the boost pose, not fly
        Assert.AreEqual(PlayerMover.Gravity, mover.FallAccel);
    }

    // A slow fall started while the hero still rises stops the rise on the spot (the speed is set to 0, not eased).
    [TestMethod]
    public void Float_StartedWhileRising_StopsTheRiseAtOnce()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 0;
        mover.Vy = -500;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Assert.AreEqual(0f, mover.Vy);
        float y = mover.Y;
        Run(world, mover, Keys(jumpHeld: true), 1);
        Assert.IsTrue(mover.Y >= y); // it does not rise another step
        Assert.IsTrue(mover.Vy > 0 && mover.Vy < 1);
    }

    // Catching a ladder in a slow fall ends it as a landing.
    [TestMethod]
    public void Float_EndsAsLandedOnALadder()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 400;
        air.Float(FloatSeconds, FloatFall, FloatPull);
        Assert.IsTrue(air.Floating);
        mover.Climb = Map().Ladders[0];
        Run(world, mover, Keys(jumpHeld: true), 1);
        Assert.IsFalse(air.Floating);
        Assert.AreEqual(FloatEnd.Landed, air.EndedBy);
    }
}
