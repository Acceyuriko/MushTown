using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Draw services for one frame. The game fills the stable refs once;
// Scene sets Hovered before drawing. Passed per call, never stored in
// the core below: Update and hit-testing stay headless-safe (the rule
// from the object-component plan; the server never calls Draw).
public sealed class UiGfx
{
    public required SpriteBatch Sb { get; init; }
    public required UiText Ui { get; init; }
    public required Func<string, Texture2D> Tex { get; init; }
    public required Texture2D Pixel { get; init; }
    public Texture2D Blur = null!; // the screen at 1/4 size: the frosted glass source
    public bool Pressed;
    public float Time; // the frame time in ms: the animated parts (the chat caret) blink on it
    public Point Mouse; // the pointer in frame coords, set by DrawHud only: the held piece and the tips follow it
    public GameObject? Hovered;
    public Button? HoverButton;
}

// Visual components implement this (the Shared core has no Draw: the
// server never draws). The walker below collects them per object.
public interface IDrawable
{
    void Draw(UiGfx gfx);
}

// Draws one object (self) or a subtree (self + children).
public static class TreeDraw
{
    public static void DrawSelf(GameObject o, UiGfx gfx)
    {
        foreach (var c in o.Components) if (c is IDrawable d) d.Draw(gfx);
    }

    public static void DrawObject(GameObject o, UiGfx gfx)
    {
        if (!o.Enabled || o.Dead) return; // a dead node is gone: step, draw, hit and modal all skip it
        DrawSelf(o, gfx);
        foreach (var ch in o.Children) DrawObject(ch, gfx);
    }
}

public sealed class Scene
{
    private readonly List<GameObject> _roots = new();
    public GameObject? Hovered { get; private set; }
    public Button? HoverButton { get; private set; }
    public Point LastClick { get; private set; } // title drags anchor their offset here (first user: W4)
    public Button? ClickButton { get; private set; } // the pressed Button (ClickTree plays its Kind)
    public void Add(GameObject root)
    {
        // Kept sorted by Z, ties by add order: Draw walks it forward,
        // Hit walks it backward, so one list is the only z rule.
        int i = _roots.Count;
        while (i > 0 && _roots[i - 1].Z > root.Z) i--;
        _roots.Insert(i, root);
    }

    // Repoint an added root at a new Z (a click brings a panel to front).
    public void Move(GameObject root, int z)
    {
        root.Z = z;
        if (_roots.Remove(root)) Add(root); // not in the tree yet: Add sorts it by this Z later
    }

    // Topmost-first (last child wins); the first hit takes it. Presses the
    // hit Button and returns the hit object (null = miss). An open modal
    // blanks hits outside its subtree (a bare container swallows too).
    public GameObject? Click(int gx, int gy)
    {
        LastClick = new Point(gx, gy);
        var hit = TopAt(gx, gy);
        ClickButton = hit == null ? null : FindButton(hit);
        if (hit == null) return null;
        ClickButton?.Press();
        return hit;
    }

    // The topmost object at the point, nothing pressed: a button may rebuild
    // its layer, so the caller reads the owner before the press detaches it.
    public GameObject? TopAt(int gx, int gy)
    {
        Layout(); // the hit test reads Screen: a build since the last frame must not leave it stale
        var hit = Hit(_roots, gx, gy);
        return hit != null && AnyModal(_roots) && !InsideModal(hit) ? null : hit;
    }

    // Fills every Rect.Screen from the tree: one walk, the parent's origin plus
    // the local offset. Hit, hover and draw all read Screen, so a moved window
    // is a moved root, not a rebuilt tree.
    public void Layout() => Layout(_roots, 0, 0);

    private static void Layout(IReadOnlyList<GameObject> objs, int ox, int oy)
    {
        foreach (var o in objs)
        {
            int x = ox, y = oy;
            if (o.Get<Rect>() is { } r)
            {
                r.Screen = new Rectangle(ox + r.Offset.X, oy + r.Offset.Y, r.Size.X, r.Size.Y);
                x = r.Screen.X;
                y = r.Screen.Y;
            }
            Layout(o.Children, x, y);
        }
    }

    // A closed layer must not keep a lit row: Esc drops the hover with it.
    public void ClearHover() { Hovered = null; HoverButton = null; }

    // Refreshes Hovered/HoverButton; returns the legacy hover id ("", none).
    public string Hover(int gx, int gy)
    {
        var hit = TopAt(gx, gy);
        Hovered = hit;
        HoverButton = hit == null ? null : FindButton(hit);
        return HoverButton?.LegacyId ?? "";
    }

    public void Draw(UiGfx gfx)
    {
        Layout(); // the draw reads Screen too
        gfx.Hovered = Hovered;
        gfx.HoverButton = HoverButton;
        foreach (var r in _roots) TreeDraw.DrawObject(r, gfx);
    }

    private static GameObject? Hit(IReadOnlyList<GameObject> objs, int gx, int gy)
    {
        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var o = objs[i];
            if (!o.Enabled || o.NoHit || o.Dead) continue;
            var child = Hit(o.Children, gx, gy);
            if (child != null) return child;
            var r = o.Get<Rect>();
            if (r != null && r.Contains(gx, gy)) return o;
        }
        return null;
    }

    private static bool InsideModal(GameObject o)
    {
        for (var p = o; p != null; p = p.Parent)
            if (p.Modal) return true;
        return false;
    }

    // A click lands on the visual child; the Button lives on the row.
    private static Button? FindButton(GameObject o)
    {
        for (var p = o; p != null; p = p.Parent)
        {
            var b = p.Get<Button>();
            if (b != null) return b;
        }
        return null;
    }

    private static bool AnyModal(IReadOnlyList<GameObject> objs)
    {
        foreach (var o in objs)
        {
            if (!o.Enabled || o.Dead) continue;
            if (o.Modal || AnyModal(o.Children)) return true;
        }
        return false;
    }
}

public sealed class SceneManager
{
    public Scene? Current { get; private set; }
    public void Show(Scene? scene) => Current = scene;
}
