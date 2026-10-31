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

    // A file with a typo must not look like a working layout: at startup the
    // defaults keep the game alive, on a reload the error goes to the caller
    // (which logs it) instead of a silent no-op.
    [TestMethod]
    public void Load_BrokenJson_KeepsTheDefaults()
    {
        string path = WriteTemp("""{"x":10,,}""");
        try
        {
            var lay = PanelLayout.Load(path);
            Assert.AreEqual(3, lay.Get("x", 3));
        }
        finally { File.Delete(path); }
    }

    // One file with a typo must not stop the others: the broken file keeps the
    // numbers it had, the error goes back to the caller (which logs it), and
    // every other file still reloads.
    [TestMethod]
    public void ReloadAll_BrokenFile_KeepsItsNumbersAndReadsTheRest()
    {
        string good = WriteTemp("""{"x":10}""");
        string broken = WriteTemp("""{"x":10}""");
        try
        {
            var a = PanelLayout.Load(good);
            var b = PanelLayout.Load(broken);
            File.WriteAllText(good, """{"x":20}""");
            File.WriteAllText(broken, """{"x":10,,}""");
            var errors = PanelLayout.ReloadAll();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(20, a.Get("x", 0));
            Assert.AreEqual(10, b.Get("x", 0)); // the numbers it had stay
        }
        finally { File.Delete(good); File.Delete(broken); }
    }

    // Every shipped layout file has to parse. A bare quote in a help line kept
    // panel-party.json dead for days and nothing said so. This looks for the
    // repo above the test output: it is a checkout check, not a unit test.
    [TestMethod]
    public void TheShippedLayouts_Parse()
    {
        string? dir = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "layouts"))) { dir = Path.Combine(d.FullName, "layouts"); break; }
        if (dir == null) { Assert.Inconclusive("no layouts folder above the test output: this check needs the repo"); return; }
        var files = Directory.GetFiles(dir!, "*.json")
            .Where(f => Path.GetFileName(f).StartsWith("panel-") || Path.GetFileName(f).EndsWith("-layout.json")).ToArray(); // not a backup copy someone dropped next to them
        Assert.IsTrue(files.Length > 10, $"only {files.Length} layout files");
        foreach (string file in files)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)); // a typo throws
            Assert.AreEqual(System.Text.Json.JsonValueKind.Object, doc.RootElement.ValueKind, Path.GetFileName(file));
        }
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"panel-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
