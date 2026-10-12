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
            w.Write(12.5f);
            w.Write(-3.25f);
            w.Write(Net.ActWalk);
            w.Write((byte)2);
            w.Write((byte)1);
        });

        Assert.AreEqual(20, frame.Length); // 4 header + 1 type + 15 body
        Assert.AreEqual(16, BitConverter.ToInt32(frame, 0));
        var payload = new NetReader().Feed(frame, frame.Length).Single();
        Assert.AreEqual(Net.State, payload[0]);
        Assert.AreEqual(7, BitConverter.ToInt32(payload, 1));
        Assert.AreEqual(12.5f, BitConverter.ToSingle(payload, 5));
        Assert.AreEqual(-3.25f, BitConverter.ToSingle(payload, 9));
        Assert.AreEqual(Net.ActWalk, payload[13]);
        Assert.AreEqual(2, payload[14]);
        Assert.AreEqual(1, payload[15]);
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
        using var server = new GameServer(_ => { }, Net.Port + 1);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 1);
        b.Connect("127.0.0.1", Net.Port + 1);
        Assert.AreNotEqual(a.PlayerId, b.PlayerId);

        a.SendState(10, 20, Net.ActWalk, 1, true);
        var atB = WaitTake(b);
        Assert.IsFalse(atB.Gone);
        Assert.AreEqual(a.PlayerId, atB.Id);
        Assert.AreEqual(10f, atB.X);
        Assert.AreEqual(20f, atB.Y);
        Assert.AreEqual(Net.ActWalk, atB.Action);
        Assert.AreEqual(1, atB.Frame);
        Assert.IsTrue(atB.FacingRight);

        b.SendState(30, 40, Net.ActJump, 0, false);
        var atA = WaitTake(a);
        Assert.AreEqual(b.PlayerId, atA.Id);
        Assert.AreEqual(30f, atA.X);
        Assert.AreEqual(Net.ActJump, atA.Action);
    }

    [TestMethod]
    public void Loopback_NewcomerGetsHistory_LeaverBroadcastsLeave()
    {
        using var server = new GameServer(_ => { }, Net.Port + 2);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 2);
        b.Connect("127.0.0.1", Net.Port + 2);
        a.SendState(1, 2, Net.ActStand, 0, false);
        WaitTake(b); // b ate a's live state; the server kept a copy as history
        b.SendState(3, 4, Net.ActWalk, 2, true);
        WaitTake(a);

        using var c = new NetClient(_ => { });
        c.Connect("127.0.0.1", Net.Port + 2);
        var seen = new Dictionary<int, RemoteEvent>();
        for (int i = 0; i < 2; i++)
        {
            var e = WaitTake(c);
            seen[e.Id] = e;
        }
        Assert.AreEqual(2, seen.Count);
        Assert.AreEqual(1f, seen[a.PlayerId].X);
        Assert.AreEqual(3f, seen[b.PlayerId].X);

        a.Dispose();
        var gone = WaitTake(b);
        Assert.IsTrue(gone.Gone);
        Assert.AreEqual(a.PlayerId, gone.Id);
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
}
