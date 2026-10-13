using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class LayoutTests
{
    private const string Full = """{"inv":[1,2],"stat":[3,4],"plate":[5,6],"hpmp":[7,8],"menuBtn":[9,10],"charBtn":[11,12],"qslot":[13,14],"coin":[15,16],"meso":[17,18]}""";

    // Layout fields are shared statics: leave them as found.
    private ((int X, int Y) inv, (int X, int Y) stat, (int X, int Y) plate, (int X, int Y) hpmp, (int X, int Y) menuBtn, (int X, int Y) charBtn, (int X, int Y) qslot, (int X, int Y) coin, (int X, int Y) meso) _saved;

    [TestInitialize]
    public void Save() => _saved = (Layout.Inv, Layout.Stat, Layout.Plate, Layout.HpMp, Layout.MenuBtn, Layout.CharBtn, Layout.Qslot, Layout.Coin, Layout.Meso);

    [TestCleanup]
    public void Restore() => (Layout.Inv, Layout.Stat, Layout.Plate, Layout.HpMp, Layout.MenuBtn, Layout.CharBtn, Layout.Qslot, Layout.Coin, Layout.Meso) = _saved;

    [TestMethod]
    public void Load_FullFile_OverridesAll()
    {
        string path = WriteTemp(Full);
        try
        {
            Layout.Load(path);
            Assert.AreEqual((1, 2), Layout.Inv);
            Assert.AreEqual((3, 4), Layout.Stat);
            Assert.AreEqual((5, 6), Layout.Plate);
            Assert.AreEqual((7, 8), Layout.HpMp);
            Assert.AreEqual((9, 10), Layout.MenuBtn);
            Assert.AreEqual((11, 12), Layout.CharBtn);
            Assert.AreEqual((13, 14), Layout.Qslot);
            Assert.AreEqual((15, 16), Layout.Coin);
            Assert.AreEqual((17, 18), Layout.Meso);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_PartialFile_KeepsMissing()
    {
        string path = WriteTemp(Full);
        try
        {
            Layout.Load(path);
            File.WriteAllText(path, """{"_help":"ignored","inv":[100,200]}""");
            Layout.Load(path);
            Assert.AreEqual((100, 200), Layout.Inv);
            Assert.AreEqual((3, 4), Layout.Stat);
            Assert.AreEqual((5, 6), Layout.Plate);
            Assert.AreEqual((7, 8), Layout.HpMp);
            Assert.AreEqual((9, 10), Layout.MenuBtn);
            Assert.AreEqual((11, 12), Layout.CharBtn);
            Assert.AreEqual((13, 14), Layout.Qslot);
            Assert.AreEqual((15, 16), Layout.Coin);
            Assert.AreEqual((17, 18), Layout.Meso);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadValue_ThrowsAndKeepsOld()
    {
        string path = WriteTemp(Full);
        try
        {
            string[] bad = ["{\"inv\":[100,200],\"stat\":[1]}", "{\"inv\":[100,200],\"stat\":\"bad\"}", "{\"inv\":[100,200],\"coin\":[1.5,2]}", "{\"inv\":[100,200],\"meso\":[1,2,3]}"];
            foreach (string json in bad)
            {
                Layout.Load(path);
                File.WriteAllText(path, json);
                try { Layout.Load(path); Assert.Fail($"expected a JsonException for {json}"); }
                catch (JsonException) { }
                Assert.AreEqual((1, 2), Layout.Inv, json);
                Assert.AreEqual((17, 18), Layout.Meso, json);
                File.WriteAllText(path, Full);
            }
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadJson_Throws()
    {
        string path = WriteTemp("{oops");
        try
        {
            try { Layout.Load(path); Assert.Fail("expected a JsonException"); }
            catch (JsonException) { } // the runtime subtype is internal; the base type is the contract
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_MissingFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"layout-nope-{Guid.NewGuid():N}.json");
        Assert.ThrowsException<FileNotFoundException>(() => Layout.Load(path));
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"layout-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
