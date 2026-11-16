using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class DeathTests
{
    [TestMethod]
    public void Die_StartsDeadAndTheTombstoneFallsThenLandsAndStays()
    {
        var d = new DeathState();
        Assert.IsFalse(d.Dead);
        d.Die(300, 400);
        Assert.IsTrue(d.Dead);
        Assert.IsFalse(d.Landed);
        Assert.AreEqual(400f - DeathState.FallFrom, d.TombY);
        d.Tick(100);
        Assert.IsTrue(d.TombY > 400f - DeathState.FallFrom && d.TombY < 400f, "it is on its way down");
        for (int i = 0; i < 20; i++) d.Tick(100);
        Assert.IsTrue(d.Landed);
        Assert.AreEqual(400f, d.TombY, "it stops on the ground, never below");
        float landedMs = d.LandedMs;
        d.Tick(100);
        Assert.AreEqual(400f, d.TombY);
        Assert.IsTrue(d.LandedMs > landedMs, "the landing clock runs on");
    }

    [TestMethod]
    public void Revive_ClearsTheDeadState()
    {
        var d = new DeathState();
        d.Die(0, 0);
        d.Revive();
        Assert.IsFalse(d.Dead);
        Assert.IsFalse(d.Landed);
    }

    [TestMethod]
    public void ReviveMap_IsTheTownTheMapBelongsTo()
    {
        // WZ info/returnMap: field maps point at their town, a town at itself.
        var info = new Dictionary<int, MapInfo>
        {
            [10000] = new() { ReturnMap = 1000000 },
            [1010100] = new() { ReturnMap = 1000000 },
            [50000] = new() { ReturnMap = 1000000 },
            [2000000] = new() { ReturnMap = 2000000 },
            [30000] = new() { ReturnMap = 30000 },
        };
        Assert.AreEqual(1000000, DeathState.ReviveMap(info[10000]));
        Assert.AreEqual(1000000, DeathState.ReviveMap(info[1010100]));
        Assert.AreEqual(1000000, DeathState.ReviveMap(info[50000]));
        Assert.AreEqual(2000000, DeathState.ReviveMap(info[2000000]));
        Assert.AreEqual(30000, DeathState.ReviveMap(info[30000]));
    }

    [TestMethod]
    public void ReviveMap_MissingFieldFailsLoud()
    {
        Assert.ThrowsException<InvalidDataException>(() => DeathState.ReviveMap(new MapInfo()));
    }

    [TestMethod]
    public void ExportedMapJson_CarriesTheReturnMap()
    {
        var map = System.Text.Json.JsonSerializer.Deserialize<MapData>(
            "{\"MapId\":10000,\"Info\":{\"ReturnMap\":1000000}}")!;
        Assert.AreEqual(1000000, DeathState.ReviveMap(map.Info));
    }
}
