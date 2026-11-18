namespace MushTown.Shared;

// A bolt that flies in a straight line from one point to another at a fixed speed and arrives once. It only
// moves (X, Y); the node it sits on draws it and acts on Arrived. WZ has no speed for a ball, so the speed is one
// number here: both the server (when the damage lands) and the client (how long the ball flies) take the flight
// time from FlightMs, and the two agree.
public sealed class Projectile : Component
{
    public const float SpeedPxPerSec = 600f; // a proposal: WZ ships no ball speed (Energy Bolt: 340 px in 567 ms)

    public float FromX;
    public float FromY;
    public float ToX;
    public float ToY;
    public int DelayMs; // the body action's attack frame; the ball is hidden and still until this runs out
    public int DurationMs;
    public int ElapsedMs;
    public float X;
    public float Y;
    public bool Started;
    public bool Arrived;

    public static int FlightMs(float fromX, float fromY, float toX, float toY) =>
        (int)MathF.Ceiling(MathF.Sqrt((toX - fromX) * (toX - fromX) + (toY - fromY) * (toY - fromY)) / SpeedPxPerSec * 1000f);

    // A pooled node comes back clean: the launch site sets the two points and the attack-frame delay.
    public void Launch(float fromX, float fromY, float toX, float toY, int delayMs = 0)
    {
        FromX = fromX; FromY = fromY; ToX = toX; ToY = toY;
        DelayMs = delayMs;
        DurationMs = FlightMs(fromX, fromY, toX, toY);
        ElapsedMs = 0;
        X = fromX; Y = fromY;
        Started = delayMs == 0;
        Arrived = false;
        Owner.Dead = false;
    }

    // Whole milliseconds, like the world tick that lands the damage: the ball never arrives later than the hit.
    public override void Update(float dt)
    {
        int ms = (int)MathF.Round(dt * 1000f);
        if (!Started)
        {
            ElapsedMs += ms;
            if (ElapsedMs < DelayMs) return;
            Started = true;
            ElapsedMs -= DelayMs; // the waiting time was not flight time; keep the overflow
        }
        else ElapsedMs += ms;
        float t = DurationMs == 0 ? 1f : Math.Min(1f, (float)ElapsedMs / DurationMs);
        X = FromX + (ToX - FromX) * t;
        Y = FromY + (ToY - FromY) * t;
        if (t < 1f) return;
        Arrived = true;
        Owner.Dead = true;
    }
}
