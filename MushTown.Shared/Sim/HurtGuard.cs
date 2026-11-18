namespace MushTown.Shared;

// One character's hurt state: a landed touch starts it and it runs 60 game
// frames (1/60 s each, the MonoGame default step, so one second). Invincibility
// is what the state grants: every touch inside it is ignored, so there is one
// timer for both.
public sealed class HurtGuard : Component
{
    public const int HurtFrames = 60;
    public const int FlashFrames = 6; // purple / normal swap: 100ms

    public int LeftFrames { get; private set; }
    public bool Invincible => LeftFrames > 0;
    public bool Dodged { get; private set; } // the window was started by a dodge: invincible, but no hit pose, flash or shove
    public bool HitShown => Invincible && !Dodged; // the hit pose and the purple flash run
    public int ElapsedFrames => HurtFrames - LeftFrames;

    public void Tick() => LeftFrames = Math.Max(0, LeftFrames - 1);

    public void Clear()
    {
        LeftFrames = 0;
        Dodged = false;
    }

    // A touch that missed: the same window as a hit (one timer), without the hit.
    public void Dodge()
    {
        LeftFrames = HurtFrames;
        Dodged = true;
    }

    // Returns the damage actually dealt. A blocked touch returns 0 and does
    // not change HP or restart the window.
    public long Apply(CharaSlot chara, long damage)
    {
        if (Invincible) return 0;
        long dealt = Progression.ApplyHurt(chara, damage);
        if (dealt > 0)
        {
            LeftFrames = HurtFrames;
            Dodged = false;
        }
        return dealt;
    }

    // The shove goes away from the source, whichever way the victim faces:
    // -1 = left (source on the right), 1 = right.
    public static int KnockDir(float victimX, float sourceX) => sourceX > victimX ? -1 : 1;

    // roll is 0..99. The panel stores Knockback Resistance; the chance to
    // be pushed is its complement, so this takes the push chance.
    public static bool KnocksBack(int chancePct, int roll) =>
        roll < Math.Clamp(chancePct, 0, 100);
}
