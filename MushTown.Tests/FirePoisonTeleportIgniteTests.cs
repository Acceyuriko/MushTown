using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Ticket T-20261119195811: the mob status Stun, Teleport Mastery (211.img 2111007: a hit, a stun and a DoT at the Teleport landing) and Ignite (210.img 2101010: a fire hit
// may start a wall of fire that puts a DoT on the mobs in it). Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Mastery = WzSkillData.TeleportMastery;
    private const int Ignite = WzSkillData.Ignite;

    // Teleport Mastery as 211.img writes it, from the WZ formulas at level 10 (the max): 100% to hit, 80% to stun for 4 s, 272% damage, 6 mobs, the box lt(-120,-100) rb(120,10),
    // a DoT of 98% every 2 s for 20 s. No attackCount (one hit), no mpCon (a passive). subProp is a parameter: 100 for a sure stun.
    private static WzSkillData MasteryData(string? subProp = null, string mobCount = "6")
    {
        var data = new WzSkillData { SkillId = Mastery, MaxLevel = 10, Kind = SkillData.KindPassive };
        data.Common["prop"] = SkillFormula.Levels("100", 10);
        data.Common["subProp"] = SkillFormula.Levels(subProp ?? "30+5*x", 10);
        data.Common["damage"] = SkillFormula.Levels("192+8*x", 10);
        data.Common["time"] = SkillFormula.Levels("2+d(x/5)", 10);
        data.Common["mobCount"] = SkillFormula.Levels(mobCount, 10);
        data.Common["dot"] = SkillFormula.Levels("68+3*x", 10);
        data.Common["dotInterval"] = SkillFormula.Levels("2", 10);
        data.Common["dotTime"] = SkillFormula.Levels("20", 10);
        data.Vectors["lt"] = new[] { -120, -100 };
        data.Vectors["rb"] = new[] { 120, 10 };
        return data;
    }

    // Ignite as 210.img writes it at level 10: fire, 40% damage, every 1500 ms, 50% to start, 8 mobs, the box lt(-40,-20) rb(40,20), 3 hits, a wall of 6 s (x).
    private static WzSkillData IgniteData()
    {
        var data = new WzSkillData { SkillId = Ignite, MaxLevel = 10, Kind = SkillData.KindPassive, ElemAttr = "f" };
        data.Common["damage"] = SkillFormula.Levels("30+x", 10);
        data.Common["subTime"] = SkillFormula.Levels("1500", 10);
        data.Common["prop"] = SkillFormula.Levels("10+4*x", 10);
        data.Common["mobCount"] = SkillFormula.Levels("8", 10);
        data.Common["attackCount"] = SkillFormula.Levels("3", 10);
        data.Common["x"] = SkillFormula.Levels("4+d(x/4)", 10);
        data.Vectors["lt"] = new[] { -40, -20 };
        data.Vectors["rb"] = new[] { 40, 20 };
        return data;
    }

    private static List<(int id, int map, float x, float y)> PlayerAt(float x) => new() { (1, 7, x, 100) };

    // The numbers of both skills, worked out from the WZ formulas at level 10: Teleport Mastery stuns 80% for 4 s, hits 272%, poisons 98% every 2 s for 20 s;
    // Ignite starts a wall with 50%, hits 40% three times every 1.5 s, for 6 s.
    [TestMethod]
    public void WzNumbers_AtMaxLevel()
    {
        var tm = MasteryData();
        Assert.AreEqual((80.0, 4.0, 272.0, 98.0), (tm.Value("subProp", 10), tm.Value("time", 10), tm.Value("damage", 10), tm.Value("dot", 10)));
        var ignite = IgniteData();
        Assert.AreEqual((50.0, 40.0, 6.0, 1500.0), (ignite.Value("prop", 10), ignite.Value("damage", 10), ignite.Value("x", 10), ignite.Value("subTime", 10)));
    }

    // The stun is one status of the mob list with the stunning skill as its value; its head icon follows it: on when it lands, nothing when it is renewed, a stun of another skill
    // takes the icon over, and the icon goes when the stun runs out unless a DoT of the same skill still shows it.
    [TestMethod]
    public void MobStatuses_Stun_HeadIconFollowsIt()
    {
        var list = new MobStatuses();
        list.Add(MobStatusKind.Stun, Mastery, 4000);
        Assert.IsTrue(list.Has(MobStatusKind.Stun));
        Assert.IsTrue(list.HasBurningTarget);
        CollectionAssert.AreEqual(new[] { (Mastery, true) }, list.TakeDotChanges().ToArray());
        CollectionAssert.AreEqual(new[] { Mastery }, list.DotSkills().ToArray());
        list.Step(3000);
        list.Add(MobStatusKind.Stun, Mastery, 4000); // renewed: the time starts again, no change to tell
        Assert.AreEqual(0, list.TakeDotChanges().Count);
        list.Step(3999);
        Assert.IsTrue(list.Has(MobStatusKind.Stun));
        list.Step(1);
        Assert.IsFalse(list.Has(MobStatusKind.Stun));
        CollectionAssert.AreEqual(new[] { (Mastery, false) }, list.TakeDotChanges().ToArray());
        Assert.AreEqual(0, list.DotSkills().Count);

        list.AddDot(DotOf(Mastery, 10000, attacker: 1));
        list.Add(MobStatusKind.Stun, Mastery, 2000); // the same skill's DoT already shows the icon
        CollectionAssert.AreEqual(new[] { (Mastery, true) }, list.TakeDotChanges().ToArray());
        list.Step(2000); // the stun is over, the DoT keeps the icon
        Assert.AreEqual(0, list.TakeDotChanges().Count);
        CollectionAssert.AreEqual(new[] { Mastery }, list.DotSkills().ToArray());

        var other = new MobStatuses();
        other.Add(MobStatusKind.Stun, Mist, 4000);
        other.TakeDotChanges();
        other.Add(MobStatusKind.Stun, Mastery, 4000);
        CollectionAssert.AreEqual(new[] { (Mist, false), (Mastery, true) }, other.TakeDotChanges().ToArray());
    }

    // The cast at the landing hits the mobs in the box (lt..rb round the caster's feet, edges inside), at most mobCount of them (the nearest), one hit each at the pct the
    // client sent; a mob out of the box is not touched. Teleport Mastery is a passive in WZ and in the file, and the server still judges it.
    [TestMethod]
    public void TeleportMastery_Cast_HitsTheNearestSixInTheBox_OneHitEach()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MasteryData("100"));
        var world = World(assets, (-121, 100), (-120, 100), (-60, 100), (0, 100), (0, 110), (30, 100), (60, 100), (120, 100), (121, 100), (0, -1), (0, 111));
        Assert.IsTrue(world.IsDamageSkill(Mastery));
        var hits = world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mastery, 272);
        Assert.AreEqual(6, hits.Count);
        Assert.IsTrue(hits.All(h => h.Skill == Mastery && !h.Dot));
        var xs = UidsHit(world, hits);
        Assert.AreEqual(5, xs.Count(x => x is >= -60 and <= 60)); // the five nearest: 0, 0 (10 px lower), 30, -60, 60
        Assert.AreEqual(1, xs.Count(x => Math.Abs(x) == 120)); // the seventh in the box is one of the two edge mobs (the same distance): only one of them is hit
    }

    // The stun: every mob hit is stunned for `time` seconds (4 s at max level): it does not walk and does not touch for those 4 s and goes on at once after; the screens
    // still get its Move, with the same place every time (the Client stands a mob that did not go anywhere: GameMobs.ApplyMove), and a mob that was not hit goes on moving.
    [TestMethod]
    public void TeleportMastery_Stun_StopsTheMobForTimeSeconds_ThenItWalksAndTouchesAgain()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100},{"MobId":100000,"X":600,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0, 0, touch: 5, speed: 40, aggro: 800, level: 200);
        assets.AddWzSkill(MasteryData("100"));
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var uids = world.MobsOn(7).Select(m => m.Uid).ToArray();
        var player = PlayerAt(95); // the player stands on the first mob: it would touch at once
        float stunnedX = world.MobsOn(7).First().X;
        world.SkillStrike(7, 95, 100, true, new Random(1), Mage, Mastery, 272); // the first mob is in the box (5 px away), the second is 505 px away
        for (int ms = 100; ms < 4000; ms += 100)
        {
            var ev = world.Tick(100, player);
            Assert.AreEqual(0, ev.Hurts.Count, $"a stunned mob does not touch ({ms} ms)");
            Assert.AreEqual(stunnedX, ev.Moves.Single(m => m.Uid == uids[0]).X, $"the stunned mob stays where it is ({ms} ms)");
        }
        var after = world.Tick(100, player); // 4000 ms: the stun is over
        CollectionAssert.AreEquivalent(uids, after.Moves.Select(m => m.Uid).ToArray());
        Assert.AreNotEqual(stunnedX, after.Moves.Single(m => m.Uid == uids[0]).X); // it walks again
        Assert.AreEqual(1, after.Hurts.Count); // and it touches the player at once
    }

    // Burning Magic counts a stun that lands in the same hit (the stun is rolled in step 1, before the damage): with no DoT on the skill and no status on the mob, the hit
    // that stuns takes Burning Magic's final damage (the same dice, a build with and without it).
    [TestMethod]
    public void TeleportMastery_StunLanding_CountsForBurningMagic()
    {
        long Damage(int dotFinalPct)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(MasteryData("100").WithoutDot());
            var world = World(assets, (0, 100));
            return world.SkillStrike(7, 0, 100, true, new Random(5), Mage with { DotFinalPct = dotFinalPct }, Mastery, 272).Single().Damage;
        }
        Assert.IsTrue(Damage(50) > Damage(0));
    }

    // The chance: 80% of 1000 mobs hit by one cast are stunned (a roll for each mob; the seed is fixed, so the count is the same every run).
    [TestMethod]
    public void TeleportMastery_StunChance_IsSubProp()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MasteryData(mobCount: "1000"));
        var world = World(assets, Enumerable.Range(0, 1000).Select(_ => (0, 100)).ToArray());
        world.SkillStrike(7, 0, 100, true, new Random(7), Mage, Mastery, 272);
        int moving = world.Tick(100, PlayerAt(900)).Moves.Count(m => m.X != 0); // the mobs that are not stunned walk (speed 1: a tenth of a pixel); they were planted at x 0
        Assert.IsTrue(moving is > 150 and < 250, $"{moving} of 1000 moved: 20% should not be stunned");
    }

    // The DoT: 98% every 2 s for 20 s (the first tick after 2 s), put on each mob that is hit; a mob that was missed has none. The icon of the skill is told to everyone.
    [TestMethod]
    public void TeleportMastery_Dot_TicksEveryTwoSecondsForTwentySeconds()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(MasteryData("100"));
        var world = World(assets, (0, 100), (500, 100));
        var uid = world.MobsOn(7).First().Uid;
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Mastery, 272);
        var hits = ZoneRun(world, 100);
        CollectionAssert.AreEqual(new[] { (uid, Mastery, true) }, world.TakeDotChanges().ToArray()); // the stun and the DoT show one icon
        hits.AddRange(ZoneRun(world, 19900));
        Assert.AreEqual(10, hits.Count(h => h.Dot && h.Skill == Mastery)); // 20 s / 2 s
        Assert.IsTrue(hits.All(h => h.Uid == uid));
        Assert.AreEqual(0, world.DotSkills(uid).Count);
        CollectionAssert.AreEqual(new[] { (uid, Mastery, false) }, world.TakeDotChanges().ToArray()); // both are over: the icon goes
    }

    // On the wire: a casts Teleport Mastery at its landing; b sees the hit and the head icon of the mob (MobDot), and the mob stands still on b's screen (no more
    // Move for it) while the mob that was not hit goes on moving.
    [TestMethod]
    public void Loopback_TeleportMastery_EveryoneSeesTheStun()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":50,"Cy":100},{"MobId":100000,"X":600,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.5, 0, speed: 40, level: 200);
        assets.AddWzSkill(MasteryData("100"));
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 92);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 92);
        b.Connect("127.0.0.1", Net.Port + 92);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 900, 100, Net.ActStand, 0, true);
        a.SendSkillCast(Mastery, 272);
        MobEvent hit = default;
        for (int i = 0; i < 500; i++)
        {
            if (b.TryTakeMob(out var e) && !e.Spawn && e.Skill == Mastery) { hit = e; break; }
            Thread.Sleep(10);
        }
        Assert.AreEqual(Mastery, hit.Skill, "b must see the hit");
        var icon = WaitDot(b);
        Assert.AreEqual((hit.Uid, Mastery, true), (icon.Uid, icon.Skill, icon.On));
        while (b.TryTakeMove(out _)) { }
        var xs = new Dictionary<int, HashSet<float>>(); // the places each mob was sent at: the stunned one stays on one, the other walks
        for (int i = 0; i < 60; i++) // 600 ms: about six server ticks
        {
            while (b.TryTakeMove(out var mv)) (xs.TryGetValue(mv.Uid, out var set) ? set : xs[mv.Uid] = new()).Add(mv.X);
            Thread.Sleep(10);
        }
        Assert.AreEqual(1, xs[hit.Uid].Count, "the stunned mob stays where it is");
        Assert.IsTrue(xs.Where(kv => kv.Key != hit.Uid).All(kv => kv.Value.Count > 1), "the mob that is not stunned moves");
    }

    // Ignite, the start: with IgniteProp 100 a fire cast starts one wall at its first target (the nearest mob it hit): the zone event is the wall box (WZ lt..rb round the mob's
    // feet), the target's feet as the height, 6 s, up after one tick. A cast that is not fire, and a build without Ignite, start none.
    [TestMethod]
    public void Ignite_FireHitStartsOneWallAtTheFirstTarget_OtherCastsDoNot()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0));
        var world = World(assets, (100, 100), (50, 100));
        var fire = Mage with { IgniteProp = 100 };
        world.SkillStrike(7, 0, 100, true, new Random(1), fire, Bolt, 78);
        var e = world.TakeZones().Single();
        Assert.AreEqual((Ignite, 7, 10f, 90f, 100f, 6000, 100, false), (e.Skill, e.Map, e.Left, e.Right, e.Y, e.LifeMs, e.DelayMs, e.Gone)); // the nearest mob stands at x 50
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Bolt, 78); // no Ignite learned
        Assert.AreEqual(0, world.TakeZones().Count);
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(IgniteData());
        assets2.AddWzSkill(BoltData(false, "", attackDelayMs: 0)); // not a fire skill
        var world2 = World(assets2, (100, 100));
        world2.SkillStrike(7, 0, 100, true, new Random(1), fire, Bolt, 78);
        Assert.AreEqual(0, world2.TakeZones().Count);
    }

    // The chance is IgniteProp (50% at level 10): one roll for each cast.
    [TestMethod]
    public void Ignite_Chance_IsProp()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0));
        var world = World(assets, (50, 100));
        var rng = new Random(3);
        for (int i = 0; i < 1000; i++) world.SkillStrike(7, 0, 100, true, rng, Mage with { IgniteProp = 50 }, Bolt, 78);
        int walls = world.TakeZones().Count;
        Assert.IsTrue(walls is > 440 and < 560, $"{walls} walls of 1000 casts");
    }

    // The wall: after one tick it puts a DoT of Ignite on the mob in it, which hits 3 times (WZ attackCount) every 1.5 s, as long as the mob is in the wall (6 s: the DoT
    // lasts one interval after the last refresh). The hits are DoT hits of the skill, the first of each three settles. A mob out of the box is not touched, and the wall is gone
    // after its 6 s. The wall starts at 200 ms and the first tick of the beat is 1500 ms later: the hits come at 1700, 3200, 4700 and 6200 ms (4 ticks, 12 hits).
    [TestMethod]
    public void Ignite_Wall_HitsThreeTimesEveryBeatWhileTheMobIsInIt()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0));
        var world = World(assets, (50, 100), (130, 100)); // the cast hits both, the first (nearest to the caster at 60) gets the wall 10..90: the second is out of it
        world.SkillStrike(7, 60, 100, true, new Random(1), Mage with { IgniteProp = 100 }, Bolt, 78);
        var hits = new List<(int ms, MobWorld.Hit hit)>();
        for (int ms = 100; ms <= 9000; ms += 100)
            hits.AddRange(world.Tick(100, new()).Strikes.Where(s => s.hit.Dot).Select(s => (ms, s.hit)));
        Assert.IsTrue(hits.All(h => h.hit.Skill == Ignite && h.hit.Uid == hits[0].hit.Uid));
        Assert.AreEqual(12, hits.Count);
        CollectionAssert.AreEqual(new[] { 1700, 3200, 4700, 6200 }, hits.Select(h => h.ms).Distinct().ToArray());
        Assert.IsTrue(hits.Chunk(3).All(g => g[0].hit.Settle && !g[1].hit.Settle && !g[2].hit.Settle));
        Assert.AreEqual(0, world.ZonesOn(7).Count);
        Assert.AreEqual(0, world.DotSkills(hits[0].hit.Uid).Count);
    }

    // A mob that walks out of the wall still takes the beat that was due: the DoT lasts one interval after the last refresh (a beat is 1500 ms, so a DoT that ended a tick
    // after the mob left would never hit a walking mob).
    [TestMethod]
    public void Ignite_Wall_ALeavingMobIsStillHitOnce()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0));
        assets.AddMap(7, """[{"MobId":100000,"X":50,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 200, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.SkillStrike(7, 60, 100, true, new Random(1), Mage with { IgniteProp = 100 }, Bolt, 78);
        var hits = new List<MobWorld.Hit>();
        for (int ms = 100; ms <= 3000; ms += 100)
            hits.AddRange(world.Tick(100, new()).Strikes.Where(s => s.hit.Dot).Select(s => s.hit));
        Assert.AreEqual(3, hits.Count); // one beat of 3 hits: the mob is out of the box (80 px wide) long before the beat is due
        Assert.IsTrue(Math.Abs(world.MobsOn(7)[0].X - 50) > 40);
    }

    // A mob immune to the element of a zone's DoT (ElemAttr "F1": fire 1 = immune; "S1": poison) is neither given the DoT nor refreshed by it, as with the DoT of a
    // plain skill (HitPipeline.Collect): in Ignite's fire wall and in Poison Mist's cloud only the other mob takes the DoT hits.
    private static (HashSet<int> victims, int immuneUid) DotVictims(string zoneSkill, string immune)
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0));
        assets.AddWzSkill(MistData());
        assets.AddMap(7, """[{"MobId":100000,"X":50,"Cy":100},{"MobId":100001,"X":60,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, elemAttr: immune);
        assets.AddMob(100001, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        int immuneUid = world.MobsOn(7).Single(m => m.Mob == 100000).Uid;
        if (zoneSkill == "ignite") world.SkillStrike(7, 20, 100, true, new Random(1), Mage with { IgniteProp = 100 }, Bolt, 78);
        else world.SkillStrike(7, 55, 100, true, new Random(1), Mage, Mist, 270);
        var victims = new HashSet<int>();
        for (int ms = 100; ms <= 4000; ms += 100)
            foreach (var s in world.Tick(100, new()).Strikes.Where(s => s.hit.Dot)) victims.Add(s.hit.Uid);
        return (victims, immuneUid);
    }

    [TestMethod]
    public void Zones_DoNotPutTheDotOnAMobImmuneToItsElement()
    {
        foreach (var (skill, immune) in new[] { ("ignite", "F1"), ("mist", "S1") })
        {
            var (victims, immuneUid) = DotVictims(skill, immune);
            Assert.AreEqual(1, victims.Count, skill); // the other mob burns or is poisoned
            Assert.IsFalse(victims.Contains(immuneUid), skill);
        }
    }

    // At most mobCount mobs (8) work in a wall: with ten in its box only the eight nearest to its middle burn.
    [TestMethod]
    public void Ignite_Wall_TakesTheEightNearestMobs()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0, mobCount: 1));
        var world = World(assets, Enumerable.Range(0, 10).Select(i => (20 + i * 4, 100)).ToArray()); // x 20..56: the cast hits the one at 20 only, the wall is -20..60 round it
        world.SkillStrike(7, 0, 100, true, new Random(1), Mage with { IgniteProp = 100 }, Bolt, 78);
        var burning = ZoneRun(world, 2000).Where(h => h.Dot).Select(h => Feet[h.Uid]).Distinct().OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(new[] { 20, 24, 28, 32, 36, 40, 44, 48 }, burning); // the eight nearest to the middle of the wall; the ones at 52 and 56 do not burn
    }

    // Two walls of one caster are two zones but one DoT on a mob that stands in both (same source, same skill: it only refreshes, one layer for Elemental Drain).
    [TestMethod]
    public void Ignite_TwoWalls_OneDotLayer()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 0));
        var world = World(assets, (50, 100));
        var build = Mage with { IgniteProp = 100 };
        world.SkillStrike(7, 0, 100, true, new Random(1), build, Bolt, 78);
        world.SkillStrike(7, 0, 100, true, new Random(2), build, Bolt, 78);
        Assert.AreEqual(2, world.TakeZones().Count);
        ZoneRun(world, 500);
        Assert.AreEqual(1, world.DrainLayers(7, 50, 100, 10, 0));
    }

    // On the wire: the Build carries IgniteProp, and the wall that a fire hit starts in the tick loop (a skill with a body action judges there) reaches every client as a
    // zone event of Ignite.
    [TestMethod]
    public void Loopback_Ignite_BuildCarriesTheChance_EveryoneSeesTheWall()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.5, 0, speed: 1, level: 200);
        assets.AddWzSkill(IgniteData());
        assets.AddWzSkill(BoltData(false, "f", attackDelayMs: 200));
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 93);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 93);
        b.Connect("127.0.0.1", Net.Port + 93);
        a.SendBuild(Mage with { IgniteProp = 100 }); // the 117-byte Build
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        b.SendState(9, 50, 100, Net.ActStand, 0, true);
        a.SendSkillCast(Bolt, 78);
        var atA = WaitZone(a);
        var atB = WaitZone(b);
        Assert.AreEqual((Ignite, 9, 100f, 6000), (atA.Skill, atA.Map, atA.Y, atA.LifeMs));
        Assert.AreEqual(60f, atA.Left, 1f); // the mob stood at 100 and has walked a fraction of a pixel (speed 1) by the time the cast landed
        Assert.AreEqual(140f, atA.Right, 1f);
        Assert.AreEqual(atA, atB);
    }
}

internal static class WzSkillTestExtensions
{
    // The same skill with no DoT (a hit that only stuns).
    public static WzSkillData WithoutDot(this WzSkillData data)
    {
        data.Common.Remove("dot");
        data.Common.Remove("dotInterval");
        data.Common.Remove("dotTime");
        return data;
    }
}
