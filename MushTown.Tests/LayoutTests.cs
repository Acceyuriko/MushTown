using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class LayoutTests
{
    private const string Full = """{"plate":[5,6],"hpmp":[7,8],"qslot":[13,14]}""";

    // Layout fields are shared statics: leave them as found.
    private ((int X, int Y) plate, (int X, int Y) hpmp, (int X, int Y) qslot) _saved;

    [TestInitialize]
    public void Save() => _saved = (Layout.Plate, Layout.HpMp, Layout.Qslot);

    [TestCleanup]
    public void Restore() => (Layout.Plate, Layout.HpMp, Layout.Qslot) = _saved;

    [TestMethod]
    public void Load_FullFile_OverridesAll()
    {
        string path = WriteTemp(Full);
        try
        {
            Layout.Load(path);
            Assert.AreEqual((5, 6), Layout.Plate);
            Assert.AreEqual((7, 8), Layout.HpMp);
            Assert.AreEqual((13, 14), Layout.Qslot);
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
            File.WriteAllText(path, """{"_help":"ignored","qslot":[100,200]}""");
            Layout.Load(path);
            Assert.AreEqual((100, 200), Layout.Qslot);
            Assert.AreEqual((5, 6), Layout.Plate);
            Assert.AreEqual((7, 8), Layout.HpMp);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadValue_ThrowsAndKeepsOld()
    {
        string path = WriteTemp(Full);
        try
        {
            string[] bad = ["{\"qslot\":[100,200],\"plate\":[1]}", "{\"qslot\":[100,200],\"plate\":\"bad\"}", "{\"qslot\":[100,200],\"hpmp\":[1.5,2]}", "{\"qslot\":[100,200],\"plate\":[1,2,3]}"];
            foreach (string json in bad)
            {
                Layout.Load(path);
                File.WriteAllText(path, json);
                try { Layout.Load(path); Assert.Fail($"expected a JsonException for {json}"); }
                catch (JsonException) { }
                Assert.AreEqual((13, 14), Layout.Qslot, json);
                Assert.AreEqual((7, 8), Layout.HpMp, json);
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
