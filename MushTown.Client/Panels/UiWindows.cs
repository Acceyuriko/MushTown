using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The windows that use the UiWindow base class: the game opens them with
// a key, draws them above the older panels, and lets them take the clicks.
public partial class MushTownGame
{
    // The windows that are open (the creation order).
    private List<UiWindow> OpenWindows() => UiWindow.List.Where(w => w.Show).ToList();

    // The window a key opens, or null.
    private UiWindow? WindowFor(HotkeyAction action) => UiWindow.List.FirstOrDefault(w => w.Action == action);

    // A key opened a window: flip it.
    private void ToggleWindow(HotkeyAction action)
    {
        var w = WindowFor(action);
        if (w == null) return;
        w.Show = !w.Show;
        w.Sync();
        PlaySfx("click");
    }

    // Open a window (the options window opens the key settings). The scene
    // is built by the next SyncWindows call, not here.
    internal void ShowWindow(HotkeyAction action)
    {
        var w = WindowFor(action);
        if (w == null) return;
        w.Show = true;
    }

    // Move a window that its title is dragging.
    private void DragWindows(bool held, int gx, int gy)
    {
        foreach (var w in UiWindow.List)
        {
            if (!w.Show) { w.Drag = null; continue; }
            if (!w.Drag.HasValue) continue;
            if (!held) { w.Drag = null; continue; }
            w.Pos = ClampPanel(new Point(gx - w.Drag.Value.X, gy - w.Drag.Value.Y), w.Rect().Width, w.Rect().Height);
        }
    }

    // Build the scenes of the open windows.
    private void SyncWindows()
    {
        foreach (var w in UiWindow.List) if (w.Show) w.Sync();
    }

    // Draw the open windows above the older panels. The last one drawn is
    // the top one.
    private void DrawWindows()
    {
        foreach (var w in OpenWindows()) w.Scene.Draw(_gfx);
    }

    // The click goes to the topmost open window that has the point.
    private bool ClickWindows(int gx, int gy)
    {
        for (int i = UiWindow.List.Count - 1; i >= 0; i--)
        {
            var w = UiWindow.List[i];
            if (!w.Show || !w.Rect().Contains(gx, gy)) continue;
            ClickTree(w.Scene, gx, gy);
            return true;
        }
        return false;
    }

    // The hover goes to the same window.
    private bool HoverWindows(int gx, int gy)
    {
        bool hit = false;
        for (int i = UiWindow.List.Count - 1; i >= 0; i--)
        {
            var w = UiWindow.List[i];
            if (!w.Show) continue;
            // The top window that has the point takes the hover. Every other
            // open window drops its old hover, or it keeps a lit row.
            if (!w.Rect().Contains(gx, gy) || hit) { w.Scene.ClearHover(); continue; }
            SfxOnHover(w.Scene.Hover(gx, gy));
            hit = true;
        }
        if (!hit) SfxOnHover(""); // no window has the mouse: the last hover id goes away
        return hit;
    }
}
