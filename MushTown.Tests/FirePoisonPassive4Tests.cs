using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Fire/Poison 4th job passives (212.img): Buff Mastery (magic attack, buff time, knockback resistance), Infinity (final damage, the
// 5 sec HP/MP recovery), Arcane Aim (DEF ignored and the damage % of its stacks) and Fervent Drain (final damage for each DoT, the
// same entry as Elemental Drain). Also Teleport Mastery's knockback resistance (2111007). Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private static IEnumerable<StatModifier> FourthJobMods() => WzSkillStats.Modifiers(WzSkillTests.FourthJobPassives(), 200, 0);

    // The WZ formulas at max level: Buff Mastery (10) madX 30, bufftimeR 50, stanceProp 60; Infinity (30) s 10; Arcane Aim (30)
    // prop 100, x 8, y 5; Fervent Drain (1) x 5. (Infinity mdR 91 and Arcane Aim ignoreMobpdpR 20 are in FourthJobPassives_StatRecords.)
    [TestMethod]
    public void FourthJobPassives_NumbersFromTheFormulas()
    {
        var skills = WzSkillTests.FourthJobPassives();
        Assert.AreEqual(10, WzSkillStats.Infinity(skills)); // s 10
        var aim = WzSkillStats.ArcaneAim(skills);
        Assert.AreEqual(100, aim.prop);           // 25 + 5 x d(30/2)
        Assert.AreEqual(8, aim.perStack);         // 2 + d(30/5)
        Assert.AreEqual(5, aim.maxStacks);
        Assert.AreEqual(5, WzSkillStats.FerventDrainPct(skills));
    }

    // The stat records: Buff Mastery's three keys, Infinity's final damage multiplier, Arcane Aim's ignore-defense multiplier and the
    // two knockback resistances (Buff Mastery's 60 and Teleport Mastery's 40) from the formulas.
    [TestMethod]
    public void FourthJobPassives_StatRecords()
    {
        var mods = FourthJobMods().ToList();
        StatModifier Get(string label, StatKey key) => mods.Single(m => m.Label == label && m.Key == key);
        Assert.AreEqual(30, Get("Buff Mastery", StatKey.MATT).Value);
        Assert.AreEqual(50, Get("Buff Mastery", StatKey.BUFF_DURATION_PCT).Value);
        Assert.AreEqual(60, Get("Buff Mastery", StatKey.KNOCKBACK_RESISTANCE).Value);
        Assert.AreEqual(StatValueType.MULTIPLIER, Get("Infinity", StatKey.FINAL_DAMAGE_PCT).Type);
        Assert.AreEqual(1.91, Get("Infinity", StatKey.FINAL_DAMAGE_PCT).Value, 1e-9);
        Assert.AreEqual(StatValueType.MULTIPLIER, Get("Arcane Aim", StatKey.IED_PCT).Type);
        Assert.AreEqual(0.8, Get("Arcane Aim", StatKey.IED_PCT).Value, 1e-9);
        Assert.AreEqual(40, Get("Teleport Mastery", StatKey.KNOCKBACK_RESISTANCE).Value);
    }

    // Teleport Mastery (2111007, stanceProp 4 x level): level 10 gives 40 points of knockback resistance. The stat window's knockback
    // row (32) reads the Skill group (Buff Mastery 60 + Teleport Mastery 40 = 100) and the snapshot keeps Teleport Mastery's own record.
    [TestMethod]
    public void TeleportMastery_KnockbackResistance_FromTheLevel()
    {
        var skills = WzSkillTests.FourthJobPassives();
        var teleMastery = skills[WzSkillData.TeleportMastery];
        Assert.AreEqual(40, (int)teleMastery.Value("stanceProp", 10));
        var snap = StatAggregator.Aggregate(FourthJobMods());
        Assert.IsTrue(snap.Modifiers.Any(m => m.Label == "Teleport Mastery" && m.Key == StatKey.KNOCKBACK_RESISTANCE));
        Assert.AreEqual(100, snap.IntValue(StatKey.KNOCKBACK_RESISTANCE));
        Assert.IsTrue(StatDisplay.Sources(snap, 32).Any(l => l.Key == StatKey.KNOCKBACK_RESISTANCE && l.Value == 100));
    }

    // Infinity: final damage +91% (a multiplier) and a recovery of 10% of each pool (capped at the max).
    [TestMethod]
    public void Infinity_FinalDamageAndRecovery()
    {
        var snap = StatAggregator.Aggregate(new[] { new StatModifier(StatKey.FINAL_DAMAGE_PCT, StatValueType.MULTIPLIER, 1.91, StatSource.Skill, "Infinity") });
        Assert.AreEqual(1.91, DamageChain.FinalDamageMultiplier(snap), 1e-9);
        var chara = new CharaSlot { Hp = 100, Mp = 50 };
        Progression.RestorePercent(chara, 1000, 500, 10);
        Assert.AreEqual(200, chara.Hp); // +10% of 1000
        Assert.AreEqual(100, chara.Mp); // +10% of 500
        chara.Hp = 999;
        Progression.RestorePercent(chara, 1000, 500, 10);
        Assert.AreEqual(1000, chara.Hp); // capped, never over
    }

    // Arcane Aim: a hit adds a stack up to 5, 5 s without a hit drops them all, and each stack adds damage % (so the damage grows with
    // the count). The stat snapshot that the client builds carries stacks x WZ x (8) as a DAMAGE_PCT record.
    [TestMethod]
    public void ArcaneAim_StacksAndDamage()
    {
        var aim = WzSkillStats.ArcaneAim(WzSkillTests.FourthJobPassives());
        var fighter = new Fighter();
        for (int i = 0; i < 6; i++) fighter.AddArcaneStack(aim.maxStacks);
        Assert.AreEqual(5, fighter.ArcaneStacks);
        fighter.Tick(4999f);
        Assert.AreEqual(5, fighter.ArcaneStacks); // still inside the 5 s
        fighter.Tick(1f);
        Assert.AreEqual(0, fighter.ArcaneStacks); // 5 s without a hit
        long Damage(int stacks) => DamageChain.Calculate(Mage with { DmgPct = stacks * aim.perStack }, 0.5, 1.0, 0.1, 200).TotalDamage;
        Assert.AreEqual(DamageChain.Calculate(Mage with { DmgPct = 40 }, 0.5, 1.0, 0.1, 200).TotalDamage, Damage(5)); // 5 x 8 = 40
        Assert.IsTrue(Damage(1) < Damage(2) && Damage(2) < Damage(3) && Damage(3) < Damage(4) && Damage(4) < Damage(5));
        var snap = StatAggregator.Aggregate(new[] { new StatModifier(StatKey.DAMAGE_PCT, StatValueType.FLAT, 3 * aim.perStack, StatSource.Buff, "Arcane Aim") });
        Assert.AreEqual(24, snap.IntValue(StatKey.DAMAGE_PCT)); // 3 stacks
    }

    // Fervent Drain goes through the Elemental Drain entry: one DoT adds its 5% (plus Elemental Drain's own 3% when both are learned),
    // and a mob with no DoT gets nothing. It is final damage, the same bucket as Elemental Drain.
    [TestMethod]
    public void FerventDrain_AddsFinalDamageOnAPoisonedMob()
    {
        long Hit(Build build, bool poisoned, out double r1, out double r2)
        {
            var mob = AuditMob();
            if (poisoned) mob.Statuses.AddDot(DotOf(2100001, 60_000));
            var hit = StatusPipeline(new List<Mob> { mob }, build, (_, _, _) => { })
                .Resolve(new[] { mob }, new HitPipeline.Attack { Build = build, Rng = new Random(3), Pct = 100, Map = 7 })[0];
            var rng = new Random(3);
            r1 = rng.NextDouble();
            r2 = rng.NextDouble();
            return hit.Damage;
        }
        var onlyFervent = Mage with { DmgPct = 100, DrainMax = 5, FerventPct = 5 };
        long poisoned = Hit(onlyFervent, true, out double r1, out double r2);
        Assert.AreEqual(Damage.SkillCalc(r1, r2, 0.1, onlyFervent, 100, 200, extraFinalPct: 5), poisoned); // one layer: +5%
        Assert.AreEqual(Damage.SkillCalc(r1, r2, 0.1, onlyFervent, 100, 200), Hit(onlyFervent, false, out _, out _)); // no DoT: no bonus
        var both = Mage with { DmgPct = 100, DrainPct = 3, DrainMax = 5, FerventPct = 5 };
        Assert.AreEqual(Damage.SkillCalc(r1, r2, 0.1, both, 100, 200, extraFinalPct: 8), Hit(both, true, out _, out _)); // 3 + 5 per layer
    }
}
