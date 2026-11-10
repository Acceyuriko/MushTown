using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class NetTests
{
    [TestMethod]
    public void Encode_State_RoundtripsAllFields()
    {
        byte[] frame = Net.Encode(Net.State, w =>
        {
            w.Write(7);
            w.Write(10000);
            w.Write(12.5f);
            w.Write(-3.25f);
            w.Write(Net.ActWalk);
            w.Write((byte)2);
            w.Write((byte)1);
        });

        Assert.AreEqual(24, frame.Length); // 4 header + 1 type + 19 body
        Assert.AreEqual(20, BitConverter.ToInt32(frame, 0));
        var payload = new NetReader().Feed(frame, frame.Length).Single();
        Assert.AreEqual(Net.State, payload[0]);
        Assert.AreEqual(7, BitConverter.ToInt32(payload, 1));
        Assert.AreEqual(10000, BitConverter.ToInt32(payload, 5));
        Assert.AreEqual(12.5f, BitConverter.ToSingle(payload, 9));
        Assert.AreEqual(-3.25f, BitConverter.ToSingle(payload, 13));
        Assert.AreEqual(Net.ActWalk, payload[17]);
        Assert.AreEqual(2, payload[18]);
        Assert.AreEqual(1, payload[19]);
    }

    [TestMethod]
    public void Reader_SplitHeader_WaitsForRest()
    {
        var reader = new NetReader();
        byte[] frame = Net.Encode(Net.Leave, w => w.Write(3));

        Assert.AreEqual(0, reader.Feed(frame, 2).Count());
        Assert.AreEqual(0, reader.Feed(frame[2..4], 2).Count());
        var payload = reader.Feed(frame[4..], frame.Length - 4).Single();
        Assert.AreEqual(Net.Leave, payload[0]);
        Assert.AreEqual(3, BitConverter.ToInt32(payload, 1));
    }

    [TestMethod]
    public void Reader_TwoFramesInOneFeed_YieldsBoth()
    {
        byte[] a = Net.Encode(Net.Leave, w => w.Write(1));
        byte[] b = Net.Encode(Net.Leave, w => w.Write(2));
        byte[] both = a.Concat(b).ToArray();

        var payloads = new NetReader().Feed(both, both.Length).ToList();
        Assert.AreEqual(2, payloads.Count);
        Assert.AreEqual(1, BitConverter.ToInt32(payloads[0], 1));
        Assert.AreEqual(2, BitConverter.ToInt32(payloads[1], 1));
    }

    [TestMethod]
    public void Reader_BadLength_ThrowsLoud()
    {
        var reader = new NetReader();
        Assert.ThrowsException<InvalidDataException>(() =>
            reader.Feed(BitConverter.GetBytes(5000), 4).ToList());
        Assert.ThrowsException<InvalidDataException>(() =>
            new NetReader().Feed(BitConverter.GetBytes(0), 4).ToList());
    }

    [TestMethod]
    public void Loopback_TwoClientsExchangeStates()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 1);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 1);
        b.Connect("127.0.0.1", Net.Port + 1);
        Assert.AreNotEqual(a.PlayerId, b.PlayerId);

        a.SendState(10000, 10, 20, Net.ActWalk, 1, true);
        var atB = WaitTake(b);
        Assert.IsFalse(atB.Gone);
        Assert.AreEqual(a.PlayerId, atB.Id);
        Assert.AreEqual(10000, atB.Map);
        Assert.AreEqual(10f, atB.X);
        Assert.AreEqual(20f, atB.Y);
        Assert.AreEqual(Net.ActWalk, atB.Action);
        Assert.AreEqual(1, atB.Frame);
        Assert.IsTrue(atB.FacingRight);

        b.SendState(10000, 30, 40, Net.ActJump, 0, false);
        var atA = WaitTake(a);
        Assert.AreEqual(b.PlayerId, atA.Id);
        Assert.AreEqual(10000, atA.Map);
        Assert.AreEqual(30f, atA.X);
        Assert.AreEqual(Net.ActJump, atA.Action);

        // newest action must pass the filter too (pins the Enqueue bound)
        b.SendState(10000, 31, 41, Net.ActSwing, 0, true);
        Assert.AreEqual(Net.ActSwing, WaitTake(a).Action);
    }

    [TestMethod]
    public void ActionCode_RoundtripsAll()
    {
        foreach (string name in Net.Actions)
            Assert.AreEqual(name, Net.ActionName(Net.ActionCode(name)));
        for (byte code = 0; code <= Net.ActShoot; code++)
            Assert.AreEqual(code, Net.ActionCode(Net.ActionName(code)));
        Assert.ThrowsException<InvalidDataException>(() => Net.ActionCode("xxx"));
        Assert.ThrowsException<InvalidDataException>(() => Net.ActionName(99));
    }

    [TestMethod]
    public void Loopback_MapsVisible_SwitchSendsLeave()
    {
        using var assets = new TempAssets();
        assets.AddMap(1);
        assets.AddMap(2);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 2);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        using var c = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 2);
        b.Connect("127.0.0.1", Net.Port + 2);
        c.Connect("127.0.0.1", Net.Port + 2);

        // a is on map 1, b on map 2: everyone hears everything, Map tells apart
        a.SendState(1, 10, 20, Net.ActStand, 0, false);
        b.SendState(2, 30, 40, Net.ActStand, 0, false);
        var atB = WaitTake(b);
        Assert.AreEqual(1, atB.Map);
        Assert.AreEqual(a.PlayerId, atB.Id);
        var atA = WaitTake(a);
        Assert.AreEqual(2, atA.Map);
        Assert.AreEqual(b.PlayerId, atA.Id);
        var first = WaitTake(c);
        var second = WaitTake(c);
        var byId = new Dictionary<int, RemoteEvent> { [first.Id] = first, [second.Id] = second };
        Assert.AreEqual(1, byId[a.PlayerId].Map);
        Assert.AreEqual(2, byId[b.PlayerId].Map);

        // a switches to map 2: Leave first (TCP order), then the fresh state
        a.SendState(2, 11, 21, Net.ActWalk, 1, true);
        var selfLeave = WaitTake(a);
        Assert.IsTrue(selfLeave.Gone);
        Assert.AreEqual(a.PlayerId, selfLeave.Id);
        var goneAtB = WaitTake(b);
        Assert.IsTrue(goneAtB.Gone);
        var atB2 = WaitTake(b);
        Assert.IsFalse(atB2.Gone);
        Assert.AreEqual(2, atB2.Map);
        Assert.AreEqual(11f, atB2.X);
        var goneAtC = WaitTake(c);
        Assert.IsTrue(goneAtC.Gone);
        Assert.AreEqual(a.PlayerId, goneAtC.Id);
        var atC2 = WaitTake(c);
        Assert.AreEqual(2, atC2.Map);

        // a disconnect sends Leave to every map
        b.Dispose();
        var goneAtA = WaitTake(a);
        Assert.IsTrue(goneAtA.Gone);
        Assert.AreEqual(b.PlayerId, goneAtA.Id);
        var goneAtC2 = WaitTake(c);
        Assert.IsTrue(goneAtC2.Gone);
        Assert.AreEqual(b.PlayerId, goneAtC2.Id);
    }

    // The cash shop removes the player from the map (Leave to everyone); the
    // State after it re-enters at the saved spot and peers see it again.
    [TestMethod]
    public void Loopback_CashShop_LeavesAndReturns()
    {
        using var assets = new TempAssets();
        assets.AddMap(1);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 57);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 57);
        b.Connect("127.0.0.1", Net.Port + 57);

        a.SendState(1, 10, 20, Net.ActStand, 0, false);
        Assert.AreEqual(a.PlayerId, WaitTake(b).Id);

        a.SendCashShop();
        a.SendCashShop();
        var goneAtA = WaitTake(a);
        Assert.IsTrue(goneAtA.Gone);
        Assert.AreEqual(a.PlayerId, goneAtA.Id);
        var goneAtB = WaitTake(b);
        Assert.IsTrue(goneAtB.Gone);
        Assert.AreEqual(a.PlayerId, goneAtB.Id);
        AssertNo(() => b.TryTake(out _)); // the second CashShop frame sends no second Leave

        a.SendState(1, 11, 21, Net.ActStand, 0, false);
        var backAtB = WaitTake(b);
        Assert.IsFalse(backAtB.Gone);
        Assert.AreEqual(a.PlayerId, backAtB.Id);
        Assert.AreEqual(1, backAtB.Map);
        Assert.AreEqual(11f, backAtB.X);
        Assert.AreEqual(21f, backAtB.Y);
    }

    // Returning from the cash shop is a first entry at the server: the saved
    // map's live mob snapshot is pushed again.
    [TestMethod]
    public void Loopback_CashShop_ReentryPushesMobs()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 40);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 58);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 58);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        var first = WaitTakeMob(a);
        Assert.IsTrue(first.Spawn);

        a.SendCashShop();
        Assert.IsTrue(WaitTake(a).Gone);
        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        var back = WaitTakeMob(a);
        Assert.IsTrue(back.Spawn);
        Assert.AreEqual(first.Uid, back.Uid);
        Assert.AreEqual(first.HpLeft, back.HpLeft);
    }

    // A player in the cash shop is not in the server's map: a swing has no body
    // to judge from, so no hit reaches the other client.
    [TestMethod]
    public void Loopback_CashShop_BlocksAttack()
    {
        using var assets = new TempAssets();
        assets.AddMap(9, """[{"MobId":100000,"X":0,"Cy":100}]""");
        assets.AddMob(100000, 1000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 59);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 59);
        b.Connect("127.0.0.1", Net.Port + 59);

        a.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.AreEqual(a.PlayerId, WaitTake(b).Id);
        Assert.IsTrue(WaitTakeMob(a).Spawn);
        b.SendState(9, -50, 100, Net.ActStand, 0, true);
        Assert.AreEqual(b.PlayerId, WaitTake(a).Id);
        Assert.IsTrue(WaitTakeMob(b).Spawn);

        a.SendAttack();
        Assert.IsFalse(WaitTakeMob(a).Spawn);
        Assert.IsFalse(WaitTakeMob(b).Spawn);

        a.SendCashShop();
        Assert.IsTrue(WaitTake(a).Gone);
        Assert.IsTrue(WaitTake(b).Gone);
        a.SendAttack();
        AssertNo(() => b.TryTakeMob(out _));
    }

    // Names relay to peers with the sender id attached; a newcomer gets
    // the existing names on connect; a re-send overwrites (ring change).
    [TestMethod]
    public void Loopback_NameRelaysToPeer()
    {
        using var assets = new TempAssets();
        assets.AddMap(10000);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 12);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 12);
        b.Connect("127.0.0.1", Net.Port + 12);

        a.SendName("hero", 58);
        var atB = WaitTakeName(b);
        Assert.AreEqual(a.PlayerId, atB.Id);
        Assert.AreEqual("hero", atB.Name);
        Assert.AreEqual(58, atB.Plate);

        using var c = new NetClient(_ => { });
        c.Connect("127.0.0.1", Net.Port + 12);
        var atC = WaitTakeName(c); // the newcomer learns a's tag
        Assert.AreEqual(a.PlayerId, atC.Id);
        Assert.AreEqual("hero", atC.Name);

        a.SendName("hero", 0); // ring taken off
        var atB2 = WaitTakeName(b);
        Assert.AreEqual(0, atB2.Plate);
    }

    private static RemoteEvent WaitTake(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTake(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a net event");
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

    private static void AssertNo(Func<bool> came)
    {
        Thread.Sleep(300);
        Assert.IsFalse(came(), "an event arrived that should not have");
    }

    private static NameEvent WaitTakeName(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeName(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a name event");
        throw new InvalidOperationException("unreachable");
    }
}
