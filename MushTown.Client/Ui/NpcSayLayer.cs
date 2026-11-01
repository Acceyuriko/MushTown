using MushTown.Shared;

namespace MushTown.Client;

// The NPC objects of the current map, one per NPC, each with its quest mark and
// (when the NPC has lines) its bubble. The layer owns the tree and the lookup
// together, so they cannot drift apart.
public sealed class NpcSayLayer : Component
{
    private readonly Dictionary<int, NpcBalloon> _byNpc = new();

    public void Build(IReadOnlyList<(int npcId, NpcQuestMark mark, NpcBalloon? balloon)> npcs)
    {
        Clear();
        foreach (var (npcId, mark, balloon) in npcs)
        {
            var o = new GameObject { NoHit = true };
            o.Add(mark);
            if (balloon != null) { o.Add(balloon); _byNpc[npcId] = balloon; }
            Owner.AddChild(o);
        }
    }

    public void Clear()
    {
        _byNpc.Clear();
        Owner.Clear();
    }

    public bool Has(int npcId) => _byNpc.ContainsKey(npcId);

    public void Say(int npcId, int index = -1)
    {
        if (_byNpc.TryGetValue(npcId, out var balloon)) balloon.Say(index);
    }
}