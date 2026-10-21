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
        if (!o.Enabled) return;
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
    public void Add(GameObject root) => _roots.Add(root);

    // Topmost-first (last child wins); the first hit takes it. Presses the
    // hit Button and returns the hit object (null = miss). An open modal
    // blanks hits outside its subtree (a bare container swallows too).
    public GameObject? Click(int gx, int gy)
    {
        LastClick = new Point(gx, gy);
        var hit = Hit(_roots, gx, gy);
        if (hit != null && AnyModal(_roots) && !InsideModal(hit)) hit = null;
        ClickButton = hit == null ? null : FindButton(hit);
        if (hit == null) return null;
        ClickButton?.Press();
        return hit;
    }

    // Clears a stale hover (a swallowed scene shows no highlight).
    public void ClearHover() { Hovered = null; HoverButton = null; }

    // Refreshes Hovered/HoverButton; returns the legacy hover id ("", none).
    public string Hover(int gx, int gy)
    {
        var hit = Hit(_roots, gx, gy);
        if (hit != null && AnyModal(_roots) && !InsideModal(hit)) hit = null;
        Hovered = hit;
        HoverButton = hit == null ? null : FindButton(hit);
        return HoverButton?.LegacyId ?? "";
    }

    public void Draw(UiGfx gfx)
    {
        gfx.Hovered = Hovered;
        gfx.HoverButton = HoverButton;
        foreach (var r in _roots) TreeDraw.DrawObject(r, gfx);
    }

    private static GameObject? Hit(IReadOnlyList<GameObject> objs, int gx, int gy)
    {
        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var o = objs[i];
            if (!o.Enabled || o.NoHit) continue;
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
            if (!o.Enabled) continue;
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
