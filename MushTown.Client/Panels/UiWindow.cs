using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// One openable window: a host in the one UI scene, a spot on the screen,
// and the rule to rebuild the content when its state changes. A move only
// repoints the host: the children are local and ride it.
public abstract class UiWindow
{
    // Every window that was made. The game walks this list, so the windows
    // are made in the game constructor and need no other wiring.
    public static readonly List<UiWindow> List = new();

    public bool Show;
    public readonly GameObject Host = new(); // the window's root in the one UI scene: its Z is the draw and hit order
    public Scene Scene = null!; // the one UI scene, set when the game builds its HUD: the click point is read from it
    public Point Pos;         // the window top-left on the screen
    public HotkeyAction Action; // the key that opens it
    public Point? Drag;       // the grab offset while the title is held
    private bool _built;      // the content was built at least once
    public bool Dirty;        // the state changed: the content has to be built again

    protected UiWindow(HotkeyAction action)
    {
        Action = action;
        List.Add(this);
    }

    // The window box, relative to its own top-left corner.
    public abstract Rectangle Rect();

    // The title strip the player drags the window by, in the same local coords.
    public abstract Rectangle TitleRect();

    // The spot the layout file gives. A reload puts the window back here.
    public abstract Point LayoutPos();

    // The window box on the screen. The rects a window makes are local, so a
    // hand drawn part or a hit test reads this. Pos is always current, so this
    // needs no layout pass.
    public Rectangle ScreenBox() => OnScreen(Rect());

    // A local box of this window on the screen (a window that draws its own
    // parts cannot read the tree's Screen).
    public Rectangle OnScreen(Rectangle local) => new(Pos.X + local.X, Pos.Y + local.Y, local.Width, local.Height);

    // Point a root at a spot without touching its children: a root that was
    // built once keeps its Rect, so a move only writes the offset.
    public static void Place(GameObject host, Point offset, Point size)
    {
        var r = host.Get<Rect>() ?? host.Add(new Rect(new Rectangle()));
        r.Offset = offset;
        r.Size = size;
    }

    // Build the content for the current state. Every rect it makes is local.
    public abstract void Build();

    // The game closed the window (Esc). A window with a draft throws it away.
    public virtual void OnClosed() { }

    // Start a title drag: the game moves the window while the mouse is held.
    public void StartDrag() => Drag = new Point(Scene.LastClick.X - Pos.X, Scene.LastClick.Y - Pos.Y);

    // Rebuild the content when the state changed. A move only repoints the
    // host rect: the children ride it, so the tree is not rebuilt.
    public void Sync()
    {
        if (!_built || Dirty)
        {
            Dirty = false; // the flag is cleared first: a build that wants a new build says so
            Build();
            _built = true;
        }
        Place(Host, Pos, Rect().Size);
    }
}
