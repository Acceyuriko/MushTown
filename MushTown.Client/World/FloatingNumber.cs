using MushTown.Shared;

namespace MushTown.Client;

// One damage number in flight: the digits rise and fade over LifeMs, then
// the node ends its own life and the game pools it. The skin key picks the
// digit set (0 = NoRed0 for monster damage, -1 = NoViolet0 for player hurt);
// Crit swaps in NoCri1 and draws the skin's effect3 prefix.
public sealed class FloatingNumber : Component
{
    public const float LifeMs = 800; // rise and fade over this long
    public const int RisePx = 60;    // px the number floats up
    public const int StackPx = 30;   // px between the numbers of one multi-hit cast
    public const int StackNearPx = 100; // mobs closer than this stack their numbers together (side by side they would overlap)
    public const int HurtSkin = -1;  // NoViolet0, kept apart from monster damage

    public string Text = "";
    public int Skin; // 0 = dmg.json, -1 = hurtdmg.json, other = dmg{key}.json
    public bool Crit; // draw the critical digit set with its effect3 mark
    public float X; // head position when the hit landed
    public float Y;
    public float Age;

    // A pooled number comes back to life: every field a spawn site does not
    // set has to start clean.
    public void Reset()
    {
        Age = 0;
        Skin = 0;
        Crit = false;
        Owner.Dead = false;
    }

    public override void Update(float dt)
    {
        Age += dt * 1000f;
        if (Age >= LifeMs) Owner.Dead = true;
    }
}
