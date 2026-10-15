using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class MenuLayoutTests
{
    private const string Full = """{"slice":1,"overlapTop":2,"botUp":3,"rowGap":4,"bodyPadTop":5,"bodyPadBottom":6,"middleExtraH":7,"serverRowW":8,"serverRowH":9,"textMargin":10,"textDown":11,"listSize":12,"textGap":13,"cancelSize":14,"cancelDown":15,"scale":0.5,"log":[16,17]}""";

    // MenuLayout fields are shared statics: leave them as found.
    private (int slice, int overlapTop, int botUp, int rowGap, int bodyPadTop, int bodyPadBottom, int middleExtraH, int serverRowW, int serverRowH, int textMargin, int textDown, int listSize, int textGap, int cancelSize, int cancelDown, float scale, (int X, int Y) log) _saved;

    [TestInitialize]
    public void Save() => _saved = (MenuLayout.Slice, MenuLayout.OverlapTop, MenuLayout.BotUp, MenuLayout.RowGap, MenuLayout.BodyPadTop, MenuLayout.BodyPadBottom, MenuLayout.MiddleExtraH, MenuLayout.ServerRowW, MenuLayout.ServerRowH, MenuLayout.TextMargin, MenuLayout.TextDown, MenuLayout.ListSize, MenuLayout.TextGap, MenuLayout.CancelSize, MenuLayout.CancelDown, MenuLayout.Scale, MenuLayout.Log);

    [TestCleanup]
    public void Restore() => (MenuLayout.Slice, MenuLayout.OverlapTop, MenuLayout.BotUp, MenuLayout.RowGap, MenuLayout.BodyPadTop, MenuLayout.BodyPadBottom, MenuLayout.MiddleExtraH, MenuLayout.ServerRowW, MenuLayout.ServerRowH, MenuLayout.TextMargin, MenuLayout.TextDown, MenuLayout.ListSize, MenuLayout.TextGap, MenuLayout.CancelSize, MenuLayout.CancelDown, MenuLayout.Scale, MenuLayout.Log) = _saved;

    [TestMethod]
    public void Load_FullFile_OverridesAll()
    {
        string path = WriteTemp(Full);
        try
        {
            MenuLayout.Load(path);
            Assert.AreEqual(1, MenuLayout.Slice);
            Assert.AreEqual(2, MenuLayout.OverlapTop);
            Assert.AreEqual(3, MenuLayout.BotUp);
            Assert.AreEqual(4, MenuLayout.RowGap);
            Assert.AreEqual(5, MenuLayout.BodyPadTop);
            Assert.AreEqual(6, MenuLayout.BodyPadBottom);
            Assert.AreEqual(7, MenuLayout.MiddleExtraH);
            Assert.AreEqual(8, MenuLayout.ServerRowW);
            Assert.AreEqual(9, MenuLayout.ServerRowH);
            Assert.AreEqual(10, MenuLayout.TextMargin);
            Assert.AreEqual(11, MenuLayout.TextDown);
            Assert.AreEqual(12, MenuLayout.ListSize);
            Assert.AreEqual(13, MenuLayout.TextGap);
            Assert.AreEqual(14, MenuLayout.CancelSize);
            Assert.AreEqual(15, MenuLayout.CancelDown);
            Assert.AreEqual(0.5f, MenuLayout.Scale);
            Assert.AreEqual((16, 17), MenuLayout.Log);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_PartialFile_KeepsMissing()
    {
        string path = WriteTemp(Full);
        try
        {
            MenuLayout.Load(path);
            File.WriteAllText(path, """{"_help":"ignored","slice":100}""");
            MenuLayout.Load(path);
            Assert.AreEqual(100, MenuLayout.Slice);
            Assert.AreEqual(2, MenuLayout.OverlapTop);
            Assert.AreEqual(12, MenuLayout.ListSize);
            Assert.AreEqual((16, 17), MenuLayout.Log);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadValue_ThrowsAndKeepsOld()
    {
        string path = WriteTemp(Full);
        try
        {
            string[] bad = ["{\"slice\":100,\"listSize\":0}", "{\"slice\":100,\"cancelSize\":-2}", "{\"slice\":100,\"log\":[1]}", "{\"slice\":\"bad\"}", "{\"slice\":100,\"scale\":\"bad\"}", "{\"slice\":100,\"log\":[\"a\",1]}"];
            foreach (string json in bad)
            {
                MenuLayout.Load(path);
                File.WriteAllText(path, json);
                try { MenuLayout.Load(path); Assert.Fail($"expected a JsonException for {json}"); }
                catch (JsonException) { }
                Assert.AreEqual(1, MenuLayout.Slice, json);
                Assert.AreEqual(12, MenuLayout.ListSize, json);
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
            try { MenuLayout.Load(path); Assert.Fail("expected a JsonException"); }
            catch (JsonException) { } // the runtime subtype is internal; the base type is the contract
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_MissingFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"menu-layout-nope-{Guid.NewGuid():N}.json");
        Assert.ThrowsException<FileNotFoundException>(() => MenuLayout.Load(path));
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"menu-layout-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
