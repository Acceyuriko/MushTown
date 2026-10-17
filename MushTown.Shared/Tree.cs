namespace MushTown.Shared;

// The headless object core (moved from the Client in S1): structure
// only, no drawing. The Client draws through IDrawable; the server
// never touches graphics at all.
public abstract class Component
{
    public GameObject Owner { get; internal set; } = null!;
}

public sealed class GameObject
{
    public bool Enabled = true;
    public bool Modal; // swallows clicks below, even outside its rect (first user: delete modal, W2)
    public bool NoHit; // art-only: hit-testing skips it (first users: HUD art, U2)
    public GameObject? Parent { get; private set; }
    private readonly List<Component> _components = new();
    private readonly List<GameObject> _children = new();
    public IReadOnlyList<Component> Components => _components;
    public IReadOnlyList<GameObject> Children => _children;
    public T Add<T>(T c) where T : Component { c.Owner = this; _components.Add(c); return c; }
    public T? Get<T>() where T : Component
    {
        foreach (var c in _components) if (c is T t) return t;
        return null;
    }
    public void AddChild(GameObject ch) { ch.Parent = this; _children.Add(ch); }
}
