namespace MushTown.Shared;

// One character's hurt cooldown: a landed touch starts the invincibility
// window, and every touch inside it is ignored. The window is the WZ
// 00002000.img/invincible animation: 90 + 360 + 90 + 540 = 1080ms.
public sealed class HurtGuard : Component
{
    public const float InvincibleMs = 1080f;

    public float LeftMs { get; private set; }
    public bool Invincible => LeftMs > 0;

    public void Tick(float dtMs) => LeftMs = Math.Max(0, LeftMs - dtMs);

    public void Clear() => LeftMs = 0;

    // Returns the damage actually dealt. A blocked touch returns 0 and does
    // not change HP or restart the window.
    public long Apply(CharaSlot chara, long damage, int defPct)
    {
        if (Invincible) return 0;
        long dealt = Progression.ApplyHurt(chara, damage, defPct);
        if (dealt > 0) LeftMs = InvincibleMs;
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
