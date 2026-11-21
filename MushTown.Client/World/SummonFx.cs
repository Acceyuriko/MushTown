using MushTown.Shared;

namespace MushTown.Client;

// The look of one summon (Creeping Toxin's totem; Ifrit will use it too). It is built from the same SummonEvent on every client; the server owns the summon, this only draws.
// A summon's WZ has three frame lists: summoned (plays once when it comes up), stand (loops while it stands) and die (plays once at the end: it starts when the time left
// is as long as die, so the last frame ends with the summon). Each frame sits on its WZ origin at the feet of the summon. WaitMs is the time before it comes up.
// Known limit (已知限制): a client that enters later gets the time left only, so it plays the summoned list again for a summon that has stood a while (the server's summon is right).
public sealed class SummonFx : Component
{
    public int Uid;
    public int Skill;
    public float X; // the feet
    public float Y;
    public float WaitMs;
    public float LifeMs; // the time left once up
    public WzArt Summoned = null!;
    public WzArt Stand = null!;
    public WzArt Die = null!;
    public WzArt Now = null!; // the list that plays now
    public int Frame;
    public float Timer;
    public bool Started => WaitMs <= 0;

    // A pooled node comes back clean.
    public void Start(SummonEvent e, WzSkillData wz)
    {
        Uid = e.Uid;
        Skill = e.Skill;
        X = e.X;
        Y = e.Y;
        WaitMs = e.DelayMs;
        LifeMs = e.LifeMs;
        Summoned = wz.Art["summoned"];
        Stand = wz.Art["stand"];
        Die = wz.Art["die"];
        Now = Summoned;
        Frame = 0;
        Timer = 0;
        Owner.Dead = false;
    }

    private static int Total(WzArt art) => art.Delays.Sum();

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
        if (Now != Die && LifeMs <= Total(Die)) // the time left is the length of die: it plays now
        {
            Now = Die;
            Frame = 0;
            Timer = 0;
        }
        Timer += ms;
        while (Timer >= Now.Delays[Frame])
        {
            Timer -= Now.Delays[Frame];
            if (Frame + 1 < Now.Frames.Count) Frame++;
            else if (Now == Summoned) { Now = Stand; Frame = 0; }
            else if (Now == Stand) Frame = 0;
            else break; // die holds its last frame
        }
    }
}
