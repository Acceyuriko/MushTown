using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The status kinds Burning Magic counts (MobStatusKind: Dot, Stun, Freeze, Darkness, Paralysis; WZ text of Burning Magic). No skill puts
// stun, freeze, darkness or paralysis on a mob yet: the tests put them on the Mob's own status list and drive the pipeline directly. Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    // HasBurningTarget is the one question "does Burning Magic count this mob": a DoT or any of the four; Elemental Weakness is not one of them.
    [TestMethod]
    public void MobStatuses_HasBurningTarget_CountsExactlyTheWzStatuses()
    {
        var list = new MobStatuses();
        Assert.IsFalse(list.HasBurningTarget);
        list.Add(MobStatusKind.ElementalWeakness, 20, 5000);
        Assert.IsFalse(list.HasBurningTarget);
        foreach (var kind in new[] { MobStatusKind.Stun, MobStatusKind.Freeze, MobStatusKind.Darkness, MobStatusKind.Paralysis })
        {
            var one = new MobStatuses();
            Assert.IsFalse(one.Has(kind));
            one.Add(kind, 0, 1000);
            Assert.IsTrue(one.Has(kind) && one.HasBurningTarget, kind.ToString());
            one.Step(1000);
            Assert.IsFalse(one.Has(kind) || one.HasBurningTarget, $"{kind} runs out");
        }
        var dot = new MobStatuses();
        Assert.IsFalse(dot.Has(MobStatusKind.Dot));
        dot.AddDot(new Dot { Skill = 1, Attacker = 0, IntervalMs = 1000, MsLeft = 1000 });
        Assert.IsTrue(dot.Has(MobStatusKind.Dot) && dot.HasBurningTarget);
        dot.Step(1000);
        Assert.IsFalse(dot.Has(MobStatusKind.Dot) || dot.HasBurningTarget);
    }

    // Stun, freeze, darkness or paralysis on the mob (put on the Mob's own status list: no skill does it yet): a skill hit and a basic swing take
    // Burning Magic's final damage, and so does a DoT tick; a build that has not learned it gets nothing. The pipeline is driven directly.
    [TestMethod]
    [DataRow(MobStatusKind.Stun)]
    [DataRow(MobStatusKind.Freeze)]
    [DataRow(MobStatusKind.Darkness)]
    [DataRow(MobStatusKind.Paralysis)]
    public void BurningMagic_EachStatus_DirectHitAndTickTakeIt(MobStatusKind kind)
    {
        var heavy = Mage with { DmgPct = 100 }; // DmgPct 100: the damage zone and the final zone give different numbers
        var trained = heavy with { DotFinalPct = 15 };
        var mob = new Mob { Uid = 1, Map = 7, Level = 200, Def = 0.1, Hp = 1_000_000_000_000, MaxHp = 1_000_000_000_000 };
        var pipeline = new HitPipeline(_ => throw new InvalidOperationException("no file"), new List<Mob> { mob }, (_, _, _) => { }, _ => { });
        long Hit(Build build, int seed, int pct, bool magic, bool tick = false) => pipeline.Resolve(new[] { mob }, new HitPipeline.Attack
        {
            Build = build,
            Rng = new Random(seed),
            Pct = pct,
            Magic = magic,
            Tick = tick,
            Element = AttackElement.Poison,
        })[0].Damage;
        long Expected(Build build, int seed, int pct, double extra, bool tick = false, bool asDamage = false)
        {
            var rng = new Random(seed);
            double roll = rng.NextDouble(), crit = tick ? 1.0 : rng.NextDouble();
            return asDamage // the same number in the damage zone instead
                ? Damage.SkillCalc(roll, crit, 0.1, build, pct, 200, "", AttackElement.Poison, extraDmgPct: extra)
                : Damage.SkillCalc(roll, crit, 0.1, build, pct, 200, "", AttackElement.Poison, extraFinalPct: extra);
        }
        Assert.IsFalse(mob.Statuses.HasBurningTarget);
        Assert.AreEqual(Expected(trained, 3, 78, 0), Hit(trained, 3, 78, true)); // no status yet: nothing
        mob.Statuses.Add(kind, 0, 600_000);
        Assert.IsTrue(mob.Statuses.Has(kind) && mob.Statuses.HasBurningTarget);
        Assert.AreEqual(Expected(trained, 3, 78, 15), Hit(trained, 3, 78, true)); // a skill hit
        Assert.AreNotEqual(Expected(trained, 3, 78, 15), Expected(trained, 3, 78, 15, asDamage: true));
        Assert.AreEqual(Expected(trained, 4, 100, 15), Hit(trained, 4, 100, false)); // a basic swing
        Assert.AreNotEqual(Expected(trained, 4, 100, 15), Expected(trained, 4, 100, 15, asDamage: true));
        Assert.AreEqual(Expected(trained, 5, 60, 15, tick: true), Hit(trained, 5, 60, true, tick: true)); // a DoT tick
        Assert.AreNotEqual(Expected(trained, 5, 60, 15, tick: true), Expected(trained, 5, 60, 15, tick: true, asDamage: true));
        Assert.AreEqual(Expected(heavy, 3, 78, 0), Hit(heavy, 3, 78, true)); // not learned: nothing
    }
}
