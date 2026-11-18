using MushTown.Shared;

namespace MushTown.Tests;

// The one meso tier table: exporter, server and client read it.
[TestClass]
public class MesoArtTests
{
    [TestMethod]
    public void Tier_BreaksAt50_100_1000()
    {
        Assert.AreEqual(0, MesoArt.Tier(1));
        Assert.AreEqual(0, MesoArt.Tier(49));
        Assert.AreEqual(1, MesoArt.Tier(50));
        Assert.AreEqual(1, MesoArt.Tier(99));
        Assert.AreEqual(2, MesoArt.Tier(100));
        Assert.AreEqual(2, MesoArt.Tier(999));
        Assert.AreEqual(3, MesoArt.Tier(1000));
    }

    [TestMethod]
    public void Table_HasOneRowPerTier()
    {
        Assert.AreEqual(4, MesoArt.Ids.Length);
        Assert.AreEqual(4, MesoArt.Origins.Length);
        Assert.AreEqual(4, MesoArt.Widths.Length);
        Assert.AreEqual("item/meso_09000003.png", MesoArt.Image(3));
    }
}
