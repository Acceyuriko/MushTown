namespace MushTown.Shared;

// The air moves of one airtime (from leaving the ground to touching ground or grabbing a ladder): what a skill may still
// do up there. Mana Wave (WZ 2001011 and its child 2001012) gives two moves, each once per airtime: a boost (the feet
// shoot up) and a slow fall (the fall speed is capped while the Jump key is held, for a few seconds). Both reset when
// the feet touch ground or a ladder. It sits on the player object before the PlayerMover, so its cap is set before the
// mover falls. The numbers come from the caller (the skill's WZ data); this component only keeps the state.
public sealed class AirMovement : Component
{
    public bool BoostUsed { get; private set; }
    public bool FloatUsed { get; private set; }
    public bool Floating { get; private set; }
    public float FloatLeft { get; private set; } // seconds of slow fall still allowed
    private float _fall; // the fall speed cap while floating, px per second

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

    // speed: px per second, up. The feet leave the ground now (the mover's own jump does not run that frame).
    public void Boost(float speed)
    {
        BoostUsed = true;
        Mover.Ground = null;
        Mover.Vy = -speed;
    }

    // seconds: how long it may last; fall: the fall speed cap, px per second. It ends when the Jump key is let go.
    public void Float(float seconds, float fall)
    {
        FloatUsed = true;
        Floating = true;
        FloatLeft = seconds;
        _fall = fall;
        Mover.FallCap = fall;
    }

    public override void Update(float dt)
    {
        Refresh();
        if (Floating)
        {
            if (!Mover.Input.JumpHeld || FloatLeft <= 0) Floating = false;
            else FloatLeft -= dt;
        }
        Mover.FallCap = Floating ? _fall : PlayerMover.MaxFall;
    }

    // Ground or a ladder under the feet: a new airtime starts.
    private void Refresh()
    {
        if (Mover.Ground == null && Mover.Climb == null) return;
        BoostUsed = false;
        FloatUsed = false;
        Floating = false;
    }
}
