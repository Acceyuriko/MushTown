using MushTown.Shared;

namespace MushTown.Client;

// The two sweeps every pooled node needs: the dead ones after the tree step,
// all of them when the map changes. Stateless on purpose: the pools live in
// the game, this only knows the three things to keep in step. The node leaves
// the tree either way: one left behind keeps ticking, and the next spawn adds
// it a second time.
public static class PoolSweep
{
    public static void CollectDead<T>(List<T> live, GameObject world, Stack<T> pool) where T : Component
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (!live[i].Owner.Dead) continue;
            world.RemoveChild(live[i].Owner);
            pool.Push(live[i]);
            live.RemoveAt(i);
        }
    }

    public static void DropAll<T>(List<T> live, GameObject world, Stack<T> pool) where T : Component
    {
        foreach (var n in live)
        {
            world.RemoveChild(n.Owner);
            pool.Push(n);
        }
        live.Clear();
    }
}
