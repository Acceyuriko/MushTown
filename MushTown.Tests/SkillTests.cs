using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SkillTests
{
    private const int Slash = SkillData.SlashBlast;
    private const int Leap = SkillData.WarLeap;

    // SkillCalc = the panel roll times the skill's percent, then the same
    // defense/min-1 tail. Beginner 5..29 at 150% pins 7..43, a crit 65.
    [TestMethod]
    public void SkillCalc_ScalesPanelRoll()
    {
        Assert.AreEqual(7, Damage.SkillCalc(0.0, 1.0, 0.0, Progression.Beginner, 150));
        Assert.AreEqual(43, Damage.SkillCalc(1.0, 1.0, 0.0, Progression.Beginner, 150));
        Assert.AreEqual(21, Damage.SkillCalc(1.0, 1.0, 0.5, Progression.Beginner, 150));
        Assert.AreEqual(1, Damage.SkillCalc(0.0, 1.0, 0.99, Progression.Beginner, 150));
        Assert.AreEqual(65, Damage.SkillCalc(1.0, 0.0, 0.0, new Build(4, 4, 32, 0, 100, 0), 150));
    }

    // The cast gate: the skill must be learned, and level, MP, and
    // cooldown must all pass.
    [TestMethod]
    public void CanCast_GatesLevelMpAndCooldown()
    {
        var skill = new SkillData { ReqLevel = 3, Mp = 5 };
        Assert.IsTrue(Progression.CanCast(new CharaSlot { Level = 5, Mp = 10 }, skill, 1, 0));
        Assert.IsFalse(Progression.CanCast(new CharaSlot { Level = 2, Mp = 10 }, skill, 1, 0)); // too young
        Assert.IsFalse(Progression.CanCast(new CharaSlot { Level = 5, Mp = 4 }, skill, 1, 0)); // too dry
        Assert.IsFalse(Progression.CanCast(new CharaSlot { Level = 5, Mp = 10 }, skill, 1, 1)); // still cooling
        Assert.IsFalse(Progression.CanCast(new CharaSlot { Level = 5, Mp = 10 }, skill, 0, 0)); // never learned
    }

    // Utility skills are never judged: a zero-damage skill fails loud.
    [TestMethod]
    public void SkillStrike_UtilitySkill_Throws()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        assets.AddSkill(Leap, 0, 0, 0, 0);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        Assert.ThrowsException<InvalidDataException>(() =>
            world.SkillStrike(7, 0, 100, true, new Random(1), Progression.Beginner, Leap, 0));
    }

    // The ring catches mobs in every direction (behind counts too), nearest
    // first, capped at the mob count. Hits carry the skill id and EXP.
    [TestMethod]
    public void SkillStrike_RingHitsNearestWithinCap()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100},{"MobId":100000,"X":10,"Cy":100},{"MobId":100000,"X":500,"Cy":100},{"MobId":100000,"X":-90,"Cy":100}]""");
        assets.AddMob(100000, 100000, 0, 35);
        assets.AddSkill(Slash, 160, 2);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var spawns = world.MobsOn(7);
        var hits = world.SkillStrike(7, 0, 100, true, new Random(2), Progression.Beginner, Slash, 150);
        Assert.AreEqual(2, hits.Count); // the 500px mob is out, the cap drops one more
        Assert.AreEqual(spawns[1].Uid, hits[0].Uid); // x=10 first: nearest wins
        Assert.AreEqual(spawns[3].Uid, hits[1].Uid); // x=-90 next: behind counts too
        Assert.IsTrue(hits[0].Damage is >= 7 and <= 43);
        Assert.AreEqual(Slash, hits[0].Skill);
        Assert.AreEqual(35, hits[0].Exp);
    }

    // Ring edge: exactly on the ring counts, one pixel past misses.
    [TestMethod]
    public void SkillStrike_RingEdges()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":160,"Cy":100},{"MobId":100000,"X":161,"Cy":100}]""");
        assets.AddMob(100000, 100000);
        assets.AddSkill(Slash, 160, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hits = world.SkillStrike(7, 0, 100, true, new Random(3), Progression.Beginner, Slash, 150);
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual(world.MobsOn(7)[0].Uid, hits[0].Uid);
    }

    [TestMethod]
    public void SkillStrike_Kill_RemovesFromMap()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        assets.AddSkill(Slash, 160, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var rng = new Random(4);
        MobWorld.Hit last = world.SkillStrike(7, 0, 100, true, rng, Progression.Beginner, Slash, 150)[0];
        for (int i = 0; i < 10 && last.HpLeft > 0; i++)
            last = world.SkillStrike(7, 0, 100, true, rng, Progression.Beginner, Slash, 150)[0];
        Assert.AreEqual(0, last.HpLeft);
        Assert.AreEqual(0, world.MobsOn(7).Count);
        Assert.AreEqual(0, world.SkillStrike(7, 0, 100, true, rng, Progression.Beginner, Slash, 150).Count);
    }

    // Id routing: a damaging skill judges, a known utility relays, an
    // unknown id fails loud (the client must never send one).
    [TestMethod]
    public void IsDamageSkill_RoutesByTable()
    {
        using var assets = new TempAssets();
        assets.AddSkill(Slash, 160, 6);
        assets.AddSkill(Leap, 0, 0, 0, 0);
        var world = new MobWorld(assets.Dir);
        Assert.IsTrue(world.IsDamageSkill(Slash));
        Assert.IsFalse(world.IsDamageSkill(Leap));
        Assert.ThrowsException<InvalidDataException>(() => world.IsDamageSkill(999999));
    }

    // Final Attack: a learned proc adds one bonus hit on the nearest
    // survivor, tagged with the Final Attack skill id.
    [TestMethod]
    public void SkillStrike_FinalAttack_BonusHit()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100},{"MobId":100000,"X":50,"Cy":100}]""");
        assets.AddMob(100000, 100000);
        assets.AddSkill(Slash, 160, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var proc = new Build(4, 4, 32, 0, 0, 100);
        var hits = world.SkillStrike(7, 0, 100, true, new Random(5), proc, Slash, 150);
        Assert.AreEqual(3, hits.Count); // two mains + one bonus
        Assert.AreEqual(SkillData.FinalAttack, hits[2].Skill);
        Assert.AreEqual(world.MobsOn(7)[0].Uid, hits[2].Uid); // nearest survivor
        var noProc = world.SkillStrike(7, 0, 100, true, new Random(6), Progression.Beginner, Slash, 150);
        Assert.AreEqual(2, noProc.Count); // unlearned: no bonus hit
    }

    // Rush shoves every mob it hits 60px along the caster's facing.
    [TestMethod]
    public void SkillStrike_Rush_PushesMobs()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 100000);
        assets.AddSkill(SkillData.Rush, 160, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hits = world.SkillStrike(7, -50, 100, true, new Random(7), Progression.Beginner, SkillData.Rush, 200);
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual(60f, world.MobsOn(7)[0].X);
        world.SkillStrike(7, 110, 100, false, new Random(8), Progression.Beginner, SkillData.Rush, 200);
        Assert.AreEqual(0f, world.MobsOn(7)[0].X); // facing left shoves back
    }

    // Full loop over TCP: a Slash Blast is judged (both clients see the
    // skill-tagged hit), and the peer gets the effect relay. The caster
    // plays its own effect live: no echo lands in its queue (it would arrive
    // before its own hit on the same stream, so this read is exact).
    [TestMethod]
    public void Loopback_SkillCast_JudgesAndRelays()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 100000, 0, 35);
        assets.AddSkill(Slash, 160, 6);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 9);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 9);
        b.Connect("127.0.0.1", Net.Port + 9);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(b).Spawn);
        a.SendSkillCast(Slash, 150);
        var fx = WaitTakeFx(b);
        Assert.AreEqual(a.PlayerId, fx.Attacker);
        Assert.AreEqual(Slash, fx.Skill);
        Assert.AreEqual(9, fx.Map);
        Assert.AreEqual(-50f, fx.X);
        Assert.AreEqual(100f, fx.Y);
        Assert.IsTrue(fx.FacingRight);
        var atA = WaitTakeMob(a);
        var atB = WaitTakeMob(b);
        Assert.IsFalse(atA.Spawn);
        Assert.AreEqual(atA.Uid, atB.Uid);
        Assert.AreEqual(atA.Damage, atB.Damage);
        Assert.AreEqual(Slash, atA.Skill);
        Assert.AreEqual(a.PlayerId, atA.Attacker);
        Assert.AreEqual(35, atA.Exp);
        Assert.IsFalse(a.TryTakeFx(out _)); // no echo for the caster
    }

    // War Leap moves on the caster's own screen: the server relays the
    // effect and judges nothing. The kill below proves it: a phantom leap
    // hit would land in the queue ahead of the killing blow.
    [TestMethod]
    public void Loopback_WarLeap_RelaysOnly()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        assets.AddSkill(Slash, 160, 6);
        assets.AddSkill(Leap, 0, 0, 0, 0); // known id, zero damage: relay-only
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 10);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 10);
        b.Connect("127.0.0.1", Net.Port + 10);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.IsTrue(WaitTakeMob(b).Spawn);
        a.SendBuild(new Build(99, 99, 99, 0, 0, 0)); // the slash below one-shots
        a.SendSkillCast(Leap, 0);
        var fx = WaitTakeFx(b);
        Assert.AreEqual(a.PlayerId, fx.Attacker);
        Assert.AreEqual(Leap, fx.Skill);
        a.SendSkillCast(Slash, 150);
        Assert.AreEqual(Slash, WaitTakeFx(b).Skill);
        Assert.AreEqual(0, WaitTakeMob(b).HpLeft); // first hit is the kill: the leap judged nothing
        Assert.AreEqual(0, WaitTakeMob(a).HpLeft);
        Assert.IsFalse(a.TryTakeFx(out _)); // no echo for the caster either
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

    private static SkillFxEvent WaitTakeFx(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeFx(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a skill effect");
        throw new InvalidOperationException("unreachable");
    }
}
