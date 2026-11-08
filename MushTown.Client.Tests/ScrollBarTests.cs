using Microsoft.Xna.Framework;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The vertical scroll bar: the track takes the click and the drag, the thumb
// shows the position.
[TestClass]
public class ScrollBarTests
{
    private const int TrackH = 368, ThumbH = 28;

    [TestMethod]
    public void Grab_JumpsToThePressedSpot()
    {
        float position = 0;
        var bar = Build(new Scene(), 100, 50, () => position, v => position = v, () => true);

        bar.Grab(50 - 10); // above the thumb: the clamp holds it at the top
        Assert.AreEqual(0f, position);

        bar.Grab(50 + TrackH - 5); // below it: the press lands on the thumb's bottom edge
        Assert.AreEqual((TrackH - 5 - ThumbH) / (float)(TrackH - ThumbH), position, 0.001f);
    }

    [TestMethod]
    public void Drag_FollowsTheMouseFromWhereItWasGrabbed()
    {
        float position = 0;
        var bar = Build(new Scene(), 100, 50, () => position, v => position = v, () => true);

        int top = 50; // position 0: the thumb's top is the track's top
        bar.Grab(top + ThumbH / 2);
        Assert.AreEqual(0f, position);

        int half = 50 + (TrackH - ThumbH) / 2;
        bar.Drag(true, half + ThumbH / 2);
        Assert.AreEqual(0.5f, position, 0.01f);

        bar.Drag(false, half + ThumbH / 2);
        Assert.AreEqual(0.5f, position, 0.01f);
    }

    [TestMethod]
    public void Drag_OnlyMovesTheBarThatWasGrabbed()
    {
        var scene = new Scene();
        float one = 0, two = 0;
        var first = Build(scene, 0, 0, () => one, v => one = v, () => true);
        var second = Build(scene, 300, 0, () => two, v => two = v, () => true);

        first.Grab(ThumbH / 2);
        second.Grab(ThumbH / 2); // grabbing one bar must not release the other
        first.Drag(true, TrackH / 2);
        Assert.AreEqual(0.5f, one, 0.01f);
        Assert.AreEqual(0f, two);

        second.Drag(true, TrackH - ThumbH / 2);
        Assert.AreEqual(0.5f, one, 0.01f);
        Assert.AreEqual(1f, two, 0.01f);
    }

    [TestMethod]
    public void Drag_ClampsToTheEnds()
    {
        float position = 0.5f;
        var bar = Build(new Scene(), 0, 0, () => position, v => position = v, () => true);

        bar.Grab(TrackH / 2);
        bar.Drag(true, -1000);
        Assert.AreEqual(0f, position);

        bar.Drag(true, 5000);
        Assert.AreEqual(1f, position);
    }

    [TestMethod]
    public void DisabledBar_IgnoresPressAndDrag()
    {
        float position = 0.25f;
        var bar = Build(new Scene(), 0, 0, () => position, v => position = v, () => false);

        bar.Grab(TrackH / 2);
        bar.Drag(true, TrackH - ThumbH);
        Assert.AreEqual(0.25f, position);
    }

    [TestMethod]
    public void Release_StopsFollowingTheMouse()
    {
        float position = 0;
        var bar = Build(new Scene(), 0, 0, () => position, v => position = v, () => true);

        bar.Grab(ThumbH / 2);
        bar.Drag(true, TrackH / 2);
        float mid = position;
        bar.Release();
        bar.Drag(true, TrackH - ThumbH);

        Assert.AreEqual(mid, position);
    }

    // A bar on a track whose top is at (x, y), in the given scene so Rect.Screen has a value.
    private static ScrollBar Build(Scene scene, int x, int y, Func<float> position, Action<float> set, Func<bool> enabled)
    {
        var bar = new ScrollBar(position, set, enabled, "track", "thumb", ThumbH);
        var track = new GameObject();
        track.Add(new Rect(new Rectangle(x, y, 5, TrackH)));
        track.Add(bar);
        scene.Add(track);
        scene.Layout();
        return bar;
    }
}
