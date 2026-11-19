namespace MushTown.Shared;

// Why a slow fall ended.
public enum FloatEnd { None, Released, TimedOut, Landed }

// The air moves of one airtime (from leaving the ground to touching ground or grabbing a ladder): what a skill may still
// do up there. Mana Wave (WZ 2001011 and its child 2001012) gives two moves, each once per airtime: a boost (the feet
// shoot up) and a slow fall (a fall that starts from rest, speeds up slowly to a cap, while the Jump key is held, for a few
// seconds). Both reset when the feet touch ground or a ladder. It sits on the player object before the PlayerMover, so its
// cap, its pull and its pose are set before the mover falls. The numbers come from the caller (the skill's WZ data); this
// component only keeps the state.
// The poses (WZ Character 00002000.img): manaWave is alert frame 2 for 30 ms, then jump; manaWaveFloat is fly frame 0.
public sealed class AirMovement : Component
{
    public const float BoostPoseSeconds = 0.03f; // WZ manaWave step 0: alert, frame 2, delay -30 (30 ms)

    public bool BoostUsed { get; private set; }
    public bool FloatUsed { get; private set; }
    public bool Floating { get; private set; }
    public FloatEnd EndedBy { get; private set; } // how the last slow fall ended (None while one runs or none has)
    public float FloatLeft { get; private set; } // seconds of slow fall still allowed
    public float BoostPoseLeft { get; private set; } // seconds of the boost's alert pose still to show
    private float _cap;  // the fall speed cap while floating, px per second
    private float _pull; // how fast the fall speeds up while floating, px per second squared

    private PlayerMover Mover => Owner.Get<PlayerMover>()!;

    // A boost is possible on the ground or in the air, not on a ladder, once per airtime.
    public bool CanBoost
    {
        get { Refresh(); return !BoostUsed && Mover.Climb == null; }
    }

    // A slow fall is possible in the air only, once per airtime.
    public bool CanFloat
    {
        get { Refresh(); return !FloatUsed && Mover.Ground == null && Mover.Climb == null; }
    }

    // speed: px per second, up. The feet leave the ground now (the mover's own jump does not run that frame). A boost
    // during a slow fall ends it without a reason (Floating false, EndedBy stays None): the Jump key is held for the boost
    // itself, so no release or time limit ended it.
    public void Boost(float speed)
    {
        BoostUsed = true;
        Floating = false;
        BoostPoseLeft = BoostPoseSeconds;
        Mover.Ground = null;
        Mover.Vy = -speed;
    }

    // seconds: how long it may last; cap: the fastest fall, px per second; pull: how fast the fall speeds up, px per
    // second squared. It starts from rest and ends when the Jump key is let go. The fall speed is set to 0 at once, also
    // when the hero is still rising: a slow fall started on the way up stops the rise on the spot.
    // What the numbers stand on (WZ 2001012: u 25, v 95): the SHAPE (slow first, faster later) is from a video of the real
    // game (BV1qv4y1v7tK, 16:35; logs/T-20261119172545/video_fall_curve.png): a steady speed-up fits it with 0.6 px of
    // error, a constant speed with 2.5 px. The NUMBERS are not proven: the video gives screen y (the camera may move),
    // the data after frame 242 is cut off by the picture, the scale (video px per game px) is only estimated (about 1.17),
    // so the video speed-up is about 31 game px per second squared, not 25 (a 25% gap), and the video never reaches a cap,
    // so v 95 is not seen. u 25 as px/s^2 and v 95 as px/s are assumptions; judge them by feel and change the WZ reading.
    public void Float(float seconds, float cap, float pull)
    {
        FloatUsed = true;
        Floating = true;
        EndedBy = FloatEnd.None;
        FloatLeft = seconds;
        _cap = cap;
        _pull = pull;
        Mover.Vy = 0;
    }

    public override void Update(float dt)
    {
        Refresh();
        if (Floating)
        {
            if (!Mover.Input.JumpHeld) End(FloatEnd.Released);
            else if (FloatLeft <= 0) End(FloatEnd.TimedOut);
            else FloatLeft -= dt;
        }
        Mover.FallCap = Floating ? _cap : PlayerMover.MaxFall;
        Mover.FallAccel = Floating ? _pull : PlayerMover.Gravity;
        Mover.AirAction = Floating ? "fly" : BoostPoseLeft > 0 ? "alert" : "jump";
        if (BoostPoseLeft > 0) BoostPoseLeft -= dt;
    }

    // Ground or a ladder under the feet: a new airtime starts.
    private void Refresh()
    {
        if (Mover.Ground == null && Mover.Climb == null) return;
        if (Floating) End(FloatEnd.Landed);
        BoostUsed = false;
        FloatUsed = false;
    }

    private void End(FloatEnd why)
    {
        Floating = false;
        EndedBy = why;
    }
}
