using MushTown.Shared;

namespace MushTown.Tests;

// Where a Teleport lands (TeleportRules): the wall first, then the platform in the +-75 range, the map edge, the keys.
[TestClass]
public class TeleportTests
{
    private static FootholdItem Fh(int x1, int y1, int x2, int y2) => new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };

    private static MapData Map(int vrLeft, int vrRight, params FootholdItem[] footholds) => new()
    {
        Info = new MapInfo { VrLeft = vrLeft, VrRight = vrRight, VrTop = -1000, VrBottom = 1000 },
        Footholds = footholds.ToList(),
    };

    // A floor at 215 up to x 231, a wall at 231 (two pieces, like WZ) of the given height, a higher floor after it.
    private static MapData Step(int high) => Map(-260, 1160,
        Fh(-270, 215, 231, 215), Fh(231, 215, 231, (215 + high) / 2), Fh(231, (215 + high) / 2, 231, high), Fh(231, high, 1170, high));

    [TestMethod]
    public void Horizontal_FlatGround_GoesTheWholeDistance()
    {
        var map = Map(-1000, 1000, Fh(-500, 100, 500, 100));
        var land = TeleportRules.Horizontal(map, 0, 100, 1, 190)!.Value;
        Assert.AreEqual(190f, land.X);
        Assert.AreEqual(100, land.Y);
        Assert.AreEqual(-190f, TeleportRules.Horizontal(map, 0, 100, -1, 190)!.Value.X);
    }

    // Teleport Boost adds 65 to the horizontal and 25 to the vertical distance (WZ x 65, y 25): Teleport 5 is 190 and 295, with the boost 255 and 320.
    // The landing rules are the same: the farther cast lands farther, a floor 310 below is out of the plain reach (295) and inside the boosted one (320).
    [TestMethod]
    public void TeleportBoost_AddsToTheDistances_RulesUnchanged()
    {
        var (horizontal, vertical) = WzSkillStats.TeleportReach(WzSkillTests.FirePoisonPassives());
        Assert.AreEqual(255, horizontal);
        Assert.AreEqual(320, vertical);
        var flat = Map(-1000, 1000, Fh(-500, 100, 500, 100));
        Assert.AreEqual(190f, TeleportRules.Horizontal(flat, 0, 100, 1, 190)!.Value.X);
        Assert.AreEqual(255f, TeleportRules.Horizontal(flat, 0, 100, 1, horizontal)!.Value.X);
        Assert.AreEqual(-255f, TeleportRules.Horizontal(flat, 0, 100, -1, horizontal)!.Value.X);
        Assert.AreEqual(230f, TeleportRules.Horizontal(Step(100), 100, 215, 1, horizontal)!.Value.X); // the wall still stops it just before it
        var tower = Map(-1000, 1000, Fh(-500, 0, 500, 0), Fh(-500, 310, 500, 310));
        Assert.IsNull(TeleportRules.Vertical(tower, 0, 0, 1, 295));
        Assert.AreEqual(310, TeleportRules.Vertical(tower, 0, 0, 1, vertical)!.Value.Y);
        Assert.AreEqual(310, TeleportRules.ForKeys(tower, 0, 0, false, true, 0, horizontal, vertical)!.Value.Y);
    }

    // The wall rule: a wall that rises above the reach (75) over the feet stops the cast just before it, even though the
    // platform on top of it would be a landing: the wall is checked first.
    [TestMethod]
    public void Horizontal_TallWallStopsTheCastBeforeIt()
    {
        var land = TeleportRules.Horizontal(Step(100), 100, 215, 1, 190)!.Value; // 115 high, in two pieces
        Assert.AreEqual(230f, land.X); // the wall is at 231; the spot 291 is on the other side
        Assert.AreEqual(215, land.Y);
    }

    [TestMethod]
    public void Horizontal_WallRightNextToTheFeet_MeansNoLanding()
    {
        Assert.IsNull(TeleportRules.Horizontal(Step(100), 230.5f, 215, 1, 190)); // the wall is half a pixel ahead: nowhere to go
    }

    // The wall to the left of the feet blocks the same way.
    [TestMethod]
    public void Horizontal_TallWallOnTheLeft_StopsTheCastBeforeIt()
    {
        var map = Map(-1000, 1000, Fh(-500, 215, 500, 215), Fh(0, 215, 0, 100));
        Assert.AreEqual(1f, TeleportRules.Horizontal(map, 100, 215, -1, 190)!.Value.X); // the wall is at 0, so the last spot before it is 1
    }

    // The user's picture 1: a low cliff (60 high, within the reach) is no wall for a Teleport: the cast goes over it
    // and lands on the plateau at the far spot. The same cliff stops a walk (MoverTests).
    [TestMethod]
    public void Horizontal_LowCliff_IsJumpedOntoThePlateau()
    {
        var land = TeleportRules.Horizontal(Step(155), 100, 215, 1, 190)!.Value;
        Assert.AreEqual(290f, land.X);
        Assert.AreEqual(155, land.Y);
        var edge = Step(140); // 75 high: the top is exactly at the reach: still a low cliff
        Assert.AreEqual(140, TeleportRules.Horizontal(edge, 100, 215, 1, 190)!.Value.Y);
        Assert.AreEqual(230f, TeleportRules.Horizontal(Step(139), 100, 215, 1, 190)!.Value.X); // 76 high: a wall
    }

    // A wall behind the feet, or whose top is at the feet (the edge of the platform we stand on), does not block.
    [TestMethod]
    public void Horizontal_WallBehindOrBelowTheFeet_DoesNotBlock()
    {
        var onTop = TeleportRules.Horizontal(Step(100), 240, 100, 1, 190)!.Value; // on the high floor, the wall is behind
        Assert.AreEqual(430f, onTop.X);
        var drop = TeleportRules.Horizontal(Step(100), 240, 100, -1, 190)!.Value; // off the high floor (115 high): the wall top is at the feet
        Assert.AreEqual(231f, drop.X); // not stopped at 232 by the wall; the low floor is 115 below (out of range), so it steps back to the ledge
        Assert.AreEqual(100, drop.Y);
    }

    // The feet height must be inside the wall's span: top < y <= bottom (and the top above the reach). The end cap of a floating platform, hanging above the
    // feet, lets the cast through (map 1010000: the caps at x -141 and 231 made the casts stop short).
    [TestMethod]
    public void Horizontal_WallSpanBoundaries_AroundTheFeet()
    {
        MapData WithWall(int top, int bottom) => Map(-1000, 1000, Fh(-500, 215, 500, 215), Fh(150, top, 150, bottom));
        Assert.AreEqual(149f, TeleportRules.Horizontal(WithWall(100, 215), 0, 215, 1, 190)!.Value.X); // bottom at the feet: a wall on the ground
        Assert.AreEqual(190f, TeleportRules.Horizontal(WithWall(95, 214), 0, 215, 1, 190)!.Value.X); // bottom 1 px above the feet: a cap
        Assert.AreEqual(149f, TeleportRules.Horizontal(WithWall(100, 300), 0, 215, 1, 190)!.Value.X); // a wall that goes below the feet too
        Assert.AreEqual(190f, TeleportRules.Horizontal(WithWall(215, 300), 0, 215, 1, 190)!.Value.X); // top at the feet
        Assert.AreEqual(190f, TeleportRules.Horizontal(WithWall(150, 215), 0, 215, 1, 190)!.Value.X); // 65 high: low
    }

    // The window is +-75 px around the feet line at the far spot: 75 is in, 76 is out; out of it the cast steps back.
    [TestMethod]
    public void Horizontal_PlatformRange_Is75AboveAndBelow()
    {
        MapData Far(int y) => Map(-1000, 1000, Fh(-500, 100, 100, 100), Fh(100, y, 500, y)); // the floor ends at 100, a platform after
        var below = TeleportRules.Horizontal(Far(175), 0, 100, 1, 190)!.Value;
        Assert.AreEqual(190f, below.X); // 75 below: inside
        Assert.AreEqual(175, below.Y);
        Assert.AreEqual(25, TeleportRules.Horizontal(Far(25), 0, 100, 1, 190)!.Value.Y); // 75 above: inside
        Assert.AreEqual(100f, TeleportRules.Horizontal(Far(176), 0, 100, 1, 190)!.Value.X); // 76 below: out, back to the floor's end
        Assert.AreEqual(100f, TeleportRules.Horizontal(Far(24), 0, 100, 1, 190)!.Value.X); // 76 above: out
    }

    // Several platforms in the range: the shortest vertical distance to the feet line wins; at the same distance the upper one.
    [TestMethod]
    public void Horizontal_SeveralPlatforms_ShortestVerticalDistance_TieGoesUp()
    {
        MapData Stack(params int[] ys) => Map(-1000, 1000, ys.Select(y => Fh(100, y, 500, y)).Prepend(Fh(-500, 200, 100, 200)).ToArray());
        Assert.AreEqual(170, TeleportRules.Horizontal(Stack(130, 170, 260), 0, 200, 1, 190)!.Value.Y); // 30 above beats 70 above
        Assert.AreEqual(230, TeleportRules.Horizontal(Stack(130, 230), 0, 200, 1, 190)!.Value.Y); // 30 below beats 70 above
        Assert.AreEqual(140, TeleportRules.Horizontal(Stack(140, 260), 0, 200, 1, 190)!.Value.Y); // 60 and 60: the upper
        Assert.AreEqual(140, TeleportRules.Horizontal(Stack(260, 140), 0, 200, 1, 190)!.Value.Y); // the order in the map does not matter
    }

    // The case of the user's picture 1: the far spot has a platform above (60 up) and nothing else, so the cast goes up to it.
    [TestMethod]
    public void Horizontal_FarSpotWithAPlatformAbove_GoesUpToIt()
    {
        var map = Map(-1000, 1000, Fh(-500, 215, 150, 215), Fh(150, 155, 600, 155)); // the floor ends, a plateau 60 above goes on
        var land = TeleportRules.Horizontal(map, 0, 215, 1, 190)!.Value;
        Assert.AreEqual(190f, land.X);
        Assert.AreEqual(155, land.Y); // the plateau is the only ground at x 190
    }

    [TestMethod]
    public void Horizontal_MapEdge_StopsAtTheEdge()
    {
        var land = TeleportRules.Horizontal(Step(100), -200, 215, -1, 190)!.Value;
        Assert.AreEqual(-260f, land.X); // VrLeft
        var right = TeleportRules.Horizontal(Map(-1000, 300, Fh(-500, 100, 500, 100)), 200, 100, 1, 190)!.Value;
        Assert.AreEqual(300f, right.X); // VrRight, the floor goes on beyond it
    }

    // No platform at the far spot: step back to the last place with ground.
    [TestMethod]
    public void Horizontal_NoGroundAtTheTarget_StopsAtTheLedge()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100)); // a ledge that ends at x 0, nothing right of it
        Assert.AreEqual(0f, TeleportRules.Horizontal(map, -30, 100, 1, 190)!.Value.X);
        Assert.IsNull(TeleportRules.Horizontal(map, 50, 100, 1, 190)); // already off it, nothing ahead
    }

    [TestMethod]
    public void Horizontal_OverAGap_LandsOnTheFarSide()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(120, 100, 400, 100));
        Assert.AreEqual(160f, TeleportRules.Horizontal(map, -30, 100, 1, 190)!.Value.X);
    }

    // A floor deeper than the range is not "ground" for the far spot: the cast stops at the ledge, no falling into the pit.
    [TestMethod]
    public void Horizontal_FloorBelowTheRange_IsNotALanding()
    {
        var map = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(0, 400, 800, 400));
        Assert.AreEqual(0f, TeleportRules.Horizontal(map, -10, 100, 1, 190)!.Value.X);
    }

    // The ground it lands on must be inside the map vertically, like the vertical cast.
    [TestMethod]
    public void Horizontal_GroundOutsideTheMapVertically_IsNotALanding()
    {
        var pit = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(0, 170, 800, 170)); // a pit floor 70 below: in range
        Assert.AreEqual(180f, TeleportRules.Horizontal(pit, -10, 100, 1, 190)!.Value.X);
        pit.Info.VrBottom = 150; // the pit floor is below the map
        Assert.AreEqual(0f, TeleportRules.Horizontal(pit, -10, 100, 1, 190)!.Value.X); // back to the ledge
        var up = Map(-1000, 1000, Fh(-300, 100, 0, 100), Fh(0, 40, 800, 40)); // a platform 60 above
        Assert.AreEqual(180f, TeleportRules.Horizontal(up, -10, 100, 1, 190)!.Value.X);
        up.Info.VrTop = 50; // the platform is above the map
        Assert.AreEqual(0f, TeleportRules.Horizontal(up, -10, 100, 1, 190)!.Value.X);
    }

    [TestMethod]
    public void Horizontal_FollowsASlope()
    {
        var slope = Map(-1000, 1000, Fh(0, 275, 90, 215), Fh(90, 215, 400, 215), Fh(-300, 275, 0, 275));
        var up = TeleportRules.Horizontal(slope, -50, 275, 1, 190)!.Value;
        Assert.AreEqual(140f, up.X); // 60 higher than the feet: inside the range
        Assert.AreEqual(215, up.Y);
        Assert.AreSame(slope.Footholds[1], up.Ground);
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

    // The keys: no direction key held means no landing (so no cast, no MP, no cooldown: the client pays only for a landing);
    // Up wins over Down, and both over Left/Right.
    [TestMethod]
    public void ForKeys_NoDirectionKey_MeansNoLanding_AndTheKeysDecide()
    {
        var map = Map(-1000, 1000, Fh(-500, 300, 500, 300), Fh(-50, 180, 50, 180), Fh(-50, 400, 50, 400));
        Assert.IsNull(TeleportRules.ForKeys(map, 0, 300, false, false, 0, 190, 295));
        Assert.AreEqual(180, TeleportRules.ForKeys(map, 0, 300, true, false, 1, 190, 295)!.Value.Y); // Up beats Right
        Assert.AreEqual(400, TeleportRules.ForKeys(map, 0, 300, false, true, -1, 190, 295)!.Value.Y);
        Assert.AreEqual(190f, TeleportRules.ForKeys(map, 0, 300, false, false, 1, 190, 295)!.Value.X);
        Assert.AreEqual(-190f, TeleportRules.ForKeys(map, 0, 300, false, false, -1, 190, 295)!.Value.X);
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
