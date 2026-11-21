using MushTown.Shared;

namespace MushTown.Client;

// The look of one ground zone (Poison Mist's cloud, Ignite's fire wall). Both are built from the same ZoneEvent on every client; the server owns the box and the DoT, this only draws.
//
// Poison Mist (StartMist): WZ gives the nine tiles (tile0..tile8: 8 frames each, an origin and a delay per frame) and the box (lt/rb round the caster's
// feet at the cast), and nothing about how the tiles are laid out. So the layout is our fit to the original game's video (bilibili BV1qv4y1v7tK, from 4:33); every number
// below is a guess (未核实). The user chose it on 2026-11-20 (T-20261120183316; the research is in .dsh/research/poison-mist-tiles.md):
// a random tile type at every place of a jittered grid over an ellipse round the caster, each tile on its WZ origin at its native size, the whole mist at alpha 0.9.
// It waits WaitMs first (the body action's attack frame), shows for LifeMs and then ends its own life.
//
// Ignite's wall (StartWall): the three flame loops of WZ stacked on the middle of its box (see WallSideDx), at full alpha. WZ gives no layout, so it is our fit (未核实).
//
// Creeping Toxin's pool (StartPool): WZ gives four tile kinds (tile0..tile3), each with the phases start, loop, end and end2 (end2: the pool blew up), each phase with two
// lists that play together (layer 0: the flat puddle, layer 1: the bubbles and mist over it), and no layout. The pool is cut into pools by ToxinPoolLayout (our fit, 未核实: the same
// code the server uses, so the index of a pool means the same on both sides); the pool widens from the totem in steps (the same widening as the server's box, GroundZone.Spread) and
// a pool comes up (its start phase, then its loop) at its Born (ms of the zone's age: the first pool ToxinFirstPoolMs after the totem, the others one step each, the server's own numbers
// from ToxinPoolLayout). A pool that blows up (Explode, on the server's PoolBlast event) plays end2 once and is gone, then comes up again at a new Born (the wait of WZ y and the steps from
// the totem, the same time as the server's). In the last ms of the zone every pool plays its end phase, so the pool is gone with the zone. The box is already cut at the platform edge by
// the server (a pool never goes over it). Known limit (已知限制): a client that enters later gets the zone with its age only, so it draws every pool that is due by then, also one that
// blew up a moment ago and is growing back (a display difference of a few seconds; the server's pools are right).
public sealed class GroundZoneFx : Component
{
    public const float Alpha = 0.9f; // the whole mist is drawn at this alpha (the wall is drawn at full alpha)
    private const int Seed = 2111003; // the skill id: the mist looks the same every run
    private const float EllipseOverBox = 1.2f; // the ellipse's half axes over half the box (1 = inside the box, sqrt 2 = through its corners)
    private const float Step = 0.5f; // the distance between two places, over the width of the first tile type picked
    private const float Jitter = 0.25f; // a place moves by up to this much of the step
    private const int WallSideDx = 22; // the wall: the middle flame's step to the left, the small one's to the right
    private const int MaxElems = 64; // the elements this fills for the box of Poison Mist (53)
    private const int PhaseStart = 0;
    private const int PhaseLoop = 1;
    private const int PhaseEnd = 2;
    private const int PhaseBlast = 3; // the end2 art of a tile: the pool blew up

    public int Uid;
    public int Skill;
    public float Opacity = Alpha;
    public float WaitMs;
    public float LifeMs;
    public bool Started => WaitMs <= 0;
    public int Count;
    public readonly WzArt?[] Arts = new WzArt?[MaxElems];
    public readonly float[] X = new float[MaxElems]; // world: the point the WZ origin goes on
    public readonly float[] Y = new float[MaxElems];
    public readonly int[] Frame = new int[MaxElems];
    public readonly float[] Timer = new float[MaxElems];
    // The pool only (see StartPool): the other zones show every element from the start.
    public readonly bool[] Shown = new bool[MaxElems];
    public readonly int[] Piece = new int[MaxElems]; // the index of the pool (ToxinPoolLayout.Pieces) the element belongs to
    public readonly int[] Kind = new int[MaxElems];
    public readonly int[] Layer = new int[MaxElems];
    public readonly int[] Phase = new int[MaxElems];
    public readonly float[] Born = new float[MaxElems]; // the zone age (ms) the piece comes up at (infinity: never again)
    private bool _pool;
    private WzSkillData _wz = null!;
    private float _originX;
    private int _stepMs;
    private float _ageMs;

    // A pooled node comes back clean. Each element starts on its own frame, so the puffs do not move together.
    public void Start(ZoneEvent e, WzSkillData wz)
    {
        Uid = e.Uid;
        Skill = e.Skill;
        WaitMs = e.DelayMs;
        LifeMs = e.LifeMs;
        Opacity = Alpha;
        _pool = wz.SkillId == WzSkillData.CreepingToxin;
        if (_pool) StartPool(e, wz);
        else if (wz.SkillId == WzSkillData.Ignite) StartWall(e, wz);
        else StartMist(e, wz);
        if (!_pool) Array.Fill(Shown, true);
        Owner.Dead = false;
    }

    private void StartPool(ZoneEvent e, WzSkillData wz)
    {
        _wz = wz;
        _originX = e.OriginX;
        _stepMs = e.StepMs;
        _ageMs = e.AgeMs;
        Opacity = 1;
        Count = 0;
        var pieces = ToxinPoolLayout.Pieces(e.OriginX, e.Left, e.Right); // the same pools the server blows up, by index
        for (int layer = 0; layer < WzSkillData.ToxinLayers; layer++) // the puddles first, the mist over them
            for (int p = 0; p < pieces.Count; p++)
            {
                int i = Count++;
                Piece[i] = p;
                Kind[i] = pieces[p].Kind;
                Layer[i] = layer;
                Phase[i] = PhaseStart;
                Born[i] = WzSkillData.ToxinFirstPoolMs + ToxinPoolLayout.SpreadMs(pieces[p].Center, e.OriginX, e.StepMs, ToxinPoolLayout.StepPx(wz));
                Shown[i] = false;
                X[i] = pieces[p].Center;
                Y[i] = e.Y;
                Frame[i] = 0;
                Timer[i] = 0;
                Arts[i] = _wz.Art[WzSkillData.ToxinTile(Kind[i], WzSkillData.ToxinPhases[PhaseStart], layer)];
            }
    }

    // The middle (world x) of pool `piece`, and where its feet are: the place of its blast.
    public (float X, float Y) PieceAt(int piece)
    {
        for (int i = 0; i < Count; i++)
            if (Piece[i] == piece) return (X[i], Y[i]);
        throw new ArgumentException("no such pool", nameof(piece));
    }

    // The pool blew up (the server's word): both layers play the end2 phase of its tile once and the pool is gone, then it comes up again at the time the server works out the same way
    // (the wait of WZ y, then the steps from the totem).
    public void Explode(int piece)
    {
        for (int i = 0; i < Count; i++)
        {
            if (Piece[i] != piece) continue;
            if (Shown[i]) SetPhase(i, PhaseBlast);
            Born[i] = _ageMs + (int)_wz.Value("y", _wz.MaxLevel) * WzSkillData.ToxinWzUnitMs + ToxinPoolLayout.SpreadMs(X[i], _originX, _stepMs, ToxinPoolLayout.StepPx(_wz));
        }
    }

    // The widening pool: the pieces whose time has come come up, and every shown piece plays its phases (start, loop, and end for the last ms of the zone).
    private void UpdatePool(float ms)
    {
        _ageMs += ms;
        for (int i = 0; i < Count; i++)
        {
            if (!Shown[i])
            {
                if (_ageMs < Born[i]) continue;
                Born[i] = float.PositiveInfinity;
                SetPhase(i, PhaseStart);
                Shown[i] = true;
            }
            int endMs = _wz.Art[WzSkillData.ToxinTile(Kind[i], WzSkillData.ToxinPhases[PhaseEnd], Layer[i])].Delays.Sum();
            if (Phase[i] != PhaseEnd && Phase[i] != PhaseBlast && LifeMs <= endMs) SetPhase(i, PhaseEnd);
            Timer[i] += ms;
            while (Timer[i] >= Arts[i]!.Delays[Frame[i]])
            {
                Timer[i] -= Arts[i]!.Delays[Frame[i]];
                if (Frame[i] + 1 < Arts[i]!.Frames.Count) Frame[i]++;
                else if (Phase[i] == PhaseStart) SetPhase(i, PhaseLoop);
                else if (Phase[i] == PhaseLoop) Frame[i] = 0;
                else // the end phase is over: the piece is gone (after end2 it comes up again at its Born, after the end phase of the zone not)
                {
                    Shown[i] = false;
                    if (Phase[i] == PhaseEnd) Born[i] = float.PositiveInfinity;
                    break;
                }
            }
        }
    }

    private void SetPhase(int i, int phase)
    {
        Phase[i] = phase;
        Frame[i] = 0;
        Timer[i] = 0;
        Arts[i] = _wz.Art[WzSkillData.ToxinTile(Kind[i], WzSkillData.ToxinPhases[phase], Layer[i])];
    }

    private void StartMist(ZoneEvent e, WzSkillData wz)
    {
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
    }

    // The fire wall: the three flame loops of WZ stacked on the middle of the box, on the feet of the target (the origin of the zone): the big one (tile2, about as wide as the box)
    // behind, the middle one (tile1) in front of it a little to the left and the small one (tile0) a little to the right. Each starts on its own frame. WZ gives only the tiles and the
    // box: how the three sit together is our fit (未核实).
    private void StartWall(ZoneEvent e, WzSkillData wz)
    {
        Opacity = 1;
        float cx = (e.Left + e.Right) / 2;
        Count = 0;
        foreach (var (tile, dx) in new[] { (2, 0), (1, -WallSideDx), (0, WallSideDx) })
        {
            int i = Count++;
            Arts[i] = wz.Art[$"tile{tile}"];
            X[i] = cx + dx;
            Y[i] = e.Y;
            Frame[i] = i % Arts[i]!.Frames.Count;
            Timer[i] = 0;
        }
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
        if (_pool)
        {
            UpdatePool(ms);
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
