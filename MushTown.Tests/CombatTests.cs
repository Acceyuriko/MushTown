using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class CombatTests
{
    // The v1 beginner (4/4, polearm 32): max range 29, min 5. Each stage below
    // pins one line of Damage.Calc (bwiki structure: range -> roll -> defense
    // -> min 1). Live rolls use NextDouble, so 29 itself never lands; the
    // test pins the formula bound, not a live roll.
    [TestMethod]
    public void Damage_RollsInsideRange()
    {
        Assert.AreEqual(5, Damage.Calc(0.0, 0.0, 4, 4, 32));
        Assert.AreEqual(29, Damage.Calc(1.0, 0.0, 4, 4, 32));
        Assert.AreEqual(17, Damage.Calc(0.5, 0.0, 4, 4, 32));
    }

    [TestMethod]
    public void Damage_DefenseBlocks()
    {
        Assert.AreEqual(14, Damage.Calc(1.0, 0.5, 4, 4, 32));
        Assert.AreEqual(1, Damage.Calc(0.0, 0.99, 4, 4, 32));
    }

    [TestMethod]
    public void MobWorld_SpawnOnce_StrikeHitsInReach()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.EnterMap(7); // re-entry keeps the old bodies
        var spawns = world.MobsOn(7);
        Assert.AreEqual(1, spawns.Count);
        Assert.AreEqual(100000, spawns[0].Mob);
        Assert.AreEqual(0f, spawns[0].X);
        Assert.AreEqual(100f, spawns[0].Y);
        Assert.AreEqual(40, spawns[0].MaxHp);

        var rng = new Random(1);
        Assert.AreEqual(0, world.Strike(7, -500, 100, true, rng, Progression.Beginner).Count); // too far
        Assert.AreEqual(0, world.Strike(7, 50, 100, true, rng, Progression.Beginner).Count); // behind us
        var hits = world.Strike(7, -50, 100, true, rng, Progression.Beginner);
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual(spawns[0].Uid, hits[0].Uid);
        Assert.IsTrue(hits[0].Damage is >= 5 and <= 29);
        Assert.AreEqual(40 - hits[0].Damage, hits[0].HpLeft);
    }

    [TestMethod]
    public void MobWorld_Strike_ReachEdges()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 100000); // one pin per strike, never dies
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var rng = new Random(3);
        Assert.AreEqual(1, world.Strike(7, 50, 100, false, rng, Progression.Beginner).Count); // facing-left hit
        Assert.AreEqual(1, world.Strike(7, 10, 100, true, rng, Progression.Beginner).Count); // 10px behind still counts
        Assert.AreEqual(0, world.Strike(7, 11, 100, true, rng, Progression.Beginner).Count); // 11px behind misses
        Assert.AreEqual(1, world.Strike(7, 0, 160, true, rng, Progression.Beginner).Count); // 60px above counts
        Assert.AreEqual(0, world.Strike(7, 0, 161, true, rng, Progression.Beginner).Count); // 61px above misses
        Assert.AreEqual(1, world.Strike(7, -90, 100, true, rng, Progression.Beginner).Count); // 90px ahead counts
        Assert.AreEqual(0, world.Strike(7, -91, 100, true, rng, Progression.Beginner).Count); // 91px ahead misses
    }

    [TestMethod]
    public void MobWorld_Reenter_KeepsWoundedHp()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hit = world.Strike(7, 0, 100, true, new Random(4), Progression.Beginner)[0];
        world.EnterMap(7); // re-entry re-lists, never respawns
        var spawns = world.MobsOn(7);
        Assert.AreEqual(1, spawns.Count);
        Assert.AreEqual(hit.HpLeft, spawns[0].Hp);
    }

    [TestMethod]
    public void MobWorld_Strike_HitsOnlySameMap()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMap(8, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.EnterMap(8);
        Assert.AreEqual(1, world.Strike(7, 0, 100, true, new Random(5), Progression.Beginner).Count);
        Assert.AreEqual(40, world.MobsOn(8)[0].Hp); // the other map untouched
    }

    // One swing hits every mob in reach, each with its own roll.
    [TestMethod]
    public void MobWorld_Strike_HitsEveryMobInReach()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100},{"MobId":100000,"X":40,"Cy":100}]""");
        assets.AddMob(100000, 100000);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hits = world.Strike(7, -50, 100, true, new Random(6), Progression.Beginner);
        Assert.AreEqual(2, hits.Count);
        Assert.AreNotEqual(hits[0].Uid, hits[1].Uid);
    }

    // Defense scales the rolled damage (half of the 5..29 range here).
    [TestMethod]
    public void MobWorld_Strike_DefReducesDamage()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 100000, 0.5);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hit = world.Strike(7, 0, 100, true, new Random(7), Progression.Beginner)[0];
        Assert.IsTrue(hit.Damage is >= 2 and <= 14);
    }

    [TestMethod]
    public void MobWorld_Kill_RemovesFromMap()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var rng = new Random(2);
        MobWorld.Hit last = world.Strike(7, 0, 100, true, rng, Progression.Beginner)[0];
        for (int i = 0; i < 10 && last.HpLeft > 0; i++)
            last = world.Strike(7, 0, 100, true, rng, Progression.Beginner)[0];
        Assert.AreEqual(0, last.HpLeft);
        Assert.AreEqual(0, world.MobsOn(7).Count);
        Assert.AreEqual(0, world.Strike(7, 0, 100, true, rng, Progression.Beginner).Count); // the dead stay dead
    }

    // Full loop over TCP: entering a map pushes its spawn, an attack comes
    // back as a hit, and repeated swings kill it.
    [TestMethod]
    public void Loopback_Attack_KillsMob()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 3);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 3);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        var spawn = WaitTakeMob(a);
        Assert.IsTrue(spawn.Spawn);
        Assert.AreEqual(100000, spawn.Mob);
        Assert.AreEqual(9, spawn.Map);
        Assert.AreEqual(0f, spawn.X);
        Assert.AreEqual(100f, spawn.Y);
        Assert.AreEqual(40, spawn.MaxHp);
        Assert.AreEqual(40, spawn.HpLeft); // fresh spawn: full HP

        MobEvent hit = default;
        for (int i = 0; i < 10; i++)
        {
            a.SendAttack();
            hit = WaitTakeMob(a);
            Assert.IsFalse(hit.Spawn);
            Assert.AreEqual(spawn.Uid, hit.Uid);
            Assert.AreEqual(9, hit.Map);
            Assert.IsTrue(hit.Damage is >= 5 and <= 29);
            if (hit.HpLeft == 0) break;
        }
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(a.PlayerId, hit.Attacker); // the killer is named
        Assert.AreEqual(0, hit.Skill); // a basic swing tags no skill
    }

    // The server judges swings on the attacker's Build: a strong build
    // one-shots, and the hit carries its attacker and EXP award.
    [TestMethod]
    public void Loopback_Build_UsesRealStats()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40, 0, 35);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 8);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 8);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        a.SendBuild(99, 99, 99); // panel min 450: one swing kills a 40 HP mob
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.IsFalse(hit.Spawn);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(a.PlayerId, hit.Attacker);
        Assert.AreEqual(35, hit.Exp);
    }

    // A client entering later sees the live HP, not MaxHp.
    [TestMethod]
    public void Loopback_LateJoiner_SeesWoundedHp()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 4);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 4);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.IsTrue(hit.HpLeft is > 0 and < 40); // one swing wounds, never kills

        using var b = new NetClient(_ => { });
        b.Connect("127.0.0.1", Net.Port + 4);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        var spawn = WaitTakeMob(b);
        Assert.IsTrue(spawn.Spawn);
        Assert.AreEqual(40, spawn.MaxHp);
        Assert.AreEqual(hit.HpLeft, spawn.HpLeft);
    }

    // An attack before the first State is dropped: the first mob event is
    // still the spawn (not a phantom hit), and exactly one swing is judged.
    [TestMethod]
    public void Loopback_AttackBeforeState_Dropped()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 5);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 5);

        a.SendAttack();
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        var spawn = WaitTakeMob(a);
        Assert.IsTrue(spawn.Spawn);
        Assert.AreEqual(40, spawn.HpLeft);
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(40 - hit.Damage, hit.HpLeft);
    }

    // Leaving and re-entering re-pushes the live HP, not MaxHp.
    [TestMethod]
    public void Loopback_SwitchBack_RepushesWoundedHp()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMap(10);
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 6);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 6);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        a.SendAttack();
        var hit = WaitTakeMob(a);
        a.SendState(10, 0, 0, Net.ActStand, 0, true); // out (its Leave lands in the other queue)
        a.SendState(9, -50, 100, Net.ActStand, 0, true); // back in
        var spawn = WaitTakeMob(a);
        Assert.IsTrue(spawn.Spawn);
        Assert.AreEqual(hit.HpLeft, spawn.HpLeft);
    }

    // Both clients on a map see the same swing land (same uid, damage, HP).
    [TestMethod]
    public void Loopback_SameMap_SeesSameHit()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 7);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 7);
        b.Connect("127.0.0.1", Net.Port + 7);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(b).Spawn);
        a.SendAttack();
        var atA = WaitTakeMob(a);
        var atB = WaitTakeMob(b);
        Assert.IsFalse(atA.Spawn);
        Assert.AreEqual(atA.Uid, atB.Uid);
        Assert.AreEqual(atA.Damage, atB.Damage);
        Assert.AreEqual(atA.HpLeft, atB.HpLeft);
        Assert.AreEqual(a.PlayerId, atA.Attacker);
        Assert.AreEqual(atA.Attacker, atB.Attacker);
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
}
