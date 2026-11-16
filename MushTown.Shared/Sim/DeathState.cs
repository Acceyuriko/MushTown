namespace MushTown.Shared;

// One character's death: from HP 0 until the revive click. The tombstone
// drops from above the screen to the ground under the death spot and stays;
// the clocks tell the Client which frame of the tombstone art to draw.
// Revive sends the character to the town the map belongs to (WZ
// info/returnMap, exported as MapInfo.ReturnMap).
public sealed class DeathState : Component
{
    public const float FallFrom = 500f; // the tombstone starts this far above the ground, px
    public const float FallSpeed = 900f; // px per second

    public bool Dead { get; private set; }
    public float X { get; private set; }
    public float GroundY { get; private set; }
    public float TombY { get; private set; }
    public float FallMs { get; private set; } // time spent falling
    public float LandedMs { get; private set; } // time since the landing
    public bool Landed => Dead && TombY >= GroundY;

    public void Die(float x, float groundY)
    {
        Dead = true;
        X = x;
        GroundY = groundY;
        TombY = groundY - FallFrom;
        FallMs = 0;
        LandedMs = 0;
    }

    public void Tick(float dtMs)
    {
        if (!Dead) return;
        if (Landed)
        {
            LandedMs += dtMs;
            return;
        }
        TombY = Math.Min(GroundY, TombY + FallSpeed * dtMs / 1000f);
        FallMs += dtMs;
    }

    public void Revive() => Dead = false;

    // The town to revive in. A map without the field is a data error (an
    // old export): fail loud instead of reviving in place.
    public static int ReviveMap(MapInfo info) =>
        info.ReturnMap != 0 ? info.ReturnMap : throw new InvalidDataException("map has no ReturnMap: re-run the export");
}
