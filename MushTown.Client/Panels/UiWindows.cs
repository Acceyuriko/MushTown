using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The windows that use the UiWindow base class: the game opens them with
// a key, and their hosts ride the one UI scene. A click moves the window to
// the end of the raise row (the panels ride it too), so draw, hit and Esc
// all read that one order.
public partial class MushTownGame
{
    // The key window, whenever one is open.
    private KeyConfigWindow? KeyWindow => WindowFor(HotkeyAction.KeyConfig) as KeyConfigWindow;

    // The window a key opens, or null.
    // A loop, not LINQ: the strip asks for the key window up to twice per cell per frame.
    private UiWindow? WindowFor(HotkeyAction action)
    {
        foreach (var w in UiWindow.List)
            if (w.Action == action) return w;
        return null;
    }

    // A key or a menu icon opens a window, raises it when it is behind, closes it when it is in front.
    private void ToggleWindow(HotkeyAction action)
    {
        var w = WindowFor(action);
        if (w == null) return;
        if (!w.Show) { ShowWindow(action); return; } // fresh content, on top of the ones already open
        if (FrontHost() == w.Host) { w.OnClosed(); w.Show = false; return; }
        if (!Pinned(w)) Raise(w);
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

    // Build the visible windows (the Z is the raise row's, see RenumberRow).
    private void SyncWindows()
    {
        foreach (var w in UiWindow.List)
        {
            w.Host.Enabled = w.Show;
            if (w.Show) w.Sync();
        }
    }

    // A click brings the window to the front: the same row the panels ride.
    private void Raise(UiWindow w) => RaiseHost(w.Host);

    // The chat and the minimap are the HUD itself: they never come to the
    // front and Esc never closes them. The constructor puts them at the
    // bottom of the raise row, so every window and panel opens above them.
    private bool Pinned(UiWindow w) => w == _chatWindow || w.Action == HotkeyAction.Minimap;

    // The window a hit object belongs to, or null (a panel, the HUD). The hit
    // is usually a child: every window box has a click catcher, so the root is
    // what identifies the window.
    private UiWindow? WindowOf(GameObject? o)
    {
        var host = RootOf(o);
        return host == null ? null : UiWindow.List.FirstOrDefault(w => w.Host == host);
    }

    // Esc closes one layer: the top of the raise row, a panel or a window.
    // The official client closes the topmost thing, not everything.
    private bool EscCloseFront()
    {
        var host = FrontHost(); // the top of the screen, the same lookup the quick icons use
        if (host == null) return false;
        if (WindowOf(host) is UiWindow w)
        {
            if (Pinned(w)) return false; // only pinned windows are open: nothing to close
            w.OnClosed(); w.Show = false; return true;
        }
        HidePanel(PanelOf(host));
        return true;
    }
}
