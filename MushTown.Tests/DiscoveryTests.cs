using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class DiscoveryTests
{
    [TestMethod]
    public async Task Scan_FindsLocalResponder()
    {
        using var responder = new DiscoverResponder(tcpPort: 16631, name: "testbox", port: 16699, bindAddress: "127.0.0.1");
        var found = new List<FoundHost>();
        for (int i = 0; i < 5 && found.Count == 0; i++) // the first Who can beat the bind
            found = await Discovery.ScanAsync(timeoutMs: 400, port: 16699, address: "127.0.0.1");
        Assert.AreEqual(1, found.Count);
        Assert.AreEqual("127.0.0.1", found[0].Ip);
        Assert.AreEqual(16631, found[0].Port);
        Assert.AreEqual("testbox", found[0].Name);
    }

    [TestMethod]
    public async Task Scan_EmptyWhenSilent()
    {
        var found = await Discovery.ScanAsync(timeoutMs: 300, port: 16698, address: "127.0.0.1");
        Assert.AreEqual(0, found.Count);
    }

    [TestMethod]
    public void Parse_DropsBadPackets()
    {
        byte[] Good() => System.Text.Encoding.ASCII.GetBytes("MushTownHere;16631;testbox");
        Assert.IsNotNull(Discovery.Parse(Good(), "10.0.0.5"));
        string[] bad =
        [
            "MushTownWho", "MushTownHere;16631", "MushTownHere;16631;a;b", "Nope;16631;x",
            "MushTownHere;0;x", "MushTownHere;70000;x", "MushTownHere;abc;x",
            "MushTownHere;16631;", $"MushTownHere;16631;{new string('x', 65)}",
        ];
        foreach (string packet in bad)
            Assert.IsNull(Discovery.Parse(System.Text.Encoding.ASCII.GetBytes(packet), "10.0.0.5"), packet);
    }
}
