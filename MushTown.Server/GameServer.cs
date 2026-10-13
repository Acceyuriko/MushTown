using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MushTown.Shared;

namespace MushTown.Server;

// Listen server: assigns ids and relays State frames to every other client;
// a map switch sends Leave to all, a disconnect too. Clients filter states
// by their own map (the server cannot: a newcomer has no map yet, so a
// server-side filter would leave it hearing nothing).
// The server also owns the mobs (a MobWorld): clients entering a map get its
// spawns, and each Attack is judged from the sender's last State and
// broadcast as MobHit (clients drop hits for other maps, like states).
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
    private readonly MobWorld _mobs;
    private readonly object _mobLock = new(); // guards _pos and the MobWorld (read loops run per client)
    private readonly Dictionary<int, (int map, float x, float y, bool facing)> _pos = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _nextId;
    private readonly ConcurrentDictionary<int, Client> _clients = new();

    public GameServer(Action<string> log, string assetsDir, int port = Net.Port)
    {
        _log = log;
        _port = port;
        _mobs = new MobWorld(assetsDir);
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
            // the newcomer's map is unknown until its first State; live states
            // fill peers in within a frame, so no history is sent here
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
        lock (_mobLock) _pos.Remove(id);
        client.Tcp.Close();
        Broadcast(Net.Encode(Net.Leave, w => w.Write(id)));
        _log($"client {id} left");
    }

    // Clients send State and Attack; anything else means a foreign client.
    // The map id only detects switches (each client filters by its own map).
    private void OnMessage(int id, byte[] payload)
    {
        if (payload[0] == Net.Attack && payload.Length == 1)
        {
            OnAttack(id);
            return;
        }
        if (payload[0] != Net.State || payload.Length != 20)
            throw new InvalidDataException($"bad message type {payload[0]}");
        int map = BitConverter.ToInt32(payload, 5);
        float x = BitConverter.ToSingle(payload, 9);
        float y = BitConverter.ToSingle(payload, 13);
        bool facing = payload[19] != 0;
        bool first, switched;
        lock (_mobLock)
        {
            first = !_pos.TryGetValue(id, out var old);
            switched = !first && old.map != map;
            _pos[id] = (map, x, y, facing);
        }
        if (switched)
            Broadcast(Net.Encode(Net.Leave, w => w.Write(id)));
        if (first || switched) PushMobs(id, map);
        Broadcast(Net.Frame(payload), id);
    }

    // The Attack carries no body: the swing is judged from the sender's last
    // State (its position and facing). Every mob in reach takes one hit. Judge
    // and broadcast under one lock: two swings landing together must reach
    // every screen in the order they were judged, or HP would jump back up.
    // No swing cooldown on purpose: LAN self-play trusts the client (like movement).
    private void OnAttack(int id)
    {
        lock (_mobLock)
        {
            if (!_pos.TryGetValue(id, out var p)) return; // no State yet: nothing to judge from
            foreach (var h in _mobs.Strike(p.map, p.x, p.y, p.facing, Random.Shared))
                Broadcast(Net.Encode(Net.MobHit, w =>
                {
                    w.Write(h.Uid);
                    w.Write(h.Map);
                    w.Write(h.Damage);
                    w.Write(h.HpLeft);
                }));
        }
    }

    // A client entering a map (first State or switch) gets its live mobs. The
    // snapshot and the sends stay under one lock: a kill slipping between
    // them would plant an unkillable ghost on the newcomer.
    private void PushMobs(int id, int map)
    {
        var client = _clients[id]; // our own read loop: this id is always present
        lock (_mobLock)
        {
            _mobs.EnterMap(map);
            foreach (var s in _mobs.MobsOn(map))
                Send(client, Net.Encode(Net.MobSpawn, w =>
                {
                    w.Write(s.Uid);
                    w.Write(s.Mob);
                    w.Write(s.Map);
                    w.Write(s.X);
                    w.Write(s.Y);
                    w.Write(s.MaxHp);
                    w.Write(s.Hp);
                }));
        }
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
