using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class JobLayoutTests
{
    private const string Full = """{"list":[3,4],"listGlass":5,"listPad":6,"pageGap":27,"name":[7,8],"mark":[9,10],"sub":[11,12],"desc":[13,14],"descLine":15,"cardPad":28,"stat":[18,19],"statValueX":20,"statLine":21,"make":[22,23],"back":[24,25],"errorY":26}""";

    // JobLayout fields are shared statics: leave them as found.
    private ((int X, int Y) list, int listGlass, int listPad, int pageGap, (int X, int Y) name, (int X, int Y) mark, (int X, int Y) sub, (int X, int Y) desc, int descLine, int cardPad, (int X, int Y) stat, int statValueX, int statLine, (int X, int Y) make, (int X, int Y) back, int errorY) _saved;

    [TestInitialize]
    public void Save() => _saved = (JobLayout.List, JobLayout.ListGlass, JobLayout.ListPad, JobLayout.PageGap, JobLayout.Name, JobLayout.Mark, JobLayout.Sub, JobLayout.Desc, JobLayout.DescLine, JobLayout.CardPad, JobLayout.Stat, JobLayout.StatValueX, JobLayout.StatLine, JobLayout.Make, JobLayout.Back, JobLayout.ErrorY);

    [TestCleanup]
    public void Restore() => (JobLayout.List, JobLayout.ListGlass, JobLayout.ListPad, JobLayout.PageGap, JobLayout.Name, JobLayout.Mark, JobLayout.Sub, JobLayout.Desc, JobLayout.DescLine, JobLayout.CardPad, JobLayout.Stat, JobLayout.StatValueX, JobLayout.StatLine, JobLayout.Make, JobLayout.Back, JobLayout.ErrorY) = _saved;

    [TestMethod]
    public void Load_FullFile_OverridesAll()
    {
        string path = WriteTemp(Full);
        try
        {
            JobLayout.Load(path);
            Assert.AreEqual((3, 4), JobLayout.List);
            Assert.AreEqual(5, JobLayout.ListGlass);
            Assert.AreEqual(6, JobLayout.ListPad);
            Assert.AreEqual(27, JobLayout.PageGap);
            Assert.AreEqual((7, 8), JobLayout.Name);
            Assert.AreEqual((9, 10), JobLayout.Mark);
            Assert.AreEqual((11, 12), JobLayout.Sub);
            Assert.AreEqual((13, 14), JobLayout.Desc);
            Assert.AreEqual(15, JobLayout.DescLine);
            Assert.AreEqual(28, JobLayout.CardPad);
            Assert.AreEqual((18, 19), JobLayout.Stat);
            Assert.AreEqual(20, JobLayout.StatValueX);
            Assert.AreEqual(21, JobLayout.StatLine);
            Assert.AreEqual((22, 23), JobLayout.Make);
            Assert.AreEqual((24, 25), JobLayout.Back);
            Assert.AreEqual(26, JobLayout.ErrorY);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_PartialFile_KeepsMissing()
    {
        string path = WriteTemp(Full);
        try
        {
            JobLayout.Load(path);
            File.WriteAllText(path, """{"_help":"ignored","stat":[100,200]}""");
            JobLayout.Load(path);
            Assert.AreEqual((100, 200), JobLayout.Stat);
            Assert.AreEqual((3, 4), JobLayout.List);
            Assert.AreEqual(15, JobLayout.DescLine);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadValue_ThrowsAndKeepsOld()
    {
        string path = WriteTemp(Full);
        try
        {
            string[] bad = ["{\"stat\":[100,200],\"list\":[1]}", "{\"stat\":[100,200],\"name\":[\"a\",1]}", "{\"stat\":[100,200],\"listGlass\":\"bad\"}"];
            foreach (string json in bad)
            {
                JobLayout.Load(path);
                File.WriteAllText(path, json);
                try { JobLayout.Load(path); Assert.Fail($"expected a JsonException for {json}"); }
                catch (JsonException) { }
                Assert.AreEqual((18, 19), JobLayout.Stat, json);
                Assert.AreEqual((3, 4), JobLayout.List, json);
                File.WriteAllText(path, Full);
            }
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_MissingFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"jobselect-layout-nope-{Guid.NewGuid():N}.json");
        Assert.ThrowsException<FileNotFoundException>(() => JobLayout.Load(path));
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"jobselect-layout-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
