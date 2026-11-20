using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Fire/Poison 2nd job actives (210.img): Flame Orb (a box on the caster that every mob inside takes, judged at the body
// action's attack frame, fire) (Meditation is a passive now: its test is below).
// Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Orb = WzSkillData.FlameOrb;

    // Flame Orb as 210.img writes it at level 20: fire, 2 segments, 6 mobs, the box lt(-420,-160) rb(0,20) on the caster, no range,
    // no ball (the effect is the orb), the body action flameOrb with its attack frame at 300 ms.
    private static WzSkillData OrbData()
    {
        var data = new WzSkillData { SkillId = Orb, MaxLevel = 20, Kind = SkillData.KindActive, ElemAttr = "f", AttackDelayMs = 300 };
        data.Common["mpCon"] = SkillFormula.Levels("12+2*d(x/4)", 20);
        data.Common["damage"] = SkillFormula.Levels("141+8*x", 20);
        data.Common["attackCount"] = SkillFormula.Levels("2", 20);
        data.Common["mobCount"] = SkillFormula.Levels("6", 20);
        data.Vectors["lt"] = new[] { -420, -160 };
        data.Vectors["rb"] = new[] { 0, 20 };
        return data;
    }

    // The box is on the caster (no rectBasedOnTarget, no range): every mob whose body touches it is hit, whatever the number of mobs
    // in a row (piercing), two segments each. Facing left the box is x -420..0 and y -160..20 from the caster's feet; facing right it
    // is mirrored. The edges are inside; a mob 1 px past any edge is not hit.
    [TestMethod]
    public void FlameOrb_BoxOnTheCaster_PiercesEveryMobInside()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(OrbData());
        var world = World(assets, (-421, 100), (-420, 100), (-200, 100), (-1, 100), (0, 100), (1, 100), (-100, 120), (-100, 121), (-100, -60), (-100, -61), (300, 100));
        var left = Cast(world, 7, 0, 100, false, new Random(1), Mage, Orb, 141 + 8 * 20);
        CollectionAssert.AreEqual(new[] { -420, -200, -100, -100, -1, 0 }, UidsHit(world, left)); // 6 mobs: the two at x -100 are the y 120 and y -60 ones
        Assert.AreEqual(12, left.Count); // two segments each
        Assert.AreEqual(6, left.Count(h => h.Settle));
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(OrbData());
        var world2 = World(assets2, (-1, 100), (0, 100), (1, 100), (420, 100), (421, 100), (-100, 100));
        var right = Cast(world2, 7, 0, 100, true, new Random(1), Mage, Orb, 141 + 8 * 20);
        CollectionAssert.AreEqual(new[] { 0, 1, 420 }, UidsHit(world2, right)); // the mirrored box: x 0..420
    }

    // At most mobCount (6) mobs are hit: the nearest ones.
    [TestMethod]
    public void FlameOrb_AtMostSixMobs_TheNearest()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(OrbData());
        var world = World(assets, (-10, 100), (-20, 100), (-30, 100), (-40, 100), (-50, 100), (-60, 100), (-70, 100), (-80, 100));
        var hits = Cast(world, 7, 0, 100, false, new Random(1), Mage, Orb, 301);
        CollectionAssert.AreEqual(new[] { -60, -50, -40, -30, -20, -10 }, UidsHit(world, hits));
    }

    // No ball: the cast itself returns nothing and no launch goes out; the box judges at the body action's attack frame (300 ms),
    // on the first server tick after it (a tick is 100 ms, so at 400 ms) and never before. The box stays where the caster cast it.
    [TestMethod]
    public void FlameOrb_JudgesAtTheAttackFrame_NotBefore()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(OrbData());
        var world = World(assets, (-100, 100));
        Assert.AreEqual(0, world.SkillStrike(7, 0, 100, false, new Random(1), Mage, Orb, 301).Count);
        Assert.AreEqual(0, world.TakeLaunches().Count); // no ball
        Assert.AreEqual(0, world.Tick(300, new()).Strikes.Count); // the attack frame is 300 ms: not yet judged on the tick that reaches it
        var late = world.Tick(100, new()).Strikes;
        Assert.AreEqual(2, late.Count); // the 2 segments
        Assert.IsTrue(late.All(s => s.attacker == 0 && s.hit.Skill == Orb));
        Assert.AreEqual(0, world.Tick(1000, new()).Strikes.Count); // once only
    }

    // The element is fire: a fire-weak mob (F3, x1.5) takes more than a mob with no element, and a fire-resist one (F2, x0.5) less. The same rolls.
    [TestMethod]
    public void FlameOrb_Fire_WeakTakesMore_ResistTakesLess()
    {
        long Hit(string elem)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(OrbData());
            assets.AddMap(7, """[{"MobId":100000,"X":-100,"Cy":100}]""", Floor);
            assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, elemAttr: elem);
            var world = new MobWorld(assets.Dir);
            world.EnterMap(7);
            return Cast(world, 7, 0, 100, false, new Random(5), Mage, Orb, 301)[0].Damage;
        }
        long weak = Hit("F3"), plain = Hit(""), resist = Hit("F2");
        var rng = new Random(5);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 301, 200, "F3", AttackElement.Fire), weak);
        Assert.IsTrue(weak > plain && plain > resist);
        Assert.AreEqual(weak / 1.5, plain, plain * 0.001); // x1.5 and x0.5 of the same roll (rounding only)
        Assert.AreEqual(plain * 0.5, resist, plain * 0.001);
    }

    // Meditation is a passive: magic attack +30 (10 + x) at max level, always on, a Skill record (not a Buff: no timer, no buff bar icon).
    // It comes with the other passives of the job (WzSkillStats.Modifiers), so the stat window shows it with no cast.
    [TestMethod]
    public void Meditation_IsAPassive_ThirtyMagicAttackAlwaysOn()
    {
        var mod = WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0).Single(m => m.Label == "Meditation");
        Assert.AreEqual((StatKey.MATT, StatValueType.FLAT, 30.0, StatSource.Skill), (mod.Key, mod.Type, mod.Value, mod.Source));
        var panel = StatAggregator.Aggregate(new[] { new StatModifier(StatKey.MATT, StatValueType.FLAT, 200, StatSource.Base), mod });
        Assert.AreEqual(230, panel.IntValue(StatKey.MATT)); // 200 + 30
        Assert.AreEqual(0, StatAggregator.Aggregate(new[] { mod }).Value(StatKey.MAX_HP)); // it adds nothing else
    }

    // Page II of the Fire/Poison job holds both, after the five passives (Meditation is the sixth passive, right before the actives; the skill window and the quick slots read the page).
    [TestMethod]
    public void FirePoison_PageTwo_HasMeditationAndFlameOrb()
    {
        var page = JobSkills.Pages(StatTables.FirePoisonMage)[1];
        CollectionAssert.AreEqual(new[] { WzSkillData.MpEater, WzSkillData.SpellMastery, WzSkillData.HighWisdom, WzSkillData.ElementalDrain,
            WzSkillData.AgileMagic, WzSkillData.Meditation, WzSkillData.FlameOrb, WzSkillData.PoisonBreath }, page);
        Assert.IsTrue(JobSkills.Has(StatTables.FirePoisonMage, Orb));
        Assert.IsFalse(JobSkills.Has(StatTables.Hero, Orb));
    }

    // Over the wire: Flame Orb is judged by the server (the Build rides the wire): nothing at the cast, then both clients get the two hits
    // of each mob in the box, after the attack frame (300 ms) and a tick.
    [TestMethod]
    public void Loopback_FlameOrb_ServerJudges_BothSeeTheHits()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(OrbData());
        assets.AddMap(9, """[{"MobId":100000,"X":-100,"Cy":100},{"MobId":100000,"X":-300,"Cy":100},{"MobId":100000,"X":200,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 76);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 76);
        b.Connect("127.0.0.1", Net.Port + 76);
        a.SendState(9, 0, 100, Net.ActStand, 0, false);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        for (int i = 0; i < 3; i++) { WaitSpawn(a); WaitSpawn(b); }
        a.SendBuild(Mage);
        var cast = System.Diagnostics.Stopwatch.StartNew();
        a.SendSkillCast(Orb, 301);
        var hits = Enumerable.Range(0, 4).Select(_ => WaitHit(a)).ToList(); // mobs at x -100 and -300 (x 200 is behind the caster), 2 segments each
        cast.Stop();
        Assert.IsTrue(cast.ElapsedMilliseconds >= 300); // never before the attack frame
        Assert.AreEqual(2, hits.Select(h => h.Uid).Distinct().Count());
        Assert.IsTrue(hits.All(h => h.Skill == Orb && h.Damage > 0));
        var seen = Enumerable.Range(0, 4).Select(_ => WaitHit(b)).ToList();
        CollectionAssert.AreEqual(hits.Select(h => h.Damage).ToList(), seen.Select(h => h.Damage).ToList());
        Assert.IsFalse(a.TryTakeMob(out _)); // nothing else
        for (int i = 0; i < 200 && !b.TryTakeFx(out _); i++) Thread.Sleep(5); // the Flame Orb effect of a, shown to b (drain it)
        Thread.Sleep(200);
        Assert.IsFalse(a.TryTakeMob(out _));
        Assert.IsFalse(b.TryTakeMob(out _));
    }
}
