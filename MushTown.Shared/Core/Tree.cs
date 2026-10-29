namespace MushTown.Shared;

// The headless object core (moved from the Client in S1): structure
// only, no drawing. The Client draws through IDrawable; the server
// never touches graphics at all.
public abstract class Component
{
    public GameObject Owner { get; internal set; } = null!;
    public virtual void Update(float dt) { } // sim tick (the mover, mobs, portals, damage numbers)
}

// The headless twin of the Client's TreeDraw: steps one subtree.
public static class TreeUpdate
{
    public static void UpdateObject(GameObject o, float dt)
    {
        if (!o.Enabled || o.Dead) return; // a dead node and its subtree are done: the owner collects it after the step
        foreach (var c in o.Components) c.Update(dt); // no AddChild/RemoveChild in here: this walk is a live foreach
        foreach (var ch in o.Children) UpdateObject(ch, dt);
    }
}

public sealed class GameObject
{
    public bool Enabled = true;
    public bool Dead; // the node finished its life (a death anim ended): step, draw and hit skip it
                      // A component that marks its owner dead still lets the rest of this node and its
                      // children run for the frame: the skip starts at the next step.
    public bool Modal; // swallows clicks below, even outside its rect (first user: delete modal, W2)
    public bool NoHit; // art-only: hit-testing skips it (first users: HUD art, U2)
    // Scene-root order: a bigger Z draws later and hits first. The scene
    // keeps its roots sorted; Scene.Move repoints one and re-sorts it.
    // Children keep the order they were added in: a Z on a child does nothing.
    public int Z;
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
    public bool RemoveChild(GameObject ch) // despawn: false when ch was never ours
    {
        if (!_children.Remove(ch)) return false;
        ch.Parent = null;
        return true;
    }

    // Drop every child (a window rebuilds its content, the host stays).
    public void Clear()
    {
        foreach (var ch in _children) ch.Parent = null;
        _children.Clear();
    }
}
