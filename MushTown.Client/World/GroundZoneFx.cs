using MushTown.Shared;

namespace MushTown.Client;

// The look of one ground zone (Poison Mist): the zone box (WZ lt/rb round the caster's feet at the cast) cut into 3x3 cells, and in each cell one looping WZ tile
// (tile0..tile8, row by row, tile0 at the top left), for the time the server gave it. Each tile is stretched to the size of its cell (the box width and height over 3,
// 167 by 133 for lt/rb 500 by 400), so the nine tiles cover the whole box. WZ has no layout of its own for the tiles: the row-by-row order and the stretch to the cell
// are our reading (未核实).
// It waits WaitMs first (the body action's attack frame), shows for LifeMs and then ends its own life. The server
// owns the box and the poison; this only draws. Every client on the map builds one from the same ZoneEvent.
public sealed class GroundZoneFx : Component
{
    private const int Grid = 3; // the box is Grid x Grid cells (the nine tiles)

    public int Uid;
    public int Skill;
    public float WaitMs;
    public float LifeMs;
    public bool Started => WaitMs <= 0;
    public readonly WzArt?[] Tiles = new WzArt?[WzSkillData.ZoneTiles];
    public float CellW; // the size of one cell: every tile is drawn stretched to it
    public float CellH;
    public readonly float[] TileX = new float[WzSkillData.ZoneTiles];
    public readonly float[] TileY = new float[WzSkillData.ZoneTiles];
    public readonly int[] Frame = new int[WzSkillData.ZoneTiles];
    public readonly float[] Timer = new float[WzSkillData.ZoneTiles];

    // A pooled node comes back clean. Each tile starts on its own frame, so the puffs do not move together.
    public void Start(ZoneEvent e, WzSkillData wz)
    {
        Uid = e.Uid;
        Skill = e.Skill;
        float top = e.Y + wz.Vectors["lt"][1];
        CellW = (e.Right - e.Left) / Grid;
        CellH = (wz.Vectors["rb"][1] - wz.Vectors["lt"][1]) / (float)Grid;
        WaitMs = e.DelayMs;
        LifeMs = e.LifeMs;
        for (int i = 0; i < Tiles.Length; i++)
        {
            Tiles[i] = wz.Art[$"tile{i}"];
            TileX[i] = e.Left + i % Grid * CellW; // the top left of its cell
            TileY[i] = top + i / Grid * CellH;
            Frame[i] = i % Tiles[i]!.Frames.Count;
            Timer[i] = 0;
        }
        Owner.Dead = false;
    }

    public override void Update(float dt)
    {
        float ms = dt * 1000f;
        if (WaitMs > 0)
        {
            WaitMs -= ms;
            if (WaitMs > 0) return;
            ms = -WaitMs; // the part of this step after the start
            WaitMs = 0;
        }
        LifeMs -= ms;
        if (LifeMs <= 0)
        {
            Owner.Dead = true;
            return;
        }
        for (int i = 0; i < Tiles.Length; i++)
        {
            Timer[i] += ms;
            int delay = Tiles[i]!.Delays[Frame[i]];
            if (Timer[i] < delay) continue;
            Timer[i] -= delay;
            Frame[i] = (Frame[i] + 1) % Tiles[i]!.Frames.Count;
        }
    }
}
