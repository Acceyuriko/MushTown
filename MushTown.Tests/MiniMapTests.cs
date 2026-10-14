using MushTown.Shared;

namespace MushTown.Tests;

// Map 10000 numbers: rect top-left (-1338, -635), size 2313x1592.
[TestClass]
public sealed class MiniMapTests
{
    private static readonly MiniMapData Mm = new()
    {
        Width = 2313,
        Height = 1592,
        CenterX = 1338,
        CenterY = 635
    };

    [TestMethod]
    public void ToDot_Corners_MapToBoxEdges()
    {
        Assert.AreEqual((0, 0), Mm.ToDot(-1338, -635, 220, 151));
        Assert.AreEqual((220, 151), Mm.ToDot(975, 957, 220, 151));
    }

    [TestMethod]
    public void ToDot_Spawn_SitsInside()
    {
        var (x, y) = Mm.ToDot(-381, 245, 220, 151);
        Assert.AreEqual(91, x);
        Assert.AreEqual(83, y);
    }
}
