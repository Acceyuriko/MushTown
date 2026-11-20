using MushTown.Shared;

namespace MushTown.Server;

// The one damage pipeline of the mob world. A basic swing, a skill hit (WZ skills and the warrior skills) and a DoT tick all go through
// Resolve; nobody orders the steps for himself. The user's rule: a skill puts its bad status on the mob FIRST and the damage is
// calculated after, so the first hit already carries the status. The five steps, in order, for all the targets of one attack:
//   1. Collect the statuses of each target, the ones this attack is about to add included: the status rolls (Elemental Weakness mark,
//      Empirical Knowledge stack) are drawn here, a DoT is checked against the mob's element immunity. A tick of a DoT counts as a mob that has the DoT
//      (a tick rolls nothing and adds nothing, but reads the statuses already there, the marks of its source included).
//   2. Collect the bonuses: the damage % (additive bucket: Weakness, Element Amplification, the mark's damage) and the final damage (own
//      zone: Elemental Drain and Burning Magic multiply), plus the DEF ignored by the mark. The numbers of the build (panel and skill)
//      are in the Build itself; DamageChain.Prepare joins them with what is collected here into one result (DamageChain.HitBase). It is
//      worked out ONCE for one target and one attack (Step2Count counts them), whatever the number of segments.
//   3. Calculate the damage of each segment: only the two dice multiply that result (DamageChain.Roll), nothing is worked out again.
//   4. Calculate the total of the segments.
//   5. Decide what the mob loses: MP Eater, HP, death (a kill buries the mob and its statuses go with it), and only on a mob that lives
//      the new statuses land (the mark, the DoT). A mob the attack did not reach (not a target) or is immune to the DoT's element gets no status.
// DamageChain keeps the formulas; this class only decides the order and what goes into which term.
public sealed class HitPipeline
{
    // What one attack is. The caller fills it once for all its targets.
    public sealed class Attack
    {
        public Build Build;
        public Random Rng = null!;
        public int Attacker;
        public int Skill; // the tag of the hits (0 = basic swing)
        public int Pct = 100; // the skill percent of each segment
        public AttackElement Element = AttackElement.Physical;
        public int Segments = 1;
        public bool Magic; // a magic skill: Elemental Weakness and Element Amplification add damage %, MP Eater may absorb MP
        public Dot? Dot; // the DoT this attack puts on each target (a copy for each mob), built by the caster
        public bool Tick; // a DoT tick: it takes every bonus a hit takes (the user's rule: a tick is no special case) except these four: it never crits, rolls no status (no Weakness or mark roll),
        // takes no MP Eater, and adds no new status; the hit is flagged Dot
        public int Map; // where the caster stands (Elemental Drain counts around him; a tick reads the place where its DoT was cast)
        public float CasterX;
        public float CasterY;
    }

    // What the first two steps found for one target.
    private sealed class Frame
    {
        public Mob M = null!;
        public bool Weaken; // the mark rolled: Elemental Weakness lands (on a mob that lives)
        public bool MarkProc; // the stack rolled: Empirical Knowledge adds one (on a mob that lives)
        public bool DotLands;
        public bool CountsDot; // the DoT skill of this attack is on the mob after step 1 (it lands now, or a tick's own DoT): one layer for Elemental Drain
        public bool Burning; // a status Burning Magic counts is on the mob, or lands now
        public double WeaknessPct; // the damage % of Elemental Weakness this hit has (the mark on the mob, or the one rolled now)
        public double MarkDmg; // the damage % and DEF ignored of this attacker's stacks after this hit
        public double MarkIed;
    }

    // How many step-2 results were worked out (one for each target of an attack, not one for each segment): the tests read it.
    public int Step2Count { get; private set; }

    private const int DrainRangePx = 800; // Elemental Drain: the WZ range, the radius of the circle around the caster whose DoTs count

    private readonly Func<int, WzSkillData> _wz;
    private readonly IReadOnlyList<Mob> _lives;
    private readonly Action<Mob, Random, int> _kill; // the mob left the world: remove it, its grave and its drops
    private readonly Action<Mob> _flushMarks; // the mark changes of the mob go to their owners

    private double _weakPct; // Elemental Weakness: the damage % it adds (WZ x) and its time (WZ time), read by Prepare
    private int _weakMs;
    private bool _markLoaded; // the Empirical Knowledge numbers, read once from skill/wz/80002762.json
    private int _markMaxStacks;
    private int _markTimeMs;
    private double _markDamagePct; // the damage %, per stack (WZ common y)
    private double _markIedPct; // the DEF-ignored %, per stack (WZ common w)

    public HitPipeline(Func<int, WzSkillData> wz, IReadOnlyList<Mob> lives, Action<Mob, Random, int> kill, Action<Mob> flushMarks)
    {
        _wz = wz;
        _lives = lives;
        _kill = kill;
        _flushMarks = flushMarks;
    }

    // The numbers the statuses of this build need, read from the WZ files now, in the cast: the tick loop that lands a ball must not read
    // files (a missing file would throw there and end the loop for everyone). Empirical Knowledge: stack cap (WZ x), time (WZ time, seconds),
    // damage % per stack (WZ y), DEF-ignored % per stack (WZ w).
    public void Prepare(Build build)
    {
        if (build.WeaknessProp > 0)
        {
            var weak = _wz(WzSkillData.ElementalWeakness);
            _weakPct = weak.Value("x", weak.MaxLevel);
            _weakMs = (int)weak.Value("time", weak.MaxLevel) * 1000;
        }
        if (build.MarkProp > 0 && !_markLoaded)
        {
            var mark = _wz(WzSkillData.EmpiricalKnowledge);
            _markMaxStacks = (int)mark.Value("x", mark.MaxLevel);
            _markTimeMs = (int)(mark.Value("time", mark.MaxLevel) * 1000);
            _markDamagePct = mark.Value("y", mark.MaxLevel);
            _markIedPct = mark.Value("w", mark.MaxLevel);
            _markLoaded = true;
        }
    }

    // Elemental Drain: how many layers count near one point. It walks the live mobs of the map and sums the DoT skills of this caster
    // (MobStatuses.DotCount: one per skill; WZ says "your" DoTs, so the DoTs of other players do not count), inside the WZ range circle
    // around the caster's feet, then caps the sum at max (the skill's DrainMax; 0 when not learned). The server also calls it once a tick, to tell each caster the layers it displays.
    // Known gap: a mob this very hit kills still counts the layer of this attack's DoT in step 2 (up to +3% on that hit).
    public int DrainLayers(int map, float x, float y, int max, int attacker) => DrainLayers(map, x, y, max, attacker, null, 0);

    // The same with the DoT this attack is about to put on its targets counted too (a skill the mob does not carry yet is one more layer).
    private int DrainLayers(int map, float x, float y, int max, int attacker, List<Frame>? frames, int dotSkill)
    {
        if (max <= 0) return 0;
        int layers = 0;
        foreach (var m in _lives)
        {
            if (m.Map != map) continue;
            float dx = m.X - x, dy = m.Y - y;
            if (dx * dx + dy * dy > DrainRangePx * DrainRangePx) continue;
            layers += m.Statuses.DotCount(attacker);
            if (frames != null && frames.Any(f => f.M == m && f.CountsDot && !m.Statuses.HasDot(attacker, dotSkill))) layers++;
        }
        return Math.Min(layers, max);
    }

    // Final damage sources multiply (Elemental Drain, Burning Magic): the percent of their product.
    private static double FinalBonus(double a, double b) => ((1 + a / 100) * (1 + b / 100) - 1) * 100;

    // One attack on its targets: the five steps above. Returns the hits, targets in order, each with its segments.
    public List<MobWorld.Hit> Resolve(IReadOnlyList<Mob> targets, Attack a)
    {
        var build = a.Build;
        // 1. statuses (the ones to add too)
        var frames = targets.Select(m => Collect(m, a)).ToList();
        // 2. bonuses: Elemental Drain is one number for the whole attack, read around the caster after the statuses of step 1 (the DoT it adds counts)
        double drain = build.DrainPct <= 0 ? 0
            : build.DrainPct * DrainLayers(a.Map, a.CasterX, a.CasterY, build.DrainMax, a.Attacker, frames, a.Tick ? a.Skill : a.Dot?.Skill ?? 0);
        var hits = new List<MobWorld.Hit>();
        foreach (var f in frames)
        {
            var m = f.M;
            double extraDmg = (a.Magic ? f.WeaknessPct + build.MagicDmgPct : 0) + f.MarkDmg;
            double extraFinal = FinalBonus(drain, f.Burning ? build.DotFinalPct : 0);
            var step2 = DamageChain.Prepare(build, m.Def, m.Level, m.ElemAttr, a.Element, a.Pct, m.Boss, extraDmg, extraFinal, f.MarkIed);
            Step2Count++;
            // 3. each segment: the dice only (a tick has no crit roll: 1.0 never crits)
            var rolls = new List<(long damage, bool crit)>();
            for (int i = 0; i < a.Segments; i++)
            {
                double roll = a.Rng.NextDouble();
                double crit = a.Tick ? 1.0 : a.Rng.NextDouble();
                var result = DamageChain.Roll(step2, roll, crit);
                rolls.Add((result.TotalDamage, result.CritMultiplier > 1.0));
            }
            // 4. the total
            long total = rolls.Sum(r => r.damage);
            // 5. what the mob loses. MP Eater: one roll for each mob a magic hit reaches (0 = not learned, no random number is spent). A proc absorbs
            // ceil(maxMP x pct%) of the mob's current MP (at least 1 when it has any; Boss uses the boss share), never more than it holds.
            int mpGain = 0;
            if (a.Magic && !a.Tick && build.MpEaterProp > 0 && a.Rng.NextDouble() * 100 < build.MpEaterProp && m.MaxMp > 0 && m.Mp > 0)
            {
                int sharePct = m.Boss ? build.MpEaterBossPct : build.MpEaterPct;
                long amount = Math.Min((long)Math.Ceiling(m.MaxMp * sharePct / 100.0), m.Mp);
                m.Mp -= amount;
                mpGain = (int)amount;
            }
            m.Hp = Math.Max(0, m.Hp - total);
            if (m.Hp == 0) _kill(m, a.Rng, a.Attacker);
            else if (!a.Tick) Land(f, a);
            for (int i = 0; i < a.Segments; i++)
                hits.Add(new MobWorld.Hit(m.Uid, m.Map, rolls[i].damage, m.Hp, m.Exp, a.Skill, m.MobId, rolls[i].crit,
                    Settle: i == 0, MpGain: i == 0 ? mpGain : 0, Dot: a.Tick));
        }
        return hits;
    }

    // Step 1 for one mob. The Elemental Weakness chance (the build's prop, in percent; 0 means not learned and no random number is spent) and the
    // Empirical Knowledge chance are rolled once per mob, before its damage. The mark's stacks counted are the ones after this hit (a proc adds one up to
    // the cap; a hit with no proc keeps them), so the first hit already has them. A mob immune to the skill's element does not get its DoT.
    private Frame Collect(Mob m, Attack a)
    {
        var f = new Frame { M = m, WeaknessPct = m.Statuses.MagicExtraDamagePct };
        var build = a.Build;
        if (!a.Tick && build.WeaknessProp > 0 && a.Rng.NextDouble() * 100 < build.WeaknessProp)
        {
            f.Weaken = true;
            f.WeaknessPct = _weakPct;
        }
        if (build.MarkProp > 0)
        {
            f.MarkProc = !a.Tick && a.Rng.NextDouble() * 100 < build.MarkProp; // a tick rolls nothing: it counts the stacks as they are
            int stacks = m.Statuses.StacksAfterHit(a.Attacker, _markMaxStacks, f.MarkProc);
            f.MarkDmg = stacks * _markDamagePct;
            f.MarkIed = stacks * _markIedPct;
        }
        if (a.Tick)
        {
            f.Burning = true; // the DoT it comes from is the status (the last tick comes after the DoT left the mob's list)
            f.CountsDot = true;
            return f;
        }
        f.DotLands = a.Dot != null && MobElemAttr.Multiplier(m.ElemAttr, a.Element) > 0;
        f.CountsDot = f.DotLands;
        f.Burning = f.DotLands || m.Statuses.HasBurningTarget;
        return f;
    }

    // Step 5, the statuses land on a mob that lives: the Weakness mark (it refreshes), the mark stacks (a proc adds, any hit refreshes the time;
    // the owner's client is told) and the DoT (the same source's same skill refreshes).
    private void Land(Frame f, Attack a)
    {
        var m = f.M;
        if (f.Weaken) m.Statuses.Add(MobStatusKind.ElementalWeakness, _weakPct, _weakMs);
        if (a.Build.MarkProp > 0)
        {
            m.Statuses.HitMark(a.Attacker, _markMaxStacks, _markTimeMs, f.MarkProc);
            _flushMarks(m);
        }
        if (f.DotLands) m.Statuses.AddDot(a.Dot!.Clone());
    }
}
