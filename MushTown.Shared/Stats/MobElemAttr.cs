namespace MushTown.Shared;

// WZ mob info/elemAttr is a compact pairs string such as "F2I3": element
// letter followed by resistance level (0 normal, 1 immune, 2 resist, 3 weak).
public static class MobElemAttr
{
    // ignorePct is the attacker's ignore element resistance: it shrinks the
    // damage cut of a resist or immune mob by that share (50% cut, 10% ignore
    // = 45% cut). Weak and normal mobs are not affected.
    public static double Multiplier(string elemAttr, AttackElement element, double ignorePct = 0)
    {
        char code = ElementCode(element);
        for (int i = 0; i + 1 < elemAttr.Length; i += 2)
        {
            if (elemAttr[i] != code) continue;
            double multiplier = (elemAttr[i + 1] - '0') switch
            {
                1 => 0.0,   // immune
                2 => 0.5,   // resist
                3 => 1.5,   // weak
                _ => 1.0,   // normal
            };
            return multiplier < 1.0 ? 1.0 - (1.0 - multiplier) * (1.0 - ignorePct / 100.0) : multiplier;
        }
        return 1.0;
    }

    private static char ElementCode(AttackElement element) => element switch
    {
        AttackElement.Fire => 'F',
        AttackElement.Ice => 'I',
        AttackElement.Lightning => 'L',
        AttackElement.Poison => 'S',
        AttackElement.Holy => 'H',
        AttackElement.Dark => 'D',
        _ => 'P',
    };
}
