using MushTown.Shared;

namespace MushTown.Client;

// One damage number in flight: the digits rise and fade over LifeMs, then
// the node ends its own life and the game pools it. The skin key picks the
// digit set (0 = NoRed0 for monster damage, -1 = NoViolet0 for player hurt).
public sealed class FloatingNumber : Component
{
    public const float LifeMs = 800; // rise and fade over this long
    public const int RisePx = 60;    // px the number floats up
    public const int HurtSkin = -1;  // NoViolet0, kept apart from monster damage

    public string Text = "";
    public int Skin; // 0 = dmg.json, -1 = hurtdmg.json, other = dmg{key}.json
    public float X; // head position when the hit landed
    public float Y;
    public float Age;

    // A pooled number comes back to life: every field a spawn site does not
    // set has to start clean.
    public void Reset()
    {
        Age = 0;
        Skin = 0;
        Owner.Dead = false;
    }

    public override void Update(float dt)
    {
        Age += dt * 1000f;
        if (Age >= LifeMs) Owner.Dead = true;
    }
}
