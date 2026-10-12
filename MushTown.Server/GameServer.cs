using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MushTown.Shared;

namespace MushTown.Server;

// Listen server: assigns ids, relays State frames to everyone else, sends a
// newcomer the last state of each peer, and broadcasts Leave on disconnect.
// All socket work runs on background tasks; the host only calls Start/Dispose.
public sealed class GameServer : IDisposable
{
    private sealed class Client
    {
        public required TcpClient Tcp;
        public readonly object SendLock = new();
    }

    private readonly Action<string> _log;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _nextId;
    private readonly ConcurrentDictionary<int, Client> _clients = new();
    private readonly ConcurrentDictionary<int, byte[]> _lastState = new();

    public GameServer(Action<string> log, int port = Net.Port)
    {
        _log = log;
        _port = port;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _log($"server listening on port {_port}");
        _ = Task.Run(() => AcceptLoop(_cts.Token));
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await _listener!.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // a failed accept must not end accepting (shutdown arrives as cancel)
                _log($"accept failed: {ex.Message}");
                continue;
            }
            tcp.NoDelay = true;
            int id = Interlocked.Increment(ref _nextId);
            var client = new Client { Tcp = tcp };
            _clients[id] = client;
            _log($"client {id} connected");
            Send(client, Net.Encode(Net.Welcome, w => w.Write(id)));
            foreach (var kv in _lastState) Send(client, kv.Value);
            _ = Task.Run(() => ReadLoop(id, client, ct), ct);
        }
    }

    private async Task ReadLoop(int id, Client client, CancellationToken ct)
    {
        try
        {
            var reader = new NetReader();
            var stream = client.Tcp.GetStream();
            var chunk = new byte[1024];
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(chunk, ct);
                if (n == 0) break; // peer closed
                foreach (byte[] payload in reader.Feed(chunk, n))
                    OnMessage(id, payload);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log($"client {id} error: {ex.Message}");
        }
        _clients.TryRemove(id, out _);
        _lastState.TryRemove(id, out _);
        client.Tcp.Close();
        Broadcast(Net.Encode(Net.Leave, w => w.Write(id)));
        _log($"client {id} left");
    }

    // Clients only ever send State; anything else means a foreign client.
    private void OnMessage(int id, byte[] payload)
    {
        if (payload[0] != Net.State)
            throw new InvalidDataException($"bad message type {payload[0]}");
        byte[] frame = Net.Frame(payload);
        _lastState[id] = frame;
        Broadcast(frame, id);
    }

    private void Broadcast(byte[] frame, int exceptId = -1)
    {
        foreach (var kv in _clients)
            if (kv.Key != exceptId) Send(kv.Value, frame);
    }

    // One send must never break the rest of a broadcast; the dead client's
    // own read loop notices and cleans up.
    private void Send(Client client, byte[] frame)
    {
        try
        {
            lock (client.SendLock)
                client.Tcp.GetStream().Write(frame);
        }
        catch (Exception ex)
        {
            _log($"send failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        foreach (var kv in _clients) kv.Value.Tcp.Close();
    }
}
