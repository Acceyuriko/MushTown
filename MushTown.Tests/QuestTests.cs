using MushTown.Shared;
using System.Text.Json;

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

    // Turn-in payout: mesos drop in the purse, potions in the USE bag (a
    // pre-W10 save has no bag yet: the first payout builds it).
    [TestMethod]
    public void GrantRewards_PaysMesoAndPotions()
    {
        var chara = Chara();
        var q = new QuestData
        {
            QuestId = 7,
            RewardMeso = 30,
            RewardItems = new() { new() { Item = ItemData.RedPotion, Count = 3 } },
        };
        QuestLog.GrantRewards(chara, q);
        Assert.AreEqual(30, chara.Mesos);
        Assert.AreEqual(3, chara.Use![ItemData.RedPotion]);
        // Same payout again: stacks like later chain quests paying the same potion.
        QuestLog.GrantRewards(chara, q);
        Assert.AreEqual(60, chara.Mesos);
        Assert.AreEqual(6, chara.Use![ItemData.RedPotion]);
    }

    // Reward preview lines: one short line per reward, empty when no rewards.
    [TestMethod]
    public void RewardLines_FormatsEachReward()
    {
        var q = new QuestData
        {
            QuestId = 8,
            RewardExp = 50,
            RewardMeso = 30,
            RewardItems = new() { new() { Item = ItemData.RedPotion, Count = 3 } },
        };
        CollectionAssert.AreEqual(new[] { "50 EXP", "30 mesos", "Red Potion x3" }, q.RewardLines(_ => "Red Potion"));
        Assert.AreEqual(0, new QuestData().RewardLines(_ => "").Count);
    }

    // Meso-only payouts never touch the bag: a pre-W10 null bag stays null.
    [TestMethod]
    public void GrantRewards_MesoOnlyKeepsNullBag()
    {
        var chara = Chara();
        QuestLog.GrantRewards(chara, new QuestData { QuestId = 9, RewardMeso = 20 });
        Assert.AreEqual(20, chara.Mesos);
        Assert.IsNull(chara.Use);
    }

    // The W15 acceptance bar, executable: a fresh hero walks the real chain
    // files (quests/*.json + exported 1027) through the real quest calls and
    // lands Lv11 (1445 total: 1115 kills + 330 rewards; Lv10 needs 1035).
    [TestMethod]
    public void ChainSim_GuidedPlayReachesLevel11()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string custom = Path.Combine(root, "quests");
        string mai = Path.Combine(root, "assets", "quest", "1027.json");
        Assert.IsTrue(Directory.Exists(custom) && File.Exists(mai),
            "needs quests/ + exported assets (run: dotnet run --project MushTown.Exporter -- export)");
        var quests = new Dictionary<int, QuestData>();
        // Official first, customs win on clash: the game loads in this order.
        var req = JsonSerializer.Deserialize<QuestData>(File.ReadAllText(mai))!;
        quests[req.QuestId] = req;
        foreach (string f in Directory.GetFiles(custom, "*.json"))
        {
            var q = JsonSerializer.Deserialize<QuestData>(File.ReadAllText(f))!;
            quests[q.QuestId] = q;
        }
        // Chain links: a wrong Pre or NPC fails here, not in play.
        var links = new (int id, int[] pre, int npc0, int npc1)[]
        {
            (90002, Array.Empty<int>(), 1541000, 1541000),
            (90101, new[] { 90002 }, 1541001, 1541001),
            (90001, Array.Empty<int>(), 10000, 10000),
            (90102, new[] { 90001, 90101 }, 12000, 12000),
            (90103, new[] { 90102 }, 12100, 12100),
            (90104, new[] { 90103 }, 12100, 12100),
            (90105, new[] { 90104 }, 12100, 12100),
            (1027, Array.Empty<int>(), 12100, 12000),
        };
        foreach (var (id, pre, npc0, npc1) in links)
        {
            CollectionAssert.AreEqual(pre, quests[id].Pre, $"chain link {id}");
            Assert.AreEqual(npc0, quests[id].Npc0, $"chain link {id}");
            Assert.AreEqual(npc1, quests[id].Npc1, $"chain link {id}");
        }
        var order = new[] { 90002, 90101, 90001, 90102, 90103, 90104, 90105 };
        // Mob pay comes from the exported mob files, like the game reads it.
        var pay = new Dictionary<int, int>();
        foreach (int mob in order.SelectMany(id => quests[id].Kills).Select(k => k.Mob).Distinct())
            pay[mob] = JsonSerializer.Deserialize<MobData>(
                File.ReadAllText(Path.Combine(root, "assets", "mob", $"{mob}.json"))!)!.Exp;
        var chara = new CharaSlot
        {
            Name = "hero",
            Sp = Progression.SpPerLevel, // CreateChara banks the first level's points
            Use = new() { { ItemData.RedPotion, 20 }, { ItemData.BluePotion, 10 } },
        };
        // Guided order: Sugar -> field Sugar -> Pio -> Lucas -> Mai. Pio's
        // snails live back on 20000, so the guide backtracks one map (kills
        // count by mob, not by map). Both snail quests hunt 100000: the sim
        // runs them one by one (10 kills), while holding both at once
        // double-dips 5 (1270 total still lands Lv11). The hero accepts 1027
        // on arrival (its LvMax 10 only gates the accept, and the hero is ~6)
        // and turns it in last, back at Lucas.
        foreach (int id in order.Take(4))
            RunChainQuest(chara, quests.Values.ToList(), quests[id], pay);
        Assert.IsTrue(QuestLog.Accept(chara, quests[1027]), "chain stuck at 1027");
        foreach (int id in order.Skip(4))
            RunChainQuest(chara, quests.Values.ToList(), quests[id], pay);
        Assert.IsTrue(QuestLog.IsComplete(chara, quests[1027]));
        Assert.IsTrue(QuestLog.Complete(chara, quests[1027]));
        QuestLog.GrantRewards(chara, quests[1027]);
        Progression.ApplyExp(chara, quests[1027].RewardExp);
        Assert.AreEqual(11, chara.Level); // 1445 - 1250 = 195 into Lv11
        Assert.AreEqual(195, chara.Exp);
        Assert.AreEqual(310, chara.Mesos);
        Assert.AreEqual(28, chara.Use![ItemData.RedPotion]);
        Assert.AreEqual(15, chara.Use![ItemData.BluePotion]);
    }

    private static void RunChainQuest(CharaSlot chara, List<QuestData> all, QuestData q, Dictionary<int, int> pay)
    {
        Assert.IsTrue(QuestLog.Accept(chara, q), $"chain stuck at {q.QuestId}");
        foreach (var k in q.Kills)
            for (int i = 0; i < k.Count; i++)
            {
                QuestLog.BumpKills(chara, all, k.Mob);
                Progression.ApplyExp(chara, pay[k.Mob]); // each kill pays, like GainExp
            }
        Assert.IsTrue(QuestLog.IsComplete(chara, q), $"chain cannot finish {q.QuestId}");
        Assert.IsTrue(QuestLog.Complete(chara, q));
        QuestLog.GrantRewards(chara, q);
        Progression.ApplyExp(chara, q.RewardExp);
    }

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
