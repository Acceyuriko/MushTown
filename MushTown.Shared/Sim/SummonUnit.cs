namespace MushTown.Shared;

// A summoned object (Creeping Toxin's totem; Ifrit will be the next): it stands on the ground where it was cast for a while and belongs to the player who cast it.
// It knows no skill and no damage, and does nothing to mobs: what a summon does (a pool, an attack) belongs to the skill that made it, and its look to the client
// (SummonFx plays the WZ summoned, stand and die art by its time). WaitMs is the time before it comes up. Held directly by MobWorld, never added to an object (like GroundZone).
public sealed class SummonUnit : Component
{
    public int Uid;
    public int Map;
    public int Attacker; // the player whose cast it is
    public int Skill;
    public float X; // where it stands: the feet of the totem
    public float Y;
    public int LifeMs; // the whole time it stays, once up
    public int WaitMs; // time left before it comes up
    public int AgeMs; // time it has been up

    public bool Over => WaitMs <= 0 && AgeMs >= LifeMs;
    public int MsLeft => Math.Max(0, LifeMs - AgeMs);

    // Runs ms: the wait first, then the life.
    public void Step(int ms)
    {
        if (Over) return;
        if (WaitMs > 0)
        {
            int wait = Math.Min(WaitMs, ms);
            WaitMs -= wait;
            ms -= wait;
        }
        AgeMs = Math.Min(LifeMs, AgeMs + ms);
    }
}
