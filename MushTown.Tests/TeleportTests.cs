using MushTown.Shared;

namespace MushTown.Tests;

// Where a Teleport lands (TeleportRules): the wall, the map edge, the empty air, the ground height.
[TestClass]
public class TeleportTests
{
    private static FootholdItem Fh(int x1, int y1, int x2, int y2) => new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };

    private static MapData Map(int vrLeft, int vrRight, params FootholdItem[] footholds) => new()
    {
        Info = new MapInfo { VrLeft = vrLeft, VrRight = vrRight, VrTop = -1000, VrBottom = 1000 },
        Footholds = footholds.ToList(),
    };

    // The step of map 40000: a floor at 215 up to x 231, a wall (two pieces, like WZ) 60 high, a floor at 155 after it.
    private static MapData Step() => Map(-260, 1160,
        Fh(-270, 215, 231, 215), Fh(231, 215, 231, 180), Fh(231, 180, 231, 155), Fh(231, 155, 1170, 155));

    [TestMethod]
    public void Horizontal_FlatGround_GoesTheWholeDistance()
    {
        var map = Map(-1000, 1000, Fh(-500, 100, 500, 100));
        var land = TeleportRules.Horizontal(map, 0, 100, 1, 190)!.Value;
        Assert.AreEqual(190f, land.X);
        Assert.AreEqual(100, land.Y);
        Assert.AreEqual(-190f, TeleportRules.Horizontal(map, 0, 100, -1, 190)!.Value.X);
    }

    // The wall rule: a wall that crosses the body stops the cast just before it.
    [TestMethod]
    public void Horizontal_WallStopsTheCastBeforeIt()
    {
        var land = TeleportRules.Horizontal(Step(), 100, 215, 1, 190)!.Value;
        Assert.AreEqual(230f, land.X); // the wall is at 231; the spot 291 is on the other side
        Assert.AreEqual(215, land.Y);
    }

    [TestMethod]
    public void Horizontal_WallRightNextToTheFeet_MeansNoLanding()
    {
        Assert.IsNull(TeleportRules.Horizontal(Step(), 230.5f, 215, 1, 190)); // the wall is half a pixel ahead: nowhere to go
    }

    // A wall behind the feet, or whose top is at the feet (the edge of the platform we stand on), does not block.
    [TestMethod]
    public void Horizontal_WallBehindOrBelowTheFeet_DoesNotBlock()
    {
        var onTop = TeleportRules.Horizontal(Step(), 240, 155, 1, 190)!.Value; // on the high floor, the wall is behind
        Assert.AreEqual(430f, onTop.X);
        var drop = TeleportRules.Horizontal(Step(), 240, 155, -1, 190)!.Value; // off the high floor: the wall top is at the feet
        Assert.AreEqual(50f, drop.X);
        Assert.AreEqual(215, drop.Y); // lands on the low floor
    }

    // The wall to the left of the feet blocks the same way.
    [TestMethod]
    public void Horizontal_WallOnTheLeft_StopsTheCastBeforeIt()
    {
        var map = Map(-1000, 1000, Fh(-500, 215, 500, 215), Fh(0, 215, 0, 155)); // a wall at x 0, 60 high
        var land = TeleportRules.Horizontal(map, 100, 215, -1, 190)!.Value;
        Assert.AreEqual(1f, land.X); // the wall is at 0, so the last spot before it is 1
    }

    // The body is [y - 68, y): a wall that ends above the head (its bottom at y - 68) does not block; one pixel more does.
    // A wall whose top is 1 px above the feet blocks; one whose top is at the feet does not.
    [TestMethod]
    public void Horizontal_WallBoundaries_AtTheHeadAndAtTheFeet()
    {
        MapData WithWall(int top, int bottom) => Map(-1000, 1000, Fh(-500, 215, 500, 215), Fh(150, top, 150, bottom));
        Assert.AreEqual(190f, TeleportRules.Horizontal(WithWall(100, 147), 0, 215, 1, 190)!.Value.X); // bottom at y - 68: above the head
        Assert.AreEqual(149f, TeleportRules.Horizontal(WithWall(100, 148), 0, 215, 1, 190)!.Value.X); // bottom 1 px into the body
        Assert.AreEqual(149f, TeleportRules.Horizontal(WithWall(214, 300), 0, 215, 1, 190)!.Value.X); // top 1 px above the feet
        Assert.AreEqual(190f, TeleportRules.Horizontal(WithWall(215, 300), 0, 215, 1, 190)!.Value.X); // top at the feet
    }

    // The ground it lands on must be inside the map vertically, like the vertical cast.
    [TestMethod]
    public void Horizontal_GroundOutsideTheMapVertically_IsNotALanding()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(0, 400, 800, 400));
        map.Info.VrBottom = 300; // the pit floor (400) is below the map
        Assert.AreEqual(0f, TeleportRules.Horizontal(map, -10, 100, 1, 190)!.Value.X); // back to the ledge
        map.Info.VrBottom = 1000;
        map.Info.VrTop = 150; // the ledge itself (100) is above the map: nothing ahead at the feet's height
        Assert.AreEqual(180f, TeleportRules.Horizontal(map, -10, 100, 1, 190)!.Value.X);
    }

    [TestMethod]
    public void Horizontal_MapEdge_StopsAtTheEdge()
    {
        var land = TeleportRules.Horizontal(Step(), -200, 215, -1, 190)!.Value;
        Assert.AreEqual(-260f, land.X); // VrLeft
        var right = TeleportRules.Horizontal(Map(-1000, 300, Fh(-500, 100, 500, 100)), 200, 100, 1, 190)!.Value;
        Assert.AreEqual(300f, right.X); // VrRight, the floor goes on beyond it
    }

    // The air rule: no ground at the spot, the cast goes back to the last place with ground.
    [TestMethod]
    public void Horizontal_NoGroundAtTheTarget_StopsAtTheLedge()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100)); // a ledge that ends at x 0, nothing right of it
        var land = TeleportRules.Horizontal(map, -30, 100, 1, 190)!.Value;
        Assert.AreEqual(0f, land.X);
        Assert.IsNull(TeleportRules.Horizontal(map, 50, 100, 1, 190)); // already off it, nothing ahead
    }

    [TestMethod]
    public void Horizontal_OverAGap_LandsOnTheFarSide()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(120, 100, 400, 100));
        Assert.AreEqual(160f, TeleportRules.Horizontal(map, -30, 100, 1, 190)!.Value.X);
    }

    [TestMethod]
    public void Horizontal_DropsToTheGroundBelow_WhenTheTargetIsOverALowerFloor()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(0, 400, 800, 400)); // a ledge over a pit floor 300 down
        var land = TeleportRules.Horizontal(map, -10, 100, 1, 190)!.Value;
        Assert.AreEqual(180f, land.X);
        Assert.AreEqual(400, land.Y);
    }

    [TestMethod]
    public void Horizontal_FollowsASlope_AndPrefersTheGroundAtTheFeetHeight()
    {
        var slope = Map(-1000, 1000, Fh(0, 275, 90, 215), Fh(90, 215, 400, 215), Fh(-300, 275, 0, 275));
        var up = TeleportRules.Horizontal(slope, -50, 275, 1, 190)!.Value;
        Assert.AreEqual(140f, up.X); // 60 higher than the feet: within ReachUp
        Assert.AreEqual(215, up.Y);
        // two floors at the target: the one at the feet height wins over a platform 70 above
        var stacked = Map(-1000, 1000, Fh(-500, 200, 500, 200), Fh(100, 130, 500, 130));
        var onFloor = TeleportRules.Horizontal(stacked, 0, 200, 1, 190)!.Value;
        Assert.AreEqual(200, onFloor.Y);
        Assert.AreSame(stacked.Footholds[0], onFloor.Ground);
        // a platform 150 above the feet is out of reach: nothing at that x, so the cast stops short of it
        var high = Map(-1000, 1000, Fh(-500, 200, 100, 200), Fh(100, 50, 500, 50));
        Assert.AreEqual(100f, TeleportRules.Horizontal(high, 0, 200, 1, 190)!.Value.X);
    }

    [TestMethod]
    public void Vertical_Up_TakesTheFarthestGroundWithinTheDistance()
    {
        var map = Map(-1000, 1000, Fh(-500, 300, 500, 300), Fh(-50, 295, 50, 295), Fh(-50, 180, 50, 180), Fh(-50, 40, 50, 40), Fh(-50, 0, 50, 0));
        var land = TeleportRules.Vertical(map, 0, 300, -1, 295)!.Value;
        Assert.AreSame(map.Footholds[3], land.Ground);
        Assert.AreEqual(40, land.Y); // 260 up; the one at 0 is 300 up (too far); 295 is 5 up (the ground we stand on)
        Assert.AreEqual(0f, land.X);
    }

    // MinStep: a ground 9 px away is the one we stand on; 10 px is a step.
    [TestMethod]
    public void Vertical_MinStepBoundary()
    {
        Assert.IsNull(TeleportRules.Vertical(Map(-1000, 1000, Fh(-50, 91, 50, 91)), 0, 100, -1, 295)); // 9 up
        Assert.AreEqual(90, TeleportRules.Vertical(Map(-1000, 1000, Fh(-50, 90, 50, 90)), 0, 100, -1, 295)!.Value.Y); // 10 up
        Assert.IsNull(TeleportRules.Vertical(Map(-1000, 1000, Fh(-50, 109, 50, 109)), 0, 100, 1, 295)); // 9 down
        Assert.AreEqual(110, TeleportRules.Vertical(Map(-1000, 1000, Fh(-50, 110, 50, 110)), 0, 100, 1, 295)!.Value.Y); // 10 down
    }

    // The cooldown (teleportCooltime, 480 ms): a second cast while it runs pays nothing; after it the cast pays again.
    [TestMethod]
    public void Cooldown_480Ms_BlocksTheSecondCastAndTheMp()
    {
        var teleport = new WzSkillData { SkillId = WzSkillData.Teleport, MaxLevel = 5, Kind = SkillData.KindActive };
        teleport.Common["mpCon"] = SkillFormula.Levels("30-2*x", 5);
        teleport.Common["teleportCooltime"] = SkillFormula.Levels("480", 5);
        var cost = new SkillCost(new Dictionary<int, SkillData>());
        var chara = new CharaSlot { Mp = 100 };
        var cooldowns = new Dictionary<int, float>();
        Assert.IsTrue(cost.Pay(chara, teleport, 5, cooldowns));
        Assert.AreEqual(80, chara.Mp); // 30 - 2 x 5
        Assert.AreEqual(480f, cooldowns[WzSkillData.Teleport]);
        Assert.IsFalse(cost.Pay(chara, teleport, 5, cooldowns)); // cooling: refused, no MP
        Assert.AreEqual(80, chara.Mp);
        var fighter = new Fighter { Cooldowns = cooldowns };
        fighter.Tick(479);
        Assert.IsFalse(cost.Pay(chara, teleport, 5, cooldowns)); // 1 ms left
        fighter.Tick(1);
        Assert.IsTrue(cost.Pay(chara, teleport, 5, cooldowns));
        Assert.AreEqual(60, chara.Mp);
    }

    [TestMethod]
    public void Vertical_NoGroundInReach_MeansNoLanding()
    {
        var map = Map(-1000, 1000, Fh(-500, 300, 500, 300), Fh(-50, -100, 50, -100));
        Assert.IsNull(TeleportRules.Vertical(map, 0, 300, -1, 295)); // the empty sky: 400 up is out of reach
        Assert.IsNull(TeleportRules.Vertical(map, 300, 300, -1, 295)); // no platform at this x either
        Assert.IsNull(TeleportRules.Vertical(map, 0, 300, 1, 295)); // nothing below the floor
    }

    [TestMethod]
    public void Vertical_Down_AndTheMapEdges()
    {
        var map = Map(-1000, 1000, Fh(-500, 0, 500, 0), Fh(-500, 200, 500, 200));
        Assert.AreEqual(200, TeleportRules.Vertical(map, 0, 0, 1, 295)!.Value.Y);
        map.Info.VrBottom = 150; // the lower floor is outside the map
        Assert.IsNull(TeleportRules.Vertical(map, 0, 0, 1, 295));
        map.Info.VrBottom = 1000;
        map.Info.VrTop = 100; // the upper floor is outside the map
        Assert.IsNull(TeleportRules.Vertical(map, 0, 200, -1, 295));
    }
}
