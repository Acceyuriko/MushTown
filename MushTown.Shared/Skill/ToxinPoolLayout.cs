namespace MushTown.Shared;

// How Creeping Toxin's pool is cut into pools (the pieces that blow up one by one): the server and every client work it out the same way from the totem's x and the two edges of the
// spread (the box of the zone, already cut at the platform edge). WZ does not say how the pool is laid out, so this is our fit (未核实): the widest puddle that fits on both sides of the
// totem in the middle (tile kind 3, 264 px, or a smaller one on a small platform), then pieces of 200 px (kind 2) end to end to each side, and one of 88 px (kind 1) in the last gap when it fits.
// So a pool never goes over the edge (a platform narrower than 88 px is the only case). The pieces are in order of x.
public static class ToxinPoolLayout
{
    public static List<(float Center, int Kind)> Pieces(float origin, float left, float right)
    {
        var w = WzSkillData.ToxinPoolWidths;
        var pieces = new List<(float Center, int Kind)>();
        float room = Math.Min(origin - left, right - origin) * 2;
        int mid = 3;
        while (mid > 0 && w[mid] > room) mid--;
        pieces.Add((origin, mid));
        foreach (int dir in new[] { -1, 1 })
        {
            float edge = origin + dir * w[mid] / 2f;
            float limit = dir < 0 ? left : right;
            while (true)
            {
                float space = Math.Abs(limit - edge);
                int kind = space >= w[2] ? 2 : space >= w[1] ? 1 : -1;
                if (kind < 0) break;
                pieces.Add((edge + dir * w[kind] / 2f, kind));
                edge += dir * w[kind];
            }
        }
        pieces.Sort((a, b) => a.Center.CompareTo(b.Center));
        return pieces;
    }

    // The width of one step of the spreading (WZ u) and how far the pool reaches to each side: half the widest puddle (the middle pool) and WzSkillData.ToxinSteps steps.
    public static int StepPx(WzSkillData toxin) => (int)toxin.Value("u", toxin.MaxLevel);
    public static int Reach(WzSkillData toxin) => WzSkillData.ToxinPoolWidths[3] / 2 + WzSkillData.ToxinSteps * StepPx(toxin);

    // Where the totem stands: WzSkillData.ToxinFrontPx in front of the caster.
    public static float TotemX(float casterX, bool facingRight) => casterX + (facingRight ? WzSkillData.ToxinFrontPx : -WzSkillData.ToxinFrontPx);

    // The step of the spreading that brings the pool whose middle is at `center`: 0 for the middle pool, then one more for every stepPx of distance from the totem (the box has reached
    // (step + 1) x stepPx by then, see GroundZone.Spread). The pools of one step come up together.
    public static int Step(float center, float origin, int stepPx) => Math.Max(0, (int)Math.Ceiling(Math.Abs(center - origin) / stepPx) - 1);

    // How long after the first pool the spreading reaches that pool: stepMs for each step. The first spreading (after ToxinFirstPoolMs) and the growing back of a pool that blew up
    // (after its own wait) both use it, so the server and the clients work it out the same way.
    public static int SpreadMs(float center, float origin, int stepMs, int stepPx) => Step(center, origin, stepPx) * stepMs;
}
