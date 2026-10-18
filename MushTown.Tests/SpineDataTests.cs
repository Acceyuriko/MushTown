using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SpineDataTests
{
    // The bake json contract: short keys, exact shape. Exporter and client
    // share these types; this locks the wire form they agree on.
    [TestMethod]
    public void Clip_RoundTripsWithShortKeys()
    {
        var clip = new SpineData
        {
            Fps = 30,
            Pages = ["a.png"],
            Ox = 625,
            Oy = 880,
            Pma = true,
            Slots = [new SpineSlot
            {
                Blend = 1, Tris = [0, 1, 2], Uvs = [0, 0, 1, 1],
                Frames = [new SpineFrame { V = [1, 2], C = [1, 1, 1, 1], P = 0 },
                    new SpineFrame { V = [3, 4], C = [1, 1, 1, 1], P = 0, Uv = [0, 0, 1, 1] }],
            }],
        };

        string json = JsonSerializer.Serialize(clip);
        var back = JsonSerializer.Deserialize<SpineData>(json)!;

        Assert.AreEqual(30, back.Fps);
        Assert.AreEqual(625, back.Ox);
        Assert.AreEqual(880, back.Oy);
        Assert.IsTrue(back.Pma);
        Assert.IsTrue(json.Contains("\"ox\":625"));
        Assert.IsTrue(json.Contains("\"oy\":880"));
        Assert.IsTrue(json.Contains("\"pma\":true"));
        Assert.AreEqual(1, back.Slots.Count);
        Assert.AreEqual(1, back.Slots[0].Blend);
        Assert.AreEqual(2, back.Slots[0].Frames[0].V[1]);
        Assert.IsTrue(json.Contains("\"v\":[1,2]"));
        Assert.IsTrue(json.Contains("\"uv\":[0,0,1,1]"));
        Assert.IsFalse(json.Contains("\"uv\":null"));
        Assert.AreEqual(1, back.Slots[0].Frames[1].Uv![3]);
    }
}
