using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class PartyTests
{
    // The leader makes a party, invites another player and the invite joins
    // the party. Both sides get the member list.
    [TestMethod]
    public void Loopback_InviteJoinsTheParty()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 20);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 20);
        b.Connect("127.0.0.1", Net.Port + 20);
        a.SendName("hero", 58);
        b.SendName("friend", 58);

        a.SendPartyMake();
        var alone = WaitTakeParty(a);
        Assert.AreEqual(1, alone.Members.Length);
        Assert.AreEqual("hero", alone.Members[0].Name);

        a.SendPartyInvite(b.PlayerId);
        var ask = WaitTakeAsk(b);
        Assert.AreEqual(a.PlayerId, ask.Id);
        Assert.AreEqual("hero", ask.Name);

        b.SendPartyAnswer(true);
        var atB = WaitTakeParty(b);
        Assert.AreEqual(2, atB.Members.Length);
        Assert.AreEqual(a.PlayerId, atB.Members[0].Id); // the leader comes first
        Assert.AreEqual("friend", atB.Members[1].Name);
        var atA = WaitTakeParty(a);
        Assert.AreEqual(2, atA.Members.Length);
        Assert.AreEqual(b.PlayerId, atA.Members[1].Id);
    }

    // A decline leaves the party empty: the invitee stays alone.
    [TestMethod]
    public void Loopback_DeclineKeepsTheTargetAlone()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 21);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 21);
        b.Connect("127.0.0.1", Net.Port + 21);

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
        b.SendPartyAnswer(false);

        // the leader keeps the party of one
        a.SendPartyMake(); // no-op: the leader is already in a party
        var alone = WaitTakeParty(a);
        Assert.AreEqual(1, alone.Members.Length);
        AssertNo(() => b.TryTakeParty(out _)); // the target did not join
    }

    // The party chat goes to the party only. A player without a party sends
    // nothing.
    [TestMethod]
    public void Loopback_PartyChatStaysInTheParty()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 22);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 22);
        b.Connect("127.0.0.1", Net.Port + 22);
        c.Connect("127.0.0.1", Net.Port + 22);
        a.SendName("hero", 58);
        b.SendName("friend", 58);

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
        b.SendPartyAnswer(true);
        WaitTakeParty(b);
        WaitTakeParty(a);

        a.SendPartyChat("hello");
        var line = WaitTakeChat(b);
        Assert.AreEqual("hero", line.Name);
        Assert.AreEqual("hello", line.Text);

        c.SendPartyChat("outside");
        AssertNo(() => c.TryTakeChat(out _)); // no party, no line
    }

    // The line of one party does not reach another party.
    [TestMethod]
    public void Loopback_OtherPartyDoesNotGetTheLine()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 25);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        using var d = new NetClient(_ => { });
        foreach (var client in new[] { a, b, c, d })
            client.Connect("127.0.0.1", Net.Port + 25);

        Join(a, b);
        Join(c, d);

        a.SendPartyChat("ours");
        WaitTakeChat(b); // the line goes to the party
        AssertNo(() => c.TryTakeChat(out _)); // the other party hears nothing
        AssertNo(() => d.TryTakeChat(out _));
    }

    // Leaving gives the member an empty list and the rest a shorter one.
    [TestMethod]
    public void Loopback_LeaveClearsTheParty()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 23);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 23);
        b.Connect("127.0.0.1", Net.Port + 23);
        a.SendName("hero", 58);
        b.SendName("friend", 58);

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
        b.SendPartyAnswer(true);
        WaitTakeParty(b);
        WaitTakeParty(a);

        b.SendPartyLeave();
        var atB = WaitTakeParty(b);
        Assert.AreEqual(0, atB.Members.Length);
        var atA = WaitTakeParty(a);
        Assert.AreEqual(1, atA.Members.Length);
    }

    // The leader leaving promotes the member that comes next: it can invite.
    [TestMethod]
    public void Loopback_TheLeaderLeavingPromotesTheNext()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 26);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        foreach (var client in new[] { a, b, c })
            client.Connect("127.0.0.1", Net.Port + 26);

        Join(a, b);
        a.SendPartyLeave();
        var atB = WaitTakeParty(b);
        Assert.AreEqual(1, atB.Members.Length);
        Assert.AreEqual(b.PlayerId, atB.Members[0].Id); // the new leader

        b.SendPartyInvite(c.PlayerId); // the new leader may invite
        var ask = WaitTakeAsk(c);
        Assert.AreEqual(b.PlayerId, ask.Id);
    }

    // Only the leader invites: the other members get nothing.
    [TestMethod]
    public void Loopback_OnlyTheLeaderInvites()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 27);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        foreach (var client in new[] { a, b, c })
            client.Connect("127.0.0.1", Net.Port + 27);

        Join(a, b);
        b.SendPartyInvite(c.PlayerId); // b is not the leader
        AssertNo(() => c.TryTakePartyAsk(out _));

        a.SendPartyInvite(c.PlayerId); // the leader may invite
        var ask = WaitTakeAsk(c);
        Assert.AreEqual(a.PlayerId, ask.Id);
    }

    // A player that leaves the game is out of the party.
    [TestMethod]
    public void Loopback_DisconnectRemovesTheMember()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 28);
        server.Start();
        using var a = new NetClient(_ => { });
        var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 28);
        b.Connect("127.0.0.1", Net.Port + 28);

        Join(a, b);
        b.Dispose(); // the player leaves the game

        var atA = WaitTakeParty(a);
        Assert.AreEqual(1, atA.Members.Length);
        Assert.AreEqual(a.PlayerId, atA.Members[0].Id);
    }

    // The invite list holds the players of the map.
    [TestMethod]
    public void Loopback_PlayerListHoldsTheMap()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 24);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 24);
        b.Connect("127.0.0.1", Net.Port + 24);
        a.SendName("hero", 58);
        b.SendName("friend", 58);
        a.SendState(10000, 0, 0, Net.ActStand, 0, true); // both stand on the map
        b.SendState(10000, 100, 0, Net.ActStand, 0, true);

        a.SendPartyList();
        var list = WaitTakePlayers(a);
        Assert.AreEqual(1, list.Players.Length);
        Assert.AreEqual(b.PlayerId, list.Players[0].Id);
        Assert.AreEqual("friend", list.Players[0].Name);
    }

    // The players of another map stay out of the list.
    [TestMethod]
    public void Loopback_PlayerListHoldsOnlyTheMap()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        assets.AddMap(10001);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 29);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 29);
        b.Connect("127.0.0.1", Net.Port + 29);
        a.SendState(10000, 0, 0, Net.ActStand, 0, true);
        b.SendState(10001, 100, 0, Net.ActStand, 0, true);

        a.SendPartyList();
        var list = WaitTakePlayers(a);
        Assert.AreEqual(0, list.Players.Length);
    }

    // A player that is in a party cannot be invited again.
    [TestMethod]
    public void Loopback_PlayerListHoldsOnlyTheFreePlayers()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 30);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 30);
        b.Connect("127.0.0.1", Net.Port + 30);
        a.SendState(10000, 0, 0, Net.ActStand, 0, true);
        b.SendState(10000, 100, 0, Net.ActStand, 0, true);
        Join(a, b);

        a.SendPartyList();
        var list = WaitTakePlayers(a);
        Assert.AreEqual(0, list.Players.Length); // b is in a party
    }

    // A party holds six members: the seventh invite goes nowhere.
    [TestMethod]
    public void Loopback_ThePartyHoldsSixMembers()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 31);
        server.Start();
        var clients = new List<NetClient>();
        for (int i = 0; i < 7; i++)
        {
            var client = new NetClient(_ => { });
            client.Connect("127.0.0.1", Net.Port + 31);
            clients.Add(client);
        }
        try
        {
            var leader = clients[0];
            leader.SendPartyMake();
            WaitTakeParty(leader);
            PartyEvent full = default;
            for (int i = 1; i < 6; i++) // five more: the party is full
            {
                leader.SendPartyInvite(clients[i].PlayerId);
                WaitTakeAsk(clients[i]);
                clients[i].SendPartyAnswer(true);
                WaitTakeParty(clients[i]);
                full = WaitTakeParty(leader);
            }
            Assert.AreEqual(6, full.Members.Length);

            leader.SendPartyInvite(clients[6].PlayerId); // the party is full
            AssertNo(() => clients[6].TryTakePartyAsk(out _));
            var line = WaitTakeChat(leader); // the leader hears why
            Assert.AreEqual("the party is full.", line.Text);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    // Nothing came: the server had the time to send it.
    private static void AssertNo(Func<bool> came)
    {
        Thread.Sleep(300);
        Assert.IsFalse(came());
    }

    // The invite of a leader who left the game is dead: the late answer
    // joins nothing.
    [TestMethod]
    public void Loopback_TheInviteOfALeaderWhoLeftIsDead()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 32);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 32);
        b.Connect("127.0.0.1", Net.Port + 32);

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
        a.SendPartyLeave(); // the leader leaves: the invite he sent is dead
        b.SendPartyAnswer(true);
        AssertNo(() => b.TryTakeParty(out _));
        var line = WaitTakeChat(b); // the invited player hears that it failed
        Assert.AreEqual("the party invite expired.", line.Text);
    }

    // The party is full when the answer comes: the waiting guest stays out.
    [TestMethod]
    public void Loopback_TheFullPartyRefusesTheAnswer()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 33);
        server.Start();
        var clients = new List<NetClient>();
        for (int i = 0; i < 7; i++)
        {
            var client = new NetClient(_ => { });
            client.Connect("127.0.0.1", Net.Port + 33);
            clients.Add(client);
        }
        try
        {
            var leader = clients[0];
            var waiting = clients[1];
            leader.SendPartyMake();
            WaitTakeParty(leader);
            leader.SendPartyInvite(waiting.PlayerId);
            WaitTakeAsk(waiting); // this one waits for the end of the test

            foreach (var guest in clients.Skip(2).Take(4)) // four more: the party has five
            {
                leader.SendPartyInvite(guest.PlayerId);
                WaitTakeAsk(guest);
                guest.SendPartyAnswer(true);
                WaitTakeParty(guest);
                WaitTakeParty(leader);
            }

            leader.SendPartyInvite(clients[6].PlayerId); // the sixth member fits
            WaitTakeAsk(clients[6]);
            clients[6].SendPartyAnswer(true);
            WaitTakeParty(clients[6]);
            WaitTakeParty(leader);

            waiting.SendPartyAnswer(true); // the party is full now
            AssertNo(() => waiting.TryTakeParty(out _));
            var line = WaitTakeChat(waiting);
            Assert.AreEqual("the party is full.", line.Text);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    // The inviter hears that the invite was declined.
    [TestMethod]
    public void Loopback_TheInviterHearsTheDecline()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 34);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 34);
        b.Connect("127.0.0.1", Net.Port + 34);
        a.SendName("hero", 58);
        b.SendName("guest", 58);

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
        b.SendPartyAnswer(false);

        var line = WaitTakeChat(a);
        Assert.AreEqual("", line.Name); // a system line
        Assert.AreEqual("guest declined the party invite.", line.Text);
    }

    // The invited player made a party of their own: the answer does not
    // join the other party and says so.
    [TestMethod]
    public void Loopback_JoiningAnotherPartyFirstIsRefused()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 38);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 38);
        b.Connect("127.0.0.1", Net.Port + 38);

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
        b.SendPartyMake(); // the invited player makes a party of their own
        WaitTakeParty(b);
        b.SendPartyAnswer(true);

        var party = WaitTakeParty(b); // still alone in their own party
        Assert.AreEqual(1, party.Members.Length);
        var line = WaitTakeChat(b);
        Assert.AreEqual("you are already in a party.", line.Text);
    }

    // The party stays together when a member changes map.
    [TestMethod]
    public void Loopback_AMemberThatChangesMapStaysInTheParty()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        assets.AddMap(20000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 39);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 39);
        b.Connect("127.0.0.1", Net.Port + 39);
        a.SendState(10000, 0, 0, Net.ActStand, 0, true);
        b.SendState(10000, 0, 0, Net.ActStand, 0, true);
        Join(a, b);

        b.SendState(20000, 0, 0, Net.ActStand, 0, true); // the guest changes map
        Thread.Sleep(300);
        AssertNo(() => a.TryTakeParty(out _)); // the member list does not change
        a.SendPartyChat("still here");
        WaitTakeChat(b); // the guest is still in the party
    }

    // Only a player on the same map can be invited.
    [TestMethod]
    public void Loopback_OnlyAPlayerOnTheSameMapCanBeInvited()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        assets.AddMap(20000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 35);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 35);
        b.Connect("127.0.0.1", Net.Port + 35);
        a.SendState(10000, 0, 0, 0, 0, true);
        b.SendState(20000, 0, 0, 0, 0, true); // another map

        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendPartyInvite(b.PlayerId);
        AssertNo(() => b.TryTakePartyAsk(out _));

        b.SendState(10000, 0, 0, 0, 0, true); // now the same map
        a.SendPartyInvite(b.PlayerId);
        WaitTakeAsk(b);
    }

    // The member list carries the level of every member.
    [TestMethod]
    public void Loopback_TheMemberListHasTheLevel()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 36);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 36);
        a.SendName("hero", 58);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });

        a.SendPartyMake();
        var party = WaitTakeParty(a);
        Assert.AreEqual(1, party.Members.Length);
        Assert.AreEqual(42, party.Members[0].Level);
        Assert.AreEqual("hero", party.Members[0].Name);
    }

    // The invite of a player who left is dead: they cannot join the party
    // any more. (The party state is checked directly: a player who left
    // the game cannot be asked anything.)
    [TestMethod]
    public void TheInviteOfAPlayerWhoLeftIsDead()
    {
        var party = new PartyWorld();
        party.Make(1);
        Assert.IsTrue(party.Invite(1, 2));
        party.Leave(2); // the invited player leaves
        var answer = party.Answer(2, true);
        Assert.AreEqual(0, answer.Inviter); // the invite is gone
        Assert.IsFalse(answer.Joined);
        Assert.AreEqual(0, party.PartyOf(2));
    }

    // A player who is already in a party cannot be invited: the leader
    // hears that the invite did not work.
    [TestMethod]
    public void Loopback_TheInviteOfAPlayerInAPartyFails()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 40);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 40);
        b.Connect("127.0.0.1", Net.Port + 40);
        a.SendPartyMake();
        WaitTakeParty(a);
        b.SendPartyMake();
        WaitTakeParty(b);

        a.SendPartyInvite(b.PlayerId); // b is in a party of their own
        AssertNo(() => b.TryTakePartyAsk(out _));
        var line = WaitTakeChat(a);
        Assert.AreEqual("the invite failed.", line.Text);
    }

    // The member list shows the new level of a member who levels up.
    [TestMethod]
    public void Loopback_AMemberThatLevelsUpShowsTheNewLevel()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 41);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 41);
        a.SendPartyMake();
        WaitTakeParty(a);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 5 });
        Assert.AreEqual(5, WaitTakeParty(a).Members[0].Level);

        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 6 }); // the level-up
        Assert.AreEqual(6, WaitTakeParty(a).Members[0].Level);
    }

    // Another leader cannot take the invite away: the first invite stays.
    [TestMethod]
    public void Loopback_AnotherLeaderCannotStealTheInvite()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 42);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 42);
        b.Connect("127.0.0.1", Net.Port + 42);
        c.Connect("127.0.0.1", Net.Port + 42);
        a.SendPartyMake();
        WaitTakeParty(a);
        c.SendPartyMake();
        WaitTakeParty(c);

        a.SendPartyInvite(b.PlayerId);
        var ask = WaitTakeAsk(b);
        Assert.AreEqual(a.PlayerId, ask.Id);
        c.SendPartyInvite(b.PlayerId); // the second leader is refused
        AssertNo(() => b.TryTakePartyAsk(out _));
        var line = WaitTakeChat(c);
        Assert.AreEqual("the invite failed.", line.Text);
    }

    // A map with one snail and a ground line under it: without the line the
    // mob falls while the test swings and the swing misses.
    private static void AddSnailMap(TempAssets assets, int map = 9) =>
        assets.AddMap(map, """[{"MobId":100000,"X":0,"Cy":100}]""", """[{"X1":-300,"Y1":100,"X2":300,"Y2":100,"Layer":0}]""");

    // The leader makes a party and the other player joins it.
    private static void Join(NetClient leader, NetClient guest)
    {
        leader.SendPartyMake();
        WaitTakeParty(leader);
        leader.SendPartyInvite(guest.PlayerId);
        WaitTakeAsk(guest);
        guest.SendPartyAnswer(true);
        WaitTakeParty(guest);
        WaitTakeParty(leader);
    }

    // A party kill splits the exp: the teammate on the same map gets an
    // equal share, the rest of the award stays with the killer.
    [TestMethod]
    public void Loopback_APartyKillSplitsTheExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 101);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 43);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 43);
        b.Connect("127.0.0.1", Net.Port + 43);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0)); // one swing kills the mob
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.IsFalse(hit.Spawn);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(51, hit.Exp); // the killer's half plus the rest
        Assert.AreEqual(50, WaitTakeExp(b)); // the teammate's half
        AssertNo(() => a.TryTakeExp(out _)); // the killer gets no share frame: the exp is not made
    }

    // A member that is not the leader can kill: the party shares the exp of
    // every member's kill.
    [TestMethod]
    public void Loopback_AMemberThatIsNotTheLeaderCanKill()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 47);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 47);
        b.Connect("127.0.0.1", Net.Port + 47);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Assert.IsTrue(WaitTakeMob(b).Spawn); // the mob of map 9
        Join(a, b);

        b.SendBuild(new Build(99, 99, 99, 0, 0, 0)); // the guest kills the mob
        b.SendAttack();
        var hit = WaitTakeMob(b);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(50, hit.Exp); // the guest's half
        Assert.AreEqual(50, WaitTakeExp(a)); // the leader's half
        AssertNo(() => b.TryTakeExp(out _)); // the killer gets no share frame: the exp is not made
    }

    // A party member on another map gets no share: the exp needs the same map.
    [TestMethod]
    public void Loopback_APartyMemberOnAnotherMapGetsNoExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        AddSnailMap(assets, 10);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 44);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 44);
        b.Connect("127.0.0.1", Net.Port + 44);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Assert.IsTrue(WaitTakeMob(b).Spawn); // the mob of map 9
        Join(a, b);

        b.SendState(10, -50, 100, Net.ActStand, 0, true);
        var moved = WaitTakeMob(b); // the new map's mob: the switch went through
        Assert.IsTrue(moved.Spawn);
        Assert.AreEqual(10, moved.Map);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(100, hit.Exp); // the whole award stays with the killer
        AssertNo(() => b.TryTakeExp(out _));
    }

    // A solo kill keeps the whole award: nothing changes without a party.
    [TestMethod]
    public void Loopback_ASoloKillKeepsTheWholeExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 45);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 45);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(100, hit.Exp);
    }

    // A member more than five levels below the killer gets no share (the
    // official level gate).
    [TestMethod]
    public void Loopback_AMemberTooFarBelowGetsNoExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 46);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 46);
        b.Connect("127.0.0.1", Net.Port + 46);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 36 }); // six levels below
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(100, hit.Exp); // the whole award stays with the killer
        AssertNo(() => b.TryTakeExp(out _));
    }

    // Five levels below the killer is still inside the gate: that member
    // gets a share.
    [TestMethod]
    public void Loopback_AMemberFiveLevelsBelowGetsExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 48);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 48);
        b.Connect("127.0.0.1", Net.Port + 48);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 37 }); // five levels below
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(50, hit.Exp);
        Assert.AreEqual(50, WaitTakeExp(b));
    }

    // A killer without a level (no quest sync yet) shares nothing: the whole
    // award stays with the killer.
    [TestMethod]
    public void Loopback_AKillerWithNoLevelGetsTheWholeExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 49);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 49);
        b.Connect("127.0.0.1", Net.Port + 49);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(100, hit.Exp); // the whole award stays with the killer
        AssertNo(() => b.TryTakeExp(out _));
    }

    // A member without a level (no quest sync yet) gets no share.
    [TestMethod]
    public void Loopback_AMemberWithNoLevelGetsNoExp()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 50);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 50);
        b.Connect("127.0.0.1", Net.Port + 50);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(100, hit.Exp);
        AssertNo(() => b.TryTakeExp(out _));
    }

    // An award too small to split (one exp between two) stays with the
    // killer: nobody gets a share of zero.
    [TestMethod]
    public void Loopback_ASmallExpIsNotSplit()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 1);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 51);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 51);
        b.Connect("127.0.0.1", Net.Port + 51);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(1, hit.Exp);
        AssertNo(() => b.TryTakeExp(out _));
    }

    // A member that left the game does not eat a share: the killer keeps
    // the whole award.
    [TestMethod]
    public void Loopback_AMemberThatLeftGetsNoShare()
    {
        using var assets = new TempAssets();
        AddSnailMap(assets);
        assets.AddMob(100000, 40, 0, 100);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 52);
        server.Start();
        using var a = new NetClient(_ => { });
        var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 52);
        b.Connect("127.0.0.1", Net.Port + 52);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        a.SendQuestSync(new CharaSlot { Name = "hero", Level = 42 });
        b.SendQuestSync(new CharaSlot { Name = "friend", Level = 42 });
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        Join(a, b);

        b.Dispose(); // the guest leaves the game
        // the server pushes the fresh list when the member leaves: that is
        // the sign the disconnect went through
        var left = WaitTakeParty(a);
        Assert.AreEqual(1, left.Members.Length);
        a.SendBuild(new Build(99, 99, 99, 0, 0, 0));
        a.SendAttack();
        var hit = WaitTakeMob(a);
        Assert.AreEqual(0, hit.HpLeft);
        Assert.AreEqual(100, hit.Exp);
    }

    private static int WaitTakeExp(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeExp(out int exp)) return exp;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for an exp share");
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

    private static PartyEvent WaitTakeParty(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeParty(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a party event");
        throw new InvalidOperationException("unreachable");
    }

    private static PartyPlayersEvent WaitTakePlayers(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakePartyPlayers(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a party player list");
        throw new InvalidOperationException("unreachable");
    }

    private static PartyAskEvent WaitTakeAsk(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakePartyAsk(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a party invite");
        throw new InvalidOperationException("unreachable");
    }

    private static ChatEvent WaitTakeChat(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeChat(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a chat line");
        throw new InvalidOperationException("unreachable");
    }
}
