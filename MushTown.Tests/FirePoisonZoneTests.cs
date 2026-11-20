using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The generic ground zone (GroundZone, MobWorld.StartZone) and Poison Mist (211.img), its first user. Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Mist = WzSkillData.PoisonMist;

    // Poison Mist as 211.img writes it at level 20: poison, a zone of 15 s on the box lt(-250,-200) rb(250,200), 270% a hit, a DoT of 240% every 1 s
    // for 6 s, no mobCount and no attackCount, the body action poisonMist with its attack frame at 480 ms.
    private static WzSkillData MistData(int id = Mist)
    {
        var data = new WzSkillData { SkillId = id, MaxLevel = 20, Kind = SkillData.KindActive, ElemAttr = "s", AttackDelayMs = 480 };
        data.Common["mpCon"] = SkillFormula.Levels("30", 20);
        data.Common["time"] = SkillFormula.Levels("15", 20);
        data.Common["damage"] = SkillFormula.Levels("270", 20);
        data.Common["dot"] = SkillFormula.Levels("240", 20);
        data.Common["dotInterval"] = SkillFormula.Levels("1", 20);
        data.Common["dotTime"] = SkillFormula.Levels("6", 20);
        data.Vectors["lt"] = new[] { -250, -200 };
        data.Vectors["rb"] = new[] { 250, 200 };
        return data;
    }

    private static List<MobWorld.Hit> ZoneRun(MobWorld world, int totalMs)
    {
        var hits = new List<MobWorld.Hit>();
        for (int t = 0; t < totalMs; t += 100) hits.AddRange(world.Tick(100, new()).Strikes.Select(s => s.hit));
        return hits;
    }

    // GroundZone alone: it waits, then the first period goes off at once, the next ones every period, none at or after the end (3 s of 1 s: 3 periods).
    [TestMethod]
    public void GroundZone_WaitsThenPeriods_NoneAtTheEnd()
    {
        var zone = new GroundZone { PeriodMs = 1000, LifeMs = 3000, WaitMs = 500 };
        Assert.AreEqual(0, zone.Step(499));
        Assert.AreEqual(0, zone.Step(1)); // the wait ends exactly here: no time of this step is left
        Assert.IsFalse(zone.Over);
        Assert.AreEqual(1, zone.Step(1)); // the first period, at the start
        Assert.AreEqual(1, zone.Step(999)); // 1000 ms in
        Assert.AreEqual(1, zone.Step(2000)); // one big step: 2000 ms is a period, 3000 ms is the end and not one
        Assert.IsTrue(zone.Over);
        Assert.AreEqual(0, zone.Step(5000));
    }

    // The box: edges are inside, and the body height counts (a body that reaches into the box is in). A zone ended by Detonate is over at once
    // (the hook for Mist Eruption).
    [TestMethod]
    public void GroundZone_Contains_EdgesInside_Detonate_Ends()
    {
        var zone = new GroundZone { Left = -250, Right = 250, Top = -100, Bottom = 300, PeriodMs = 1000, LifeMs = 5000 };
        Assert.IsTrue(zone.Contains(-250, 100, 0));
        Assert.IsTrue(zone.Contains(250, 100, 0));
        Assert.IsFalse(zone.Contains(251, 100, 0));
        Assert.IsTrue(zone.Contains(0, 300, 0)); // the feet on the bottom edge
        Assert.IsFalse(zone.Contains(0, 301, 0));
        Assert.IsTrue(zone.Contains(0, -50, 50)); // the feet are above the top edge, the body reaches down into it
        Assert.IsFalse(zone.Contains(0, -101, 0));
        Assert.IsFalse(zone.Over);
        zone.Detonate();
        Assert.IsTrue(zone.Over);
        Assert.AreEqual(0, zone.Step(1000));
    }

    // The ground zone skills are the ones listed in WzSkillData.GroundZones (Poison Mist only); Explosion and Poison Breath are not.
    [TestMethod]
    public void IsGroundZone_ReadsTheGroundZonesSet()
    {
        Assert.IsTrue(MistData().IsGroundZone);
        Assert.IsFalse(BlastData().IsGroundZone);
        Assert.IsFalse(BreathData().IsGroundZone);
    }

    // The cast makes no hit and no ball; it makes one zone event: the box lt..rb across the caster (mirrored by facing), the caster's feet as the
    // origin (an airborne caster keeps it where he is, not on the floor below), 15 s, and the wait of the body action's attack frame and one tick
    // (480 + 100 ms). A client that enters later is told what is left: the wait keeps counting down, then the time.
    [TestMethod]
    public void PoisonMist_CastMakesOneZoneEvent_AtTheCastersFeet_EntryListsTheRest()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        var world = World(assets, (100, 100));
        Assert.AreEqual(0, world.SkillStrike(7, 40, 20, true, new Random(1), Mage, Mist, 270).Count); // 80 px above the floor (y 100)
        Assert.AreEqual(0, world.TakeLaunches().Count);
        var events = world.TakeZones();
        Assert.AreEqual(1, events.Count);
        var e = events[0];
        Assert.AreEqual((Mist, 7, -210f, 290f, 20f), (e.Skill, e.Map, e.Left, e.Right, e.Y)); // x 40 +- 250, the feet y 20
        Assert.AreEqual((15000, 580, false), (e.LifeMs, e.DelayMs, e.Gone));
        Assert.AreEqual(0, world.TakeZones().Count);
        world.Tick(200, new());
        var rest = world.ZonesOn(7).Single();
        Assert.AreEqual((e.Uid, 380, 15000), (rest.Uid, rest.DelayMs, rest.LifeMs));
        world.Tick(1000, new()); // 380 ms of wait, 620 ms of life
        rest = world.ZonesOn(7).Single();
        Assert.AreEqual((0, 14380), (rest.DelayMs, rest.LifeMs));
        Assert.AreEqual(0, world.ZonesOn(8).Count); // another map
    }

    // The zone lives `time` seconds from its start and then goes. The mob in it takes the hit once (the first period) and every later period refreshes
    // the poison, so the poison ticks run on until the zone is over and 6 s after the last refresh.
    [TestMethod]
    public void PoisonMist_LivesTimeSeconds_OneHit_PoisonRefreshedToTheEnd_ThenGone()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        var world = World(assets, (100, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mist, 270);
        var hits = ZoneRun(world, 14000);
        Assert.AreEqual(1, hits.Count(h => !h.Dot && h.Skill == Mist)); // one segment, once
        Assert.AreEqual(1, world.ZonesOn(7).Count); // 14 s in: the zone still stands
        Assert.AreEqual(1, world.DrainLayers(7, 0, 100, 10, 0));
        hits.AddRange(ZoneRun(world, 12000));
        Assert.AreEqual(0, world.ZonesOn(7).Count);
        Assert.AreEqual(1, hits.Count(h => !h.Dot));
        Assert.IsTrue(hits.Count(h => h.Dot) >= 15); // a tick each second all the way
        Assert.AreEqual(0, world.DrainLayers(7, 0, 100, 10, 0)); // 26 s in: the poison is out too
    }

    // The box is around the caster, not around the floor: the floor 200 under the caster is on the bottom edge (a mob on it is in), 201 under him is out.
    [TestMethod]
    public void PoisonMist_BoxIsAroundTheCaster_NotTheGround()
    {
        long Hits(float casterY)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(MistData());
            var world = World(assets, (100, 100)); // the mob's feet are at y 100
            world.SkillStrike(7, 0, casterY, true, new Random(1), Mage, Mist, 270);
            return ZoneRun(world, 1000).Count(h => !h.Dot);
        }
        Assert.AreEqual(1, Hits(-100)); // the mob is 200 under the caster: on the edge
        Assert.AreEqual(0, Hits(-101)); // 201 under him: out
        Assert.AreEqual(1, Hits(300)); // the caster is lower than the mob: it is 200 over him, on the edge
        Assert.AreEqual(0, Hits(301));
    }

    // Many zones: casts one after another leave many zones standing, and a mob in all of them takes the direct hit once (the first zone poisons it, the
    // others only refresh); a cast later, while the poison lasts, does the same.
    [TestMethod]
    public void PoisonMist_ManyZones_OneDirectHitForAMob()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        var world = World(assets, (100, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mist, 270);
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Mist, 270); // the same moment: two zones
        Assert.AreEqual(2, world.ZonesOn(7).Count);
        var hits = ZoneRun(world, 3000);
        world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Mist, 270); // a third, while the poison runs
        hits.AddRange(ZoneRun(world, 3000));
        Assert.AreEqual(3, world.ZonesOn(7).Count);
        Assert.AreEqual(1, hits.Count(h => !h.Dot && h.Skill == Mist));
        Assert.IsTrue(hits.Count(h => h.Dot) >= 5);
    }

    // Only the mist's own DoT from the same caster counts: a mob poisoned by Poison Breath still takes the direct hit of the mist, and so does a mob that
    // has the mist's DoT of another caster (each caster's poison is his own).
    [TestMethod]
    public void PoisonMist_OnlyItsOwnDotFromTheSameCasterCounts()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        assets.AddWzSkill(BreathData());
        var world = World(assets, (100, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Breath, 130); // caster 0: Poison Breath puts its DoT
        Assert.AreEqual(1, world.DrainLayers(7, 0, 100, 10, 0));
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Mist, 270, 5); // caster 5's mist: the direct hit lands, the DoT is its own
        Assert.AreEqual(1, ZoneRun(world, 1000).Count(h => !h.Dot && h.Skill == Mist));
        world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Mist, 270); // caster 0's mist: neither Breath nor the mist of caster 5 counts
        Assert.AreEqual(1, ZoneRun(world, 1000).Count(h => !h.Dot && h.Skill == Mist));
        Assert.AreEqual(2, world.DrainLayers(7, 0, 100, 10, 0)); // caster 0: Breath and the mist
        Assert.AreEqual(1, world.DrainLayers(7, 0, 100, 10, 5)); // caster 5: the mist
    }

    // Every mob in the box is hit when the zone starts (after the attack frame, not before); the mob outside (260 px away) takes nothing. The edges and the
    // height of the box are GroundZone_Contains's test (the mobs walk a pixel a second here). There is no cap on the number of mobs.
    [TestMethod]
    public void PoisonMist_HitsEveryMobInTheBox_NotBeforeTheStart_EdgesInside()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        var world = World(assets, (-260, 100), (-240, 100), (-1, 100), (0, 100), (1, 100), (240, 100), (260, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mist, 270);
        Assert.AreEqual(0, ZoneRun(world, 500).Count); // 480 ms is the attack frame: the first tick after it (600 ms) is the start
        var first = world.Tick(100, new()).Strikes.Select(s => s.hit).Where(h => !h.Dot).ToList();
        CollectionAssert.AreEqual(new[] { -240, -1, 0, 1, 240 }, UidsHit(world, first));
        Assert.IsTrue(first.All(h => h.Skill == Mist));
    }

    // The poison: a mob in the box is poisoned at once (the DoT of the skill, 240% every 1 s for 6 s); ticks come as Dot hits; the same skill again
    // only refreshes (still one layer), another skill is another layer. DrainLayers counts the layers the way Elemental Drain does.
    [TestMethod]
    public void PoisonMist_PutsTheDot_SameSkillOnlyRefreshes()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        assets.AddWzSkill(DotSkillData(Breath));
        var world = World(assets, (100, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mist, 270);
        Assert.AreEqual(0, world.DrainLayers(7, 0, 100, 10, 0));
        var hits = ZoneRun(world, 3000);
        Assert.AreEqual(1, world.DrainLayers(7, 0, 100, 10, 0)); // many periods and one layer
        Assert.IsTrue(hits.Count(h => h.Dot) >= 2); // the DoT ticks, one a second
        world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Mist, 270); // the second cast: the zones overlap, the layer does not
        ZoneRun(world, 1000);
        Assert.AreEqual(1, world.DrainLayers(7, 0, 100, 10, 0));
        world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Breath, 130);
        ZoneRun(world, 1000);
        Assert.AreEqual(2, world.DrainLayers(7, 0, 100, 10, 0)); // another skill is another layer
    }

    // A mob that leaves the box has its poison run out on its own: no refresh, so the last tick is 6 s after the last period it stood in. The mob walks right at
    // 100 px/s toward a player far on that side and leaves the box (250 px) at 2.5 s; the poison ends by 8.6 s and the mob is never hit again.
    [TestMethod]
    public void PoisonMist_MobLeaves_PoisonRunsOut_NoMoreHits()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 100, aggro: 600, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mist, 270);
        var players = new List<(int id, int map, float x, float y)> { (1, 7, 500, 100) }; // the player pulls the mob right
        var timeline = new List<(int ms, bool dot)>();
        for (int ms = 100; ms <= 20000; ms += 100)
            foreach (var s in world.Tick(100, players).Strikes)
                timeline.Add((ms, s.hit.Dot));
        Assert.AreEqual(1, timeline.Count(t => !t.dot)); // the first period only; the later ones in the box refreshed the poison
        int lastTick = timeline.Last(t => t.dot).ms;
        Assert.IsTrue(lastTick >= 7000 && lastTick <= 2600 + 6000 + 100, $"the poison must end 6 s after the last period in the box (last tick at {lastTick} ms)");
    }

    // The zone goes through the pipeline: Elemental Drain (a final damage for each of the caster's DoT skills near him) raises the hit of a zone like any other.
    [TestMethod]
    public void PoisonMist_HitsGoThroughThePipeline_DrainApplies()
    {
        long Damage(Build build)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(MistData());
            var world = World(assets, (100, 100));
            world.SkillStrike(7, 0, 100, true, new Random(5), build, Mist, 270);
            return world.Tick(600, new()).Strikes.Select(s => s.hit).First(h => !h.Dot).Damage;
        }
        long plain = Damage(Mage);
        long drained = Damage(Mage with { DrainPct = 10, DrainMax = 5 });
        Assert.IsTrue(drained > plain, $"{drained} must be more than {plain}");
    }

    // The caster leaving (CancelZones) takes his zones away at once: no more hits, and a gone event for every client.
    [TestMethod]
    public void PoisonMist_CasterLeaves_ZoneGoneWithAGoneEvent()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MistData());
        var world = World(assets, (100, 100));
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mist, 270, 4);
        var started = world.TakeZones().Single();
        ZoneRun(world, 1000);
        world.CancelZones(3); // another caster: nothing
        Assert.AreEqual(0, world.TakeZones().Count);
        world.CancelZones(4);
        var gone = world.TakeZones().Single();
        Assert.AreEqual((started.Uid, true), (gone.Uid, gone.Gone));
        Assert.AreEqual(0, world.ZonesOn(7).Count);
        Assert.AreEqual(0, ZoneRun(world, 3000).Count(h => !h.Dot));
    }

    // On the wire: a casts Poison Mist and both a and b get the zone (the caster too: it draws no echo for a ground zone), the server judges the poison
    // (b gets the hit of the skill as a MobHit from the tick loop), and c, who enters the map later, is told the zone again with the time that is left.
    [TestMethod]
    public void Loopback_PoisonMist_EveryoneSeesTheZone_ServerJudges_LateJoinerGetsTheRest()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.5, 0, speed: 1, level: 200);
        assets.AddWzSkill(MistData());
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 91);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 91);
        b.Connect("127.0.0.1", Net.Port + 91);
        c.Connect("127.0.0.1", Net.Port + 91);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 50, 100, Net.ActStand, 0, true);
        a.SendSkillCast(Mist, 270);
        var atA = WaitZone(a);
        var atB = WaitZone(b);
        Assert.AreEqual((Mist, 9, -250f, 250f, 100f, 15000, 580, false), (atA.Skill, atA.Map, atA.Left, atA.Right, atA.Y, atA.LifeMs, atA.DelayMs, atA.Gone));
        Assert.AreEqual(atA, atB);
        Assert.AreEqual(atA, WaitZone(c)); // the cast goes to every connection (the client drops what is not on its map); c has not entered the map yet
        MobEvent hit = default;
        for (int i = 0; i < 500; i++)
        {
            if (b.TryTakeMob(out var e) && !e.Spawn && e.Skill == Mist) { hit = e; break; }
            Thread.Sleep(10);
        }
        Assert.AreEqual(Mist, hit.Skill, "b must see the poison hit the server judged");
        Thread.Sleep(300);
        c.SendState(9, -50, 100, Net.ActStand, 0, true); // entering the map: the server lists the zones that run there
        var atC = WaitZone(c);
        Assert.AreEqual(atA.Uid, atC.Uid);
        Assert.IsTrue(atC.LifeMs > 0 && atC.LifeMs < 15000, $"the time left, not the whole time ({atC.LifeMs})");
        Assert.AreEqual(0, atC.DelayMs);
    }

    private static ZoneEvent WaitZone(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeZone(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a zone event");
        throw new InvalidOperationException("unreachable");
    }
}
