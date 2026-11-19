using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The damage-over-time effect (MobStatuses.Dot, MobWorld.DotTick) and Poison Breath (210.img, the first skill that puts one on a mob).
// Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Breath = WzSkillData.PoisonBreath;

    // Poison Breath as 210.img writes it at level 10: poison, 6 segments, 6 mobs, the box lt(-200,-200) rb(200,120) centred on the target
    // (rectBasedOnTarget, no range), no ball, a body action with its attack frame at attackDelayMs; a DoT of 60% every 1 s for 10 s.
    // targetBox false hangs the same box on the caster, so a test can look at the DoT alone.
    private static WzSkillData BreathData(int id = Breath, bool targetBox = false, int attackDelayMs = 0)
    {
        var data = new WzSkillData { SkillId = id, MaxLevel = 10, Kind = SkillData.KindActive, ElemAttr = "s", AttackDelayMs = attackDelayMs };
        data.Common["mpCon"] = SkillFormula.Levels("11+3*d(x/4)", 10);
        data.Common["damage"] = SkillFormula.Levels("130+5*x", 10);
        data.Common["attackCount"] = SkillFormula.Levels("6", 10);
        data.Common["mobCount"] = SkillFormula.Levels("6", 10);
        data.Common["dot"] = SkillFormula.Levels("30+3*x", 10);
        data.Common["dotInterval"] = SkillFormula.Levels("1", 10);
        data.Common["dotTime"] = SkillFormula.Levels("10", 10);
        data.Vectors["lt"] = new[] { -200, -200 };
        data.Vectors["rb"] = new[] { 200, 120 };
        if (targetBox) data.Info["rectBasedOnTarget"] = "1";
        return data;
    }

    // A DoT skill with its own id, to give a mob one more layer (Elemental Drain counts the skills).
    private static WzSkillData DotSkillData(int id) => BreathData(id);

    private static Dot DotOf(int skill, int timeMs, int intervalMs = 1000) => new() { Skill = skill, IntervalMs = intervalMs, MsLeft = timeMs };

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
        Assert.AreEqual(0, list.DotCount);
        list.AddDot(DotOf(Breath, 5000));
        list.AddDot(DotOf(Breath, 5000)); // the same skill: refresh, no second layer
        Assert.AreEqual(1, list.DotCount);
        list.AddDot(DotOf(2111003, 2000)); // another skill: another layer
        Assert.AreEqual(2, list.DotCount);
        list.Step(2000); // the second one runs out at 0
        Assert.AreEqual(1, list.DotCount);
        list.AddDot(DotOf(Breath, 5000)); // a refresh restarts the time
        list.Step(4999);
        Assert.AreEqual(1, list.DotCount);
        list.Step(1);
        Assert.AreEqual(0, list.DotCount);
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
        Assert.AreEqual(0, list.DotCount);
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

    // Poison Breath picks the nearest mob ahead within its reach (the box half-width, 200: WZ has no range) and judges with the box centred on it: a mob
    // 200 px ahead is the target, and another mob 150 px beyond it is inside the box too (splash), one 210 px beyond is not. It judges at the body
    // action's attack frame, not at the cast. A mob 201 px ahead is no target (no hit at all).
    [TestMethod]
    public void PoisonBreath_TargetsTheNearestAhead_BoxCentredOnIt_JudgesAtTheAttackFrame()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 500));
        var world = World(assets, (-100, 100), (200, 100), (350, 100), (410, 100));
        Assert.AreEqual(0, world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180).Count); // not at the cast
        Assert.AreEqual(0, world.TakeLaunches().Count); // no ball
        Assert.AreEqual(0, Ticks(world, 500).Count); // the attack frame is 500 ms: not yet judged on the tick that reaches it
        var hits = Ticks(world, 100);
        Assert.AreEqual(12, hits.Count); // 2 mobs x 6 segments
        CollectionAssert.AreEqual(new[] { 200, 350 }, UidsHit(world, hits));
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 500));
        var world2 = World(assets2, (201, 100));
        world2.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 180);
        Assert.AreEqual(0, Ticks(world2, 2000).Count);
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

    // The caster leaves the game while its DoT runs: the server loop does not throw (the other client keeps getting the ticks), the DoT runs to its end, and
    // the mob that dies of it dies for everyone (HP 0, settled), with no EXP for anyone and no server error.
    [TestMethod]
    public void Loopback_CasterLeaves_DotKillsTheMob_ServerKeepsTicking()
    {
        long cast = 6 * Poison(Steady, 180, 1.0), tick = Poison(Steady, 60, 1.0);
        using var assets = new TempAssets();
        assets.AddWzSkill(BreathData(targetBox: true, attackDelayMs: 300));
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, cast + 2 * tick + 100, 0.1, 0, speed: 1, level: 200); // alive after the cast and two ticks; the third tick kills
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
        var tick2 = WaitHit(b);
        var tick3 = WaitHit(b);
        Assert.IsTrue(tick2.Dot && tick2.HpLeft > 0);
        Assert.IsTrue(tick3.Dot && tick3.Settle && tick3.HpLeft == 0); // the poison killed it
        Thread.Sleep(300);
        Assert.IsFalse(b.TryTakeMob(out var more) && more.Dot && more.HpLeft > 0); // nothing ticks on the dead mob
        lock (log) Assert.IsFalse(log.Any(m => m.Contains("error", StringComparison.OrdinalIgnoreCase)), string.Join("; ", log));
    }
}
