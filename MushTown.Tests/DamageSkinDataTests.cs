using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class DamageSkinDataTests
{
    [TestMethod]
    public void BaseSkinFile_StillLoadsWithoutTheNewFields()
    {
        // assets/effect/dmg.json predates Ox, the other sets and the unit.
        var data = JsonSerializer.Deserialize<DmgData>("{\"Digits\":[{\"Image\":\"effect/dmg/0.png\",\"Oy\":32}]}")!;
        Assert.AreEqual(32, data.Digits[0].Oy);
        Assert.AreEqual(0, data.Digits[0].Ox);
        Assert.AreEqual(0, data.Red1.Count);
        Assert.IsNull(data.Unit);
    }

    [TestMethod]
    public void Manifest_ListsDamageSkinsAndAnOldManifestHasNone()
    {
        var manifest = JsonSerializer.Deserialize<ManifestData>("{\"DamageSkins\":[1332,1426]}")!;
        CollectionAssert.AreEqual(new[] { 1332, 1426 }, manifest.DamageSkins);
        Assert.AreEqual(0, JsonSerializer.Deserialize<ManifestData>("{}")!.DamageSkins.Count);
    }

}
