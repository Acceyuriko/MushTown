using MushTown.Shared;

namespace MushTown.Client;

// Live map furniture, rebuilt per map load under the map object (S8).
// Portal comps hold their anim state (killing the parallel lists);
// NPC comps hold their spawn (a reference, not a copy: Quest.cs:80).
// No Update overrides: the portal tick stays at its exact Update spot,
// NPCs never tick, and both still draw through the y-sorted list.
// Static backs/tiles/objs stay MapData-direct (same rule: zero behavior
// to house, so shells would be over-design).
public sealed class PortalComp : Component
{
    public PortalItem Portal = null!;
    public List<int> Delays = new();
    public int Frame;
    public float Timer;
}

public sealed class NpcArt : Component
{
    public NpcSpawn Spawn = null!;
}
