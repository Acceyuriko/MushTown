using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SceneMathTests
{
    private const int ViewW = 1366;
    private const int ViewH = 768;

    [TestMethod]
    public void BackTileMode_MapsAllTypes()
    {
        (bool h, bool v, bool sh, bool sv)[] expected =
        [
            (false, false, false, false), // 0
            (true, false, false, false),  // 1
            (false, true, false, false),  // 2
            (true, true, false, false),   // 3
            (true, false, true, false),   // 4
            (false, true, false, true),   // 5
            (true, true, true, false),    // 6
            (true, true, false, true),    // 7
        ];
        for (int type = 0; type < 8; type++)
            Assert.AreEqual(expected[type], SceneMath.BackTileMode(type), $"type {type}");
        Assert.AreEqual((false, false, false, false), SceneMath.BackTileMode(99));
    }

    [TestMethod]
    public void BackLayout_RxMinus100_PinnedToMap()
    {
        var b = new BackItem { X = 50, Y = 60, Rx = -100, Ry = -100 };
        var a = SceneMath.BackLayout(b, 100, 100, 370, -203, ViewW, ViewH);
        var c = SceneMath.BackLayout(b, 100, 100, 0, 0, ViewW, ViewH);
        Assert.AreEqual(50, a.X);
        Assert.AreEqual(60, a.Y);
        Assert.AreEqual(a.X, c.X);
        Assert.AreEqual(a.Y, c.Y);
    }

    [TestMethod]
    public void BackLayout_RxZero_PinnedToScreen()
    {
        var b = new BackItem { X = 10, Y = 20, Rx = 0, Ry = 0 };
        var a = SceneMath.BackLayout(b, 100, 100, 370, -203, ViewW, ViewH);
        var c = SceneMath.BackLayout(b, 100, 100, 0, 0, ViewW, ViewH);
        // screen pos must not move when the camera moves
        Assert.AreEqual(a.X - 370, c.X - 0);
        Assert.AreEqual(a.Y - -203, c.Y - 0);
        Assert.AreEqual(10 + ViewW / 2, a.X - 370);
        Assert.AreEqual(20 + ViewH / 2, a.Y + 203);
    }

    [TestMethod]
    public void BackLayout_TilingCoversCameraRect()
    {
        var b = new BackItem { X = 0, Y = 0, Type = 3, Rx = -100, Ry = -100 };
        int imgW = 100, imgH = 50, camX = 370, camY = -203;
        var l = SceneMath.BackLayout(b, imgW, imgH, camX, camY, ViewW, ViewH);
        Assert.IsTrue(l.X + l.Left * l.Cx <= camX);
        Assert.IsTrue(l.X + (l.Right - 1) * l.Cx + imgW >= camX + ViewW);
        Assert.IsTrue(l.Y + l.Top * l.Cy <= camY);
        Assert.IsTrue(l.Y + (l.Bottom - 1) * l.Cy + imgH >= camY + ViewH);
        Assert.AreEqual(1, l.Left);
        Assert.AreEqual(20, l.Right);
        Assert.AreEqual(-7, l.Top);
        Assert.AreEqual(14, l.Bottom);
    }

    [TestMethod]
    public void BackLayout_TilingWithZeroStep_FallsBackToImageSize()
    {
        var b = new BackItem { Type = 3 };
        var l = SceneMath.BackLayout(b, 100, 50, 0, 0, ViewW, ViewH);
        Assert.AreEqual(100, l.Cx);
        Assert.AreEqual(50, l.Cy);
    }

    [TestMethod]
    public void BackLayout_NoTiling_DrawsOnce()
    {
        var b = new BackItem { Type = 0 };
        var l = SceneMath.BackLayout(b, 100, 50, 370, -203, ViewW, ViewH);
        Assert.AreEqual(0, l.Left);
        Assert.AreEqual(1, l.Right);
        Assert.AreEqual(0, l.Top);
        Assert.AreEqual(1, l.Bottom);
        Assert.AreEqual(0, l.Cx);
        Assert.AreEqual(0, l.Cy);
    }

    [TestMethod]
    public void BackLayout_ScrollAxis_HasNoParallax()
    {
        var h = new BackItem { X = 30, Type = 4, Rx = 0 };
        var v = new BackItem { Y = 40, Type = 5, Ry = 0 };
        var hl = SceneMath.BackLayout(h, 100, 50, 370, -203, ViewW, ViewH);
        var vl = SceneMath.BackLayout(v, 100, 50, 370, -203, ViewW, ViewH);
        Assert.AreEqual(30, hl.X);
        Assert.AreEqual(40, vl.Y);
        // the other axis still has parallax: screen pos = own offset + half view
        Assert.AreEqual(0 + ViewH / 2, hl.Y + 203);
        Assert.AreEqual(0 + ViewW / 2, vl.X - 370);
    }

    [TestMethod]
    public void ClampCamera_ClampsToMapBounds()
    {
        Assert.AreEqual(500, SceneMath.ClampCamera(500, 0, 2000, ViewW));
        Assert.AreEqual(0, SceneMath.ClampCamera(-50, 0, 2000, ViewW));
        Assert.AreEqual(2000 - ViewW, SceneMath.ClampCamera(9999, 0, 2000, ViewW));
    }

    [TestMethod]
    public void ClampCamera_MapSmallerThanView_PinsToMin()
    {
        Assert.AreEqual(100, SceneMath.ClampCamera(0, 100, 500, ViewW));
        Assert.AreEqual(100, SceneMath.ClampCamera(9999, 100, 500, ViewW));
    }

    [TestMethod]
    public void BlitRect_SameSize_FillsScreen()
    {
        Assert.AreEqual((0, 0, ViewW, ViewH), SceneMath.BlitRect(ViewW, ViewH, ViewW, ViewH));
    }

    [TestMethod]
    public void BlitRect_FullHd_LeavesOnePixelBottomBar()
    {
        Assert.AreEqual((0, 0, 1920, 1079), SceneMath.BlitRect(1920, 1080, ViewW, ViewH));
    }

    [TestMethod]
    public void BlitRect_4k_CentersImage()
    {
        Assert.AreEqual((0, 1, 3840, 2158), SceneMath.BlitRect(3840, 2160, ViewW, ViewH));
    }

    [TestMethod]
    public void BlitRect_Ultrawide_LeavesSideBars()
    {
        Assert.AreEqual((320, 0, 1920, 1080), SceneMath.BlitRect(2560, 1080, ViewW, ViewH));
    }

    [TestMethod]
    public void SpawnGroundY_PicksCoveringFootholdBelowPortal()
    {
        List<FootholdItem> fs =
        [
            new() { X1 = 0, Y1 = -146, X2 = 1400, Y2 = -146 }, // above the portal: skip
            new() { X1 = 600, Y1 = 334, X2 = 800, Y2 = 334 },  // the ground
            new() { X1 = 0, Y1 = 500, X2 = 1400, Y2 = 500 },   // further down: skip
        ];
        Assert.AreEqual(334, SceneMath.SpawnGroundY(fs, 698, 297));
    }

    [TestMethod]
    public void SpawnGroundY_NoCoveringFoothold_ReturnsNull()
    {
        List<FootholdItem> fs = [new() { X1 = 0, Y1 = 500, X2 = 100, Y2 = 500 }];
        Assert.IsNull(SceneMath.SpawnGroundY(fs, 698, 297));
    }

    [TestMethod]
    public void SpawnGroundY_SlopedFoothold_InterpolatesY()
    {
        List<FootholdItem> fs = [new() { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100 }];
        Assert.AreEqual(25, SceneMath.SpawnGroundY(fs, 25, -10));
    }

    [TestMethod]
    public void SpawnGroundY_VerticalFoothold_Ignored()
    {
        List<FootholdItem> fs = [new() { X1 = 698, Y1 = 0, X2 = 698, Y2 = 500 }];
        Assert.IsNull(SceneMath.SpawnGroundY(fs, 698, 297));
    }

    [TestMethod]
    public void SpawnGroundY_EdgeX_CountsAsCovering()
    {
        List<FootholdItem> fs = [new() { X1 = 600, Y1 = 300, X2 = 698, Y2 = 300 }];
        Assert.AreEqual(300, SceneMath.SpawnGroundY(fs, 698, 297));
    }

    [TestMethod]
    public void SpawnGroundY_GroundAtPortalY_CountsAsGround()
    {
        List<FootholdItem> fs = [new() { X1 = 600, Y1 = 297, X2 = 800, Y2 = 297 }];
        Assert.AreEqual(297, SceneMath.SpawnGroundY(fs, 698, 297));
    }

    [TestMethod]
    public void PartVisible_EarRules()
    {
        // humanEar always draws; the other three follow EarType
        Assert.IsTrue(SceneMath.PartVisible("humanEar", true, 0, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("humanEar", true, 2, false, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("ear", true, 0, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("ear", true, 1, false, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("lefEar", true, 1, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("lefEar", true, 2, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("highlefEar", true, 3, false, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("highlefEar", true, 0, false, false, 0, false));
    }

    [TestMethod]
    public void PartVisible_HairRules()
    {
        // hair out: over-head layers draw, under-cap layers hide
        Assert.IsTrue(SceneMath.PartVisible("hairOverHead", true, 0, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("backHair", true, 0, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("backHairOverCape", true, 0, false, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("hair", true, 0, false, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("backHairBelowCap", true, 0, false, false, 0, false));
        // hair covered (hat): flipped
        Assert.IsFalse(SceneMath.PartVisible("hairOverHead", true, 0, true, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("backHairOverCape", true, 0, true, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("backHair", true, 0, true, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("backHairBelowCap", true, 0, true, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("hair", true, 0, true, false, 0, false));
        // shade follows its own flag
        Assert.IsFalse(SceneMath.PartVisible("hairShade", true, 0, false, false, 0, false));
        // true needs the hairShade/child-0 lookup before any caller may pass it
        Assert.IsTrue(SceneMath.PartVisible("hairShade", true, 0, false, true, 0, false));
    }

    [TestMethod]
    public void PartVisible_FaceWeaponAndPlainNames()
    {
        Assert.IsTrue(SceneMath.PartVisible("face", true, 0, false, false, 0, false));
        Assert.IsFalse(SceneMath.PartVisible("face", false, 0, false, false, 0, false));
        // single weapon: every weapon part draws
        Assert.IsTrue(SceneMath.PartVisible("weapon", true, 0, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("weapon1", true, 0, false, false, 0, false));
        // multi weapon: only the picked index draws
        Assert.IsTrue(SceneMath.PartVisible("weapon", true, 0, false, false, 0, true));
        Assert.IsFalse(SceneMath.PartVisible("weapon1", true, 0, false, false, 0, true));
        Assert.IsFalse(SceneMath.PartVisible("weapon", true, 0, false, false, 1, true));
        Assert.IsTrue(SceneMath.PartVisible("weapon1", true, 0, false, false, 1, true));
        Assert.IsTrue(SceneMath.PartVisible("weapon2", true, 0, false, false, 2, true));
        Assert.IsFalse(SceneMath.PartVisible("weapon0", true, 0, false, false, 0, true));
        // anything else always draws
        Assert.IsTrue(SceneMath.PartVisible("body", true, 0, false, false, 0, false));
        Assert.IsTrue(SceneMath.PartVisible("mail", true, 0, false, false, 0, false));
    }

    [TestMethod]
    public void FootholdY_Flat_ReturnsLineY()
    {
        var f = new FootholdItem { X1 = 0, Y1 = 100, X2 = 50, Y2 = 100 };
        Assert.AreEqual(100, SceneMath.FootholdY(f, 25));
    }

    [TestMethod]
    public void FootholdY_Slope_Interpolates()
    {
        var f = new FootholdItem { X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 };
        Assert.AreEqual(3, SceneMath.FootholdY(f, 3));
        Assert.AreEqual(5, SceneMath.FootholdY(f, 5));
    }

    [TestMethod]
    public void FootholdY_Half_RoundsToEven()
    {
        var f = new FootholdItem { X1 = 0, Y1 = 0, X2 = 4, Y2 = 1 };
        Assert.AreEqual(0, SceneMath.FootholdY(f, 2)); // 0.5 -> 0, kept from the original
    }

    [TestMethod]
    public void GroundBelow_PicksNearestBelow()
    {
        var low = new FootholdItem { X1 = 0, Y1 = 200, X2 = 100, Y2 = 200 };
        var high = new FootholdItem { X1 = 0, Y1 = 100, X2 = 100, Y2 = 100 };
        List<FootholdItem> fs = [low, high];
        Assert.AreSame(high, SceneMath.GroundBelow(fs, 50, 50));
    }

    [TestMethod]
    public void GroundBelow_SkipsAboveAndVertical()
    {
        List<FootholdItem> fs =
        [
            new() { X1 = 10, Y1 = 0, X2 = 10, Y2 = 100 }, // vertical: skip
            new() { X1 = 0, Y1 = 0, X2 = 100, Y2 = 0 },   // above: skip
        ];
        Assert.IsNull(SceneMath.GroundBelow(fs, 10, 50));
    }

    [TestMethod]
    public void GroundBelow_Tie_KeepsFirst()
    {
        var a = new FootholdItem { X1 = 0, Y1 = 60, X2 = 100, Y2 = 60 };
        var b = new FootholdItem { X1 = 0, Y1 = 60, X2 = 100, Y2 = 60 };
        List<FootholdItem> fs = [a, b];
        Assert.AreSame(a, SceneMath.GroundBelow(fs, 50, 50));
    }

    [TestMethod]
    public void AdvanceFrame_TimerBelowDelay_Stays()
    {
        Assert.AreEqual((0, 100f), SceneMath.AdvanceFrame(0, 0, 100, [180, 180]));
    }

    [TestMethod]
    public void AdvanceFrame_TimerPastDelay_AdvancesAndLoops()
    {
        Assert.AreEqual((0, 10f), SceneMath.AdvanceFrame(1, 170, 20, [180, 180]));
    }

    [TestMethod]
    public void AdvanceFrame_BigDt_AdvancesMany()
    {
        Assert.AreEqual((2, 140f), SceneMath.AdvanceFrame(0, 0, 500, [180, 180, 180, 180]));
    }

    [TestMethod]
    public void FlipDrawX_MirrorsRectAroundFeet()
    {
        Assert.AreEqual(80, SceneMath.FlipDrawX(100, -10, 30)); // [90,120] -> [80,110]
        Assert.AreEqual(80, SceneMath.FlipDrawX(100, 0, 20));   // [100,120] -> [80,100]
    }

    [TestMethod]
    public void StepTo_SameLevel_PicksIt()
    {
        var same = new FootholdItem { X1 = 90, Y1 = 100, X2 = 200, Y2 = 100 };
        List<FootholdItem> fs = [same];
        Assert.AreSame(same, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_OnePixelRise_PicksIt()
    {
        var rise = new FootholdItem { X1 = 90, Y1 = 99, X2 = 200, Y2 = 99 };
        List<FootholdItem> fs = [rise];
        Assert.AreSame(rise, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_TwoPixelRise_ReturnsNull()
    {
        var rise = new FootholdItem { X1 = 90, Y1 = 98, X2 = 200, Y2 = 98 };
        var below = new FootholdItem { X1 = 90, Y1 = 110, X2 = 200, Y2 = 110 };
        List<FootholdItem> fs = [rise, below];
        Assert.IsNull(SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_OnePixelDrop_PicksIt()
    {
        var drop = new FootholdItem { X1 = 90, Y1 = 101, X2 = 200, Y2 = 101 };
        List<FootholdItem> fs = [drop];
        Assert.AreSame(drop, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_SameBeatsDrop()
    {
        var drop = new FootholdItem { X1 = 90, Y1 = 101, X2 = 200, Y2 = 101 };
        var same = new FootholdItem { X1 = 90, Y1 = 100, X2 = 200, Y2 = 100 };
        List<FootholdItem> fs = [drop, same];
        Assert.AreSame(same, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_RiseBeatsDrop()
    {
        var drop = new FootholdItem { X1 = 90, Y1 = 101, X2 = 200, Y2 = 101 };
        var rise = new FootholdItem { X1 = 90, Y1 = 99, X2 = 200, Y2 = 99 };
        List<FootholdItem> fs = [drop, rise];
        Assert.AreSame(rise, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_SameBeatsRise()
    {
        var rise = new FootholdItem { X1 = 90, Y1 = 99, X2 = 200, Y2 = 99 };
        var same = new FootholdItem { X1 = 90, Y1 = 100, X2 = 200, Y2 = 100 };
        List<FootholdItem> fs = [rise, same];
        Assert.AreSame(same, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void StepTo_Tie_KeepsFirst()
    {
        var a = new FootholdItem { X1 = 90, Y1 = 100, X2 = 200, Y2 = 100 };
        var b = new FootholdItem { X1 = 90, Y1 = 100, X2 = 200, Y2 = 100 };
        List<FootholdItem> fs = [a, b];
        Assert.AreSame(a, SceneMath.StepTo(fs, 95, 100));
    }

    [TestMethod]
    public void SpringStep_FirstStep_MovesTowardTarget()
    {
        var (x, v) = SceneMath.SpringStep(0, 0, 100, 1f / 60);
        Assert.IsTrue(x > 0 && x < 100);
        Assert.IsTrue(v > 0);
    }

    [TestMethod]
    public void SpringStep_HoldsStill_AtTarget()
    {
        var (x, v) = SceneMath.SpringStep(100, 0, 100, 1f / 60);
        Assert.AreEqual(100, x);
        Assert.AreEqual(0, v);
    }

    [TestMethod]
    public void SpringStep_ManySteps_SettlesNearTarget()
    {
        float x = 0, v = 0, prev = 0;
        for (int i = 0; i < 60 * 10; i++)
        {
            (x, v) = SceneMath.SpringStep(x, v, 100, 1f / 60);
            Assert.IsTrue(x >= prev && x <= 100.01f, $"step {i}: x={x}"); // no backtrack, no overshoot
            prev = x;
        }
        Assert.IsTrue(Math.Abs(x - 100) < 1);
        Assert.IsTrue(Math.Abs(v) < 5);
    }
}
