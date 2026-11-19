namespace MushTown.Shared;

// Where a Teleport (WZ 2001009) lands. Pure numbers in, a landing out: the client asks before it pays, and moves
// the feet only when this says yes. The distances are map pixels, the unit of the foothold and VR numbers: WZ gives
// no unit (the tooltip says "a distance of 190"); the wiki pages of other Teleport versions (Combat Teleport, Lethe's
// Teleport) list their distances in pixels (unverified for this version).
// The rule is "the farthest legal spot within the distance". A spot is legal when:
//   - it is inside the map (VrLeft..VrRight for x, and the ground it lands on between VrTop..VrBottom, both ways);
//   - no wall stands between the feet and the spot (horizontal only). A wall is a vertical foothold; it blocks when
//     it crosses the body [y - BodyH, y) and is not the one behind the feet;
//   - there is a ground to stand on: horizontally the ground at that x whose height is nearest to the feet (at most
//     ReachUp above them, any depth below); vertically a ground at the feet's x between MinStep and the distance
//     away in the pressed direction.
// Nothing legal means no landing (null): the cast is refused and costs nothing.
public static class TeleportRules
{
    // The three numbers below are the developer's proposal, unverified (the leader accepted the rule, not the numbers).
    public const int BodyH = 68; // the stand frame height of the hero (from the stand frame of the hero art)
    public const int ReachUp = 100; // a ground this much above the feet still counts (a slope rises under a long jump)
    public const int MinStep = 10; // a vertical move shorter than this is the ground we already stand on

    public readonly record struct Landing(float X, int Y, FootholdItem Ground);

    // dir: -1 left, +1 right.
    public static Landing? Horizontal(MapData map, float x, float y, int dir, int distance)
    {
        for (int step = distance; step > 0; step--)
        {
            float nx = x + dir * step;
            if (nx < map.Info.VrLeft || nx > map.Info.VrRight || WallBetween(map, x, nx, y, dir)) continue;
            var ground = NearestGround(map, (int)Math.Floor(nx), y);
            if (ground == null) continue;
            int gy = SceneMath.FootholdY(ground, (int)Math.Floor(nx));
            if (gy >= map.Info.VrTop && gy <= map.Info.VrBottom) return new Landing(nx, gy, ground);
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

    private static bool WallBetween(MapData map, float x0, float x1, float y, int dir)
    {
        foreach (var w in map.Footholds)
        {
            if (w.X1 != w.X2) continue;
            if ((w.X1 - x0) * dir <= 0 || (w.X1 - x1) * dir > 0) continue; // not in (x0, x1]
            if (Math.Min(w.Y1, w.Y2) < y && Math.Max(w.Y1, w.Y2) > y - BodyH) return true;
        }
        return false;
    }

    // The ground at x nearest in height to the feet, not more than ReachUp above them.
    private static FootholdItem? NearestGround(MapData map, int x, float y)
    {
        FootholdItem? best = null;
        float bestGap = float.MaxValue;
        foreach (var f in map.Footholds)
        {
            if (f.X1 == f.X2 || x < Math.Min(f.X1, f.X2) || x > Math.Max(f.X1, f.X2)) continue;
            float fy = SceneMath.FootholdY(f, x);
            if (fy < y - ReachUp || Math.Abs(fy - y) >= bestGap) continue;
            bestGap = Math.Abs(fy - y);
            best = f;
        }
        return best;
    }
}
