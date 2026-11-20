using MushTown.Shared;

namespace MushTown.Client;

// The look of one ground zone (Poison Mist). WZ gives the nine tiles (tile0..tile8: 8 frames each, an origin and a delay per frame) and the box (lt/rb round the caster's
// feet at the cast), and nothing about how the tiles are laid out. So the layout is our fit to the original game's video (bilibili BV1qv4y1v7tK, from 4:33); every number
// below is a guess (未核实). The user chose it on 2026-11-20 (T-20261120183316; the research is in .dsh/research/poison-mist-tiles.md):
// a random tile type at every place of a jittered grid over an ellipse round the caster, each tile on its WZ origin at its native size, the whole mist at alpha 0.9.
// It waits WaitMs first (the body action's attack frame), shows for LifeMs and then ends its own life. The server
// owns the box and the poison; this only draws. Every client on the map builds one from the same ZoneEvent.
public sealed class GroundZoneFx : Component
{
    public const float Alpha = 0.9f; // the whole mist is drawn at this alpha
    private const int Seed = 2111003; // the skill id: the mist looks the same every run
    private const float EllipseOverBox = 1.2f; // the ellipse's half axes over half the box (1 = inside the box, sqrt 2 = through its corners)
    private const float Step = 0.5f; // the distance between two places, over the width of the first tile type picked
    private const float Jitter = 0.25f; // a place moves by up to this much of the step
    private const int MaxElems = 64; // the elements this fills for the box of Poison Mist (53)

    public int Uid;
    public int Skill;
    public float WaitMs;
    public float LifeMs;
    public bool Started => WaitMs <= 0;
    public int Count;
    public readonly WzArt?[] Arts = new WzArt?[MaxElems];
    public readonly float[] X = new float[MaxElems]; // world: the point the WZ origin goes on
    public readonly float[] Y = new float[MaxElems];
    public readonly int[] Frame = new int[MaxElems];
    public readonly float[] Timer = new float[MaxElems];

    // A pooled node comes back clean. Each element starts on its own frame, so the puffs do not move together.
    public void Start(ZoneEvent e, WzSkillData wz)
    {
        Uid = e.Uid;
        Skill = e.Skill;
        WaitMs = e.DelayMs;
        LifeMs = e.LifeMs;
        float boxW = e.Right - e.Left;
        float boxH = wz.Vectors["rb"][1] - wz.Vectors["lt"][1];
        float cx = e.Left + boxW / 2;
        float cy = e.Y + wz.Vectors["lt"][1] + boxH / 2;
        float rx = EllipseOverBox * boxW / 2;
        float ry = EllipseOverBox * boxH / 2;
        var rng = new Random(Seed);
        float step = Step * 2 * wz.Art[$"tile{rng.Next(WzSkillData.ZoneTiles)}"].Origins[0][0];
        Count = 0;
        for (float y = -ry; y <= ry; y += step)
            for (float x = -rx; x <= rx; x += step)
            {
                float px = x + (rng.NextSingle() * 2 - 1) * Jitter * step;
                float py = y + (rng.NextSingle() * 2 - 1) * Jitter * step;
                int tile = rng.Next(WzSkillData.ZoneTiles);
                if (px * px / (rx * rx) + py * py / (ry * ry) > 1) continue;
                int i = Count++;
                Arts[i] = wz.Art[$"tile{tile}"];
                X[i] = cx + px;
                Y[i] = cy + py;
                Frame[i] = i % Arts[i]!.Frames.Count;
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
        for (int i = 0; i < Count; i++)
        {
            Timer[i] += ms;
            int delay = Arts[i]!.Delays[Frame[i]];
            if (Timer[i] < delay) continue;
            Timer[i] -= delay;
            Frame[i] = (Frame[i] + 1) % Arts[i]!.Frames.Count;
        }
    }
}
