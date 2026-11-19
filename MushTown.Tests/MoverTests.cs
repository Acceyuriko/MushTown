using MushTown.Shared;

namespace MushTown.Tests;

// The headless feet: walk, wall, jump-land, portal, ladder, drop-through,
// swing lock, void fall, plus world spawn/despawn. Flat test map: ground
// at y=300, a lower floor at y=400, a ladder at x=500, a portal at (100,300).
[TestClass]
public sealed class MoverTests
{
    private const float Dt = 1f / 60f;

    private static MapData TestMap() => new()
    {
        Info = new MapInfo { VrLeft = 0, VrTop = 0, VrRight = 1000, VrBottom = 600 },
        Footholds = new List<FootholdItem>
        {
            new() { X1 = 0, Y1 = 300, X2 = 1000, Y2 = 300 },
            new() { X1 = 0, Y1 = 400, X2 = 1000, Y2 = 400 },
        },
        Portals = new List<PortalItem>
        {
            new() { Pn = "0", X = 100, Y = 300, Tm = 20000, Tn = "1" },
        },
        Ladders = new List<LadderItem>
        {
            new() { X = 500, Y1 = 200, Y2 = 300, Ladder = true },
        },
    };

    private static PlayerMover Mover(MapData map, float x, float y)
    {
        var m = new PlayerMover { Map = map, X = x, Y = y };
        m.Ground = SceneMath.GroundBelow(map.Footholds, (int)x, (int)y);
        return m;
    }

    private static PlayerInput Input(int dir = 0) => new() { Dir = dir, Walk = PlayerMover.WalkSpeed, Jump = PlayerMover.JumpV0 };

    [TestMethod]
    public void Walk_Right_MovesAndFaces()
    {
        var map = TestMap();
        var m = Mover(map, 100, 300);
        var world = new GameObject();
        var player = new GameObject();
        player.Add(m);
        world.AddChild(player);
        m.Input = Input(1);
        TreeUpdate.UpdateObject(world, 1f); // the real path the Client steps
        Assert.AreEqual(240f, m.X);
        Assert.IsTrue(m.FacingRight);
        Assert.AreEqual("walk1", m.Action);
        Assert.IsNotNull(m.Ground);
        world.Enabled = false;
        TreeUpdate.UpdateObject(world, 1f);
        Assert.AreEqual(240f, m.X); // a disabled root skips the step
    }

    [TestMethod]
    public void Walk_IntoWall_ClampsAndKillsMomentum()
    {
        var map = TestMap();
        var m = Mover(map, 990, 300);
        m.Input = Input(1);
        m.Update(1f);
        Assert.AreEqual(1000f, m.X);
        Assert.AreEqual(0f, m.Vx);
    }

    // A wall (a vertical foothold) whose span holds the feet height stops the walk 1 px before it. The hero keeps its
    // ground and has no momentum (before: it walked into the cliff and fell into the void).
    [TestMethod]
    public void Walk_IntoWallOnTheGround_StopsBeforeIt()
    {
        var map = TestMap();
        map.Footholds.Add(new() { X1 = 600, Y1 = 240, X2 = 600, Y2 = 300 });
        var m = Mover(map, 590, 300);
        m.Input = Input(1);
        m.Update(1f);
        Assert.AreEqual(599f, m.X);
        Assert.AreEqual(0f, m.Vx);
        Assert.IsNotNull(m.Ground);
        Assert.AreEqual(300f, m.Y);
    }

    // A wall whose top is at the feet (the edge of a floor) or whose bottom is above the feet (an end cap) does not stop it.
    [TestMethod]
    public void Walk_PastWallTopOrEndCap_IsNotStopped()
    {
        var map = TestMap();
        map.Footholds.Add(new() { X1 = 600, Y1 = 300, X2 = 600, Y2 = 340 }); // top at the feet
        map.Footholds.Add(new() { X1 = 700, Y1 = 250, X2 = 700, Y2 = 299 }); // cap above the feet
        var m = Mover(map, 590, 300);
        m.Input = Input(1);
        m.Update(1f);
        Assert.AreEqual(590 + PlayerMover.WalkSpeed, m.X, 0.01f);
        var m2 = Mover(map, 690, 300);
        m2.Input = Input(1);
        m2.Update(1f);
        Assert.AreEqual(690 + PlayerMover.WalkSpeed, m2.X, 0.01f);
    }

    // Map 1020000, x 497: a 1 px vertical foothold joins two slopes. It is a seam in the floor: the walk goes on over it
    // (the same shape shifted down by 500, so it fits the test map). A 60 px wall still stops the walk (above).
    [TestMethod]
    public void Walk_Over1PxWallBetweenSlopes_IsNotStopped()
    {
        var map = TestMap();
        map.Footholds = new List<FootholdItem>
        {
            new() { X1 = 400, Y1 = 314, X2 = 495, Y2 = 314 },
            new() { X1 = 495, Y1 = 314, X2 = 497, Y2 = 313 },
            new() { X1 = 497, Y1 = 313, X2 = 497, Y2 = 314 },
            new() { X1 = 497, Y1 = 314, X2 = 534, Y2 = 297 },
        };
        var m = Mover(map, 490, 314);
        m.Input = Input(1);
        for (int i = 0; i < 12; i++) m.Update(0.1f / 6);
        Assert.IsTrue(m.X > 498f, $"stopped at {m.X}");
        Assert.IsNotNull(m.Ground);
    }

    // The pair of the 1 px seam above: a 2 px wall is a wall and stops the walk 1 px before it.
    [TestMethod]
    public void Walk_Into2PxWall_StopsBeforeIt()
    {
        var map = TestMap();
        map.Footholds = new List<FootholdItem>
        {
            new() { X1 = 400, Y1 = 314, X2 = 497, Y2 = 314 },
            new() { X1 = 497, Y1 = 312, X2 = 497, Y2 = 314 },
            new() { X1 = 497, Y1 = 312, X2 = 600, Y2 = 312 },
        };
        var m = Mover(map, 490, 314);
        m.Input = Input(1);
        for (int i = 0; i < 12; i++) m.Update(0.1f / 6);
        Assert.AreEqual(496f, m.X);
    }

    // The wall rule in the air: a jump with the key held toward a 60 px cliff is stopped by the cliff face while the feet
    // are below its top, passes once they are above it, and lands on the plateau.
    [TestMethod]
    public void Jump_TowardLowCliff_HeldKey_LandsOnThePlateau()
    {
        var map = TestMap();
        map.Footholds.Add(new() { X1 = 600, Y1 = 240, X2 = 600, Y2 = 300 }); // the cliff face
        map.Footholds.Add(new() { X1 = 600, Y1 = 240, X2 = 1000, Y2 = 240 }); // the plateau
        var m = Mover(map, 590, 300);
        var jump = Input(1);
        jump.JumpHeld = true;
        m.Input = jump;
        m.Update(Dt);
        Assert.IsNull(m.Ground);
        m.Input = Input(1);
        for (int i = 0; i < 120 && m.Ground == null; i++) m.Update(Dt);
        Assert.IsNotNull(m.Ground);
        Assert.AreEqual(240f, m.Y);
        Assert.IsTrue(m.X > 600f, $"x {m.X}");
    }

    // A Teleport from a rope (the user's rule): the landing is the usual one, and the feet leave the rope. A held Up must not
    // grab the rope again at once (the landing is at the rope's own top).
    [TestMethod]
    public void Teleport_FromARope_LeavesItAndHeldUpDoesNotGrabItBack()
    {
        var map = TestMap();
        map.Footholds = new List<FootholdItem>
        {
            new() { X1 = 0, Y1 = 200, X2 = 1000, Y2 = 200 },
            new() { X1 = 0, Y1 = 300, X2 = 1000, Y2 = 300 },
        };
        var m = new PlayerMover { Map = map, X = 500, Y = 250, Climb = map.Ladders[0] };
        var land = TeleportRules.ForKeys(map, m.X, m.Y, true, false, 0, 190, 295)!.Value; // Up: the ground above
        Assert.AreEqual(200, land.Y);
        m.Land(land.X, land.Y, land.Ground);
        Assert.IsNull(m.Climb);
        Assert.AreSame(land.Ground, m.Ground);
        m.Input = Input() with { Up = true };
        for (int i = 0; i < 10; i++) m.Update(Dt);
        Assert.IsNull(m.Climb);
        Assert.AreEqual(200f, m.Y);
    }

    [TestMethod]
    public void StopBeforeWall_BothDirections_AndNoWall()
    {
        var walls = new List<FootholdItem> { new() { X1 = 100, Y1 = 40, X2 = 100, Y2 = 100 } };
        Assert.AreEqual(99f, SceneMath.StopBeforeWall(walls, 0, 190, 100));
        Assert.AreEqual(101f, SceneMath.StopBeforeWall(walls, 200, 10, 100));
        Assert.AreEqual(190f, SceneMath.StopBeforeWall(walls, 0, 190, 39)); // above the wall
        Assert.AreEqual(50f, SceneMath.StopBeforeWall(walls, 0, 50, 100)); // the wall is farther than the target
        Assert.AreEqual(190f, SceneMath.StopBeforeWall(walls, 100, 190, 100)); // standing on the wall x: it is not ahead, no stop
    }

    // WZ cuts a tall wall into pieces: they are one wall (the piece at the feet is only the low part of it), and step lets
    // the caller step over a wall that rises no more than that above the feet.
    [TestMethod]
    public void StopBeforeWall_PiecesAreOneWall_AndStepJumpsLowOnes()
    {
        var wall = new List<FootholdItem> { new() { X1 = 100, Y1 = 100, X2 = 100, Y2 = 60 }, new() { X1 = 100, Y1 = 60, X2 = 100, Y2 = 20 } };
        Assert.AreEqual(99f, SceneMath.StopBeforeWall(wall, 0, 190, 100)); // 80 high in two pieces
        Assert.AreEqual(99f, SceneMath.StopBeforeWall(wall, 0, 190, 100, 75)); // 80 high is above the step
        Assert.AreEqual(190f, SceneMath.StopBeforeWall(wall, 0, 190, 100, 80)); // top 20 = feet - 80: stepped over
        Assert.AreEqual(190f, SceneMath.StopBeforeWall(wall, 0, 190, 20)); // the feet at the top of the wall: it is the edge of the platform on top
    }

    [TestMethod]
    public void Jump_RisesThenLandsBack()
    {
        var map = TestMap();
        var m = Mover(map, 100, 300);
        var jump = Input();
        jump.JumpHeld = true;
        m.Input = jump;
        m.Update(Dt);
        Assert.IsNull(m.Ground);
        Assert.IsTrue(m.JumpSfx);
        m.Input = Input();
        for (int i = 0; i < 120 && m.Ground == null; i++) m.Update(Dt);
        Assert.AreEqual(300f, m.Y);
        Assert.IsNotNull(m.Ground);
    }

    [TestMethod]
    public void Portal_UpTap_SetsTravelThenCooldownBlocks()
    {
        var map = TestMap();
        var m = Mover(map, 100, 300);
        var up = Input();
        up.Up = true;
        up.UpPressed = true;
        m.Input = up;
        m.Update(Dt);
        Assert.AreEqual(map.Portals[0], m.Travel);
        m.OnTravel(); // the Client switches maps, then the block holds
        m.Input = up;
        m.Update(Dt);
        Assert.IsNull(m.Travel);
    }

    [TestMethod]
    public void Ladder_Up_GrabsAndClimbs()
    {
        var map = TestMap();
        var m = Mover(map, 500, 300);
        var up = Input();
        up.Up = true;
        m.Input = up;
        m.Update(Dt);
        Assert.AreEqual(map.Ladders[0], m.Climb);
        Assert.AreEqual(500f, m.X);
        Assert.AreEqual("ladder", m.Action);
        m.Update(Dt);
        Assert.IsTrue(m.Y < 300f);
    }

    [TestMethod]
    public void LadderTopPlatform_ReturnsTop_NotBottomGround()
    {
        var map = TestMap();
        Assert.IsNull(PlayerMover.LadderTopPlatform(map, map.Ladders[0])); // the ground at Y2 is the ladder's own bottom
        var platform = new FootholdItem { X1 = 450, Y1 = 198, X2 = 550, Y2 = 198 };
        map.Footholds.Insert(0, platform);
        Assert.AreEqual(platform, PlayerMover.LadderTopPlatform(map, map.Ladders[0]));
    }

    [TestMethod]
    public void Ladder_Top_StopsOnPlatformBelowTallTip()
    {
        var map = TestMap();
        map.Footholds.Insert(0, new FootholdItem { X1 = 450, Y1 = 100, X2 = 550, Y2 = 100 });
        map.Ladders[0] = new LadderItem { X = 500, Y1 = 0, Y2 = 300, Ladder = true };
        var m = Mover(map, 500, 300);
        var up = Input();
        up.Up = true;
        m.Input = up;
        for (int i = 0; i < 300 && m.Climb == null; i++) // grab on
        {
            m.Update(Dt);
            Assert.IsTrue(m.Y >= 100f, $"grab frame {i}: Y={m.Y}");
        }
        for (int i = 0; i < 300 && m.Climb != null; i++) // climb to the platform
        {
            m.Update(Dt);
            Assert.IsTrue(m.Y >= 100f, $"climb frame {i}: Y={m.Y}");
        }
        Assert.IsNull(m.Climb);
        Assert.AreEqual(map.Footholds[0], m.Ground);
        Assert.AreEqual(100f, m.Y); // not the ladder tip at 0
    }

    [TestMethod]
    public void Ladder_Top_LandsOnPlatformAboveTip()
    {
        var map = TestMap(); // the usual ladder: the platform is 2px above the tip
        map.Footholds.Insert(0, new FootholdItem { X1 = 450, Y1 = 198, X2 = 550, Y2 = 198 });
        var m = Mover(map, 500, 300);
        var up = Input();
        up.Up = true;
        m.Input = up;
        for (int i = 0; i < 300 && m.Climb == null; i++) m.Update(Dt); // grab on
        for (int i = 0; i < 300 && m.Climb != null; i++) m.Update(Dt); // climb to the top
        Assert.IsNull(m.Climb);
        Assert.AreEqual(map.Footholds[0], m.Ground);
        Assert.AreEqual(198f, m.Y); // landed on the platform above the tip
    }

    [TestMethod]
    public void Ladder_Top_IgnoresGroundAtLadderBottom()
    {
        var map = TestMap(); // the ladder ends 100px above its own bottom ground
        var m = Mover(map, 500, 300);
        var up = Input();
        up.Up = true;
        m.Input = up;
        for (int i = 0; i < 300 && m.Climb == null; i++) m.Update(Dt); // grab on
        for (int i = 0; i < 300 && m.Y > map.Ladders[0].Y1; i++) m.Update(Dt); // climb to the tip
        Assert.AreEqual(map.Ladders[0], m.Climb); // the bottom ground is not a platform: stay on the ladder
        Assert.AreEqual(200f, m.Y);
    }

    [TestMethod]
    public void Drop_DownJump_LandsOnLowerFloor()
    {
        var map = TestMap();
        var m = Mover(map, 100, 300);
        var drop = Input();
        drop.Down = true;
        drop.JumpPressed = true;
        m.Input = drop;
        m.Update(Dt);
        Assert.IsNull(m.Ground);
        m.Input = Input();
        for (int i = 0; i < 120 && m.Ground == null; i++) m.Update(Dt);
        Assert.AreEqual(400f, m.Y);
    }

    [TestMethod]
    public void Locked_FeetPlantedAndSwingPose()
    {
        var map = TestMap();
        var m = Mover(map, 100, 300);
        var locked = Input(1);
        locked.Locked = true;
        m.Input = locked;
        m.Update(Dt);
        Assert.AreEqual(100f, m.X);
        Assert.AreEqual("swingO1", m.Action);
    }

    [TestMethod]
    public void Locked_PortalUpTap_DoesNotTravel()
    {
        var map = TestMap();
        var m = Mover(map, 100, 300);
        var up = Input();
        up.Up = true;
        up.UpPressed = true;
        up.Locked = true;
        m.Input = up;
        m.Update(Dt);
        Assert.IsNull(m.Travel);
    }

    [TestMethod]
    public void Locked_OnLadder_DoesNotClimb()
    {
        var map = TestMap();
        var m = Mover(map, 500, 300);
        var up = Input();
        up.Up = true;
        m.Input = up;
        m.Update(Dt); // grab on
        Assert.AreEqual(map.Ladders[0], m.Climb);
        float y = m.Y;
        var locked = Input();
        locked.Up = true;
        locked.Locked = true;
        m.Input = locked;
        m.Update(Dt);
        Assert.AreEqual(y, m.Y);
    }

    // Step with no keys held until the knock lands. A hit does not lock the player.
    private static int StepUntilLanded(PlayerMover m, out float topY, int dir = 0)
    {
        m.Input = Input(dir);
        topY = m.Y;
        int frames = 0;
        do
        {
            m.Update(Dt);
            topY = Math.Min(topY, m.Y);
            frames++;
        } while (m.Ground == null && frames < 120);
        return frames;
    }

    [TestMethod]
    public void Knock_FliesAnArcAwayFromTheSourceAndLands()
    {
        var map = TestMap();
        var m = Mover(map, 300, 300);
        m.FacingRight = true; // facing right, pushed right: the direction is the caller's, not the facing
        m.Knock(1);
        Assert.IsNull(m.Ground);
        int frames = StepUntilLanded(m, out float topY);
        Assert.IsTrue(frames > 10 && frames < 120, "the arc takes a while, then lands");
        Assert.IsTrue(topY < 300 - 10, "it leaves the ground, not a teleport");
        Assert.IsTrue(topY > 300 - 30, "a low hop");
        Assert.AreEqual(300f, m.Y);
        Assert.IsNotNull(m.Ground);
        Assert.IsTrue(m.X > 325 && m.X < 400, $"landed {m.X - 300}px away");
        float landed = m.X;
        m.Update(Dt);
        Assert.AreEqual(landed, m.X, "it stops after landing");
    }

    [TestMethod]
    public void Knock_LeftGoesLeft()
    {
        var map = TestMap();
        var m = Mover(map, 300, 300);
        m.Knock(-1);
        StepUntilLanded(m, out _);
        Assert.IsTrue(m.X < 270);
    }

    [TestMethod]
    public void Knock_ArcFollowsTheNormalAirRules()
    {
        var map = TestMap();
        var plain = Mover(map, 300, 300);
        plain.Knock(1);
        StepUntilLanded(plain, out _);
        var held = Mover(map, 300, 300);
        held.Knock(1);
        StepUntilLanded(held, out _, -1); // holding the other way in the air
        Assert.IsTrue(held.X < plain.X, "the held key drifts the arc, as in any jump");
        var jump = Mover(map, 300, 300);
        jump.Input = new PlayerInput { Dir = 1, JumpHeld = true, Walk = PlayerMover.WalkSpeed, Jump = PlayerMover.JumpV0 };
        jump.Update(Dt); // an ordinary jump takes off
        Assert.IsNull(jump.Ground);
        jump.Input = Input(-1);
        jump.Update(Dt);
        Assert.IsFalse(jump.FacingRight, "a normal jump turns in the air too");
    }

    [TestMethod]
    public void Knock_AttackLockStillPlaysTheSwingPose()
    {
        var map = TestMap();
        var m = Mover(map, 300, 300);
        m.Knock(1);
        var swing = Input();
        swing.Locked = true; // the Client sets this for the swing, not for the hit
        m.Input = swing;
        m.Update(Dt);
        Assert.IsNull(m.Ground, "the arc goes on under a swing");
        Assert.AreEqual("swingO1", m.Action);
    }

    [TestMethod]
    public void Knock_LandingEndsItAndTheKeysWork()
    {
        var map = TestMap();
        var m = Mover(map, 300, 300);
        m.Knock(1);
        StepUntilLanded(m, out _);
        m.Input = Input(1);
        m.Update(Dt);
        Assert.AreEqual("walk1", m.Action);
    }

    [TestMethod]
    public void Knock_PlayerCanJumpRightAfterLanding()
    {
        var map = TestMap();
        var m = Mover(map, 300, 300);
        m.Knock(1);
        StepUntilLanded(m, out _);
        var jump = Input();
        jump.JumpHeld = true;
        m.Input = jump;
        m.Update(Dt);
        Assert.IsNull(m.Ground);
    }

    [TestMethod]
    public void Knock_OnLadder_LeavesTheLadder()
    {
        var map = TestMap();
        var m = Mover(map, 500, 300);
        var up = Input();
        up.Up = true;
        m.Input = up;
        m.Update(Dt); // grab on
        Assert.AreEqual(map.Ladders[0], m.Climb);
        m.Knock(1);
        Assert.IsNull(m.Climb);
        for (int i = 0; i < 120 && (m.Ground == null || i == 0); i++) m.Update(Dt); // Up stays held
        Assert.IsNull(m.Climb, "a held Up must not grab the ladder back");
        Assert.IsNotNull(m.Ground);
        Assert.IsTrue(m.X > 500);
    }

    [TestMethod]
    public void VoidFall_BelowVrBottom_SetsFellOut()
    {
        var map = TestMap();
        var m = new PlayerMover { Map = map, X = 100, Y = 610 };
        m.Input = Input();
        m.Update(Dt);
        Assert.IsTrue(m.FellOut);
    }

    [TestMethod]
    public void World_SpawnDespawn_RemoveChild()
    {
        var world = new GameObject();
        var a = new GameObject();
        var b = new GameObject();
        world.AddChild(a);
        world.AddChild(b);
        Assert.IsTrue(world.RemoveChild(a));
        Assert.AreEqual(1, world.Children.Count);
        Assert.IsNull(a.Parent);
        Assert.IsFalse(world.RemoveChild(a));
    }
}
