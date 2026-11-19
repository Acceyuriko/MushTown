using MushTown.Shared;

namespace MushTown.Client;

// One damage number in flight: the digits rise and fade over LifeMs, then
// the node ends its own life and the game pools it. The skin key picks the
// digit set (0 = NoRed0 for monster damage, -1 = NoViolet0 for player hurt);
// Crit swaps in NoCri1 and draws the skin's effect3 prefix.
// The timing of a cast with several numbers lives here and only here: the numbers of one mob appear one after the
// other from the bottom up (each starts AppearStepMs after the one below, DelayMs), then each runs the same rise
// and fade, so they fade one after the other from the bottom up too. Where they stand is the spawn site's business.
public sealed class FloatingNumber : Component
{
    public const float LifeMs = 800; // rise and fade over this long
    public const int RisePx = 60;    // px the number floats up
    public const int StackPx = 30;   // px between the numbers of one multi-hit cast
    public const int AppearStepMs = 100; // the next number of one mob appears this long after the one below (a proposal: not read from the video)
    public const int StackNearPx = 100; // mobs closer than this stack their numbers together (side by side they would overlap)
    public const int HurtSkin = -1;  // NoViolet0, kept apart from monster damage

    public string Text = "";
    public int Skin; // 0 = dmg.json, -1 = hurtdmg.json, other = dmg{key}.json
    public bool Crit; // draw the critical digit set with its effect3 mark
    public float X; // head position when the hit landed
    public float Y;
    public float Age; // ms since the node was spawned (it keeps counting while the number waits for its turn)
    public float DelayMs; // the number is not there until Age reaches this

    // A pooled number comes back to life: every field a spawn site does not
    // set has to start clean.
    public void Reset()
    {
        Age = 0;
        DelayMs = 0;
        Skin = 0;
        Crit = false;
        Owner.Dead = false;
    }

    public bool Visible => Age >= DelayMs;

    // 0..1 through its own life (from the moment it appeared).
    private float Progress => (Age - DelayMs) / LifeMs;

    // Full for 70% of the life, then it fades out.
    public int Alpha => Progress < 0.7f ? 255 : (int)(255 * (1 - (Progress - 0.7f) / 0.3f));

    public float Rise => RisePx * Progress;

    public override void Update(float dt)
    {
        Age += dt * 1000f;
        if (Age >= DelayMs + LifeMs) Owner.Dead = true;
    }
}
