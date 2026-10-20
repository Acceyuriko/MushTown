using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class PanelLayoutTests
{
    [TestMethod]
    public void Get_ReadsTheNumbers()
    {
        string path = WriteTemp("""{"x":1114,"y":45,"w":242}""");
        try
        {
            var lay = PanelLayout.Load(path);
            Assert.AreEqual(1114, lay.Get("x", 0));
            Assert.AreEqual(45, lay.Get("y", 0));
            Assert.AreEqual(242, lay.Get("w", 0));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Get_MissingKey_KeepsTheDefault()
    {
        string path = WriteTemp("""{"x":10}""");
        try
        {
            var lay = PanelLayout.Load(path);
            Assert.AreEqual(7, lay.Get("nope", 7));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Get_TextValue_KeepsTheDefault()
    {
        string path = WriteTemp("""{"x":"10","y":[1,2],"w":{"a":1}}""");
        try
        {
            var lay = PanelLayout.Load(path);
            Assert.AreEqual(7, lay.Get("x", 7));
            Assert.AreEqual(7, lay.Get("y", 7));
            Assert.AreEqual(7, lay.Get("w", 7));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_MissingFile_AllDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"panel-nope-{Guid.NewGuid():N}.json");
        var lay = PanelLayout.Load(path);
        Assert.AreEqual(3, lay.Get("x", 3));
    }

    [TestMethod]
    public void Load_SamePath_GivesTheSameObject()
    {
        string path = WriteTemp("""{"x":10}""");
        try
        {
            Assert.AreSame(PanelLayout.Load(path), PanelLayout.Load(path));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ReloadAll_ReadsTheFileAgain()
    {
        string path = WriteTemp("""{"x":10}""");
        try
        {
            var lay = PanelLayout.Load(path);
            Assert.AreEqual(10, lay.Get("x", 0));
            File.WriteAllText(path, """{"x":20}""");
            PanelLayout.ReloadAll();
            Assert.AreEqual(20, lay.Get("x", 0));
        }
        finally { File.Delete(path); }
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"panel-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
