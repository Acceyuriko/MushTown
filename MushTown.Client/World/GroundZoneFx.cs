using MushTown.Shared;

namespace MushTown.Client;

// The look of one ground zone (Poison Mist): the looping WZ tile art set in a ring around the caster's feet at the cast (the reference picture shows the mist
// all around the hero), for the time the server gave it. The ring is an ellipse of RingRx and RingRy whose middle is RingUp over the feet; WZ has no tile
// layout, so these three numbers and the order of the tiles round the ring are ours (未核实).
// It waits WaitMs first (the body action's attack frame), shows for LifeMs and then ends its own life. The server
// owns the box and the damage; this only draws. Every client on the map builds one from the same ZoneEvent.
public sealed class GroundZoneFx : Component
{
    private const float RingRx = 110;
    private const float RingRy = 50;
    private const float RingUp = 60;

    public int Uid;
    public int Skill;
    public float WaitMs;
    public float LifeMs;
    public bool Started => WaitMs <= 0;
    public readonly WzArt?[] Tiles = new WzArt?[WzSkillData.ZoneTiles];
    public readonly float[] TileX = new float[WzSkillData.ZoneTiles];
    public readonly float[] TileY = new float[WzSkillData.ZoneTiles];
    public readonly int[] Frame = new int[WzSkillData.ZoneTiles];
    public readonly float[] Timer = new float[WzSkillData.ZoneTiles];

    // A pooled node comes back clean. The tiles stand evenly round the ring, each on its own frame, so the puffs do not move together.
    public void Start(ZoneEvent e, WzSkillData wz)
    {
        Uid = e.Uid;
        Skill = e.Skill;
        float centerX = (e.Left + e.Right) / 2;
        WaitMs = e.DelayMs;
        LifeMs = e.LifeMs;
        for (int i = 0; i < Tiles.Length; i++)
        {
            Tiles[i] = wz.Art[$"tile{i}"];
            float angle = i * MathF.Tau / Tiles.Length;
            TileX[i] = centerX + RingRx * MathF.Cos(angle);
            TileY[i] = e.Y - RingUp + RingRy * MathF.Sin(angle);
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
