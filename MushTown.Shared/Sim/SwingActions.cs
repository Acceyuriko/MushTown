namespace MushTown.Shared;

// The swing action of a basic attack: a pool of action names and the rule that
// picks one (uniform random). A weapon whose attack has several swings fills
// the pool (Progression.SwingPools) and rolls once per swing; Current is the
// swing in progress. Held directly like Fighter (never added to an object).
public sealed class SwingActions : Component
{
    private readonly Random _rng;

    public IReadOnlyList<string> Pool { get; private set; } = Array.Empty<string>();
    public string Current { get; private set; } = "swingO1"; // the action of the last roll

    // A fixed seed makes the rolls repeat (tests, screenshots).
    public SwingActions(Random rng) => _rng = rng;

    public string Roll(IReadOnlyList<string> pool)
    {
        if (pool.Count == 0) throw new InvalidOperationException("swing pool is empty");
        Pool = pool;
        return Current = pool[_rng.Next(pool.Count)];
    }
}
