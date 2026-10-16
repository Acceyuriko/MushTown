using MushTown.Shared;

namespace MushTown.Tests;

// The cursor brain: set selection plus frame advance (the OS hands
// stay untested: textures and Mouse.SetCursor need the game).
[TestClass]
public sealed class CursorTests
{
    private static CursorAnimData TwoFrames() => new()
    {
        Frames = new List<CursorAnimFrame>
        {
            new() { Image = "a.png", Delay = 100 },
            new() { Image = "b.png", Delay = 100 },
        }
    };

    [TestMethod]
    public void Select_Normal()
    {
        Assert.AreEqual(0, CursorAnim.Select("", false, true, false));
    }

    [TestMethod]
    public void Select_HoverBlink()
    {
        Assert.AreEqual(1, CursorAnim.Select("save0", false, true, false));
    }

    [TestMethod]
    public void Select_Pressed()
    {
        Assert.AreEqual(2, CursorAnim.Select("", false, true, true));
    }

    [TestMethod]
    public void Select_GrabBeatsHover()
    {
        Assert.AreEqual(3, CursorAnim.Select("save0", true, true, false));
    }

    [TestMethod]
    public void Select_FullHandsKeepGrabAnywhere()
    {
        Assert.AreEqual(3, CursorAnim.Select("", false, false, false));
        Assert.AreEqual(4, CursorAnim.Select("", false, false, true));
    }

    [TestMethod]
    public void Tick_StateChangeResetsAndApplies()
    {
        var c = new CursorAnim { State = 1, Frame = 1 };
        Assert.IsTrue(c.Tick(2, new CursorAnimData(), 16));
        Assert.AreEqual(2, c.State);
        Assert.AreEqual(0, c.Frame);
    }

    [TestMethod]
    public void Tick_NormalAndFrozenNeverApply()
    {
        var c = new CursorAnim();
        Assert.IsFalse(c.Tick(0, null, 1000));
        c.State = 4;
        Assert.IsFalse(c.Tick(4, null, 1000));
    }

    [TestMethod]
    public void Tick_SingleFrameNeverAdvances()
    {
        var c = new CursorAnim { State = 2 };
        Assert.IsFalse(c.Tick(2, new CursorAnimData(), 10000));
        Assert.AreEqual(0, c.Frame);
    }

    [TestMethod]
    public void Tick_AdvancesAndWraps()
    {
        var c = new CursorAnim { State = 1 };
        var data = TwoFrames();
        Assert.IsFalse(c.Tick(1, data, 50));
        Assert.IsTrue(c.Tick(1, data, 60));
        Assert.AreEqual(1, c.Frame);
        Assert.IsTrue(c.Tick(1, data, 100));
        Assert.AreEqual(0, c.Frame);
    }
}
