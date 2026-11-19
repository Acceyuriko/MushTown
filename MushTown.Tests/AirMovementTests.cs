using MushTown.Shared;

namespace MushTown.Tests;

// The air moves of one airtime (Mana Wave): a boost and a slow fall, each once per airtime, reset on the ground or a ladder.
// The numbers here are the ones the Client reads from WZ: boost y 1200, slow fall time 5 s and v 95 (units unverified).
[TestClass]
public sealed class AirMovementTests
{
    private const float Dt = 1f / 60f;
    private const float BoostSpeed = 1200f;
    private const float FloatSeconds = 5f;
    private const float FloatFall = 95f;

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

    // The slow fall: the fall speed is capped at v while the Jump key is held.
    [TestMethod]
    public void Float_CapsTheFallSpeed_WhileJumpIsHeld()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 0;
        Assert.IsTrue(air.CanFloat);
        air.Float(FloatSeconds, FloatFall);
        Run(world, mover, Keys(jumpHeld: true), 60); // one second
        Assert.IsTrue(air.Floating);
        Assert.AreEqual(FloatFall, mover.Vy, 0.5f);
        Assert.AreEqual(FloatFall, mover.Y, 5f); // about 95 px in a second
    }

    // Let go of Jump: the fall speeds up again, and it cannot start again in the same airtime.
    [TestMethod]
    public void Float_EndsWhenJumpIsReleased_AndOnlyOncePerAirtime()
    {
        var (world, air, mover) = Rig();
        mover.Ground = null;
        mover.Y = 0;
        air.Float(FloatSeconds, FloatFall);
        Run(world, mover, Keys(jumpHeld: true), 30);
        Run(world, mover, Keys(jumpHeld: false), 30);
        Assert.IsFalse(air.Floating);
        Assert.IsTrue(mover.Vy > FloatFall * 2); // falling freely again
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
        air.Float(FloatSeconds, FloatFall);
        Run(world, mover, Keys(jumpHeld: true), 4 * 60);
        Assert.IsTrue(air.Floating);
        Run(world, mover, Keys(jumpHeld: true), 2 * 60);
        Assert.IsFalse(air.Floating);
        Assert.IsTrue(mover.Vy > FloatFall * 2);
    }

    [TestMethod]
    public void Float_NotOnTheGround_AndResetsOnLanding()
    {
        var (world, air, mover) = Rig();
        Assert.IsFalse(air.CanFloat); // on the ground
        mover.Ground = null;
        mover.Y = 500;
        air.Float(FloatSeconds, FloatFall);
        mover.Input = Keys(jumpHeld: true);
        for (int i = 0; i < 200 && mover.Ground == null; i++) TreeUpdate.UpdateObject(world, Dt); // 100 px at 95 px/s: lands
        Assert.IsNotNull(mover.Ground);
        Assert.IsTrue(air.CanBoost); // the next airtime is fresh (the Client asks before the step)
        Assert.IsFalse(air.Floating);
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
        air.Float(FloatSeconds, FloatFall);
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
        air.Float(FloatSeconds, FloatFall);
        mover.Climb = Map().Ladders[0];
        mover.Vy = 0;
        Assert.IsFalse(air.CanBoost); // on the rope: no boost, but the counters are fresh
        mover.Climb = null;
        Assert.IsTrue(air.CanBoost);
        Assert.IsTrue(air.CanFloat);
    }
}
