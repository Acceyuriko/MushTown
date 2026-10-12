using System.Collections.Concurrent;
using System.Net.Sockets;
using MushTown.Shared;

namespace MushTown.Server;

// One connection to a GameServer. Sends our state, queues peer events for the
// game loop to drain with TryTake. Reading runs on a background task; when the
// link dies Connected flips false, queued events stay drained, game goes solo.
public sealed class NetClient : IDisposable
{
    private readonly Action<string> _log;
    private readonly ConcurrentQueue<RemoteEvent> _events = new();
    private readonly object _sendLock = new();
    private TcpClient? _tcp;
    private CancellationTokenSource? _cts;

    public int PlayerId { get; private set; }
    private volatile bool _connected; // written by the read task, read by the game loop
    public bool Connected { get => _connected; private set => _connected = value; }

    public NetClient(Action<string> log)
    {
        _log = log;
    }

    // Blocks until the server welcomes us (its first message); loud on failure.
    public void Connect(string host, int port = Net.Port)
    {
        _tcp = new TcpClient { NoDelay = true };
        try
        {
            ConnectInner(host, port);
        }
        catch
        {
            _tcp.Close();
            throw;
        }
    }

    private void ConnectInner(string host, int port)
    {
        try
        {
            _tcp!.Connect(host, port);
        }
        catch (Exception ex)
        {
            throw new IOException($"cannot reach {host}:{port} ({ex.Message})", ex);
        }
        var reader = new NetReader();
        var stream = _tcp.GetStream();
        stream.ReadTimeout = 5000;
        try
        {
            var chunk = new byte[1024];
            while (PlayerId == 0)
            {
                int n = stream.Read(chunk, 0, chunk.Length);
                if (n == 0) throw new IOException($"host {host} closed the connection");
                foreach (byte[] p in reader.Feed(chunk, n))
                    if (p[0] == Net.Welcome && p.Length == 5 && PlayerId == 0)
                        PlayerId = BitConverter.ToInt32(p, 1);
                    else
                        Enqueue(p);
            }
        }
        catch (IOException ex)
        {
            throw new IOException($"host {host} did not welcome us ({ex.Message})", ex);
        }
        stream.ReadTimeout = Timeout.Infinite;
        Connected = true;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoop(reader, _cts.Token));
        _log($"connected to {host}:{port} as player {PlayerId}");
    }

    public void SendState(int map, float x, float y, byte action, byte frame, bool facingRight)
    {
        if (!Connected) return;
        try
        {
            lock (_sendLock)
                _tcp!.GetStream().Write(Net.Encode(Net.State, w =>
                {
                    w.Write(PlayerId);
                    w.Write(map);
                    w.Write(x);
                    w.Write(y);
                    w.Write(action);
                    w.Write(frame);
                    w.Write(facingRight ? (byte)1 : (byte)0);
                }));
        }
        catch (Exception ex)
        {
            _log($"send failed: {ex.Message}");
            Connected = false;
        }
    }

    public bool TryTake(out RemoteEvent e) => _events.TryDequeue(out e);

    private async Task ReadLoop(NetReader reader, CancellationToken ct)
    {
        var chunk = new byte[1024];
        var stream = _tcp!.GetStream();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(chunk, ct);
                if (n == 0) break;
                foreach (byte[] p in reader.Feed(chunk, n)) Enqueue(p);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log($"connection lost: {ex.Message}");
        }
        Connected = false;
    }

    private void Enqueue(byte[] p)
    {
        if (p[0] == Net.State && p.Length == 20 && p[17] < Net.Actions.Length)
            _events.Enqueue(new RemoteEvent(false,
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5),
                BitConverter.ToSingle(p, 9), BitConverter.ToSingle(p, 13),
                p[17], p[18], p[19] != 0));
        else if (p[0] == Net.Leave && p.Length == 5)
            _events.Enqueue(new RemoteEvent(true, BitConverter.ToInt32(p, 1), 0, 0, 0, 0, 0, false));
        else if (p[0] != Net.Welcome)
            _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _tcp?.Close();
    }
}
