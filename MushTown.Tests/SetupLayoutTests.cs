using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SetupLayoutTests
{
    private const string Full = """
    {"panel":[1,2],"avatar":[3,4],"cardSize":[5,6],"cardBot":[7,8],"cardBotSize":[9,10],
     "nameField":[11,12],"nameFieldSize":[13,14],"nameFont":15,"nameText":[16,17],
     "caret":[18,19],"caretGap":20,"className":[21,22],"classNameSize":[23,24],
     "classTint":[25,26,27],"confirm":[28,29],"confirmSize":[30,31],
     "cancel":[32,33],"cancelSize":[34,35],"errorFont":36,"error":[37,38]}
    """;

    // SetupLayout fields are shared statics: leave them as found.
    private ((int X, int Y) panel, (int X, int Y) avatar, (int W, int H) cardSize, (int X, int Y) cardBot, (int W, int H) cardBotSize, (int X, int Y) nameField, (int W, int H) nameFieldSize, int nameFont, (int X, int Y) nameText, (int W, int H) caret, int caretGap, (int X, int Y) className, (int W, int H) classNameSize, (int R, int G, int B) classTint, (int X, int Y) confirm, (int W, int H) confirmSize, (int X, int Y) cancel, (int W, int H) cancelSize, int errorFont, (int X, int Y) error) _saved;

    [TestInitialize]
    public void Save() => _saved = (SetupLayout.Panel, SetupLayout.Avatar, SetupLayout.CardSize, SetupLayout.CardBot, SetupLayout.CardBotSize, SetupLayout.NameField, SetupLayout.NameFieldSize, SetupLayout.NameFont, SetupLayout.NameText, SetupLayout.Caret, SetupLayout.CaretGap, SetupLayout.ClassName, SetupLayout.ClassNameSize, SetupLayout.ClassTint, SetupLayout.Confirm, SetupLayout.ConfirmSize, SetupLayout.Cancel, SetupLayout.CancelSize, SetupLayout.ErrorFont, SetupLayout.Error);

    [TestCleanup]
    public void Restore() => (SetupLayout.Panel, SetupLayout.Avatar, SetupLayout.CardSize, SetupLayout.CardBot, SetupLayout.CardBotSize, SetupLayout.NameField, SetupLayout.NameFieldSize, SetupLayout.NameFont, SetupLayout.NameText, SetupLayout.Caret, SetupLayout.CaretGap, SetupLayout.ClassName, SetupLayout.ClassNameSize, SetupLayout.ClassTint, SetupLayout.Confirm, SetupLayout.ConfirmSize, SetupLayout.Cancel, SetupLayout.CancelSize, SetupLayout.ErrorFont, SetupLayout.Error) = _saved;

    [TestMethod]
    public void Load_FullFile_OverridesAll()
    {
        string path = WriteTemp(Full);
        try
        {
            SetupLayout.Load(path);
            Assert.AreEqual((1, 2), SetupLayout.Panel);
            Assert.AreEqual((3, 4), SetupLayout.Avatar);
            Assert.AreEqual((5, 6), SetupLayout.CardSize);
            Assert.AreEqual((7, 8), SetupLayout.CardBot);
            Assert.AreEqual((9, 10), SetupLayout.CardBotSize);
            Assert.AreEqual((11, 12), SetupLayout.NameField);
            Assert.AreEqual((13, 14), SetupLayout.NameFieldSize);
            Assert.AreEqual(15, SetupLayout.NameFont);
            Assert.AreEqual((16, 17), SetupLayout.NameText);
            Assert.AreEqual((18, 19), SetupLayout.Caret);
            Assert.AreEqual(20, SetupLayout.CaretGap);
            Assert.AreEqual((21, 22), SetupLayout.ClassName);
            Assert.AreEqual((23, 24), SetupLayout.ClassNameSize);
            Assert.AreEqual((25, 26, 27), SetupLayout.ClassTint);
            Assert.AreEqual((28, 29), SetupLayout.Confirm);
            Assert.AreEqual((30, 31), SetupLayout.ConfirmSize);
            Assert.AreEqual((32, 33), SetupLayout.Cancel);
            Assert.AreEqual((34, 35), SetupLayout.CancelSize);
            Assert.AreEqual(36, SetupLayout.ErrorFont);
            Assert.AreEqual((37, 38), SetupLayout.Error);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_PartialFile_KeepsMissing()
    {
        string path = WriteTemp(Full);
        try
        {
            SetupLayout.Load(path);
            File.WriteAllText(path, """{"_help":"ignored","nameFont":20,"className":[100,200]}""");
            SetupLayout.Load(path);
            Assert.AreEqual(20, SetupLayout.NameFont);
            Assert.AreEqual((100, 200), SetupLayout.ClassName);
            Assert.AreEqual((1, 2), SetupLayout.Panel);
            Assert.AreEqual((23, 24), SetupLayout.ClassNameSize);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_BadValue_ThrowsAndKeepsOld()
    {
        string path = WriteTemp(Full);
        try
        {
            string[] bad =
            [
                """{"avatar":[100,200],"panel":[1]}""",
                """{"avatar":[100,200],"panel":["a",1]}""",
                """{"avatar":[100,200],"classTint":[1,2]}""",
                """{"avatar":[100,200],"cardSize":[1,2,3]}""",
                """{"avatar":[100,200],"nameFont":0}""",
                """{"avatar":[100,200],"nameFeild":[1,2]}""",
                """{"avatar":[100,200],"classTint":[300,0,0]}""",
            ];
            foreach (string json in bad)
            {
                SetupLayout.Load(path);
                File.WriteAllText(path, json);
                try { SetupLayout.Load(path); Assert.Fail($"expected a JsonException for {json}"); }
                catch (JsonException) { }
                Assert.AreEqual((1, 2), SetupLayout.Panel, json);
                Assert.AreEqual((3, 4), SetupLayout.Avatar, json);
                File.WriteAllText(path, Full);
            }
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Load_MissingFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"setup-layout-nope-{Guid.NewGuid():N}.json");
        Assert.ThrowsException<FileNotFoundException>(() => SetupLayout.Load(path));
    }

    private static string WriteTemp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"setup-layout-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
