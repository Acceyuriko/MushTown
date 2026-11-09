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

    // The frame playing now: the delays add up and the idle starts over. The
    // game's clock is read here, so every stand portrait uses one time source.
    public int IndexAt() => Idle.At(Art.Delays, Game.TimeMs);
}
