namespace MushTown.Shared;

// Pure scene math shared by the exporter, the client, and the tests.
// No files, images, or game-library types here: numbers in, numbers out.
public static class SceneMath
{
    // WZ back "type" (Patches2/BackItem.cs:77-91) as four flags.
    public static (bool TileH, bool TileV, bool ScrollH, bool ScrollV) BackTileMode(int type) => type switch
    {
        1 => (true, false, false, false),
        2 => (false, true, false, false),
        3 => (true, true, false, false),
        4 => (true, false, true, false),
        5 => (false, true, false, true),
        6 => (true, true, true, false),
        7 => (true, true, false, true),
        _ => (false, false, false, false)
    };

    // Where one background lands: parallax anchor (X, Y), tile step (Cx, Cy),
    // and the tile index range that covers the camera rect.
    // Mirrors FrmMapRender2.SceneRendering.cs:807-814,845-903. A scrolling axis
    // drifts with time (rate * 100 * ms / 20000, wrapped by tile step) instead
    // of following the camera (SceneRendering.cs:816-824).
    public static (int X, int Y, int Cx, int Cy, int Left, int Right, int Top, int Bottom) BackLayout(
        BackItem b, int imgW, int imgH, int cameraX, int cameraY, int viewW, int viewH, double timeMs)
    {
        var (tileH, tileV, scrollH, scrollV) = BackTileMode(b.Type);

        int cx = b.Cx, cy = b.Cy;
        if ((tileH || tileV) && (cx == 0 || cy == 0))
        {
            if (cx == 0) cx = imgW;
            if (cy == 0) cy = imgH;
        }

        float centerX = cameraX + viewW / 2f;
        float centerY = cameraY + viewH / 2f;
        int x = (int)Math.Floor(b.X + (scrollH ? ScrollOffset(b.Rx, cx, timeMs) : centerX * (100 + b.Rx) / 100f));
        int y = (int)Math.Floor(b.Y + (scrollV ? ScrollOffset(b.Ry, cy, timeMs) : centerY * (100 + b.Ry) / 100f));

        int left = 0, right = 1, top = 0, bottom = 1;
        if (tileH && cx > 0)
        {
            left = (int)Math.Floor((cameraX - x - imgW) / (float)cx) - 1;
            right = (int)Math.Ceiling((cameraX + viewW - x) / (float)cx) + 2;
        }
        if (tileV && cy > 0)
        {
            top = (int)Math.Floor((cameraY - y - imgH) / (float)cy) - 1;
            bottom = (int)Math.Ceiling((cameraY + viewH - y) / (float)cy) + 2;
        }

        return (x, y, cx, cy, left, right, top, bottom);
    }

    // Time drift of one scrolling back axis, wrapped by tile step.
    public static float ScrollOffset(int rate, int tile, double timeMs) =>
        tile <= 0 ? 0f : (float)(rate * 100.0 * timeMs / 20000.0 % tile);

    // Arrival ground shared by exporter proof and client landing: the ground
    // at/below the portal, searched from above the portal point.
    public static FootholdItem? ArrivalGround(List<FootholdItem> footholds, int x, int y) =>
        GroundBelow(footholds, x, y - PortalRules.ArrivalLookUp);

    // Portal catch for a fresh Up press: skips NoTarget tms (boat scripts and
    // other non-walkable portals), nearest in x wins. Ranges cover the exported
    // geometry (portals sit 0-4px above their ground).
    public static PortalItem? FindPortal(List<PortalItem> portals, float px, float py)
    {
        PortalItem? best = null;
        foreach (var p in portals)
        {
            if (p.Tm == PortalRules.NoTarget) continue;
            if (Math.Abs(px - p.X) > 25 || Math.Abs(py - p.Y) > 30) continue;
            if (best != null && Math.Abs(px - p.X) >= Math.Abs(px - best.X)) continue;
            best = p;
        }
        return best;
    }

    // Ladder catch for a held Up/Down key, nearest in x wins. The y range
    // covers standing at the foot/top (grounds sit 2px off each end) and
    // grabbing mid-air after a jump.
    public static LadderItem? FindLadder(List<LadderItem> ladders, float px, float py)
    {
        LadderItem? best = null;
        foreach (var l in ladders)
        {
            if (Math.Abs(px - l.X) > 10) continue;
            if (py < l.Y1 - 10 || py > l.Y2 + 10) continue;
            if (best != null && Math.Abs(px - l.X) >= Math.Abs(px - best.X)) continue;
            best = l;
        }
        return best;
    }

    // Ladder-top mount: the platform at the top sits ~2px above Y1. Mounts
    // only when the nearest ground at/below Y1-10 lands within [Y1-10, Y1+2];
    // ground farther below (or none) means no mount.
    public static FootholdItem? MountTop(List<FootholdItem> footholds, int x, int y1)
    {
        var g = GroundBelow(footholds, x, y1 - 10);
        return g != null && FootholdY(g, x) <= y1 + 2 ? g : null;
    }

    // Camera clamp that also works when the map is smaller than the view.
    public static int ClampCamera(int target, int min, int max, int viewSize) =>
        Math.Clamp(target, min, Math.Max(min, max - viewSize));

    // Scales the game image up to fill the screen, keeping the aspect ratio:
    // the rect (X, Y, W, H) of the image inside the screen.
    public static (int X, int Y, int W, int H) BlitRect(int screenW, int screenH, int viewW, int viewH)
    {
        float scale = Math.Min((float)screenW / viewW, (float)screenH / viewH);
        int w = (int)(viewW * scale);
        int h = (int)(viewH * scale);
        return ((screenW - w) / 2, (screenH - h) / 2, w, h);
    }

    // Which part names survive, per AvatarCanvas.cs:718-745. A beginner calls it
    // with face from the body frame, earType 0, no hair cover, no shade, weapon 0.
    public static bool PartVisible(string name, bool face, int earType, bool hairCover,
        bool showHairShade, int weaponIndex, bool multiWeapon) => name switch
        {
            "face" => face,
            "ear" => earType == 1,
            "lefEar" => earType == 2,
            "highlefEar" => earType == 3,
            "hairOverHead" or "backHairOverCape" or "backHair" => !hairCover,
            "hair" or "backHairBelowCap" => hairCover,
            // showHairShade=true also needs the hairShade/child-0 lookup (AvatarCanvas.cs:707-714)
            "hairShade" => showHairShade,
            _ when multiWeapon && name.StartsWith("weapon") =>
                name == "weapon" + (weaponIndex == 0 ? "" : weaponIndex.ToString()),
            _ => true
        };

    // Y of a foothold line at x. The foothold must be non-vertical and cover x.
    public static int FootholdY(FootholdItem f, int x) => f.Y1 == f.Y2 ? f.Y1
        : f.Y1 + (int)Math.Round((f.Y2 - f.Y1) * (double)(x - f.X1) / (f.X2 - f.X1));

    // Nearest foothold at or below y covering x. Null when none is at or below.
    public static FootholdItem? GroundBelow(List<FootholdItem> footholds, int x, int y)
    {
        FootholdItem? best = null;
        int bestY = int.MaxValue;
        foreach (var f in footholds)
        {
            int minX = Math.Min(f.X1, f.X2), maxX = Math.Max(f.X1, f.X2);
            if (x < minX || x > maxX || f.X1 == f.X2) continue;
            int fy = FootholdY(f, x);
            if (fy < y || fy >= bestY) continue;
            bestY = fy;
            best = f;
        }
        return best;
    }

    // Ground under spawn: foothold covering spawnX, at or below the portal, closest to its y.
    // Null when no foothold is at or under the spawn point.
    public static int? SpawnGroundY(List<FootholdItem> footholds, int spawnX, int portalY)
    {
        var g = GroundBelow(footholds, spawnX, portalY);
        return g == null ? null : FootholdY(g, spawnX);
    }

    // Nearest foothold covering x within 1px of y. Prefers same level, then a
    // 1px rise, then a 1px drop. Anything further counts as walked off the edge.
    // Null when the player truly walked off the edge.
    public static FootholdItem? StepTo(List<FootholdItem> footholds, int x, int y)
    {
        FootholdItem? best = null;
        int bestRank = 3;
        foreach (var f in footholds)
        {
            int minX = Math.Min(f.X1, f.X2), maxX = Math.Max(f.X1, f.X2);
            if (x < minX || x > maxX || f.X1 == f.X2) continue;
            int fy = FootholdY(f, x);
            int rank = fy == y ? 0 : fy == y - 1 ? 1 : fy == y + 1 ? 2 : -1;
            if (rank < 0 || rank >= bestRank) continue;
            bestRank = rank;
            best = f;
        }
        return best;
    }

    // Frame timer: add dt, walk past spent delays, loop. Delays are checked > 0
    // at load (the exporter fails, the client throws), so this loop always ends.
    public static (int frame, float timer) AdvanceFrame(int frame, float timer, float dtMs, List<int> delays)
    {
        timer += dtMs;
        while (timer >= delays[frame])
        {
            timer -= delays[frame];
            frame = (frame + 1) % delays.Count;
        }
        return (frame, timer);
    }

    // Left edge of a part mirrored around feetX (DrawTexture flips inside the rect).
    public static int FlipDrawX(int feetX, int partX, int texWidth) => feetX - partX - texWidth;

    // Camera viscosity: critically damped spring (Damp = 2*sqrt(Stiff)), so the
    // camera lags about 70 px while walking and settles without overshoot.
    // Hand-tuned feel.
    private const float CamStiff = 16f;
    private const float CamDamp = 8f;
    public static (float x, float v) SpringStep(float x, float v, float target, float dt)
    {
        v += ((target - x) * CamStiff - v * CamDamp) * dt;
        x += v * dt;
        return (x, v);
    }
}
