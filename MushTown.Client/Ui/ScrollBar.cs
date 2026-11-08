using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// A vertical scroll bar: the owner rect is the track, the thumb rides it.
// The position is 0 at the top and 1 at the bottom; the owner turns that into
// a row or an item index. The component draws the track and handles the drag;
// the caller feeds it the held state and the mouse y once a frame.
// Two assumptions: the position is only read while _enabled() is true, so a
// disabled owner may leave it undefined; and the track is taller than the
// thumb, because Follow divides by the travel.
public sealed class ScrollBar : Component, IDrawable
{
    private readonly Func<float> _position;
    private readonly Action<float> _set;
    private readonly Func<bool> _enabled; // no thumb and no drag when false
    private readonly string _baseArt;
    private readonly string _thumbStem;
    private readonly int _thumbH;
    private bool _dragging;
    private int _grabDy; // where on the thumb the press landed

    public ScrollBar(Func<float> position, Action<float> set, Func<bool> enabled,
        string baseArt, string thumbStem, int thumbH)
    {
        _position = position;
        _set = set;
        _enabled = enabled;
        _baseArt = baseArt;
        _thumbStem = thumbStem;
        _thumbH = thumbH;
    }

    // A press on the track: jump there, then follow the mouse.
    public void Grab(int gy)
    {
        if (!_enabled()) return;
        var r = Owner.Get<Rect>()!.Screen;
        int travel = r.Height - _thumbH;
        int top = r.Y + (int)(travel * _position());
        _grabDy = Math.Clamp(gy - top, 0, _thumbH);
        _dragging = true;
        Follow(gy);
    }

    // Every frame while the button is held; the let-go frame still follows,
    // or a fast drag stops a frame short.
    public void Drag(bool held, int gy)
    {
        if (!_dragging) return;
        Follow(gy);
        if (!held) _dragging = false;
    }

    public void Release() => _dragging = false;

    private void Follow(int gy)
    {
        var r = Owner.Get<Rect>()!.Screen;
        int travel = r.Height - _thumbH;
        int top = Math.Clamp(gy - _grabDy - r.Y, 0, travel);
        _set((float)top / travel);
    }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex(_baseArt), r, Color.White);
        if (!_enabled()) return;
        string state = _dragging ? "2" : gfx.Hovered == Owner ? (gfx.Pressed ? "2" : "1") : "0";
        var thumb = gfx.Tex(_thumbStem + state + ".png");
        int travel = r.Height - _thumbH;
        int y = r.Y + (int)(travel * _position());
        gfx.Sb.Draw(thumb, new Rectangle(r.X + (r.Width - thumb.Width) / 2, y, thumb.Width, _thumbH), Color.White);
    }
}
