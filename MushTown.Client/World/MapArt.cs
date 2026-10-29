using MushTown.Shared;

namespace MushTown.Client;

// Live map furniture, rebuilt per map load under the map object (S8).
// Portal comps hold their anim state (killing the parallel lists) and
// tick it themselves; NPC comps hold their spawn (a reference, not a
// copy: Quest.cs:80) and never tick. Both still draw through the
// y-sorted list. Static backs/tiles/objs stay MapData-direct (same rule:
// zero behavior to house, so shells would be over-design).
public sealed class PortalComp : Component
{
    public PortalItem Portal = null!;
    public List<int> Delays = new();
    public int Frame;
    public float Timer;

    // The gate shimmers on its own loop, the same one the map art names.
    public override void Update(float dt)
    {
        if (Portal.Frames.Count > 0)
            (Frame, Timer) = SceneMath.AdvanceFrame(Frame, Timer, dt * 1000f, Delays);
    }
}

public sealed class NpcArt : Component
{
    public NpcSpawn Spawn = null!;
}
