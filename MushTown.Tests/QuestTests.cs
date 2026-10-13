using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class QuestTests
{
    private static CharaSlot Chara(int level = 7) => new() { Name = "hero", Level = level };

    private static QuestData KillQuest(int id = 90001, int mob = 100000, int count = 5) => new()
    {
        QuestId = id,
        Name = "Snail Hunt",
        Npc0 = 10000,
        Npc1 = 10000,
        LvMin = 1,
        Kills = new List<QuestKill> { new() { Mob = mob, Count = count } },
    };

    // Accept gates: level band, pre quests, item need, and repeat state.
    [TestMethod]
    public void Accept_GatesLevelPreItemsAndState()
    {
        var q = KillQuest();
        var accepted = Chara();
        Assert.IsTrue(QuestLog.Accept(accepted, q));
        Assert.IsFalse(QuestLog.Accept(accepted, q)); // already accepted

        var done = Chara();
        done.QuestsDone.Add(q.QuestId);
        Assert.IsFalse(QuestLog.CanAccept(done, q));
        Assert.IsFalse(QuestLog.Accept(done, q));

        Assert.IsFalse(QuestLog.CanAccept(Chara(0), KillQuest())); // below LvMin
        Assert.IsFalse(QuestLog.CanAccept(Chara(9), new QuestData { QuestId = 2, LvMin = 1, LvMax = 8 }));
        Assert.IsTrue(QuestLog.CanAccept(Chara(8), new QuestData { QuestId = 2, LvMin = 1, LvMax = 8 }));
        Assert.IsTrue(QuestLog.CanAccept(Chara(99), KillQuest())); // LvMax 0 = no cap
        Assert.IsFalse(QuestLog.CanAccept(Chara(), new QuestData { QuestId = 3, Pre = new List<int> { 1019 } }));
        var pre = Chara();
        pre.QuestsDone.Add(1019);
        Assert.IsTrue(QuestLog.CanAccept(pre, new QuestData { QuestId = 3, Pre = new List<int> { 1019 } }));
        Assert.IsFalse(QuestLog.CanAccept(Chara(), new QuestData { QuestId = 4, NeedItemsToAccept = true }));
    }

    // Kills tick only accepted quests hunting this mob, capped per objective.
    [TestMethod]
    public void BumpKills_CapsPerObjective()
    {
        var chara = Chara();
        var hunted = KillQuest(90001);
        var other = KillQuest(90009, mob: 100001);
        QuestLog.Accept(chara, hunted);
        QuestLog.BumpKills(chara, new[] { hunted, other }, 100001); // wrong mob for 90001
        Assert.AreEqual(0, QuestLog.KillCount(chara, 90001, 0));
        Assert.AreEqual(0, QuestLog.KillCount(chara, 90009, 0)); // never accepted
        for (int i = 0; i < 7; i++)
            QuestLog.BumpKills(chara, new[] { hunted, other }, 100000);
        Assert.AreEqual(5, QuestLog.KillCount(chara, 90001, 0));
    }

    // Complete moves accepted -> done only when every kill lands (item quests
    // never complete; talk quests complete on turn-in with no kills at all).
    [TestMethod]
    public void Complete_MovesAcceptedToDoneOnlyWhenComplete()
    {
        var chara = Chara();
        var q = KillQuest();
        Assert.IsFalse(QuestLog.IsComplete(chara, q)); // not accepted yet
        QuestLog.Accept(chara, q);
        chara.QuestKills["90001:0"] = 4;
        Assert.IsFalse(QuestLog.Complete(chara, q));
        chara.QuestKills["90001:0"] = 5;
        Assert.IsTrue(QuestLog.Complete(chara, q));
        Assert.IsFalse(chara.Quests.Contains(90001));
        Assert.IsTrue(chara.QuestsDone.Contains(90001));

        var item = KillQuest(90003);
        item.NeedItemsToComplete = true;
        QuestLog.Accept(chara, item);
        chara.QuestKills["90003:0"] = 5;
        Assert.IsFalse(QuestLog.IsComplete(chara, item));

        var talk = new QuestData { QuestId = 90002, Npc0 = 1541000, Npc1 = 1541000 };
        QuestLog.Accept(chara, talk);
        Assert.IsTrue(QuestLog.IsComplete(chara, talk));
        Assert.IsTrue(QuestLog.Complete(chara, talk));
        Assert.IsFalse(QuestLog.Complete(Chara(), KillQuest(90005))); // never accepted
    }

    // One mob shared by two objectives: both tick, each capped on its own.
    [TestMethod]
    public void BumpKills_SameMobTicksBothObjectives()
    {
        var chara = Chara();
        var q = new QuestData
        {
            QuestId = 6,
            Kills = new List<QuestKill>
            {
                new() { Mob = 100000, Count = 2 },
                new() { Mob = 100000, Count = 3 },
            },
        };
        QuestLog.Accept(chara, q);
        QuestLog.BumpKills(chara, new[] { q }, 100000);
        Assert.AreEqual(1, QuestLog.KillCount(chara, 6, 0));
        Assert.AreEqual(1, QuestLog.KillCount(chara, 6, 1));
        for (int i = 0; i < 5; i++)
            QuestLog.BumpKills(chara, new[] { q }, 100000);
        Assert.AreEqual(2, QuestLog.KillCount(chara, 6, 0));
        Assert.AreEqual(3, QuestLog.KillCount(chara, 6, 1));
    }

    // Turn-in archives the counters: nothing reads them once the quest leaves.
    [TestMethod]
    public void Complete_ClearsKillCounters()
    {
        var chara = Chara();
        var q = KillQuest();
        q.Kills.Add(new QuestKill { Mob = 100001, Count = 2 });
        QuestLog.Accept(chara, q);
        chara.QuestKills["90001:0"] = 5;
        chara.QuestKills["90001:1"] = 2;
        Assert.IsTrue(QuestLog.Complete(chara, q));
        Assert.IsFalse(chara.QuestKills.ContainsKey("90001:0"));
        Assert.IsFalse(chara.QuestKills.ContainsKey("90001:1"));
    }

    // Live codes: hero, NPC/mob names, and the running kill counter.
    [TestMethod]
    public void Render_ReplacesLiveCodes()
    {
        var chara = Chara();
        chara.QuestKills["1016:0"] = 3;
        string s = QuestText.Render(
            "Hi #h0#, hunt #o100100#s for #p12100# (#a10161# of 5) in map #m60000#.",
            chara, id => $"mob{id}", id => $"npc{id}");
        Assert.AreEqual("Hi hero, hunt mob100100s for npc12100 (3 of 5) in map map 60000.", s);
    }

    // Dead codes: formats, icons, options, counters, and CRLF normalize away.
    [TestMethod]
    public void Render_StripsDeadCodes()
    {
        string s = QuestText.Render(
            "#bBold#k #eEvent#n #rRed#k#i4036846:# #L1#Pick me#l #questorder1# #t4036846:# x#c4036846#\r\nNext",
            Chara(), id => $"mob{id}", id => $"npc{id}");
        Assert.AreEqual("Bold Event Red Pick me  item 4036846 x0\nNext", s);
        Assert.AreEqual("NPC x", QuestText.Render("#px#", Chara(), id => "", id => ""));
        Assert.AreEqual("mob x", QuestText.Render("#ox#", Chara(), id => "", id => ""));
        Assert.AreEqual("Pick me", QuestText.Render("#L1#Pick me#l#", Chara(), id => "", id => "")); // close with #
        Assert.AreEqual("0", QuestText.Render("#a99999999999#", Chara(), id => "", id => "")); // huge qid: 0, no throw
    }
}
