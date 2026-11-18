using Microsoft.Xna.Framework;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

[TestClass]
public class MinimapDotsTests
{
    // The world rect starts at (-CenterX, -CenterY): 1000 x 500 world units drawn on a 200 x 100 art.
    private static MiniMapData Mini() => new() { Width = 1000, Height = 500, CenterX = 500, CenterY = 250, ArtW = 200, ArtH = 100 };

    [TestMethod]
    public void NpcIcons_ShopAndQuestShowBoth_ShopFirst()
    {
        CollectionAssert.AreEqual(new[] { "shop", "npcstart" }, MinimapDots.NpcIcons(0, true));
        CollectionAssert.AreEqual(new[] { "shop", "npcend" }, MinimapDots.NpcIcons(2, true));
        CollectionAssert.AreEqual(new[] { "npcstart" }, MinimapDots.NpcIcons(0, false));
        CollectionAssert.AreEqual(new[] { "npcend" }, MinimapDots.NpcIcons(2, false));
        CollectionAssert.AreEqual(new[] { "shop" }, MinimapDots.NpcIcons(-1, true));
        CollectionAssert.AreEqual(new[] { "shop" }, MinimapDots.NpcIcons(1, true)); // a quest under way draws no mark
        CollectionAssert.AreEqual(new[] { "npc" }, MinimapDots.NpcIcons(-1, false));
    }

    // The quest mark stands right on top of the shop triangle: no gap, no overlap, centred on it.
    [TestMethod]
    public void StackBoxes_SecondIconStandsOnTheFirst()
    {
        var boxes = MinimapDots.StackBoxes(Mini(), 0, 0, 200, 100, new[] { new Point(9, 10), new Point(10, 18) });

        Assert.AreEqual(new Rectangle(96, 41, 9, 10), boxes[0]);
        Assert.AreEqual(new Rectangle(96 + 4 - 5, 41 - 18, 10, 18), boxes[1]);
        Assert.AreEqual(boxes[0].Top, boxes[1].Bottom);
    }

    [TestMethod]
    public void StackBoxes_OutsideTheArt_IsEmpty()
    {
        Assert.AreEqual(0, MinimapDots.StackBoxes(Mini(), -600, 0, 200, 100, new[] { new Point(9, 10), new Point(10, 18) }).Length);
    }

    // The icon stands on the map pixel by its bottom edge, centred on it.
    [TestMethod]
    public void DotBox_StandsOnThePixelByItsBottomEdge()
    {
        var box = MinimapDots.DotBox(Mini(), 0, 0, 200, 100, 9, 10);

        Assert.AreEqual(new Rectangle(100 - 4, 50 - 10 + 1, 9, 10), box);
    }

    [TestMethod]
    public void DotBox_OutsideTheArt_IsNull()
    {
        Assert.IsNull(MinimapDots.DotBox(Mini(), -600, 0, 200, 100, 9, 10)); // left of the map
        Assert.IsNull(MinimapDots.DotBox(Mini(), 0, 300, 200, 100, 9, 10)); // under the map
        Assert.IsNotNull(MinimapDots.DotBox(Mini(), -500, -250, 200, 100, 9, 10)); // the top left pixel is inside
    }
}
