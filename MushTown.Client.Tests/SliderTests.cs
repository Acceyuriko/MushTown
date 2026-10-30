using Microsoft.Xna.Framework;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The volume slider: the track takes the click and the drag, the knob shows the
// value.
[TestClass]
public class SliderTests
{
    // Any track at least a hundred pixels wider than the knob: the round trip below
    // holds for all of them. The shipped bgmW/sfxW are 193.
    private const int TrackW = 200;

    [TestMethod]
    public void VolumeAt_ClickingTheKnobKeepsItsVolume()
    {
        for (int volume = 0; volume <= 100; volume++)
        {
            int left = Slider.KnobX(volume, TrackW); // where the knob is drawn
            Assert.AreEqual(volume, Slider.VolumeAt(left + Slider.KnobW / 2, TrackW), $"volume {volume}");
        }
    }

    [TestMethod]
    public void VolumeAt_StaysInsideTheTrack()
    {
        Assert.AreEqual(0, Slider.VolumeAt(0, TrackW));
        Assert.AreEqual(100, Slider.VolumeAt(400, TrackW));
    }

    [TestMethod]
    public void Drag_MovesOnlyTheSliderThatWasPressed_AndSavesOnceWhenLetGo()
    {
        int bgm = 50, sfx = 50, saves = 0, sets = 0;
        var scene = new Scene();
        var bgmSlider = Build(scene, 0, () => bgm, v => bgm = v, () => saves++);
        var sfxSlider = Build(scene, 300, () => sfx, v => { sfx = v; sets++; }, () => saves++);
        scene.Layout();

        // The grabbed track does not start at x=0, so a drag that forgets the
        // track's own left edge reads the wrong volume here.
        sfxSlider.Grab(KnobCenter(300, 50)); // the knob's own center: the value stays
        Assert.AreEqual(50, sfx);

        sfxSlider.Drag(true, 450); // 150 from the track's left edge: louder
        bgmSlider.Drag(true, 450); // not grabbed: must not move
        Assert.AreEqual(Slider.VolumeAt(150, TrackW), sfx);
        Assert.IsTrue(sfx > 50, $"sfx {sfx}");
        Assert.AreEqual(50, bgm);
        Assert.AreEqual(0, saves); // still held

        sfxSlider.Drag(false, 450); // let go: one save
        Assert.AreEqual(1, saves);

        int after = sfx;
        sfxSlider.Drag(true, 5); // no longer grabbed
        Assert.AreEqual(after, sfx);
        Assert.AreEqual(1, saves);

        // Closing the window mid-drag lets go where the knob is, without moving it to
        // wherever the mouse went.
        sfxSlider.Grab(KnobCenter(300, after));
        int setsBefore = sets;
        sfxSlider.Release();
        Assert.AreEqual(setsBefore, sets);
        Assert.AreEqual(after, sfx);
        Assert.AreEqual(1, saves); // nothing moved: nothing to save
    }

    // A slider on a track whose left edge is at x, in a scene so Rect.Screen has a value.
    private static Slider Build(Scene scene, int x, Func<int> volume, Action<int> set, Action released)
    {
        var slider = new Slider(volume, set, released);
        var track = new GameObject();
        track.Add(new Rect(new Rectangle(x, 0, TrackW, 22)));
        track.Add(slider);
        scene.Add(track);
        return slider;
    }

    private static int KnobCenter(int trackX, int volume) =>
        trackX + Slider.KnobX(volume, TrackW) + Slider.KnobW / 2;
}
