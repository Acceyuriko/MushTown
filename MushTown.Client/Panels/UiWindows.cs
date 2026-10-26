using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The windows that use the UiWindow base class: the game opens them with
// a key, and their hosts ride the one UI scene. A click moves the window to
// the end of the list, and the Z is 300 + that order (pinned windows first),
// so draw, hit and Esc all read the same order.
public partial class MushTownGame
{
    // The window a key opens, or null.
    private UiWindow? WindowFor(HotkeyAction action) => UiWindow.List.FirstOrDefault(w => w.Action == action);

    // A key opened a window: flip it.
    private void ToggleWindow(HotkeyAction action)
    {
        var w = WindowFor(action);
        if (w == null) return;
        w.Show = !w.Show;
        if (w.Show) { w.Dirty = true; if (!Pinned(w)) Raise(w); } // it opens on top, with fresh content
        PlaySfx("click");
    }

    // Open a window (the options window opens the key settings). The content
    // is built by the next SyncWindows call, not here.
    internal void ShowWindow(HotkeyAction action)
    {
        var w = WindowFor(action);
        if (w == null) return;
        w.Show = true;
        w.Dirty = true; // the window opens with fresh content
        if (!Pinned(w)) Raise(w); // on top of the ones already open
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

    // Build the visible windows, then give every host its Z.
    private void SyncWindows()
    {
        foreach (var w in UiWindow.List)
        {
            w.Host.Enabled = w.Show;
            if (w.Show) w.Sync();
        }
        SyncWindowZ();
    }

    // The host Z follows the list (pinned first): last in the list draws last and hits first.
    // Pinned windows stay under the rest, in their own order, so a window
    // opened later always lands above the chat and the minimap.
    private void SyncWindowZ()
    {
        int z = WindowZ;
        foreach (var w in UiWindow.List) if (Pinned(w)) SetRootZ(w.Host, z++);
        foreach (var w in UiWindow.List) if (!Pinned(w)) SetRootZ(w.Host, z++);
    }

    // A click brings the window to the front.
    private void Raise(UiWindow w)
    {
        UiWindow.List.Remove(w);
        UiWindow.List.Add(w);
        SyncWindowZ(); // the draw follows Update: the new order has to be in the tree now
    }

    // The chat and the minimap are the HUD itself: they never come to the
    // front and Esc never closes them. SyncWindowZ keeps them under the rest.
    private bool Pinned(UiWindow w) => w == _chatWindow || w.Action == HotkeyAction.Minimap;

    // Esc closes one layer: the front window, else the front panel of the
    // HUD. The official client closes the topmost thing, not everything.
    private bool EscCloseFront()
    {
        UiWindow? front = null; // by Z, like the draw and the hit: pinned windows never close, never win
        foreach (var w in UiWindow.List)
            if (w.Show && !Pinned(w) && (front == null || w.Host.Z > front.Host.Z)) front = w;
        if (front != null) { front.OnClosed(); front.Show = false; return true; }
        int panel = FrontPanel();
        if (panel < 0) return false;
        HidePanel(panel); // the raise order stays: the next open panel is now the front
        return true;
    }
}
