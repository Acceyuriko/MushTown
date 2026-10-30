namespace MushTown.Shared;

// The cursor brain: which set shows, which frame plays. The OS hands
// (scaled textures, Mouse.SetCursor) stay in the client. States:
// 0 normal, 1 hover blink, 2 click hand, 3 grab, 4 grab frozen.
public sealed class CursorAnim
{
    public int State;
    public int Frame;
    private float _t;

    public static int Select(string hoverId, bool hoverGrab, bool cursorFree, bool pressed) =>
        hoverGrab || !cursorFree ? pressed ? 4 : 3 : pressed ? 2 : hoverId != "" ? 1 : 0;

    // True means re-apply the OS cursor. States 0/4 never advance;
    // single-frame sets (click) never advance either. Data must be
    // non-null for states 1-3 (0/4 return before touching it).
    public bool Tick(int want, CursorAnimData? data, float dtMs)
    {
        if (want != State) { State = want; Frame = 0; _t = 0; return true; }
        if (want == 0 || want == 4) return false;
        var frames = data!.Frames;
        if (frames.Count < 2) return false;
        _t += dtMs;
        if (_t < frames[Frame].Delay) return false;
        _t -= frames[Frame].Delay;
        Frame = (Frame + 1) % frames.Count;
        return true;
    }
}
