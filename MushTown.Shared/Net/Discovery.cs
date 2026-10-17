using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MushTown.Shared;

// LAN host discovery: one UDP broadcast, every running host replies with its
// TCP port and PC name. Best effort: any socket error means "no hosts found",
// never a crash (home hardware varies too much to enumerate the failures).
public sealed record FoundHost(string Ip, int Port, string Name);

public static class Discovery
{
    public const int Port = Net.Port + 1; // UDP; the game itself runs TCP on Net.Port

    internal const string Who = "MushTownWho";
    internal const string Here = "MushTownHere";

    public static async Task<List<FoundHost>> ScanAsync(int timeoutMs = 2000, int port = Port, string address = "255.255.255.255")
    {
        var found = new List<FoundHost>();
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            byte[] q = Encoding.ASCII.GetBytes(Who);
            await udp.SendAsync(q, q.Length, address, port);
            using var cts = new CancellationTokenSource(timeoutMs);
            while (true)
            {
                UdpReceiveResult r;
                try { r = await udp.ReceiveAsync(cts.Token); }
                catch (OperationCanceledException) { break; }
                var h = Parse(r.Buffer, r.RemoteEndPoint.Address.ToString());
                if (h != null && found.All(f => f.Ip != h.Ip)) found.Add(h);
            }
        }
        catch (Exception) { } // no network, no permission, no hosts: the menu shows this PC only
        return found;
    }

    internal static FoundHost? Parse(byte[] msg, string ip)
    {
        string[] parts = Encoding.ASCII.GetString(msg).Split(';');
        if (parts.Length != 3 || parts[0] != Here || parts[2].Length is 0 or > 64) return null;
        if (!int.TryParse(parts[1], out int port) || port is < 1 or > 65535) return null;
        return new FoundHost(ip, port, parts[2]);
    }
}

// Answers ScanAsync while the server runs: one instance per GameServer, with
// the same lifetime as the TCP listener (Start creates it, Dispose stops it).
public sealed class DiscoverResponder : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public DiscoverResponder(int tcpPort = Net.Port, string? name = null, int port = Discovery.Port, string? bindAddress = null)
    {
        _loop = RunAsync(port, tcpPort, name ?? Environment.MachineName, bindAddress, _cts.Token);
    }

    private static async Task RunAsync(int port, int tcpPort, string name, string? bindAddress, CancellationToken ct)
    {
        try
        {
            using var udp = bindAddress == null
                ? new UdpClient(port)
                : new UdpClient(new IPEndPoint(IPAddress.Parse(bindAddress), port));
            byte[] reply = Encoding.ASCII.GetBytes($"{Discovery.Here};{tcpPort};{name}");
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult r;
                try { r = await udp.ReceiveAsync(ct); }
                catch (OperationCanceledException) { break; }
                if (Encoding.ASCII.GetString(r.Buffer) == Discovery.Who)
                    await udp.SendAsync(reply, reply.Length, r.RemoteEndPoint);
            }
        }
        catch (Exception) { } // a dead responder only hides this host (same rule as ScanAsync)
    }

    public void Dispose()
    {
        _cts.Cancel();
        _loop.Wait(1000);
        _cts.Dispose();
    }
}
