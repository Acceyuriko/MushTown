using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class MobAiTests
{
    private const string Flat = """[{"X1":0,"Y1":100,"X2":200,"Y2":100,"Layer":0}]""";

    private static List<(int id, int map, float x, float y)> Solo(float x) =>
        new() { (1, 7, x, 100) };

    // Wander: no player in aggro range, so the mob paces left at 40px/s and
    // turns at the platform edge (last good feet, facing flipped).
    [TestMethod]
    public void Tick_WanderWalksAndTurnsAtEdge()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40, 0, 35, 5, 40, 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var m1 = world.Tick(1000, Solo(900)).Moves;
        Assert.AreEqual(1, m1.Count);
        Assert.AreEqual(60f, m1[0].X);
        Assert.AreEqual(100f, m1[0].Y);
        Assert.IsFalse(m1[0].FacingRight);
        world.Tick(1000, Solo(900));
        var m3 = world.Tick(1000, Solo(900)).Moves;
        Assert.AreEqual(0f, m3[0].X); // ran out of platform: turned here
        Assert.IsTrue(m3[0].FacingRight);
    }

    // Chase: a player inside aggro range pulls the mob (facing flips to it).
    // The player stands past touch range, so no hurt lands.
    [TestMethod]
    public void Tick_ChaseMovesTowardPlayer()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40, 0, 35, 5, 40, 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var ev = world.Tick(1000, Solo(180));
        Assert.AreEqual(140f, ev.Moves[0].X);
        Assert.IsTrue(ev.Moves[0].FacingRight);
        Assert.AreEqual(0, ev.Hurts.Count);
    }

    // Touch: contact hurts once, then the 1s cooldown blocks, then it lands
    // again (the mob walks past and turns back onto the player).
    [TestMethod]
    public void Tick_TouchHurtsOncePerCooldown()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40, 0, 35, 5, 40, 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var first = world.Tick(100, Solo(110)).Hurts;
        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(1, first[0].Victim);
        Assert.AreEqual(100000, first[0].Mob);
        Assert.AreEqual(5, first[0].Damage);
        Assert.AreEqual(0, world.Tick(100, Solo(110)).Hurts.Count); // cooling
        Assert.AreEqual(0, world.Tick(500, Solo(110)).Hurts.Count); // still cooling
        Assert.AreEqual(1, world.Tick(500, Solo(110)).Hurts.Count); // cooldown spent
    }

    // Empty maps sleep: the mob still walks, but no moves go out (nobody watches).
    [TestMethod]
    public void Tick_EmptyMapSendsNoMoves()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var ev = world.Tick(1000, new List<(int id, int map, float x, float y)>());
        Assert.AreEqual(0, ev.Moves.Count);
        Assert.AreEqual(60f, world.MobsOn(7)[0].X); // still walked
    }

    // Rush shoves the mob off the edge: it falls straight down, and the void
    // pops it back home (no ground anywhere under x=250).
    [TestMethod]
    public void Tick_FallLandsPushedMobThenVoidPopsHome()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":190,"Cy":100}]""", Flat);
        assets.AddMob(100000, 100000);
        assets.AddSkill(SkillData.Rush, 150, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.SkillStrike(7, 190, 100, true, new Random(0), Progression.Beginner, SkillData.Rush, 200);
        Assert.AreEqual(250f, world.MobsOn(7)[0].X);
        world.Tick(100, new List<(int id, int map, float x, float y)>());
        Assert.AreEqual(250f, world.MobsOn(7)[0].X);
        Assert.AreEqual(170f, world.MobsOn(7)[0].Y); // 700px/s for 100ms
        world.Tick(20000, new List<(int id, int map, float x, float y)>());
        Assert.AreEqual(190f, world.MobsOn(7)[0].X); // void pop: back home
        Assert.AreEqual(100f, world.MobsOn(7)[0].Y);
    }

    // A kill plants a grave and mesos (10-30, always). The grave re-plants a
    // fresh body at home after 10s, under a new uid.
    [TestMethod]
    public void Kill_DropsMesosAndRespawnsAfter10s()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var hits = world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        Assert.AreEqual(0, hits[0].HpLeft);
        var fresh = world.TakeDrops();
        Assert.IsTrue(fresh.Count >= 1);
        Assert.AreEqual(DropKind.Mesos, fresh[0].Kind);
        Assert.IsTrue(fresh[0].Id is >= 10 and <= 30);
        Assert.AreEqual(1, fresh[0].Count);
        Assert.AreEqual(7, fresh[0].Map);
        Assert.AreEqual(0, world.TakeDrops().Count); // drained: second take is empty
        Assert.AreEqual(0, world.MobsOn(7).Count);
        Assert.AreEqual(0, world.Tick(9999, Solo(900)).Spawns.Count);
        var spawns = world.Tick(1, Solo(900)).Spawns;
        Assert.AreEqual(1, spawns.Count);
        Assert.AreEqual(2, spawns[0].Uid); // the kill was uid 1
        Assert.AreEqual(100f, spawns[0].X);
        Assert.AreEqual(100f, spawns[0].Y);
        Assert.AreEqual(1, spawns[0].Hp);
    }

    // Boss kill: a fat purse (150-300 mesos, 2 reds, 1 blue, the mop) and
    // a 5-min grave (normals pay 10-30 and re-plant in 10s).
    [TestMethod]
    public void Boss_KillDropsPurseAndRespawnsAfter5Min()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":6130101,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(6130101, 1, boss: true);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var hits = world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        Assert.AreEqual(0, hits[0].HpLeft);
        var fresh = world.TakeDrops();
        Assert.AreEqual(4, fresh.Count);
        Assert.AreEqual(DropKind.Mesos, fresh[0].Kind);
        Assert.IsTrue(fresh[0].Id is >= 150 and <= 300);
        Assert.AreEqual(DropKind.Item, fresh[1].Kind);
        Assert.AreEqual(ItemData.RedPotion, fresh[1].Id);
        Assert.AreEqual(2, fresh[1].Count);
        Assert.AreEqual(DropKind.Item, fresh[2].Kind);
        Assert.AreEqual(ItemData.BluePotion, fresh[2].Id);
        Assert.AreEqual(1, fresh[2].Count);
        Assert.AreEqual(DropKind.Item, fresh[3].Kind);
        Assert.AreEqual(ItemData.Mop, fresh[3].Id);
        Assert.AreEqual(1, fresh[3].Count);
        Assert.AreEqual(0, world.Tick(299999, Solo(900)).Spawns.Count);
        var spawns = world.Tick(1, Solo(900)).Spawns;
        Assert.AreEqual(1, spawns.Count);
        Assert.AreEqual(6130101, spawns[0].Mob);
        Assert.AreEqual(100f, spawns[0].X);
    }

    // Boss touch reaches wide: the player stands at dx 55 (touch sees 51
    // after the pace step): the small box (35) misses but the boss box
    // (60) lands (both mobs pace the same flat, so only the box differs).
    [TestMethod]
    public void Boss_TouchReachesWide()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40);
        var normal = new MobWorld(assets.Dir);
        normal.EnterMap(7);
        Assert.AreEqual(0, normal.Tick(100, Solo(155)).Hurts.Count);

        using var assets2 = new TempAssets();
        assets2.AddMap(7, """[{"MobId":6130101,"X":100,"Cy":100}]""", Flat);
        assets2.AddMob(6130101, 40, boss: true);
        var boss = new MobWorld(assets2.Dir);
        boss.EnterMap(7);
        Assert.AreEqual(1, boss.Tick(100, Solo(155)).Hurts.Count);
    }

    // Orange rare: seed 4 pays the sergeant coat (the 5% roll; the
    // potion seeds pin their drops the same way).
    [TestMethod]
    public void Orange_KillMayDropSergeantCoat()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100004,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100004, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var hits = world.Strike(7, 60, 100, true, new Random(4), Progression.Beginner);
        Assert.AreEqual(0, hits[0].HpLeft);
        var coat = world.TakeDrops().Find(d => d.Kind == DropKind.Item && d.Id == ItemData.Sergeant);
        Assert.IsNotNull(coat);
        Assert.AreEqual(1, coat.Count);
    }

    // Same seed, wrong mob: the snail never pays the sergeant coat (the
    // drop is gated on mob 100004, not on luck).
    [TestMethod]
    public void Snail_KillNeverDropsSergeantCoat()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var hits = world.Strike(7, 60, 100, true, new Random(4), Progression.Beginner);
        Assert.AreEqual(0, hits[0].HpLeft);
        Assert.IsNull(world.TakeDrops().Find(d => d.Kind == DropKind.Item && d.Id == ItemData.Sergeant));
    }

    // Pickup: the drop must exist, sit on the taker's map, and be near. Gone
    // means gone (the second grab misses), far and wrong-map miss too.
    [TestMethod]
    public void Pickup_ClaimsOnceAndValidatesReach()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100},{"MobId":100000,"X":150,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.Strike(7, 110, 100, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        Assert.IsTrue(fresh.Count >= 2);
        var first = world.Pickup(fresh[0].Uid, 7, fresh[0].X, fresh[0].Y);
        Assert.IsNotNull(first);
        Assert.AreEqual(fresh[0].Uid, first!.Uid);
        Assert.IsNull(world.Pickup(fresh[0].Uid, 7, fresh[0].X, fresh[0].Y)); // already taken
        Assert.IsNull(world.Pickup(fresh[1].Uid, 7, fresh[1].X + 500, fresh[1].Y)); // too far
        Assert.IsNull(world.Pickup(fresh[1].Uid, 8, fresh[1].X, fresh[1].Y)); // wrong map
        Assert.IsNull(world.Pickup(fresh[1].Uid, 7, fresh[1].X + MobWorld.PickDx + 1, fresh[1].Y)); // one past: out
        Assert.IsNotNull(world.Pickup(fresh[1].Uid, 7, fresh[1].X + MobWorld.PickDx, fresh[1].Y + MobWorld.PickDy)); // box edge: in
        Assert.AreEqual(fresh.Count - 2, world.DropsOn(7).Count);
    }

    // Unclaimed drops fade after 60s (the take list clears client views).
    [TestMethod]
    public void Drop_ExpiresAfter60s()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        int drops = world.TakeDrops().Count;
        Assert.AreEqual(0, world.Tick(59999, Solo(900)).Takes.Count);
        Assert.AreEqual(drops, world.Tick(1, Solo(900)).Takes.Count);
        Assert.AreEqual(0, world.DropsOn(7).Count);
    }

    // Potions drop sometimes (fixed seeds: the first red lands on seed 7,
    // the first blue on seed 12 — the test fails if the rolls ever drift).
    [TestMethod]
    public void Kill_DropsPotionsSometimes()
    {
        int red = -1, blue = -1;
        for (int seed = 0; seed < 60 && (red < 0 || blue < 0); seed++)
        {
            using var assets = new TempAssets();
            assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
            assets.AddMob(100000, 1);
            var world = new MobWorld(assets.Dir);
            world.EnterMap(7);
            world.Strike(7, 60, 100, true, new Random(seed), Progression.Beginner);
            foreach (var d in world.TakeDrops())
            {
                if (red < 0 && d.Kind == DropKind.Item && d.Id == ItemData.RedPotion) red = seed;
                if (blue < 0 && d.Kind == DropKind.Item && d.Id == ItemData.BluePotion) blue = seed;
            }
        }
        Assert.AreEqual(7, red);
        Assert.AreEqual(12, blue);
    }

    // VR walls turn the mob in place (no slide past, no teleport onto the
    // wall): the next tick walks away.
    [TestMethod]
    public void Tick_VrWallTurnsInPlace()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":10,"Cy":100}]""", Flat, 0, -1000, 1000, 1000);
        assets.AddMob(100000, 40, 0, 35, 5, 40, 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var hit = world.Tick(1000, Solo(900)).Moves;
        Assert.AreEqual(10f, hit[0].X); // clamped stride (10-40): stayed, turned
        Assert.IsTrue(hit[0].FacingRight);
        var away = world.Tick(1000, Solo(900)).Moves;
        Assert.AreEqual(50f, away[0].X);
    }

    // 16ms local ticks keep their fractions (60 x 0.64px = 38.4px): no freeze.
    [TestMethod]
    public void Tick_SmallStepsKeepFractions()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40, 0, 35, 5, 40, 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        int moves = 0;
        for (int i = 0; i < 60; i++)
            moves += world.Tick(16, Solo(900)).Moves.Count;
        Assert.AreEqual(60, moves);
        Assert.AreEqual(61.6, world.MobsOn(7)[0].X, 0.01);
    }

    // A mob killed mid-fall leaves its loot on the ground below, not hanging.
    [TestMethod]
    public void Kill_MidFall_DropsLandOnGround()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":50}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.Tick(10, Solo(900)); // airborne: y 50 -> 57
        world.Strike(7, 60, 57, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        Assert.IsTrue(fresh.Count >= 1);
        Assert.AreEqual(100f, fresh[0].Y);
    }

    // Drops never land past the VR walls (a kill at the edge clamps them in).
    [TestMethod]
    public void Kill_AtVrEdge_DropsClampInside()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":5,"Cy":100}]""", Flat, 0, -1000, 1000, 1000);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.Strike(7, -40, 100, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        Assert.IsTrue(fresh.Count >= 1);
        Assert.AreEqual(0f, fresh[0].X); // mesos at 5-7: clamped to the wall
        Assert.IsTrue(fresh.All(d => d.X >= 0));
    }

    // Over the void there is no ground: the drop keeps the kill height.
    [TestMethod]
    public void Kill_OverVoid_DropsKeepKillHeight()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""");
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.Tick(10, Solo(900)); // no ground: y 100 -> 107
        world.Strike(7, 60, 107, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        Assert.IsTrue(fresh.Count >= 1);
        Assert.AreEqual(107f, fresh[0].Y);
    }

    // Rush never shoves a mob past the VR walls (or Walk would flip in place).
    [TestMethod]
    public void Rush_ClampsToVr()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":10,"Cy":100}]""", Flat, 0, -1000, 1000, 1000);
        assets.AddMob(100000, 99999, 0, 35, 5, 40, 200);
        assets.AddSkill(SkillData.Rush, 160, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        world.SkillStrike(7, 100, 100, false, new Random(0), Progression.Beginner, SkillData.Rush, 200);
        Assert.AreEqual(0f, world.MobsOn(7)[0].X);
        world.Tick(100, Solo(900)); // wall: turns in place
        Assert.AreEqual(0f, world.MobsOn(7)[0].X);
        world.Tick(100, Solo(900)); // walks away: no flip loop
        Assert.AreEqual(4f, world.MobsOn(7)[0].X);
    }

    // Two mobs on one player: one tick lands two hurts (the client drops the
    // extras after death; the world just reports every touch).
    [TestMethod]
    public void Tick_TwoMobsTwoHurts()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100},{"MobId":100000,"X":120,"Cy":100}]""", Flat);
        assets.AddMob(100000, 40, 0, 35, 5, 40, 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var hurts = world.Tick(100, Solo(110)).Hurts;
        Assert.AreEqual(2, hurts.Count);
        Assert.AreEqual(1, hurts[0].Victim);
        Assert.AreEqual(1, hurts[1].Victim);
        Assert.AreNotEqual(hurts[0].Uid, hurts[1].Uid);
    }

    // Bad AI stats fail loud at load (zero speed, negative touch/aggro).
    [TestMethod]
    public void EnterMap_BadAiStats_Throws()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        foreach ((int touch, int speed, int aggro) in new[] { (5, 0, 200), (-1, 40, 200), (5, 40, -1) })
        {
            assets.AddMob(100000, 40, 0, 35, touch, speed, aggro);
            Assert.ThrowsException<InvalidDataException>(() => new MobWorld(assets.Dir).EnterMap(7));
        }
    }

    // ApplyHurt: DEF% blocks its share (min 1 gets through), HP floors at 0.
    // Returns the damage dealt (the client shows it; Hp 0 means dead).
    [TestMethod]
    public void ApplyHurt_BlocksDefAndFloorsAtZero()
    {
        var chara = new CharaSlot { Hp = 50 };
        Assert.AreEqual(10, Progression.ApplyHurt(chara, 10, 0));
        Assert.AreEqual(40, chara.Hp);
        Assert.AreEqual(5, Progression.ApplyHurt(chara, 10, 50));
        Assert.AreEqual(35, chara.Hp);
        Assert.AreEqual(1, Progression.ApplyHurt(chara, 1, 90)); // min 1
        chara.Hp = 3;
        Assert.AreEqual(100, Progression.ApplyHurt(chara, 100, 0));
        Assert.AreEqual(0, chara.Hp);
    }

    // A Touch-0 mob is harmless (0 in, 0 out, HP untouched).
    [TestMethod]
    public void ApplyHurt_ZeroTouchIsHarmless()
    {
        var chara = new CharaSlot { Hp = 50 };
        Assert.AreEqual(0, Progression.ApplyHurt(chara, 0, 0));
        Assert.AreEqual(50, chara.Hp);
    }

    // Full loop over TCP: moves flow at 10Hz, the touch lands on the victim
    // only, the kill drops mesos on both screens, and the pickup takes on
    // both (first grab wins: the take carries the loot to the taker).
    [TestMethod]
    public void Loopback_MovesHurtsDropsAndPickup()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""",
            """[{"X1":-500,"Y1":100,"X2":500,"Y2":100,"Layer":0}]""");
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 11);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 11);
        b.Connect("127.0.0.1", Net.Port + 11);

        a.SendState(9, 10, 100, Net.ActStand, 0, true); // in touch range
        int uid = WaitTakeMob(a).Uid;
        b.SendState(9, 400, 100, Net.ActStand, 0, true); // far: sees moves, never hurts
        Assert.AreEqual(uid, WaitTakeMob(b).Uid);

        var mv = WaitTakeMove(a);
        Assert.AreEqual(uid, mv.Uid);
        Assert.AreEqual(9, mv.Map);
        var hurt = WaitTakeHurt(a);
        Assert.AreEqual(uid, hurt.Uid);
        Assert.AreEqual(100000, hurt.Mob);
        Assert.AreEqual(5, hurt.Damage);
        Thread.Sleep(300);
        Assert.IsFalse(b.TryTakeHurt(out _)); // victim-only: b never hears it

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0)); // the swings below one-shot
        MobEvent hit = default;
        for (int i = 0; i < 10; i++)
        {
            a.SendAttack();
            hit = WaitTakeMob(a);
            if (hit.HpLeft == 0) break;
        }
        Assert.AreEqual(0, hit.HpLeft);
        // The kill left its piles (mesos always, a potion sometimes): let
        // them all land, then drain them (only spawns sit queued so far).
        Thread.Sleep(300);
        var piles = new List<DropEvent>();
        while (a.TryTakeDrop(out var p)) piles.Add(p);
        Assert.IsTrue(piles.Count >= 1 && piles.All(p => p.Spawn));
        var mesos = piles.First(p => p.Kind == DropKind.Mesos);
        var pilesB = new List<DropEvent>();
        while (b.TryTakeDrop(out var p)) pilesB.Add(p);
        Assert.AreEqual(piles.Count, pilesB.Count); // both screens see the loot
        a.SendPickup(mesos.Uid);
        var takeA = WaitTakeDrop(a);
        Assert.IsFalse(takeA.Spawn);
        Assert.AreEqual(a.PlayerId, takeA.Taker);
        Assert.AreEqual(mesos.Id, takeA.Id); // the take carries the loot
        var takeB = WaitTakeDrop(b);
        Assert.AreEqual(a.PlayerId, takeB.Taker);
    }

    private static MobEvent WaitTakeMob(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeMob(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a mob event");
        throw new InvalidOperationException("unreachable");
    }

    private static MoveEvent WaitTakeMove(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeMove(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a mob move");
        throw new InvalidOperationException("unreachable");
    }

    private static HurtEvent WaitTakeHurt(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeHurt(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a hurt");
        throw new InvalidOperationException("unreachable");
    }

    private static DropEvent WaitTakeDrop(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeDrop(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a drop event");
        throw new InvalidOperationException("unreachable");
    }
}
