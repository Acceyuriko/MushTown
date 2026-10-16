using Microsoft.Xna.Framework.Graphics;

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
    public bool Pressed;
    public GameObject? Hovered;
    public Button? HoverButton;
}

public abstract class Component
{
    public GameObject Owner { get; internal set; } = null!;
    public virtual void Draw(UiGfx gfx) { }
}

public sealed class GameObject
{
    public bool Enabled = true;
    public bool Modal; // swallows clicks below, even outside its rect (first user: delete modal, W2)
    public GameObject? Parent { get; private set; }
    private readonly List<Component> _components = new();
    private readonly List<GameObject> _children = new();
    public IReadOnlyList<GameObject> Children => _children;
    public T Add<T>(T c) where T : Component { c.Owner = this; _components.Add(c); return c; }
    public T? Get<T>() where T : Component
    {
        foreach (var c in _components) if (c is T t) return t;
        return null;
    }
    public void AddChild(GameObject ch) { ch.Parent = this; _children.Add(ch); }
    public void DrawSelf(UiGfx gfx) { foreach (var c in _components) c.Draw(gfx); }
}

public sealed class Scene
{
    private readonly List<GameObject> _roots = new();
    public GameObject? Hovered { get; private set; }
    public Button? HoverButton { get; private set; }
    public void Add(GameObject root) => _roots.Add(root);

    // Topmost-first (last child wins); the first hit takes it. True when
    // something took the click (a bare container swallows too).
    public bool Click(int gx, int gy)
    {
        var hit = Hit(_roots, gx, gy);
        if (hit == null) return AnyModal(_roots);
        FindButton(hit)?.Press();
        return true;
    }

    // Refreshes Hovered/HoverButton; returns the legacy hover id ("", none).
    public string Hover(int gx, int gy)
    {
        var hit = Hit(_roots, gx, gy);
        Hovered = hit;
        HoverButton = hit == null ? null : FindButton(hit);
        return HoverButton?.LegacyId ?? "";
    }

    public void Draw(UiGfx gfx)
    {
        gfx.Hovered = Hovered;
        gfx.HoverButton = HoverButton;
        foreach (var r in _roots) DrawObject(r, gfx);
    }

    private static void DrawObject(GameObject o, UiGfx gfx)
    {
        if (!o.Enabled) return;
        o.DrawSelf(gfx);
        foreach (var ch in o.Children) DrawObject(ch, gfx);
    }

    private static GameObject? Hit(IReadOnlyList<GameObject> objs, int gx, int gy)
    {
        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var o = objs[i];
            if (!o.Enabled) continue;
            var child = Hit(o.Children, gx, gy);
            if (child != null) return child;
            var r = o.Get<Rect>();
            if (r != null && r.Contains(gx, gy)) return o;
        }
        return null;
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
