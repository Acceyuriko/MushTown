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
