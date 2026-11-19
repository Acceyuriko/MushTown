namespace MushTown.Shared;

// Where a Teleport (WZ 2001009) lands. Pure numbers in, a landing out: the client asks before it pays, and moves
// the feet only when this says yes. The distances are map pixels, the unit of the foothold and VR numbers: WZ gives
// no unit (the tooltip says "a distance of 190"); the wiki pages of other Teleport versions (Combat Teleport, Lethe's
// Teleport) list their distances in pixels (unverified for this version).
//
// Horizontal (the user's rule), in this order:
//   1. the farthest spot: x + dir * distance, cut to the map (VrLeft..VrRight);
//   2. the wall first: a wall (a vertical foothold whose span holds the feet height, SceneMath.StopBeforeWall) between
//      the feet and that spot cuts it back to 1 px before the wall. A wall that rises no higher than HorizontalReach above
//      the feet is a low cliff: the cast goes over it (the user's picture 1: a 60 px cliff up to a plateau);
//   3. then the platform: at that x, the grounds from HorizontalReach above the feet to HorizontalReach below them
//      (and inside VrTop..VrBottom); the one with the shortest vertical distance to the feet line wins, a tie goes to the
//      upper one (the user's choice after pictures 2 and 3);
//   4. no platform in that range: step one pixel back toward the feet and look again (2 does not need repeating: the
//      cut already stands before the wall).
// Vertical (Up or Down): the farthest ground at the feet's x between MinStep and the distance away in the pressed
// direction, inside VrTop..VrBottom.
// Nothing found means no landing (null): the cast is refused and costs nothing.
public static class TeleportRules
{
    public const int HorizontalReach = 75; // the user's off-hand value ("the usual search range of a projectile, say 75"): up and down from the feet line
    // MinStep is the developer's proposal, unverified.
    public const int MinStep = 10; // a vertical move shorter than this is the ground we already stand on

    public readonly record struct Landing(float X, int Y, FootholdItem Ground);

    // The direction keys held decide: Up, else Down, else Left or Right (dir -1 or +1). No direction key held: no landing.
    public static Landing? ForKeys(MapData map, float x, float y, bool up, bool down, int dir, int horizontal, int vertical) =>
        up ? Vertical(map, x, y, -1, vertical)
        : down ? Vertical(map, x, y, 1, vertical)
        : dir != 0 ? Horizontal(map, x, y, dir, horizontal)
        : null;

    // dir: -1 left, +1 right.
    public static Landing? Horizontal(MapData map, float x, float y, int dir, int distance)
    {
        float far = Math.Clamp(x + dir * distance, map.Info.VrLeft, map.Info.VrRight);
        far = SceneMath.StopBeforeWall(map.Footholds, x, far, y, HorizontalReach);
        for (float cx = far; (cx - x) * dir > 0; cx -= dir)
        {
            var ground = PlatformNear(map, (int)Math.Floor(cx), y);
            if (ground != null) return new Landing(cx, SceneMath.FootholdY(ground, (int)Math.Floor(cx)), ground);
        }
        return null;
    }

    // dir: -1 up, +1 down.
    public static Landing? Vertical(MapData map, float x, float y, int dir, int distance)
    {
        int ix = (int)Math.Floor(x);
        FootholdItem? best = null;
        int bestY = 0;
        foreach (var f in map.Footholds)
        {
            if (f.X1 == f.X2 || ix < Math.Min(f.X1, f.X2) || ix > Math.Max(f.X1, f.X2)) continue;
            int fy = SceneMath.FootholdY(f, ix);
            int moved = (fy - (int)y) * dir; // positive: toward the pressed direction
            if (moved < MinStep || moved > distance || fy < map.Info.VrTop || fy > map.Info.VrBottom) continue;
            if (best == null || moved > (bestY - (int)y) * dir) { best = f; bestY = fy; }
        }
        return best == null ? null : new Landing(x, bestY, best);
    }

    // The ground at x within HorizontalReach above or below the feet line (and inside the map) whose height is the
    // closest to the feet; at the same distance the upper one.
    private static FootholdItem? PlatformNear(MapData map, int x, float y)
    {
        FootholdItem? best = null;
        float bestGap = float.MaxValue;
        float bestY = 0;
        foreach (var f in map.Footholds)
        {
            if (f.X1 == f.X2 || x < Math.Min(f.X1, f.X2) || x > Math.Max(f.X1, f.X2)) continue;
            float fy = SceneMath.FootholdY(f, x);
            float gap = Math.Abs(fy - y);
            if (gap > HorizontalReach || fy < map.Info.VrTop || fy > map.Info.VrBottom) continue;
            if (gap > bestGap || (gap == bestGap && fy > bestY)) continue;
            bestGap = gap;
            bestY = fy;
            best = f;
        }
        return best;
    }
}
