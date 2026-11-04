using MushTown.Shared;

namespace MushTown.Client;

// The stand animation of one NPC or monster: the pictures, how long each stays
// on screen, and the origin each one is anchored by. The NPC that stands in a
// map and the world map tip that shows the same NPC both carry this component,
// so the two cannot pick different frames. A live monster's action (flinch,
// death) is MobArt's job; this is only the idle.
public sealed class LifeStand : Component
{
    public required MushTownGame Game { get; init; }
    public required int Kind { get; init; } // 0 = NPC, 1 = monster
    public required int Id { get; init; }

    internal IdleArt Art => Game.LifeIdle(Kind, Id);

    public string Name => Kind == 0 ? Game.NpcName(Id) : Game.MobName(Id);

    // The frame playing at this time: the delays add up and the idle starts
    // over. The clock is passed in (the game's ms, the HUD's frame time), so a
    // still shot shows the first frame like every other animation.
    public int IndexAt(float timeMs) => Idle.At(Art.Delays, timeMs);
}
