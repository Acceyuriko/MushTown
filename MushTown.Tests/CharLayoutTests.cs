using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class CharLayoutTests
{
    private const string Full = """{"slotTopX":[1,2,3,4,5,6],"slotBotX":[7,8,9,10,11,12],"slotTopY":13,"slotBotY":14,"slotW":15,"slotH":16,"tagUp":17,"pillar":[18,19],"pill":[20,21],"board":[22,23],"boardCount":[24,25],"pager":[26,27],"pagerGlass":28,"pageL":[47,48],"pageR":[49,50],"new":[29,30],"delete":[31,32],"select":[33,34],"back":[35,36],"delPanel":[37,38],"delSize":[39,40],"delTextY":41,"delYes":[42,43],"delNo":[44,45],"errorY":46}""";

    // CharLayout fields are shared statics: leave them as found.
    private (int[] slotTopX, int[] slotBotX, int slotTopY, int slotBotY, int slotW, int slotH, int tagUp, (int X, int Y) pillar, (int X, int Y) pill, (int X, int Y) board, (int X, int Y) boardCount, (int X, int Y) pager, int pagerGlass, (int X, int Y) pageL, (int X, int Y) pageR, (int X, int Y) @new, (int X, int Y) delete, (int X, int Y) select, (int X, int Y) back, (int X, int Y) delPanel, (int X, int Y) delSize, int delTextY, (int X, int Y) delYes, (int X, int Y) delNo, int errorY) _saved;

    [TestInitialize]
    public void Save() => _saved = (CharLayout.SlotTopX, CharLayout.SlotBotX, CharLayout.SlotTopY, CharLayout.SlotBotY, CharLayout.SlotW, CharLayout.SlotH, CharLayout.TagUp, CharLayout.Pillar, CharLayout.Pill, CharLayout.Board, CharLayout.BoardCount, CharLayout.Pager, CharLayout.PagerGlass, CharLayout.PageL, CharLayout.PageR, CharLayout.New, CharLayout.Delete, CharLayout.Select, CharLayout.Back, CharLayout.DelPanel, CharLayout.DelSize, CharLayout.DelTextY, CharLayout.DelYes, CharLayout.DelNo, CharLayout.ErrorY);

    [TestCleanup]
    public void Restore() => (CharLayout.SlotTopX, CharLayout.SlotBotX, CharLayout.SlotTopY, CharLayout.SlotBotY, CharLayout.SlotW, CharLayout.SlotH, CharLayout.TagUp, CharLayout.Pillar, CharLayout.Pill, CharLayout.Board, CharLayout.BoardCount, CharLayout.Pager, CharLayout.PagerGlass, CharLayout.PageL, CharLayout.PageR, CharLayout.New, CharLayout.Delete, CharLayout.Select, CharLayout.Back, CharLayout.DelPanel, CharLayout.DelSize, CharLayout.DelTextY, CharLayout.DelYes, CharLayout.DelNo, CharLayout.ErrorY) = _saved;

    [TestMethod]
    public void Load_FullFile_OverridesAll()
    {
        string path = WriteTemp(Full);
        try
        {
            CharLayout.Load(path);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, CharLayout.SlotTopX);
            CollectionAssert.AreEqual(new[] { 7, 8, 9, 10, 11, 12 }, CharLayout.SlotBotX);
            Assert.AreEqual(13, CharLayout.SlotTopY);
            Assert.AreEqual(14, CharLayout.SlotBotY);
            Assert.AreEqual(15, CharLayout.SlotW);
            Assert.AreEqual(16, CharLayout.SlotH);
            Assert.AreEqual(17, CharLayout.TagUp);
            Assert.AreEqual((18, 19), CharLayout.Pillar);
            Assert.AreEqual((20, 21), CharLayout.Pill);
            Assert.AreEqual((22, 23), CharLayout.Board);
            Assert.AreEqual((24, 25), CharLayout.BoardCount);
            Assert.AreEqual((26, 27), CharLayout.Pager);
            Assert.AreEqual(28, CharLayout.PagerGlass);
            Assert.AreEqual((47, 48), CharLayout.PageL);
            Assert.AreEqual((49, 50), CharLayout.PageR);
            Assert.AreEqual((29, 30), CharLayout.New);
            Assert.AreEqual((31, 32), CharLayout.Delete);
            Assert.AreEqual((33, 34), CharLayout.Select);
            Assert.AreEqual((35, 36), CharLayout.Back);
            Assert.AreEqual((37, 38), CharLayout.DelPanel);
            Assert.AreEqual((39, 40), CharLayout.DelSize);
            Assert.AreEqual(41, CharLayout.DelTextY);
            Assert.AreEqual((42, 43), CharLayout.DelYes);
            Assert.AreEqual((44, 45), CharLayout.DelNo);
            Assert.AreEqual(46, CharLayout.ErrorY);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_PartialFile_KeepsMissing()
    {
        string path = WriteTemp(Full);
        try
        {
            CharLayout.Load(path);
            File.WriteAllText(path, """{"_help":"ignored","slotTopY":100}""");
            CharLayout.Load(path);
            Assert.AreEqual(100, CharLayout.SlotTopY);
            Assert.AreEqual(14, CharLayout.SlotBotY);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, CharLayout.SlotTopX);
            Assert.AreEqual((33, 34), CharLayout.Select);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadValue_ThrowsAndKeepsOld()
    {
        string path = WriteTemp(Full);
        try
        {
            string[] bad = ["{\"slotTopY\":100,\"slotTopX\":[1,2]}", "{\"slotTopY\":100,\"slotTopX\":[1,\"a\",3,4,5,6]}", "{\"slotTopY\":100,\"select\":[1]}", "{\"slotTopY\":\"bad\"}", "{\"slotTopY\":100,\"select\":[\"a\",1]}"];
            foreach (string json in bad)
            {
                CharLayout.Load(path);
                File.WriteAllText(path, json);
                try { CharLayout.Load(path); Assert.Fail($"expected a JsonException for {json}"); }
                catch (JsonException) { }
                Assert.AreEqual(13, CharLayout.SlotTopY, json);
                Assert.AreEqual((33, 34), CharLayout.Select, json);
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
            try { CharLayout.Load(path); Assert.Fail("expected a JsonException"); }
            catch (JsonException) { } // the runtime subtype is internal; the base type is the contract
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_MissingFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"charselect-layout-nope-{Guid.NewGuid():N}.json");
        Assert.ThrowsException<FileNotFoundException>(() => CharLayout.Load(path));
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"charselect-layout-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
