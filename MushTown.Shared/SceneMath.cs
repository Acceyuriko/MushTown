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
    // Mirrors FrmMapRender2.SceneRendering.cs:807-814,845-903.
    public static (int X, int Y, int Cx, int Cy, int Left, int Right, int Top, int Bottom) BackLayout(
        BackItem b, int imgW, int imgH, int cameraX, int cameraY, int viewW, int viewH)
    {
        var (tileH, tileV, scrollH, scrollV) = BackTileMode(b.Type);

        int cx = b.Cx, cy = b.Cy;
        if ((tileH || tileV) && (cx == 0 || cy == 0))
        {
            if (cx == 0) cx = imgW;
            if (cy == 0) cy = imgH;
        }

        // a distant background sits near the camera center and moves slower than the
        // map; a scrolling axis drifts with time instead, and W2 exports one still
        // frame, so it stays at offset 0
        float centerX = cameraX + viewW / 2f;
        float centerY = cameraY + viewH / 2f;
        int x = (int)Math.Floor(b.X + (scrollH ? 0f : centerX * (100 + b.Rx) / 100f));
        int y = (int)Math.Floor(b.Y + (scrollV ? 0f : centerY * (100 + b.Ry) / 100f));

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

    // Ground under spawn: foothold covering spawnX, below the portal, closest to its y.
    // Null when no foothold is under the spawn point.
    public static int? SpawnGroundY(List<FootholdItem> footholds, int spawnX, int portalY)
    {
        int groundY = int.MaxValue;
        int bestDist = int.MaxValue;
        foreach (var f in footholds)
        {
            int minX = Math.Min(f.X1, f.X2), maxX = Math.Max(f.X1, f.X2);
            if (spawnX < minX || spawnX > maxX || f.X1 == f.X2) continue;
            int y = f.Y1 == f.Y2 ? f.Y1
                : f.Y1 + (int)Math.Round((f.Y2 - f.Y1) * (double)(spawnX - f.X1) / (f.X2 - f.X1));
            if (y < portalY) continue; // a foothold above the portal is not the ground
            int dist = y - portalY;
            if (dist < bestDist)
            {
                bestDist = dist;
                groundY = y;
            }
        }
        return groundY == int.MaxValue ? null : groundY;
    }
}
