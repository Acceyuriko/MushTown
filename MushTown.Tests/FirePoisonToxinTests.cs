using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Ticket T-20261119195812: the generic summon (SummonUnit, MobWorld summons) and Creeping Toxin (211.img 2111013), its first user: a totem on the floor and a pool that spreads
// to both sides (a spreading GroundZone, cut at the platform edge) that poisons the mobs in it. The pool is cut into pools (ToxinPoolLayout) that a fire skill whose box touches
// them blows up once they are 1.5 s old (the child skill 2111014); a blast sets off the pools next to it. Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Toxin = WzSkillData.CreepingToxin;
    private const int ToxinBlast = WzSkillData.CreepingToxinBlast;

    // Creeping Toxin as 211.img writes it at level 14 (the max): poison, 60 s, the pool that spreads 200 px (u) to each side every 900 ms (subTime), 200% a hit, a DoT of 204% every 1 s for 10 s, 4 hits, 10 mobs,
    // +50% against normal mobs (nbdR), a pool is ripe after 1500 ms (w2) and grows back 1200 ms after it blew up (y 120 x 10 ms), blasts within 0.4 s (t) of each other cut the final damage by 60% (u2). Its child, the blast: 200% 4 hits
    // 10 mobs on the box lt(-190,-170) rb(190,170), 60 MP, and sets off the next pools 600 ms later (y 60 x 10 ms).
    private static WzSkillData ToxinData(bool normal = true)
    {
        var data = new WzSkillData { SkillId = Toxin, MaxLevel = 14, Kind = SkillData.KindActive, ElemAttr = "s" };
        data.Common["mpCon"] = SkillFormula.Levels("40+x", 14);
        data.Common["time"] = SkillFormula.Levels("60", 14);
        data.Common["subTime"] = SkillFormula.Levels("900", 14);
        data.Common["w2"] = SkillFormula.Levels("1500", 14);
        data.Common["y"] = SkillFormula.Levels("120", 14);
        data.Common["u"] = SkillFormula.Levels("200", 14);
        data.Common["u2"] = SkillFormula.Levels("60", 14);
        data.Common["t"] = SkillFormula.Levels("0.4", 14);
        if (normal) data.Common["nbdR"] = SkillFormula.Levels("50", 14);
        data.Common["damage"] = SkillFormula.Levels("158+3*x", 14);
        data.Common["attackCount"] = SkillFormula.Levels("4", 14);
        data.Common["mobCount"] = SkillFormula.Levels("10", 14);
        data.Common["dot"] = SkillFormula.Levels("120+6*x", 14);
        data.Common["dotInterval"] = SkillFormula.Levels("1", 14);
        data.Common["dotTime"] = SkillFormula.Levels("3+d(x/2)", 14);
        data.Vectors["lt"] = new[] { -100, -85 };
        data.Vectors["rb"] = new[] { 100, 60 };
        var blast = new WzSkillData { SkillId = ToxinBlast, MaxLevel = 14, Kind = SkillData.KindActive };
        blast.Common["damage"] = SkillFormula.Levels("158+3*x", 14);
        blast.Common["attackCount"] = SkillFormula.Levels("4", 14);
        blast.Common["mobCount"] = SkillFormula.Levels("10", 14);
        blast.Common["mpCon"] = SkillFormula.Levels("46+x", 14);
        blast.Common["y"] = SkillFormula.Levels("60", 14);
        blast.Vectors["lt"] = new[] { -190, -170 };
        blast.Vectors["rb"] = new[] { 190, 170 };
        data.Children.Add(blast);
        return data;
    }

    // A fire spell without a body action: it judges in the cast (the box lt(-120,-75) rb(120,75) round the caster).
    private static WzSkillData FireData() => BoltData(false, "f", attackDelayMs: 0);

    // A random that always rolls the middle: the damage of a hit is the same every time, so two hits can be compared.
    private sealed class MiddleRandom : Random
    {
        public override double NextDouble() => 0.5;
    }

    private static MobWorld ToxinWorld(TempAssets assets, string floor, params (int x, int y)[] mobs)
    {
        assets.AddMap(7, "[" + string.Join(",", mobs.Select(m => $$"""{"MobId":100000,"X":{{m.x}},"Cy":{{m.y}}}""")) + "]", floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        return world;
    }

    // Casts Creeping Toxin so that its totem stands at x (the caster faces right and stands ToxinFrontPx behind it; CritPct 0 and the middle roll: the damage is the same every time) and runs
    // the world ms; the summon and zone events of the cast are dropped. The pool has nine pools (the totem is the middle one, index 4: -832, -632, ..., 832, 200 px apart, the first and the last
    // ones 132 + 4 x 200 px from the totem), the middle one is up 1100 ms after the totem, the next ones every 900 ms (ripe 1500 ms later: all of them at 6200 ms, and a little more for the wait).
    private static (MobWorld world, List<MobWorld.Hit> hits) ToxinRun(TempAssets assets, string floor, int x, int ms, params (int x, int y)[] mobs)
    {
        var world = ToxinWorld(assets, floor, mobs);
        world.SkillStrike(7, x - WzSkillData.ToxinFrontPx, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Toxin, 200);
        world.TakeSummons();
        world.TakeZones();
        return (world, ZoneRun(world, ms));
    }

    // SummonUnit alone knows nothing of any skill: it waits, then stands for its life, and is over at the end.
    [TestMethod]
    public void SummonUnit_WaitsThenStands_OverAtTheEnd()
    {
        var unit = new SummonUnit { LifeMs = 3000, WaitMs = 500 };
        unit.Step(499);
        Assert.AreEqual((1, 0, 3000), (unit.WaitMs, unit.AgeMs, unit.MsLeft));
        unit.Step(1001); // 1 ms of wait, then 1000 ms up
        Assert.AreEqual((0, 1000, 2000), (unit.WaitMs, unit.AgeMs, unit.MsLeft));
        Assert.IsFalse(unit.Over);
        unit.Step(5000);
        Assert.IsTrue(unit.Over);
        Assert.AreEqual(0, unit.MsLeft);
    }

    // The skill is a summon (the Summons set), not a ground zone: the numbers of the WZ at level 14, and the cooldown the user asked for (WZ has none).
    [TestMethod]
    public void CreepingToxin_IsASummon_WzNumbers_HasItsOwnCooldown()
    {
        var data = ToxinData();
        Assert.IsTrue(data.IsSummon);
        Assert.IsFalse(data.IsGroundZone);
        Assert.AreEqual((60.0, 900.0, 1500.0, 200.0, 204.0, 10.0), (data.Value("time", 14), data.Value("subTime", 14), data.Value("w2", 14), data.Value("damage", 14), data.Value("dot", 14), data.Value("dotTime", 14)));
        Assert.AreEqual((60.0, 0.4, 50.0), (data.Value("u2", 14), data.Value("t", 14), data.Value("nbdR", 14)));
        Assert.AreEqual((200.0, 60.0), (data.Children.Single().Value("damage", 14), data.Children.Single().Value("mpCon", 14)));
        Assert.AreEqual(30000f, SkillCost.CooldownMs(data, 14));
    }

    // The pool is cut into pools: the widest puddle that fits in the middle (264 px), pieces of 200 px end to end to each side and a piece of 88 px in the last gap; a platform that is
    // too small for the middle one gets a smaller one; the pieces never go over the edges.
    [TestMethod]
    public void ToxinPoolLayout_MiddleThenPiecesToBothSides_NeverOverTheEdge()
    {
        CollectionAssert.AreEqual(new[] { (-376f, 1), (-232f, 2), (0f, 3), (232f, 2), (376f, 1) }, ToxinPoolLayout.Pieces(0, -480, 480));
        var lopsided = ToxinPoolLayout.Pieces(0, -60, 480); // 60 px of floor on the left: the widest puddle that fits 120 px is the 88 px one
        Assert.AreEqual((0f, 1), lopsided[0]);
        foreach (var (center, kind) in lopsided)
            Assert.IsTrue(center - WzSkillData.ToxinPoolWidths[kind] / 2f >= -60 - 44 && center + WzSkillData.ToxinPoolWidths[kind] / 2f <= 480, $"piece at {center}");
        Assert.AreEqual(1, ToxinPoolLayout.Pieces(0, -20, 30).Count); // a very small platform: one small pool
    }

    // The cast makes no hit and no ball. It makes one summon event (ToxinFrontPx in front of the caster, on the floor, 60 s, up after one tick) and one zone event for its pool: the box
    // Reach to each side of the totem that spreads from the totem in steps (OriginX, StepMs 900), the same life and wait. A client that enters later gets what is left of both.
    [TestMethod]
    public void CreepingToxin_CastMakesATotemInFrontOnTheFloor_AndASpreadingPool_EntryListsTheRest()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData();
        assets.AddWzSkill(toxin);
        var world = ToxinWorld(assets, Floor, (60, 100));
        Assert.AreEqual(0, world.SkillStrike(7, -60, 20, true, new Random(1), Mage, Toxin, 200).Count); // 80 px above the floor, the totem 100 px in front
        Assert.AreEqual(0, world.TakeLaunches().Count);
        var summon = world.TakeSummons().Single();
        Assert.AreEqual((Toxin, 7, 40f, 100f, 60000, 100, false), (summon.Skill, summon.Map, summon.X, summon.Y, summon.LifeMs, summon.DelayMs, summon.Gone));
        int reach = ToxinPoolLayout.Reach(toxin);
        Assert.AreEqual(932, reach); // half of the 264 px middle puddle and 4 steps of u (200)
        var zone = world.TakeZones().Single();
        Assert.AreEqual((Toxin, 7, 40f - reach, 40f + reach, 100f, 60000, 100), (zone.Skill, zone.Map, zone.Left, zone.Right, zone.Y, zone.LifeMs, zone.DelayMs));
        Assert.AreEqual((40f, 900, 0), (zone.OriginX, zone.StepMs, zone.AgeMs));
        Assert.AreEqual(0, world.TakeSummons().Count);
        world.Tick(1000, new());
        var rest = world.SummonsOn(7).Single();
        Assert.AreEqual((summon.Uid, 0, 59100), (rest.Uid, rest.DelayMs, rest.LifeMs)); // 100 ms of wait, 900 ms of life gone
        var poolRest = world.ZonesOn(7).Single();
        Assert.AreEqual((0, 59100, 900), (poolRest.DelayMs, poolRest.LifeMs, poolRest.AgeMs));
        Assert.AreEqual(0, world.SummonsOn(8).Count); // another map
    }

    // The totem stands ToxinFrontPx in front of the caster, to the side he faces, and needs floor there: a caster at the end of the floor who faces the empty side makes nothing (and the
    // client pays no MP for it); the other way he does.
    [TestMethod]
    public void CreepingToxin_TotemStandsInFrontOfTheCaster_NoFloorThereNoTotem()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        var world = ToxinWorld(assets, Floor);
        world.SkillStrike(7, 950, 100, true, new Random(1), Mage, Toxin, 200); // the totem would be at 1050: the floor ends at 1000
        Assert.AreEqual(0, world.TakeSummons().Count);
        Assert.AreEqual(0, world.TakeZones().Count);
        world.SkillStrike(7, 950, 100, false, new Random(1), Mage, Toxin, 200);
        Assert.AreEqual(850f, world.TakeSummons().Single().X);
        Assert.AreEqual(1050f, ToxinPoolLayout.TotemX(950, true));
        Assert.AreEqual(850f, ToxinPoolLayout.TotemX(950, false));
    }

    // The pool comes up in steps: nothing before ToxinFirstPoolMs (1100 ms) after the totem, then 200 px (u) to each side, and 200 px further every subTime (900 ms) until the full reach.
    // The mob 60 px from the totem is poisoned after the first step, the one 150 px away too (the edge is at 200 px), the one 700 px away only when the edge has got past it (the 4th
    // step: 800 px), not at the 3rd (600 px). The cast makes no direct hit (the user, 2026-11-21): the mob at 60 inside the WZ cast box (lt/rb: 100 px) takes none either, only the poison.
    [TestMethod]
    public void CreepingToxin_PoolSpreadsInSteps_FarMobIsPoisonedLater_NoDirectHitAtTheCast()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        var world = ToxinWorld(assets, Floor, (60, 100), (150, 100), (700, 100));
        Assert.AreEqual(0, world.SkillStrike(7, -100, 100, true, new Random(1), Mage, Toxin, 200).Count, "the cast hits no one"); // the totem at 0
        var hits = ZoneRun(world, 1100); // 1000 ms after the totem is up
        Assert.AreEqual(0, hits.Count, "no direct hit when the totem comes up, also on the mob in the cast box");
        Assert.AreEqual(0, world.DotsOn(7).Count, "the first pool is not up yet");
        ZoneRun(world, 200); // 1200 ms: the first step
        Assert.AreEqual(2, world.DotsOn(7).Count(d => d.skill == Toxin));
        ZoneRun(world, 2400); // 3600 ms: the 3rd step (edge at 600 px)
        Assert.AreEqual(2, world.DotsOn(7).Count(d => d.skill == Toxin));
        ZoneRun(world, 400); // 4000 ms: the 4th step (800 px)
        Assert.AreEqual(3, world.DotsOn(7).Count(d => d.skill == Toxin));
        Assert.AreEqual(0, hits.Count(h => h.Dot));
    }

    // The widening of the box in steps: a line at the origin before the first step, then stepPx further on both sides at the first age and after every stepMs, at most to the full edge.
    [TestMethod]
    public void GroundZone_Spread_InSteps_FromTheFirstAge_UpToTheFullEdge()
    {
        Assert.AreEqual(0f, GroundZone.Spread(0, 932, 1099, 1100, 900, 200));
        Assert.AreEqual(200f, GroundZone.Spread(0, 932, 1100, 1100, 900, 200));
        Assert.AreEqual(200f, GroundZone.Spread(0, 932, 1999, 1100, 900, 200));
        Assert.AreEqual(400f, GroundZone.Spread(0, 932, 2000, 1100, 900, 200));
        Assert.AreEqual(-800f, GroundZone.Spread(0, -932, 3800, 1100, 900, 200));
        Assert.AreEqual(932f, GroundZone.Spread(0, 932, 4700, 1100, 900, 200), "the 5th step goes over the edge: the edge is the end");
        Assert.AreEqual(40f + 100f, GroundZone.Spread(40, 140, 60000, 1100, 900, 200), "a short side is reached in the first step");
        Assert.AreEqual(932f, GroundZone.Spread(0, 932, 0, 0, 0, 200), "no step time: the zone does not spread");
    }

    // The pool stops at the platform edge: the floor here is x -60..1000, so the left edge of the pool is cut at -60 and the right one is whole (932 px from the totem at 0).
    [TestMethod]
    public void CreepingToxin_PoolStopsAtThePlatformEdge()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        var world = ToxinWorld(assets, """[{"X1":-60,"Y1":100,"X2":1000,"Y2":100,"Layer":0}]""", (50, 100));
        world.SkillStrike(7, -100, 100, true, new Random(1), Mage, Toxin, 200); // the totem at 0
        var zone = world.TakeZones().Single();
        Assert.AreEqual((-60f, 932f), (zone.Left, zone.Right));
        using var tiny = new TempAssets();
        tiny.AddWzSkill(ToxinData());
        var small = ToxinWorld(tiny, """[{"X1":-20,"Y1":100,"X2":30,"Y2":100,"Layer":0}]""");
        small.SkillStrike(7, -100, 100, true, new Random(1), Mage, Toxin, 200);
        var e = small.TakeZones().Single();
        Assert.AreEqual((-20f, 30f), (e.Left, e.Right)); // a small platform: the pool covers it and no more
    }

    // The pool poisons the mob in it with periodic hits (a number every second), and the totem and the pool go when the 60 s are over: a gone event for the totem, nothing left.
    [TestMethod]
    public void CreepingToxin_PoisonsMobsInThePool_ThenTotemAndPoolVanishAfterTheTime()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        var world = ToxinWorld(assets, Floor, (50, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Toxin, 200);
        world.TakeSummons();
        world.TakeZones();
        var hits = ZoneRun(world, 20000);
        int ticks = hits.Count(h => h.Dot && h.Skill == Toxin);
        Assert.IsTrue(ticks >= 17, $"one hit a second for about 19 s ({ticks})");
        Assert.AreEqual(0, hits.Count(h => !h.Dot), "no direct hit at any time");
        Assert.AreEqual(1, world.SummonsOn(7).Count);
        Assert.AreEqual(1, world.ZonesOn(7).Count);
        ZoneRun(world, 39000); // 59 s: still standing
        Assert.AreEqual(1, world.SummonsOn(7).Count);
        ZoneRun(world, 2000);
        Assert.AreEqual(0, world.SummonsOn(7).Count);
        Assert.AreEqual(0, world.ZonesOn(7).Count);
        var gone = world.TakeSummons().Single();
        Assert.IsTrue(gone.Gone);
        Assert.AreEqual(0, world.TakeZones().Count, "a zone that ends by its time sends nothing: the clients end their copy by the time they were given");
        Assert.AreEqual(0, world.TakePoolBlasts().Count, "the totem never blows up");
    }

    // A mob that is immune to poison is not poisoned by the pool (the element of the skill is poison, WZ elemAttr).
    [TestMethod]
    public void CreepingToxin_PoisonImmuneMobIsNotPoisoned()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddMap(7, """[{"MobId":100002,"X":50,"Cy":100}]""", Floor);
        assets.AddMob(100002, 1_000_000_000, 0.1, 0, speed: 1, level: 200, elemAttr: "S1");
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Toxin, 200);
        ZoneRun(world, 3000);
        Assert.AreEqual(0, world.DotsOn(7).Count);
    }

    // The summons of a caster go with the caster (CancelSummons): the totem and its pool end at once with a gone event each, and the summons of another caster stay. A new cast of the
    // same caster takes his old totem away (one totem of a skill for a caster).
    [TestMethod]
    public void CreepingToxin_CancelTakesTotemAndPoolAway_OtherCasterStays_RecastReplaces()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        var world = ToxinWorld(assets, Floor);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Toxin, 200, attacker: 3);
        world.SkillStrike(7, 500, 100, true, new Random(1), Mage, Toxin, 200, attacker: 4);
        world.TakeSummons();
        world.TakeZones();
        world.CancelSummons(3);
        var gone = world.TakeSummons().Single();
        Assert.IsTrue(gone.Gone);
        Assert.AreEqual(600f, world.SummonsOn(7).Single().X); // 100 px in front of the caster at 500
        Assert.IsTrue(world.TakeZones().Single().Gone);
        Assert.AreEqual(1, world.ZonesOn(7).Count);
        world.SkillStrike(7, 200, 100, true, new Random(1), Mage, Toxin, 200, attacker: 4);
        Assert.AreEqual(1, world.SummonsOn(7).Count);
        Assert.AreEqual(300f, world.SummonsOn(7).Single().X);
        Assert.AreEqual(1, world.ZonesOn(7).Count);
        var events = world.TakeSummons();
        Assert.AreEqual((true, false), (events[0].Gone, events[1].Gone)); // the old one goes, the new one comes
    }

    // A pool cannot be blown up before it is 1.5 s old (WZ w2): a fire cast whose box touches the middle pool (it comes up 1100 ms after the totem, so it is ripe at 2600 ms of the zone, 2700 ms
    // of the cast) does nothing at 2300 ms and blows it up at 2800 ms, with no mob anywhere. The pools next to it (up at 2000) are not ripe yet and stay.
    [TestMethod]
    public void CreepingToxin_PoolBlowsUpOnlyWhenOneAndAHalfSecondsOld_AndNoMobIsNeeded()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 2300);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(0, world.TakePoolBlasts().Count, "too young");
        ZoneRun(world, 500);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        var blast = world.TakePoolBlasts().Single(); // the middle pool (the box of the spell reaches 120 px, the pool next to it starts at 132)
        Assert.AreEqual((4, 7, 0, true), (blast.Index, blast.Map, blast.Attacker, blast.Paid));
    }

    // One fire hit blows up every ripe pool its box touches, in the same tick, and pays the MP once (the first blast says paid, the others do not). The box here is the 240 px of Energy Bolt
    // round x 300 (180..420): it touches the pools at 232 (132..332) and 432 (332..532), not the middle one (-132..132).
    [TestMethod]
    public void CreepingToxin_OneHitBlowsUpEveryRipePoolItTouches_AndPaysOnce()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 6500);
        world.SkillStrike(7, 300, 100, true, new Random(2), Mage, Bolt, 78);
        var blasts = world.TakePoolBlasts();
        CollectionAssert.AreEqual(new[] { (5, true), (6, false) }, blasts.Select(b => (b.Index, b.Paid)).ToArray());
    }

    // A pool that is not ripe stays when the box of the same hit blows up a ripe one next to it: at 2800 ms the middle pool is ripe and the ones at 232 and -232 (up at 2000) are not.
    [TestMethod]
    public void CreepingToxin_OneHitLeavesTheYoungPoolsItTouches()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 2800);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78); // a box of 120 px to each side: only the middle pool (the ones next to it start at 132) is touched
        Assert.AreEqual(4, world.TakePoolBlasts().Single().Index);
        world.SkillStrike(7, 150, 100, true, new Random(2), Mage, Bolt, 78); // 30..270: the middle pool is spent now, the one at 232 is not ripe
        Assert.AreEqual(0, world.TakePoolBlasts().Count);
    }

    // The pools next to a blast follow one chain wait later (WZ y of the blast: 600 ms) for free, wave by wave: the hit blows up the pools at 232 and 432; their neighbours that are ripe come
    // 600 ms later (the middle pool and the one at 632), the next ones 600 ms after that, and so on out to both ends. A pool that grew back is not ripe yet, so the chain ends.
    [TestMethod]
    public void CreepingToxin_ChainBlowsUpTheRipeNeighboursForFree_OneChainWaitApart()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData();
        assets.AddWzSkill(toxin);
        assets.AddWzSkill(FireData());
        int wait = (int)toxin.Children.Single().Value("y", 14) * WzSkillData.ToxinWzUnitMs;
        Assert.AreEqual(600, wait);
        var (world, _) = ToxinRun(assets, Floor, 0, 6500);
        world.SkillStrike(7, 300, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(2, world.TakePoolBlasts().Count);
        var waves = new[] { new[] { 4, 7 }, new[] { 3, 8 }, new[] { 2 }, new[] { 1 }, new[] { 0 } };
        foreach (var wave in waves)
        {
            ZoneRun(world, wait - 100);
            Assert.AreEqual(0, world.TakePoolBlasts().Count, "nothing blows up before the chain wait is over");
            ZoneRun(world, 100);
            var blasts = world.TakePoolBlasts();
            CollectionAssert.AreEquivalent(wave, blasts.Select(b => b.Index).ToArray());
            Assert.IsTrue(blasts.All(b => !b.Paid), "a chain blast costs no MP");
        }
        ZoneRun(world, 2000);
        Assert.AreEqual(0, world.TakePoolBlasts().Count, "every pool blew up once, the ones that grew back are not ripe");
    }

    // Two blasts in the same tick cut the final damage of the later one on a mob both hit. The cast blows up the pools at 232 (box 42..422) and 432 (box 242..622) together: the mob at 300 is in
    // both boxes. The pool at 232 blows up first and hits it full; the pool at 432 hits it with WZ u2 (60%) less final damage (the window t is 0.4 s). The mob walks about 1 px a second.
    [TestMethod]
    public void CreepingToxin_BlastsInTheSameMomentCutTheFinalDamageOfTheLaterOnTheSameMob()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData(normal: false);
        assets.AddWzSkill(toxin);
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 6500, (300, 100));
        var hits = world.SkillStrike(7, 300, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).Where(h => h.Skill == Toxin).ToList();
        Assert.AreEqual(8, hits.Count, "two pools, 4 hits each");
        double cut = toxin.Value("u2", 14) / 100;
        for (int i = 0; i < 4; i++)
            Assert.AreEqual(hits[i].Damage * (1 - cut), (double)hits[4 + i].Damage, hits[i].Damage * 0.002 + 3, "the second blast has the final damage cut by the WZ u2");
    }

    // Blasts one chain wait (600 ms, more than the window t of 0.4 s) apart are not cut: the cast blows up the pool at 232 (box 42..422), and the middle pool (box -190..190) follows 600 ms
    // later; the mob at 100 is in both and takes the same damage from both.
    [TestMethod]
    public void CreepingToxin_BlastsOneChainWaitApartAreNotCut()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData(normal: false);
        assets.AddWzSkill(toxin);
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 6500, (100, 100));
        var first = world.SkillStrike(7, 300, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).Where(h => h.Skill == Toxin).ToList();
        Assert.AreEqual(4, first.Count);
        var chain = new List<MobWorld.Hit>();
        for (int i = 0; i < 6; i++) chain.AddRange(world.Tick(100, new()).Strikes.Select(s => s.hit).Where(h => h.Skill == Toxin && !h.Dot));
        Assert.AreEqual(4, chain.Count, "the middle pool hits the mob once more (the pool at 432 is too far)");
        for (int i = 0; i < 4; i++)
            Assert.AreEqual((double)first[i].Damage, chain[i].Damage, first[i].Damage * 0.002 + 3, "600 ms after the first blast: no cut");
    }

    // The cut is between the blasts of one caster: two casters each have a totem, and a fire hit of each blows up the middle pool of his own in the same tick. The mob at 100 is in both blasts and
    // takes the same damage from each (the blast of the second caster is not cut by the first one's).
    [TestMethod]
    public void CreepingToxin_BlastsOfTwoCastersDoNotCutEachOther()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData(normal: false);
        assets.AddWzSkill(toxin);
        assets.AddWzSkill(FireData());
        var world = ToxinWorld(assets, Floor, (100, 100));
        world.SkillStrike(7, -100, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Toxin, 200, attacker: 1);
        world.SkillStrike(7, -100, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Toxin, 200, attacker: 2);
        ZoneRun(world, 6500);
        var one = world.SkillStrike(7, 0, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78, attacker: 1).Where(h => h.Skill == Toxin).ToList();
        var two = world.SkillStrike(7, 0, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78, attacker: 2).Where(h => h.Skill == Toxin).ToList();
        Assert.AreEqual((4, 4), (one.Count, two.Count));
        for (int i = 0; i < 4; i++)
            Assert.AreEqual((double)one[i].Damage, two[i].Damage, one[i].Damage * 0.002 + 3, "no cut: the first blast was another caster's");
    }

    // The cut belongs to the blast only: a fire cast on a mob a blast just hit does the damage it does on a mob no blast hit.
    [TestMethod]
    public void CreepingToxin_TheCutDoesNotChangeOtherSkillsOnTheMob()
    {
        using var assetsA = new TempAssets();
        assetsA.AddWzSkill(ToxinData(normal: false));
        assetsA.AddWzSkill(FireData());
        var (a, _) = ToxinRun(assetsA, Floor, 0, 6500, (100, 100));
        a.SkillStrike(7, 0, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78); // the blast chain on the mob
        ZoneRun(a, 700);
        var after = a.SkillStrike(7, 100, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).Where(h => h.Skill == Bolt).ToList();
        using var assetsB = new TempAssets();
        assetsB.AddWzSkill(FireData());
        var control = ToxinWorld(assetsB, Floor, (100, 100));
        var plain = control.SkillStrike(7, 100, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).Where(h => h.Skill == Bolt).ToList();
        CollectionAssert.AreEqual(plain.Select(h => h.Damage).ToList(), after.Select(h => h.Damage).ToList());
    }

    // The window: a mob hit by a blast takes no cut from a blast more than t (0.4 s) later. At 2800 ms only the middle pool is ripe: the cast blows it up and hits the mob at 100. A second
    // cast 1 s later blows up the pool at 232 (up at 2000, ripe at 3500; its box 42..422 holds the mob) and hits it full.
    [TestMethod]
    public void CreepingToxin_BlastMoreThanTheWindowLater_IsNotCut()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData(normal: false);
        assets.AddWzSkill(toxin);
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 2800, (100, 100));
        var first = world.SkillStrike(7, 0, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).Where(h => h.Skill == Toxin).ToList();
        Assert.AreEqual(4, first.Count);
        ZoneRun(world, 1000); // the pool at 232 is ripe now and was not set off (the chain only takes ripe pools)
        var later = world.SkillStrike(7, 300, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).Where(h => h.Skill == Toxin).ToList();
        Assert.AreEqual(4, later.Count);
        for (int i = 0; i < 4; i++) Assert.AreEqual((double)first[i].Damage, (double)later[i].Damage, first[i].Damage * 0.002 + 3, "a blast more than 0.4 s after the last one is not cut");
    }

    // Ignite's wall does not blow a pool up (a wall is not a fire skill's hit): the fire spell here is a small box (30 px) on a mob just outside the last pool (it ends at 932), and the wall it
    // starts (40 px each side of the mob) overlaps the pool. The wall is up and the pool stays.
    [TestMethod]
    public void CreepingToxin_IgniteWallDoesNotBlowAPoolUp()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", lt: new[] { -15, -50 }, rb: new[] { 15, 50 }, attackDelayMs: 0));
        var (world, _) = ToxinRun(assets, Floor, 0, 6500, (960, 100));
        world.SkillStrike(7, 960, 100, true, new Random(2), Mage with { IgniteProp = 100 }, Bolt, 78);
        Assert.AreEqual(Ignite, world.TakeZones().Single().Skill, "the wall starts");
        ZoneRun(world, 5000);
        Assert.AreEqual(0, world.TakePoolBlasts().Count);
        Assert.AreEqual(1, world.SummonsOn(7).Count);
    }

    // A pool that blew up grows back: it is there again after the wait of WZ y (120 x 10 ms = 1200 ms; the middle pool needs no spreading from the totem) and can blow up w2 (1500 ms) after that:
    // 2700 ms after the blast. Before that the box of a fire spell on it does nothing.
    [TestMethod]
    public void CreepingToxin_PoolThatBlewUpGrowsBack_AndBlowsUpAgainWhenRipe()
    {
        using var assets = new TempAssets();
        var toxin = ToxinData();
        assets.AddWzSkill(toxin);
        assets.AddWzSkill(FireData());
        int back = (int)toxin.Value("y", 14) * WzSkillData.ToxinWzUnitMs + (int)toxin.Value("w2", 14);
        Assert.AreEqual(2700, back);
        var (world, _) = ToxinRun(assets, Floor, 0, 3000); // only the middle pool is ripe
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(4, world.TakePoolBlasts().Single().Index);
        ZoneRun(world, back - 200); // 2500 ms after the blast
        world.TakePoolBlasts();
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(0, world.TakePoolBlasts().Count, "it is not ripe yet");
        ZoneRun(world, 300);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        var again = world.TakePoolBlasts().Single();
        Assert.AreEqual((4, true), (again.Index, again.Paid), "it grew back and blows up once more, for MP");
    }

    // A pool far from the totem grows back later, in the same steps as it first came up: the step is the distance from the totem over u (200 px) rounded up, less one (the middle pool and
    // the ones within 200 px are step 0), and each step takes subTime. The pools of one step come up together. The layout of the whole pool: nine pools, 4 steps to each side.
    [TestMethod]
    public void ToxinPoolLayout_Steps_SpreadMs_Reach_AreTheStepsOfThePool()
    {
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 2, 2, 3, 4 }, new[] { 0, 100, 200, 232, 400, 432, 600, 632, 832 }.Select(d => ToxinPoolLayout.Step(d, 0, 200)).ToArray());
        Assert.AreEqual(900, ToxinPoolLayout.SpreadMs(232, 0, 900, 200));
        Assert.AreEqual(900, ToxinPoolLayout.SpreadMs(-232, 0, 900, 200), "both sides alike");
        Assert.AreEqual(3600, ToxinPoolLayout.SpreadMs(832, 0, 900, 200));
        Assert.AreEqual(0, ToxinPoolLayout.SpreadMs(0, 0, 900, 200));
        var pieces = ToxinPoolLayout.Pieces(0, -932, 932);
        CollectionAssert.AreEqual(new[] { -832f, -632f, -432f, -232f, 0f, 232f, 432f, 632f, 832f }, pieces.Select(p => p.Center).ToArray());
        Assert.AreEqual(932, ToxinPoolLayout.Reach(ToxinData()));
    }

    // The pool grows back in the same steps after a blast: the pool at 232 (step 1) is up 1200 + 900 ms after it blew up, and ripe 1500 ms later: 3600 ms. A fire box of 60 px at 232 blows up
    // that pool only; 3500 ms later it is still too young, 3700 ms later it blows up again.
    [TestMethod]
    public void CreepingToxin_FarPoolGrowsBackLater_ThanTheMiddleOne()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(BoltData(false, "f", lt: new[] { -30, -75 }, rb: new[] { 30, 75 }, attackDelayMs: 0)); // a box of 60 px: one pool
        var (world, _) = ToxinRun(assets, Floor, 0, 6500);
        world.SkillStrike(7, 232, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(5, world.TakePoolBlasts().Single().Index);
        ZoneRun(world, 3500);
        world.TakePoolBlasts(); // the chain
        world.SkillStrike(7, 232, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(0, world.TakePoolBlasts().Count, "1200 + 900 + 1500 = 3600 ms: not yet");
        ZoneRun(world, 200);
        world.SkillStrike(7, 232, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(5, world.TakePoolBlasts().Single().Index);
    }

    // The elements: a spell that is not fire does not blow a pool up, and neither does the fire spell of another player.
    [TestMethod]
    public void CreepingToxin_NotFire_OrAnotherPlayersFire_DoesNotBlowAPoolUp()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(BoltData(false, "", attackDelayMs: 0)); // not a fire spell
        var (world, _) = ToxinRun(assets, Floor, 0, 6500);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        Assert.AreEqual(0, world.TakePoolBlasts().Count);
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(ToxinData());
        assets2.AddWzSkill(FireData());
        var other = ToxinWorld(assets2, Floor);
        other.SkillStrike(7, -100, 100, true, new Random(1), Mage, Toxin, 200, attacker: 1);
        ZoneRun(other, 6500);
        other.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78, attacker: 2);
        Assert.AreEqual(0, other.TakePoolBlasts().Count, "the pools are the caster's own");
    }

    // The totem of the pool that blew up does not: it stays, and so does the poison of the rest of the pool. The hit blows up the middle pool; the chain takes the other 8, in 4 waves.
    [TestMethod]
    public void CreepingToxin_TotemStaysAfterTheBlasts()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(FireData());
        var (world, _) = ToxinRun(assets, Floor, 0, 6500);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78);
        ZoneRun(world, 3000);
        Assert.AreEqual(9, world.TakePoolBlasts().Count);
        Assert.AreEqual(1, world.SummonsOn(7).Count);
        Assert.AreEqual(1, world.ZonesOn(7).Count);
    }

    // The poison of the pool has no mob cap (mobCount 10 is the blast's): 11 mobs in the cast box and the pool are all poisoned, and the cast hits none of them.
    [TestMethod]
    public void CreepingToxin_PoolPoisonsElevenMobs_NoDirectHit()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(ToxinData());
        var world = ToxinWorld(assets, Floor, Enumerable.Range(0, 11).Select(i => (10 + i * 8, 100)).ToArray()); // 10..90: inside the cast box (100 px) and the first pool
        world.SkillStrike(7, -100, 100, true, new Random(1), Mage, Toxin, 200); // the totem at 0
        var hits = ZoneRun(world, 1300);
        Assert.AreEqual(0, hits.Count(h => !h.Dot), "no direct hit");
        Assert.AreEqual(11, world.DotsOn(7).Count(d => d.skill == Toxin), "all 11 are poisoned");
    }

    // +50% against normal mobs (nbdR) also counts in the poison ticks and in the blasts, with nbdR both do more to a normal mob, the same to a boss.
    [TestMethod]
    public void CreepingToxin_NormalEnemyDamage_AlsoInPoisonTicksAndBlasts()
    {
        (long dot, long blast) Run(bool normal, bool boss)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(ToxinData(normal));
            assets.AddWzSkill(FireData());
            assets.AddMap(7, """[{"MobId":100000,"X":50,"Cy":100}]""", Floor);
            assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, boss: boss);
            var world = new MobWorld(assets.Dir);
            world.EnterMap(7);
            world.SkillStrike(7, -100, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Toxin, 200); // the totem at 0
            long dot = ZoneRun(world, 3000).First(h => h.Dot && h.Skill == Toxin).Damage;
            ZoneRun(world, 3500); // 6500 ms: the pools are ripe
            long blast = world.SkillStrike(7, 0, 100, true, new MiddleRandom(), Mage with { CritPct = 0 }, Bolt, 78).First(h => h.Skill == Toxin).Damage;
            return (dot, blast);
        }
        var (dotN, blastN) = Run(true, false);
        var (dotPlain, blastPlain) = Run(false, false);
        Assert.IsTrue(dotN > dotPlain, "the poison tick");
        Assert.IsTrue(blastN > blastPlain, "the blast");
        Assert.AreEqual(Run(false, true), Run(true, true), "a boss takes no nbdR");
    }

    // The MP of a blast: mpCon of the level (46 + x: 47 at level 1, 60 at 14), raised by the MP percent of the damage skills (Element Amplification) and rounded down, like Pay.
    [TestMethod]
    public void SkillCost_BlastMp_IsTheLevelMpRaisedByTheMpPercent_RoundedDown()
    {
        var blast = ToxinData().Children.Single();
        Assert.AreEqual(60, SkillCost.BlastMp(blast, 14, 0));
        Assert.AreEqual(90, SkillCost.BlastMp(blast, 14, 50));
        Assert.AreEqual(47, SkillCost.BlastMp(blast, 1, 0));
        Assert.AreEqual(70, SkillCost.BlastMp(blast, 1, 50), "47 x 150 / 100 = 70.5, down");
        var parent = ToxinData();
        parent.Common["damage"] = SkillFormula.Levels("1", 14);
        var chara = new CharaSlot { Mp = 1000 };
        Assert.IsTrue(new SkillCost(new Dictionary<int, SkillData>()).Pay(chara, parent, 14, new Dictionary<int, float>(), 50));
        Assert.AreEqual(1000 - 54 * 150 / 100, chara.Mp, "Pay raises the cost of its skill the same way (54 = 40 + 14)");
    }

    // On the wire: a casts Creeping Toxin; a and b both get the totem and the pool (the cast goes to every connection), c who enters the map later is told both again with the
    // time that is left; then a casts a fire spell on a pool that is 1.5 s old and every client gets the blast (paid: it is a's blast) from the server.
    [TestMethod]
    public void Loopback_CreepingToxin_EveryoneSeesTotemAndPool_LateJoinerGetsTheRest_BlastReachesEveryone()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, "[]", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.5, 0, speed: 1, level: 200);
        assets.AddWzSkill(ToxinData());
        assets.AddWzSkill(FireData());
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 92);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 92);
        b.Connect("127.0.0.1", Net.Port + 92);
        c.Connect("127.0.0.1", Net.Port + 92);
        a.SendState(9, -100, 100, Net.ActStand, 0, true); // the totem comes up 100 px in front: at 0
        b.SendState(9, 50, 100, Net.ActStand, 0, true);
        a.SendSkillCast(Toxin, 200);
        var since = System.Diagnostics.Stopwatch.StartNew(); // the server runs on real time: the checks below wait on this clock, with a limit
        var atA = WaitSummon(a);
        var atB = WaitSummon(b);
        Assert.AreEqual((Toxin, 9, 0f, 100f, 60000, 100, false), (atA.Skill, atA.Map, atA.X, atA.Y, atA.LifeMs, atA.DelayMs, atA.Gone));
        Assert.AreEqual(atA, atB);
        var poolA = WaitZone(a);
        Assert.AreEqual((Toxin, 9, -932f, 932f, 900, 0f), (poolA.Skill, poolA.Map, poolA.Left, poolA.Right, poolA.StepMs, poolA.OriginX));
        Assert.AreEqual(poolA, WaitZone(b));
        Assert.AreEqual(atA, WaitSummon(c)); // the cast goes to every connection (the client drops what is not on its map); c has not entered the map yet
        Assert.AreEqual(poolA, WaitZone(c));
        Assert.IsTrue(SpinWait.SpinUntil(() => since.ElapsedMilliseconds >= 300, 5000));
        c.SendState(9, -50, 100, Net.ActStand, 0, true); // entering the map: the server lists the summons and zones that run there
        var atC = WaitSummon(c);
        Assert.AreEqual(atA.Uid, atC.Uid);
        Assert.IsTrue(atC.LifeMs > 0 && atC.LifeMs < 60000, $"the time left, not the whole time ({atC.LifeMs})");
        var poolC = WaitZone(c);
        Assert.AreEqual(poolA.Uid, poolC.Uid);
        Assert.IsTrue(poolC.AgeMs > 0);
        // The middle pool is up after 1.1 s and ripe after 2.6 s of the zone: a fire cast blows it up from then on. Cast again every 200 ms (at most 10 s) until the blast event comes: a cast
        // too early does nothing, so a slow server only makes the test take longer.
        PoolBlastEvent blastA = default;
        bool blasted = false;
        for (int tries = 0; tries < 50 && !blasted; tries++)
        {
            Thread.Sleep(200);
            a.SendSkillCast(Bolt, 78);
            for (int i = 0; i < 20 && !blasted; i++)
            {
                blasted = a.TryTakePoolBlast(out blastA);
                if (!blasted) Thread.Sleep(10);
            }
        }
        Assert.IsTrue(blasted, "timed out waiting for the pool blast");
        Assert.AreEqual((poolA.Uid, 4, 9, a.PlayerId, true), (blastA.Zone, blastA.Index, blastA.Map, blastA.Attacker, blastA.Paid));
        Assert.AreEqual(blastA, WaitPoolBlast(b));
    }

    private static PoolBlastEvent WaitPoolBlast(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakePoolBlast(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a pool blast event");
        throw new InvalidOperationException("unreachable");
    }

    private static SummonEvent WaitSummon(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeSummon(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a summon event");
        throw new InvalidOperationException("unreachable");
    }
}
