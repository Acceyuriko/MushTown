using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// One slider: the owner's rect is the knob's box, so the knob draws at its top-left
// and the rect is the hit box. The knob shows the value.
// The object owns its own drag: pressed on the track, the knob follows the mouse
// until the button is let go. It reads the value every frame, so a move shows
// without a rebuild.
public sealed class Slider : Component, IDrawable
{
    public const int KnobW = 20; // the knob art
    public const string KnobArt = "ui/opt_knob.png";

    private readonly Func<int> _volume; // the value, 0-100
    private readonly Action<int> _set;  // ask for a new value
    private readonly Action _released;  // the gesture changed the value: the game saves it once
    private bool _dragging;
    private int _startVolume; // the value when the drag started

    public Slider(Func<int> volume, Action<int> set, Action released)
    {
        _volume = volume;
        _set = set;
        _released = released;
    }

    // How far the knob may move: the track minus the knob.
    private static int Travel(int trackW) => trackW - KnobW;

    // The knob's left edge for a volume (truncated, like the draw).
    public static int KnobX(int volume, int trackW) => volume * Travel(trackW) / 100;

    // The volume a click asks for, clickX counted from the track's left edge. The
    // knob is drawn with a truncated offset, so the click is read the same way: a
    // click on the knob's own center gives its own volume back, and the knob stays
    // put. Exact while the travel is wider than 100 (the 193px track here); a
    // narrower track can put two volumes on one knob spot.
    public static int VolumeAt(int clickX, int trackW)
    {
        int left = clickX - KnobW / 2; // the knob's left edge under the click
        return Math.Clamp((left + 1) * 100 / Travel(trackW), 0, 100); // the +1 undoes the truncated draw
    }

    // A press on the track: grab the knob and jump to the click.
    public void Grab(int gx)
    {
        _dragging = true;
        _startVolume = _volume();
        Follow(gx);
    }

    // Every frame while the button is held: only the grabbed slider moves. The
    // let-go frame still follows, or a fast drag stops a frame short.
    public void Drag(bool held, int gx)
    {
        if (!_dragging) return;
        Follow(gx);
        if (!held) Release();
    }

    // Let go where the knob is. The window closing calls this: the mouse may be
    // somewhere else, and the knob is not visible any more to show the jump.
    public void Release()
    {
        if (!_dragging) return;
        _dragging = false;
        if (_volume() != _startVolume) _released(); // nothing moved: nothing to save
    }

    private void Follow(int gx)
    {
        var s = Owner.Get<Rect>()!.Screen;
        _set(VolumeAt(gx - s.X, s.Width));
    }

    public void Draw(UiGfx gfx)
    {
        var s = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex(KnobArt), new Vector2(s.X + KnobX(_volume(), s.Width), s.Y), Color.White);
    }
}
