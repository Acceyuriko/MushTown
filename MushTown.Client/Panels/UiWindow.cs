using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// One openable window: a scene, a spot on the screen, and the rule to
// rebuild the scene when it moves. The panels that already exist keep
// their own code; the new windows (minimap, world map, key settings) use this.
public abstract class UiWindow
{
    // Every window that was made. The game walks this list, so the windows
    // are made in the game constructor and need no other wiring.
    public static readonly List<UiWindow> List = new();

    public bool Show;
    public Scene Scene = new();
    public Point Pos;         // the window top-left on the screen
    public HotkeyAction Action; // the key that opens it
    public Point? Drag;       // the grab offset while the title is held
    private Point _builtPos;
    public bool Built;        // the scene was built at least once

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

    // Build the scene for the current state.
    public abstract void Build();

    // The game closed the window (Esc). A window with a draft throws it away.
    public virtual void OnClosed() { }

    // Start a title drag: the game moves the window while the mouse is held.
    public void StartDrag() => Drag = new Point(Scene.LastClick.X - Pos.X, Scene.LastClick.Y - Pos.Y);

    // Rebuild the scene when the window moved.
    public void Sync()
    {
        if (!Built || Pos != _builtPos)
        {
            Build();
            _builtPos = Pos;
            Built = true;
        }
    }
}
