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
        Assert.IsNull(data.Sample);
    }

    [TestMethod]
    public void UnitFormat_UsesKMBWithOneDecimal()
    {
        Assert.AreEqual("999", DmgUnit.Format(999));
        Assert.AreEqual("1K", DmgUnit.Format(1_000));
        Assert.AreEqual("123.4K", DmgUnit.Format(123_456));
        Assert.AreEqual("1.2B", DmgUnit.Format(1_234_567_890));
        Assert.AreEqual("123B", DmgUnit.Format(123_045_000_000));
    }

    [TestMethod]
    public void Manifest_ListsDamageSkinsAndAnOldManifestHasNone()
    {
        var manifest = JsonSerializer.Deserialize<ManifestData>("{\"DamageSkins\":[1332,1426]}")!;
        CollectionAssert.AreEqual(new[] { 1332, 1426 }, manifest.DamageSkins);
        Assert.AreEqual(0, JsonSerializer.Deserialize<ManifestData>("{}")!.DamageSkins.Count);
    }

}
