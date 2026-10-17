using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The quest protocol over loopback: the client syncs its save's state, the
// server judges intents and kills on its own copy, QuestOk/QuestKill come
// back. Silence (Sleep + no event) proves the server said no.
[TestClass]
public sealed class QuestProtocolTests
{
    private const int Hunt = 90001;
    private const int Snail = 100000;

    private static CharaSlot Chara(int level, params int[] taken)
    {
        var chara = new CharaSlot { Name = "hero", Level = level };
        chara.Quests.AddRange(taken);
        return chara;
    }

    // An accept lands (QuestOk) and sticks: the repeat fails on the
    // server's copy, so it answers nothing.
    [TestMethod]
    public void Loopback_Accept_AckedThenRepeatSilent()
    {
        using var assets = new TempAssets();
        assets.AddMap(9);
        assets.AddQuest(Hunt, Snail, 2);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 13);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 13);

        a.SendQuestSync(Chara(7));
        a.SendQuestAccept(Hunt);
        var ok = WaitTakeQuest(a);
        Assert.IsFalse(ok.Kill);
        Assert.AreEqual(Hunt, ok.Quest);
        Assert.IsFalse(ok.Completed);

        a.SendQuestAccept(Hunt);
        Thread.Sleep(300);
        Assert.IsFalse(a.TryTakeQuest(out _)); // already taken there: silence
    }

    // A turn-in before the kills answers nothing: the server validates.
    [TestMethod]
    public void Loopback_Complete_BeforeKills_Silent()
    {
        using var assets = new TempAssets();
        assets.AddMap(9);
        assets.AddQuest(Hunt, Snail, 2);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 14);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 14);

        a.SendQuestSync(Chara(7, Hunt));
        a.SendQuestComplete(Hunt);
        Thread.Sleep(300);
        Assert.IsFalse(a.TryTakeQuest(out _)); // 0/2 kills: silence
    }

    // Two snails, one swing: both kills tick the server's copy (counts 1
    // then 2), and the finished hunt turns in with QuestOk.
    [TestMethod]
    public void Loopback_Kills_PushDeltas_ThenComplete()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100},{"MobId":100000,"X":40,"Cy":100}]""");
        assets.AddMob(Snail, 40);
        assets.AddQuest(Hunt, Snail, 2);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 15);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 15);

        a.SendQuestSync(Chara(7, Hunt));
        a.SendState(9, -50, 100, Net.ActStand, 0, true); // both snails ahead of the swing
        WaitTakeMob(a); // spawns (two snails listed)
        WaitTakeMob(a);
        a.SendBuild(new Build(99, 99, 99, 0, 0, 0)); // the swing below one-shots
        a.SendAttack();
        Assert.AreEqual(0, WaitTakeMob(a).HpLeft);
        Assert.AreEqual(0, WaitTakeMob(a).HpLeft);

        var first = WaitTakeQuest(a);
        Assert.IsTrue(first.Kill);
        Assert.AreEqual(Hunt, first.Quest);
        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(2, WaitTakeQuest(a).Count);

        a.SendQuestComplete(Hunt);
        var ok = WaitTakeQuest(a);
        Assert.IsFalse(ok.Kill);
        Assert.AreEqual(Hunt, ok.Quest);
        Assert.IsTrue(ok.Completed);
    }

    // A quest the host never loaded (joiner customs) answers nothing.
    [TestMethod]
    public void Loopback_UnknownQuest_Silent()
    {
        using var assets = new TempAssets();
        assets.AddMap(9);
        assets.AddQuest(Hunt, Snail, 2);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 16);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 16);

        a.SendQuestSync(Chara(7));
        a.SendQuestAccept(99999);
        Thread.Sleep(300);
        Assert.IsFalse(a.TryTakeQuest(out _));
    }

    // An intent before any Sync (CLI, or a rushed client) answers nothing.
    [TestMethod]
    public void Loopback_PreSyncIntent_Silent()
    {
        using var assets = new TempAssets();
        assets.AddMap(9);
        assets.AddQuest(Hunt, Snail, 2);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 17);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 17);

        a.SendQuestAccept(Hunt);
        Thread.Sleep(300);
        Assert.IsFalse(a.TryTakeQuest(out _));
    }

    // The synced level gates accepts: Lv 1 cannot take the Lv 10 hunt.
    [TestMethod]
    public void Loopback_LevelGate_JudgedServerSide()
    {
        using var assets = new TempAssets();
        assets.AddMap(9);
        assets.AddQuest(Hunt, Snail, 2, lvMin: 10);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 18);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 18);

        a.SendQuestSync(Chara(1));
        a.SendQuestAccept(Hunt);
        Thread.Sleep(300);
        Assert.IsFalse(a.TryTakeQuest(out _));
    }

    // A re-sync upgrades the mirror: Lv 1 cannot take the Lv 10 hunt, but
    // the same client at Lv 10 can (this also runs the merge branch: the
    // second Sync lands on an existing copy).
    [TestMethod]
    public void Loopback_Resync_UpgradesMirror()
    {
        using var assets = new TempAssets();
        assets.AddMap(9);
        assets.AddQuest(Hunt, Snail, 2, lvMin: 10);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 19);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 19);

        a.SendQuestSync(Chara(1));
        a.SendQuestAccept(Hunt);
        Thread.Sleep(300);
        Assert.IsFalse(a.TryTakeQuest(out _)); // too young: silence

        a.SendQuestSync(Chara(10));
        a.SendQuestAccept(Hunt);
        var ok = WaitTakeQuest(a);
        Assert.IsFalse(ok.Kill);
        Assert.AreEqual(Hunt, ok.Quest);
        Assert.IsFalse(ok.Completed);
    }

    // BumpKills reports each bumped objective with its new count, skips
    // capped ones and passing mobs: the server pushes exactly these.
    [TestMethod]
    public void BumpKills_ReportsNewCounts_SkipsCapped()
    {
        var log = new QuestLog(new[] { new QuestData
        {
            QuestId = Hunt,
            Kills = new List<QuestKill> { new() { Mob = Snail, Count = 2 }, new() { Mob = 100004, Count = 1 } },
        } }.ToDictionary(q => q.QuestId));
        var chara = Chara(7, Hunt);

        Assert.AreEqual((Hunt, 0, 1), log.BumpKills(chara, Snail).Single());
        Assert.AreEqual((Hunt, 0, 2), log.BumpKills(chara, Snail).Single());
        Assert.AreEqual(0, log.BumpKills(chara, Snail).Count); // capped: no change, no delta
        Assert.AreEqual((Hunt, 1, 1), log.BumpKills(chara, 100004).Single());
        Assert.AreEqual(0, log.BumpKills(chara, 999999).Count); // nobody hunts it
    }

    private static QuestEvent WaitTakeQuest(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeQuest(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a quest event");
        throw new InvalidOperationException("unreachable");
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
