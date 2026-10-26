using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// One openable window: a host in the one UI scene, a spot on the screen,
// and the rule to rebuild the content when it moves or its state changes.
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
    private Point _builtPos;
    private bool _built;      // the content was built at least once
    public bool Dirty;        // the state changed: the content has to be built again

    protected UiWindow(HotkeyAction action)
    {
        Action = action;
        List.Add(this);
    }

    // The window box on the screen.
    public abstract Rectangle Rect();

    // The title strip the player drags the window by.
    public abstract Rectangle TitleRect();

    // The spot the layout file gives. A reload puts the window back here.
    public abstract Point LayoutPos();

    // Build the content for the current state.
    public abstract void Build();

    // The game closed the window (Esc). A window with a draft throws it away.
    public virtual void OnClosed() { }

    // Start a title drag: the game moves the window while the mouse is held.
    public void StartDrag() => Drag = new Point(Scene.LastClick.X - Pos.X, Scene.LastClick.Y - Pos.Y);

    // Rebuild the scene when the window moved or the state changed.
    public void Sync()
    {
        if (!_built || Pos != _builtPos || Dirty)
        {
            Dirty = false; // the flag is cleared first: a build that wants a new build says so
            Build();
            _builtPos = Pos;
            _built = true;
        }
    }
}
