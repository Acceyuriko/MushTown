using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The damage-over-time effect (MobStatuses.Dot, MobWorld.DotTick) and Poison Breath (210.img, the first skill that puts one on a mob).
// Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Breath = WzSkillData.PoisonBreath;

    // Poison Breath as 210.img writes it at level 10: poison, 6 segments, 6 mobs, the box lt(-200,-200) rb(200,120) on the caster
    // (it also carries rectBasedOnTarget, but a skill with no ball ignores it), no ball, a body action with its attack frame at
    // attackDelayMs; a DoT of 60% every 1 s for dotTime s (10 at max level).
    private static WzSkillData BreathData(int id = Breath, bool targetBox = false, int attackDelayMs = 0, int dotTime = 10)
    {
        var data = new WzSkillData { SkillId = id, MaxLevel = 10, Kind = SkillData.KindActive, ElemAttr = "s", AttackDelayMs = attackDelayMs };
        data.Common["mpCon"] = SkillFormula.Levels("11+3*d(x/4)", 10);
        data.Common["damage"] = SkillFormula.Levels("130+5*x", 10);
        data.Common["attackCount"] = SkillFormula.Levels("6", 10);
        data.Common["mobCount"] = SkillFormula.Levels("6", 10);
        data.Common["dot"] = SkillFormula.Levels("30+3*x", 10);
        data.Common["dotInterval"] = SkillFormula.Levels("1", 10);
        data.Common["dotTime"] = SkillFormula.Levels(dotTime.ToString(), 10);
        data.Vectors["lt"] = new[] { -200, -200 };
        data.Vectors["rb"] = new[] { 200, 120 };
        if (targetBox) data.Info["rectBasedOnTarget"] = "1";
        return data;
    }

    // A DoT skill with its own id, to give a mob one more layer (Elemental Drain counts the skills).
    private static WzSkillData DotSkillData(int id) => BreathData(id);

    private static Dot DotOf(int skill, int timeMs, int intervalMs = 1000, int attacker = 0) =>
        new() { Skill = skill, Attacker = attacker, IntervalMs = intervalMs, MsLeft = timeMs };

    // A world with one mob of the given HP, level 200, DEF 10%, at x 100 (the caster stands at x 0, facing right).
    private static MobWorld OneMob(TempAssets assets, long hp, string elemAttr = "")
    {
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, hp, 0.1, 0, speed: 1, level: 200, elemAttr: elemAttr);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        return world;
    }

    // One world step of ms with no players: what the server sends (the balls that landed and the DoT ticks).
    private static List<MobWorld.Hit> Ticks(MobWorld world, int ms) => world.Tick(ms, new()).Strikes.Select(s => s.hit).ToList();

    // MobStatuses: one DoT for each skill. The same skill again only refreshes (the time restarts, the count stays); another skill is another layer.
    [TestMethod]
    public void MobStatuses_Dots_CountedBySkill_RefreshAndExpire()
    {
        var list = new MobStatuses();
        Assert.AreEqual(0, list.DotCount(0));
        list.AddDot(DotOf(Breath, 5000));
        list.AddDot(DotOf(Breath, 5000)); // the same skill: refresh, no second layer
        Assert.AreEqual(1, list.DotCount(0));
        list.AddDot(DotOf(2111003, 2000)); // another skill: another layer
        Assert.AreEqual(2, list.DotCount(0));
        list.Step(2000); // the second one runs out at 0
        Assert.AreEqual(1, list.DotCount(0));
        list.AddDot(DotOf(Breath, 5000)); // a refresh restarts the time
        list.Step(4999);
        Assert.AreEqual(1, list.DotCount(0));
        list.Step(1);
        Assert.AreEqual(0, list.DotCount(0));
    }

    // Two sources, the same skill: two independent effects, each with its own clock. DotCount(source) counts only that source's DoTs (it is
    // the Elemental Drain layer count: WZ says "your" DoTs), and both sources tick every interval, and each stops at its own dotTime.
    [TestMethod]
    public void MobStatuses_Dots_TwoSourcesSameSkill_AreIndependent()
    {
        var list = new MobStatuses();
        list.AddDot(DotOf(Breath, 3000, attacker: 5));
        list.AddDot(DotOf(Breath, 5000, attacker: 6)); // another source: another effect on its own clock
        Assert.AreEqual(1, list.DotCount(5)); // each source counts only its own DoT
        Assert.AreEqual(1, list.DotCount(6));
        Assert.AreEqual(0, list.DotCount(7));
        list.AddDot(DotOf(Breath, 3000, attacker: 5)); // same source and skill: refresh, no second effect
        Assert.AreEqual(1, list.DotCount(5));
        list.Step(1000);
        Assert.AreEqual(2, list.TakeDue().Count); // both sources ticked
        list.Step(2000);
        Assert.AreEqual(4, list.TakeDue().Count); // 2..3 s: both still tick (2 each)
        list.Step(2000);
        Assert.AreEqual(2, list.TakeDue().Count); // 3..5 s: source 5 is gone, source 6 still ticks
        list.Step(1000);
        Assert.AreEqual(0, list.TakeDue().Count);
        Assert.AreEqual(0, list.DotCount(5));
        Assert.AreEqual(0, list.DotCount(6));
    }

    // The clock: the first tick comes one interval after the cast, then one every interval, the last one at the end of the time (3 ticks for 3 s of
    // 1 s), and no more. One big step gives only the ticks inside the time.
    [TestMethod]
    public void MobStatuses_DotTicks_OnePerInterval_StopAtTheEnd()
    {
        var list = new MobStatuses();
        list.AddDot(DotOf(Breath, 3000));
        list.Step(999);
        Assert.AreEqual(0, list.TakeDue().Count);
        list.Step(1);
        Assert.AreEqual(1, list.TakeDue().Count);
        list.Step(1000);
        list.Step(1000); // the dot ran out in this step: its last tick still comes
        Assert.AreEqual(2, list.TakeDue().Count);
        Assert.AreEqual(0, list.DotCount(0));
        list.Step(5000);
        Assert.AreEqual(0, list.TakeDue().Count);
        list.AddDot(DotOf(Breath, 3000));
        list.Step(60_000); // one huge step: 3 ticks, not 60
        Assert.AreEqual(3, list.TakeDue().Count);
    }

    // A cast puts the DoT on the mob: from then on every dotInterval (1 s) a tick lands (a settled hit flagged Dot, the skill's id, no crit),
    // dotTime (10 s) long, then no more. Each tick is one roll through the Damage chain with the cast's Build, the dot % (60) and the poison element.
    [TestMethod]
    public void Dot_TicksEverySecond_TenSeconds_ThenStops()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000);
        var build = Mage with { CritPct = 100 }; // every hit of the cast crits, a DoT never does
        var cast = world.SkillStrike(7, 0, 100, true, new Random(4), build, Breath, 180);
        Assert.AreEqual(6, cast.Count); // the 6 segments, judged at once (no body action)
        Assert.IsTrue(cast.All(h => !h.Dot && h.Crit));
        var rng = new Random(4);
        for (int i = 0; i < 12; i++) rng.NextDouble(); // the 6 segments' rolls
        long hp = cast[^1].HpLeft;
        Assert.AreEqual(0, Ticks(world, 999).Count); // not before one interval
        for (int sec = 1; sec <= 10; sec++)
        {
            var ticks = Ticks(world, sec == 1 ? 1 : 1000);
            Assert.AreEqual(1, ticks.Count, $"second {sec}");
            var tick = ticks[0];
            Assert.IsTrue(tick.Dot && tick.Settle && !tick.Crit && tick.Skill == Breath);
            Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), 1.0, 0.1, build, 60, 200, "", AttackElement.Poison), tick.Damage);
            hp -= tick.Damage;
            Assert.AreEqual(hp, tick.HpLeft);
        }
        Assert.AreEqual(0, Ticks(world, 30_000).Count); // over after dotTime
    }

    // The same skill again only refreshes the time (the numbers of the new cast): still one tick a second, and the time counts from the new cast. A
    // second DoT skill is a second layer: one tick of each skill every second.
    [TestMethod]
    public void Dot_SameSkillRefreshes_DifferentSkillsAreLayers()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        assets.AddWzSkill(DotSkillData(2111003));
        var world = OneMob(assets, 1_000_000_000);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180);
        Assert.AreEqual(5, Enumerable.Range(0, 5).Sum(_ => Ticks(world, 1000).Count)); // 5 s in
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Breath, 180); // the refresh: 10 s again, no second layer
        for (int sec = 1; sec <= 10; sec++)
            Assert.AreEqual(1, Ticks(world, 1000).Count, $"second {sec} after the refresh"); // one layer, and 10 s, not the 5 s that were left
        Assert.AreEqual(0, Ticks(world, 1000).Count);
        world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Breath, 180);
        world.SkillStrike(7, 0, 100, true, new Random(3), Mage, 2111003, 180); // another skill: its own layer
        var both = Ticks(world, 1000);
        CollectionAssert.AreEquivalent(new[] { Breath, 2111003 }, both.Select(h => h.Skill).ToArray());
    }

    // The element is poison: a poison-weak mob (S3, x1.5) takes more of each tick and of the cast than a mob with no element, a poison-resist one
    // (S2, x0.5) less. The same rolls. A poison-immune mob (S1) takes the minimum from the cast and gets no DoT.
    [TestMethod]
    public void PoisonBreath_Poison_WeakTakesMore_ResistTakesLess_ImmuneGetsNoDot()
    {
        (long cast, long tick, int ticks) Run(string elem)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(BreathData());
            var world = OneMob(assets, 1_000_000_000, elem);
            long cast = world.SkillStrike(7, 0, 100, true, new Random(5), Mage, Breath, 180)[0].Damage;
            var ticks = Ticks(world, 3000);
            return (cast, ticks.Count > 0 ? ticks[0].Damage : 0, ticks.Count);
        }
        var weak = Run("S3");
        var plain = Run("");
        var resist = Run("S2");
        var immune = Run("S1");
        var rng = new Random(5);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 180, 200, "S3", AttackElement.Poison), weak.cast);
        Assert.IsTrue(weak.cast > plain.cast && plain.cast > resist.cast);
        Assert.IsTrue(weak.tick > plain.tick && plain.tick > resist.tick);
        Assert.AreEqual(plain.tick * 1.5, weak.tick, plain.tick * 0.001);
        Assert.AreEqual(plain.tick * 0.5, resist.tick, plain.tick * 0.001);
        Assert.AreEqual(3, plain.ticks);
        Assert.AreEqual(1, immune.cast); // x0, the chain keeps a floor of 1
        Assert.AreEqual(0, immune.ticks);
    }

    // Poison Breath is an area skill with the full lt/rb box on the CASTER (no ball, no range, no target): a mob in front and a
    // mob behind are both hit when their body overlaps the box, and a mob just outside the box is not. It judges at the body
    // action's attack frame, not at the cast. The file's rectBasedOnTarget is set but a skill with no ball ignores it.
    [TestMethod]
    public void PoisonBreath_BoxOnTheCaster_HitsFrontAndBehind_AtTheAttackFrame()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 500));
        var world = World(assets, (-150, 100), (150, 100), (250, 100)); // behind, front, and one outside the box
        Assert.AreEqual(0, world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180).Count); // not at the cast
        Assert.AreEqual(0, world.TakeLaunches().Count); // no ball
        Assert.AreEqual(0, Ticks(world, 500).Count); // the attack frame is 500 ms: not yet judged on the tick that reaches it
        var hits = Ticks(world, 100);
        Assert.AreEqual(12, hits.Count); // the behind mob and the front mob: 6 segments each
        CollectionAssert.AreEqual(new[] { -150, 150 }, UidsHit(world, hits)); // the mob 250 px ahead is outside the box
    }

    // A kill clears the mob's DoT: the tick that kills settles (HP 0), the mob is gone and no more ticks come, and the mob that grows back in its place
    // has no DoT. A cast that kills outright puts no DoT on.
    [TestMethod]
    public void Dot_MobDeath_ClearsTheStatus()
    {
        long castTotal;
        using (var probe = new TempAssets())
        {
            probe.AddWzSkill(BreathData());
            castTotal = OneMob(probe, 1_000_000_000).SkillStrike(7, 0, 100, true, new Random(7), Mage, Breath, 180).Sum(h => h.Damage);
        }
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, castTotal + 1); // the cast leaves 1 HP
        var cast = world.SkillStrike(7, 0, 100, true, new Random(7), Mage, Breath, 180);
        Assert.AreEqual(1, cast[^1].HpLeft);
        var kill = Ticks(world, 1000).Single();
        Assert.IsTrue(kill.Dot && kill.Settle && kill.HpLeft == 0);
        Assert.AreEqual(0, world.MobsOn(7).Count);
        Assert.AreEqual(0, Ticks(world, 5000).Count); // nothing ticks on a dead mob
        world.Tick(10_000, new()); // the grave re-plants the mob
        Assert.AreEqual(1, world.MobsOn(7).Count);
        Assert.AreEqual(0, Ticks(world, 9000).Count); // the new mob has no DoT
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(BreathData());
        var world2 = OneMob(assets2, castTotal); // the cast kills outright
        var dead = world2.SkillStrike(7, 0, 100, true, new Random(7), Mage, Breath, 180);
        Assert.AreEqual(0, dead[^1].HpLeft);
        Assert.AreEqual(0, Ticks(world2, 5000).Count);
    }

    // Over the wire: the server ticks the DoT and sends each tick as a MobHit flagged Dot to every client on the map (the caster too): the same
    // damage, the skill's id, settled, no crit. The first hits are the six segments (not flagged).
    [TestMethod]
    public void Loopback_PoisonBreath_BothClientsSeeEveryTick()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 300));
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 77);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 77);
        b.Connect("127.0.0.1", Net.Port + 77);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        WaitSpawn(b);
        a.SendBuild(Mage);
        a.SendSkillCast(Breath, 180);
        var hits = Enumerable.Range(0, 6 + 2).Select(_ => WaitHit(a)).ToList();
        Assert.IsTrue(hits.Take(6).All(h => !h.Dot && h.Skill == Breath));
        Assert.IsTrue(hits.Skip(6).All(h => h.Dot && h.Settle && !h.Crit && h.Skill == Breath && h.Damage > 0)); // two ticks, a second apart
        var seen = Enumerable.Range(0, 6 + 2).Select(_ => WaitHit(b)).ToList();
        CollectionAssert.AreEqual(hits.Select(h => (h.Damage, h.Dot)).ToList(), seen.Select(h => (h.Damage, h.Dot)).ToList());
    }

    // Mastery 100: the weakest and the strongest roll are the same (to 2 points), so a mob's HP can be set to die on a chosen tick.
    private static readonly Build Steady = Mage with { SkillMasteryPct = 100 };

    private static long Poison(Build build, int pct, double roll) => Damage.SkillCalc(roll, 1.0, 0.1, build, pct, 200, "", AttackElement.Poison);

    // The same skill cast more often than the interval (every 0.5 s, the interval is 1 s) only refreshes the time and the numbers: the beat of the next
    // tick stays, so a tick still lands every second. (A refresh that restarted the beat would never tick.)
    [TestMethod]
    public void Dot_RefreshedFasterThanTheInterval_StillTicksEverySecond()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180);
        for (int sec = 1; sec <= 10; sec++)
        {
            Assert.AreEqual(0, Ticks(world, 500).Count, $"half of second {sec}");
            world.SkillStrike(7, 0, 100, true, new Random(sec), Mage, Breath, 180); // refreshed in the middle of the second
            Assert.AreEqual(1, Ticks(world, 500).Count, $"end of second {sec}");
            world.SkillStrike(7, 0, 100, true, new Random(sec), Mage, Breath, 180); // and again at the beat
        }
    }

    // Two mobs with a DoT: the first dies of its tick (it leaves _lives in the middle of the loop), the second still gets its tick in the
    // same step and every second after.
    [TestMethod]
    public void Dot_FirstMobDiesOfItsTick_SecondMobStillTicks()
    {
        long castOnA;
        using (var probe = new TempAssets())
        {
            probe.AddWzSkill(BreathData());
            probe.AddMap(7, """[{"MobId":100001,"X":100,"Cy":100}]""", Floor);
            probe.AddMob(100001, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
            var one = new MobWorld(probe.Dir);
            one.EnterMap(7);
            castOnA = one.SkillStrike(7, 0, 100, true, new Random(7), Mage, Breath, 180).Sum(h => h.Damage);
        }
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        assets.AddMap(7, """[{"MobId":100001,"X":100,"Cy":100},{"MobId":100000,"X":150,"Cy":100}]""", Floor);
        assets.AddMob(100001, castOnA + 1, 0.1, 0, speed: 1, level: 200); // the nearer mob takes the first rolls: the cast leaves it 1 HP
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        int a = world.MobsOn(7).Single(m => m.Mob == 100001).Uid, b = world.MobsOn(7).Single(m => m.Mob == 100000).Uid;
        world.SkillStrike(7, 0, 100, true, new Random(7), Mage, Breath, 180);
        var first = Ticks(world, 1000);
        Assert.AreEqual(2, first.Count);
        Assert.IsTrue(first.Single(h => h.Uid == a).HpLeft == 0 && first.Single(h => h.Uid == b).HpLeft > 0);
        Assert.IsTrue(first.All(h => h.Dot && h.Settle));
        for (int sec = 2; sec <= 10; sec++)
            CollectionAssert.AreEqual(new[] { b }, Ticks(world, 1000).Select(h => h.Uid).ToArray(), $"second {sec}");
        Assert.AreEqual(0, Ticks(world, 1000).Count);
    }

    // Two sources put the same skill's DoT on one mob: each ticks on its own and each runs out on its own clock (the first
    // source's ticks stop at its dotTime, the second's continue to its own), and neither refresh replaces the other.
    [TestMethod]
    public void Dot_TwoSourcesSameSkill_TickIndependently_AndExpireIndependently()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180, attacker: 5);
        var firstFive = world.Tick(5000, new()).Strikes; // source 5 ticks at 1..5 s
        Assert.AreEqual(5, firstFive.Count);
        Assert.IsTrue(firstFive.All(s => s.attacker == 5 && s.hit.Dot));
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Breath, 180, attacker: 6); // a second source joins at 5 s
        var both = world.Tick(5000, new()).Strikes; // 5..10 s: source 5's last 5 ticks and source 6's first 5
        Assert.AreEqual(10, both.Count);
        Assert.AreEqual(5, both.Count(s => s.attacker == 5));
        Assert.AreEqual(5, both.Count(s => s.attacker == 6));
        var onlySix = world.Tick(5000, new()).Strikes; // 10..15 s: source 5 is gone, source 6's last 5
        Assert.AreEqual(5, onlySix.Count);
        Assert.IsTrue(onlySix.All(s => s.attacker == 6));
        Assert.AreEqual(0, world.Tick(5000, new()).Strikes.Count);
    }

    // Two sources put a DoT on one mob, and the last hit is source B's DoT tick: the kill (HP 0) and the mob's EXP are
    // credited to B, not to A. A's skill runs out after its first tick, so B alone ticks the mob to death.
    [TestMethod]
    public void Dot_TwoSources_LastHitKills_CreditGoesToTheDotSource()
    {
        long seg = Poison(Steady, 180, 1.0), tick = Poison(Steady, 60, 1.0);
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(2111003, dotTime: 1)); // A: one tick, then it runs out
        assets.AddWzSkill(BreathData());                     // B: the full 10 s
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 12 * seg + 3 * tick, 0.1, exp: 50, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.SkillStrike(7, 0, 100, true, new Random(1), Steady, 2111003, 180, attacker: 5); // both casts leave 3 ticks of HP
        world.SkillStrike(7, 0, 100, true, new Random(2), Steady, Breath, 180, attacker: 6);
        var firstSecond = world.Tick(1000, new()).Strikes; // 1 s: A and B both tick, the mob lives
        Assert.AreEqual(2, firstSecond.Count);
        Assert.IsTrue(firstSecond.All(s => s.hit.HpLeft > 0));
        var kill = world.Tick(1000, new()).Strikes; // 2 s: A is gone, B's tick kills
        Assert.AreEqual(1, kill.Count);
        Assert.AreEqual(6, kill[0].attacker); // the kill is B's
        Assert.AreEqual(0, kill[0].hit.HpLeft);
        Assert.AreEqual(50, kill[0].hit.Exp); // the mob's EXP goes with that kill, credited to B by the server
        Assert.AreEqual(0, world.MobsOn(7).Count);
    }

    // A source leaves the map: its DoTs go, another source's DoTs stay. (The server calls CancelDoTs on a map switch or a
    // disconnect; here the world is stepped directly.)
    [TestMethod]
    public void CancelDoTs_OneSourceLeaves_OnlyItsDotsStop()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180, attacker: 5);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Breath, 180, attacker: 6);
        world.CancelDoTs(5); // source 5 leaves
        var ticks = world.Tick(3000, new()).Strikes;
        Assert.AreEqual(3, ticks.Count);
        Assert.IsTrue(ticks.All(s => s.attacker == 6 && s.hit.Dot));
    }

    // The numbers of the formula, by hand (character-stats.md §3.1): a Lv.200 mage INT 1000 / LUK 100 / magic attack 200 on a
    // short staff (1.20, mastery 20%), a Lv.200 mob with 10% DEF, no element:
    //   stat value = 4 x 1000 + 100 = 4100
    //   max actual = 1.20 x 4100 x 200 / 100 = 9840, min actual = 20% of that = 1968 -> min panel = floor(1 + 1968) = 1969
    //   one hit = (min panel + (max panel - min panel) x roll) x skill% / 100 x (1 - DEF) x level(1.10), floored
    // The WZ dot (60) is the skill% of one tick, in the same place as the 180% of a segment (not a final multiplier).
    [TestMethod]
    public void PoisonBreath_DotValue_IsTheSkillPctOfTheFormula()
    {
        const long minPanel = 1969, maxPanel = 9840;
        static long Hand(double roll, int pct) =>
            Math.Max(1L, (long)Math.Floor((minPanel + (maxPanel - minPanel) * roll) * pct / 100.0 * 0.9 * 1.10));
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData());
        var world = OneMob(assets, 1_000_000_000);
        var cast = world.SkillStrike(7, 0, 100, true, new Random(4), Mage, Breath, 180); // Mage crit 0: no crit roll changes the number
        Assert.AreEqual(6, cast.Count);
        var rng = new Random(4);
        for (int i = 0; i < 6; i++)
        {
            double roll = rng.NextDouble();
            rng.NextDouble(); // the crit roll (no crit)
            Assert.AreEqual(Hand(roll, 180), cast[i].Damage, $"segment {i}");
        }
        var tick = Ticks(world, 1000).Single();
        Assert.AreEqual(Hand(rng.NextDouble(), 60), tick.Damage);
    }

    // The caster leaves the game while its DoT runs: his DoTs vanish at once (no more ticks), and the server keeps running for
    // the others.
    [TestMethod]
    public void Loopback_CasterLeaves_HisDoTVanishes()
    {
        long cast = 6 * Poison(Steady, 180, 1.0), tick = Poison(Steady, 60, 1.0);
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 300));
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, cast + 100 * tick, 0.1, 0, speed: 1, level: 200); // the mob lives long after the caster is gone
        var log = new List<string>();
        using var server = new GameServer(m => { lock (log) log.Add(m); }, assets.Dir, Net.Port + 78);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 78);
        b.Connect("127.0.0.1", Net.Port + 78);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        WaitSpawn(b);
        a.SendBuild(Steady);
        a.SendSkillCast(Breath, 180);
        var seen = Enumerable.Range(0, 6 + 1).Select(_ => WaitHit(b)).ToList(); // the 6 segments and the first tick
        Assert.IsTrue(seen[^1].Dot && seen[^1].HpLeft > 0);
        a.Dispose(); // the caster is gone
        Assert.IsTrue(WaitGone(b, 1), "the server never reported the caster gone");
        while (b.TryTakeMob(out _)) { } // drop any tick that raced in before the server removed the DoT
        Thread.Sleep(1300); // more than one 1 s interval: a surviving DoT would tick here
        Assert.IsFalse(b.TryTakeMob(out var more) && more.Dot && more.HpLeft > 0);
        lock (log) Assert.IsFalse(log.Any(m => m.Contains("error", StringComparison.OrdinalIgnoreCase)), string.Join("; ", log));
    }

    // The caster switches to another map: the DoTs it left on the old map vanish (the other player still on that map sees no
    // more ticks).
    [TestMethod]
    public void Loopback_CasterSwitchesMap_HisDoTVanishes()
    {
        long cast = 6 * Poison(Steady, 180, 1.0), tick = Poison(Steady, 60, 1.0);
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 300));
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMap(10, "[]", Floor);
        assets.AddMob(100000, cast + 100 * tick, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 79);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 79);
        b.Connect("127.0.0.1", Net.Port + 79);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        WaitSpawn(b);
        a.SendBuild(Steady);
        a.SendSkillCast(Breath, 180);
        var seen = Enumerable.Range(0, 6 + 1).Select(_ => WaitHit(b)).ToList();
        Assert.IsTrue(seen[^1].Dot && seen[^1].HpLeft > 0);
        a.SendState(10, 0, 100, Net.ActStand, 0, true); // the caster changes map
        Assert.IsTrue(WaitGone(b, 1), "the server never reported the map switch");
        while (b.TryTakeMob(out _)) { } // drop any tick that raced in before the DoT was removed
        Thread.Sleep(1300);
        Assert.IsFalse(b.TryTakeMob(out var more) && more.Dot && more.HpLeft > 0);
    }

}
