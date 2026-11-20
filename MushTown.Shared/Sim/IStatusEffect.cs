namespace MushTown.Shared;

// An abnormal status on the player (stun, poison, and so on). Empty on purpose: no status exists yet and nothing puts one on the player.
// A status will implement this and, before it is put on, ask Fighter.TryBlockStatus.
public interface IStatusEffect
{
}
