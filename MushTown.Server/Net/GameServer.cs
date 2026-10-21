using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
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
    private readonly object _mobLock = new(); // guards _pos/_builds/_names/_questLogs and the MobWorld (read loops run per client)
    private readonly Dictionary<int, (int map, float x, float y, bool facing)> _pos = new();
    private readonly Dictionary<int, Build> _builds = new(); // combat stats per client (missing = beginner default)
    private readonly Dictionary<int, (string name, int plate)> _names = new(); // name tags per client (missing = CLI session)
    private readonly Dictionary<int, QuestData> _questDefs = new(); // quest defs (shared ref: one log per client over these)
    private readonly Dictionary<int, (QuestLog log, CharaSlot chara)> _questLogs = new(); // quest copies per client (missing = CLI or pre-Sync)
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private DiscoverResponder? _responder; // answers LAN scans while we run (same lifetime as the listener)
    private int _nextId;
    private readonly ConcurrentDictionary<int, Client> _clients = new();

    public GameServer(Action<string> log, string assetsDir, int port = Net.Port)
    {
        _log = log;
        _port = port;
        _mobs = new MobWorld(assetsDir);
        LoadQuestDefs(assetsDir);
    }

    // Quest defs the server judges against: official plus customs (same
    // files, same clash rule as the client). No validation here: the host's
    // client already fails loud on bad customs at boot; a joiner's foreign
    // customs simply match no def and its intents go unanswered.
    private void LoadQuestDefs(string assetsDir)
    {
        string questDir = Path.Combine(assetsDir, "quest");
        if (Directory.Exists(questDir))
            foreach (string f in Directory.GetFiles(questDir, "*.json"))
            {
                var q = LoadJson<QuestData>(f);
                _questDefs[q.QuestId] = q;
            }
        string custom = Path.Combine(Path.GetDirectoryName(assetsDir)!, "quests");
        if (Directory.Exists(custom))
            foreach (string f in Directory.GetFiles(custom, "*.json"))
            {
                var q = LoadJson<QuestData>(f);
                _questDefs[q.QuestId] = q;
            }
    }

    private static T LoadJson<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream)
            ?? throw new InvalidDataException($"{path} is empty or broken");
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _responder = new DiscoverResponder(tcpPort: _port);
        _log($"server listening on port {_port}");
        _ = Task.Run(() => AcceptLoop(_cts.Token));
        _ = Task.Run(() => TickLoop(_cts.Token));
    }

    // The AI clock: 10Hz ticks drive mob steps, touches, respawns, and drop
    // expiry. Judge and tick share the mob lock, so a swing and a step never
    // interleave mid-list.
    private const int TickMs = 100;

    private async Task TickLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TickMs, ct);
                lock (_mobLock)
                {
                    var players = new List<(int id, int map, float x, float y)>();
                    foreach (var kv in _pos) players.Add((kv.Key, kv.Value.map, kv.Value.x, kv.Value.y));
                    var ev = _mobs.Tick(TickMs, players);
                    foreach (var mv in ev.Moves)
                        Broadcast(Net.Encode(Net.MobMove, w =>
                        {
                            w.Write(mv.Uid);
                            w.Write(mv.Map);
                            w.Write(mv.X);
                            w.Write(mv.Y);
                            w.Write(mv.FacingRight);
                        }));
                    foreach (var h in ev.Hurts)
                        if (_clients.TryGetValue(h.Victim, out var victim))
                            Send(victim, Net.Encode(Net.Hurt, w =>
                            {
                                w.Write(h.Uid);
                                w.Write(h.Mob);
                                w.Write(h.Map);
                                w.Write(h.Damage);
                            }));
                    foreach (var s in ev.Spawns)
                        Broadcast(MobSpawnFrame(s));
                    foreach (var t in ev.Takes)
                        BroadcastDropTake(t.uid, t.map, 0, 0, 0, 0);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
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
            lock (_mobLock) // the newcomer needs its peers' tags (states carry no names)
                foreach (var kv in _names)
                    Send(client, NameFrame(kv.Key, kv.Value.name, kv.Value.plate));
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
        lock (_mobLock)
        {
            _pos.Remove(id);
            _builds.Remove(id);
            _names.Remove(id);
            _questLogs.Remove(id);
        }
        client.Tcp.Close();
        Broadcast(Net.Encode(Net.Leave, w => w.Write(id)));
        _log($"client {id} left");
    }

    // Clients send State, Attack, Build, SkillCast, Pickup, Name, Chat, and
    // the quest trio (Sync/Accept/Complete); anything else means a foreign client.
    // The map id only detects switches (each client filters by its own map).
    private void OnMessage(int id, byte[] payload)
    {
        if (payload[0] == Net.Name)
        {
            OnName(id, payload);
            return;
        }
        if (payload[0] == Net.Chat)
        {
            OnChat(id, payload);
            return;
        }
        if (payload[0] == Net.QuestSync)
        {
            OnQuestSync(id, payload);
            return;
        }
        if ((payload[0] == Net.QuestAccept || payload[0] == Net.QuestComplete) && payload.Length == 5)
        {
            OnQuestIntent(id, payload[0] == Net.QuestComplete, BitConverter.ToInt32(payload, 1));
            return;
        }
        if (payload[0] == Net.Attack && payload.Length == 1)
        {
            OnAttack(id);
            return;
        }
        if (payload[0] == Net.Pickup && payload.Length == 5)
        {
            OnPickup(id, BitConverter.ToInt32(payload, 1));
            return;
        }
        if (payload[0] == Net.SkillCast && payload.Length == 9)
        {
            OnSkill(id, BitConverter.ToInt32(payload, 1), BitConverter.ToInt32(payload, 5));
            return;
        }
        if (payload[0] == Net.Build && payload.Length == 25)
        {
            lock (_mobLock)
                _builds[id] = new Build(BitConverter.ToInt32(payload, 1), BitConverter.ToInt32(payload, 5), BitConverter.ToInt32(payload, 9),
                    BitConverter.ToInt32(payload, 13), BitConverter.ToInt32(payload, 17), BitConverter.ToInt32(payload, 21));
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

    // A client's name + plate: stored and relayed to every other client (a
    // re-send after a ring change overwrites). Malformed means a foreign client.
    private void OnName(int id, byte[] payload)
    {
        string name;
        int plate;
        try
        {
            using var ms = new MemoryStream(payload, 1, payload.Length - 1);
            using var r = new BinaryReader(ms);
            name = r.ReadString();
            plate = r.ReadInt32();
        }
        catch (Exception ex) when (ex is IOException or FormatException)
        {
            throw new InvalidDataException($"bad name from {id} ({ex.Message})");
        }
        lock (_mobLock)
            _names[id] = (name, plate);
        Broadcast(NameFrame(id, name, plate), id);
    }

    private static byte[] NameFrame(int id, string name, int plate) =>
        Net.Encode(Net.Name, w =>
        {
            w.Write(id);
            w.Write(name);
            w.Write(plate);
        });

    // One chat line: the client sends only the text, the server adds the
    // sender name and relays the line to every client (the sender too: it
    // sees its own line like the others do).
    private void OnChat(int id, byte[] payload)
    {
        string text;
        try
        {
            using var ms = new MemoryStream(payload, 1, payload.Length - 1);
            using var r = new BinaryReader(ms);
            text = r.ReadString();
        }
        catch (Exception ex) when (ex is IOException or FormatException)
        {
            throw new InvalidDataException($"bad chat from {id} ({ex.Message})");
        }
        string name;
        lock (_mobLock)
            name = _names.TryGetValue(id, out var n) ? n.name : "";
        if (name == "") name = "Player " + id; // a client that never sent a name
        Broadcast(Net.Encode(Net.Chat, w =>
        {
            w.Write(name);
            w.Write(text);
        }));
    }

    // A client's quest state: level plus taken/done ids plus kill counters.
    // Adopted as the authoritative copy (it is the player's save); from here
    // the server evolves it through judged kills and confirmed intents, and
    // the client re-syncs on level-up to carry the mirror. Malformed means a
    // foreign client. One log per client over the shared defs: states are
    // per-log, so a shared log would tangle clients.
    private void OnQuestSync(int id, byte[] payload)
    {
        var chara = new CharaSlot();
        try
        {
            using var ms = new MemoryStream(payload, 1, payload.Length - 1);
            using var r = new BinaryReader(ms);
            chara.Level = r.ReadInt32();
            for (int n = r.ReadInt32(); n > 0; n--) chara.Quests.Add(r.ReadInt32());
            for (int n = r.ReadInt32(); n > 0; n--) chara.QuestsDone.Add(r.ReadInt32());
            for (int n = r.ReadInt32(); n > 0; n--) chara.QuestKills[r.ReadString()] = r.ReadInt32();
        }
        catch (Exception ex) when (ex is IOException or FormatException)
        {
            throw new InvalidDataException($"bad quest sync from {id} ({ex.Message})");
        }
        lock (_mobLock)
        {
            // Re-syncs merge, never clobber: a level-up Sync can trail an
            // intent or a kill push still in flight, so membership unions
            // and counters take the max (a Sync racing its QuestOk re-grows
            // then re-shrinks: harmless, payouts stay client-guarded).
            if (_questLogs.TryGetValue(id, out var old))
            {
                foreach (int q in old.chara.Quests) if (!chara.Quests.Contains(q)) chara.Quests.Add(q);
                foreach (int q in old.chara.QuestsDone) if (!chara.QuestsDone.Contains(q)) chara.QuestsDone.Add(q);
                foreach (var kv in old.chara.QuestKills)
                    chara.QuestKills[kv.Key] = Math.Max(kv.Value, chara.QuestKills.TryGetValue(kv.Key, out int n) ? n : 0);
            }
            var log = new QuestLog(_questDefs);
            log.Sync(chara);
            _questLogs[id] = (log, chara);
        }
    }

    // An accept/turn-in intent: the server runs the same act on its copy and
    // confirms, so a client can only take what it may and turn in what it
    // finished. A failed act answers nothing (the client pre-checks on the
    // same state, so only a hacked client hears silence). Unknown quests
    // (joiner customs the host lacks) and missing copies (CLI, pre-Sync)
    // also answer nothing: logged, never fatal.
    private void OnQuestIntent(int id, bool complete, int quest)
    {
        lock (_mobLock)
        {
            if (!_questLogs.TryGetValue(id, out var q) || !_questDefs.ContainsKey(quest))
            {
                _log($"client {id} asked about untracked quest {quest}");
                return;
            }
            bool ok = complete ? q.log.Complete(q.chara, quest) : q.log.Accept(q.chara, quest);
            if (!ok) return;
            Send(_clients[id], Net.Encode(Net.QuestOk, w => { w.Write(quest); w.Write(complete); }));
        }
    }

    // One of the attacker's kills: its quests tick on the server's copy and
    // each bumped counter is pushed back (the client sets, never bumps, in
    // net play). No copy means nothing hunts (CLI, pre-Sync): skip quietly.
    private void PushQuestKills(int attacker, int mob)
    {
        if (!_questLogs.TryGetValue(attacker, out var q)) return;
        var client = _clients[attacker]; // our own read loop: this id is always present
        foreach (var b in q.log.BumpKills(q.chara, mob))
            Send(client, Net.Encode(Net.QuestKill, w =>
            {
                w.Write(b.quest);
                w.Write(b.index);
                w.Write(b.count);
            }));
    }

    // The Attack carries no body: the swing is judged from the sender's last
    // State (its position and facing) and its Build (combat stats; CLI
    // clients never send one, so they swing as beginners). Every mob in
    // reach takes one hit. Judge and broadcast under one lock: two swings
    // landing together must reach every screen in the order they were judged,
    // or HP would jump back up.
    // No swing cooldown on purpose: LAN self-play trusts the client (like movement).
    private void OnAttack(int id)
    {
        lock (_mobLock)
        {
            if (!_pos.TryGetValue(id, out var p)) return; // no State yet: nothing to judge from
            if (!_builds.TryGetValue(id, out var build)) build = Progression.Beginner;
            foreach (var h in _mobs.Strike(p.map, p.x, p.y, p.facing, Random.Shared, build, id))
                BroadcastHit(h, id);
            BroadcastFreshDrops();
        }
    }

    // A skill cast: damaging actives are judged from the sender's last State
    // and Build like a swing (their hits carry the skill id for hit sparks);
    // utility (War Leap) and buffs move on the caster's own screen, so the
    // server only relays the effect. The caster plays its own effect live
    // and skips this echo. Unknown skill ids mean a foreign client. No
    // MP/cooldown/level checks: LAN self-play trusts the client (the
    // Character data lives client-side; pct arrives cast-ready).
    private void OnSkill(int id, int skill, int pct)
    {
        lock (_mobLock)
        {
            if (!_pos.TryGetValue(id, out var p)) return; // no State yet: nothing to judge from
            bool damage = _mobs.IsDamageSkill(skill); // unknown id throws: a foreign client
            Broadcast(Net.Encode(Net.SkillFx, w =>
            {
                w.Write(id);
                w.Write(skill);
                w.Write(p.map);
                w.Write(p.x);
                w.Write(p.y);
                w.Write(p.facing);
            }), id);
            if (!damage) return; // utility/buff: the effect is the whole message
            if (!_builds.TryGetValue(id, out var build)) build = Progression.Beginner;
            foreach (var h in _mobs.SkillStrike(p.map, p.x, p.y, p.facing, Random.Shared, build, skill, pct, id))
                BroadcastHit(h, id);
            BroadcastFreshDrops();
        }
    }

    // One pickup: the drop must exist on the taker's map within reach. The
    // take carries the loot, so the taker's client claims it and every other
    // client just clears the view. A miss sends nothing (the client retries).
    private void OnPickup(int id, int uid)
    {
        lock (_mobLock)
        {
            if (!_pos.TryGetValue(id, out var p)) return; // no State yet: nothing to judge from
            var d = _mobs.Pickup(uid, p.map, p.x, p.y);
            if (d == null) return;
            BroadcastDropTake(d.Uid, d.Map, id, d.Kind, d.Id, d.Count);
        }
    }

    // Kill drops land right after the judging that made them (same lock, so
    // the take race starts after every screen could see the drop).
    private void BroadcastFreshDrops()
    {
        foreach (var d in _mobs.TakeDrops())
            Broadcast(DropSpawnFrame(d));
    }

    // One drop gone: claimed (taker + loot) or expired (taker 0, no loot).
    private void BroadcastDropTake(int uid, int map, int taker, int kind, int id, int count) =>
        Broadcast(Net.Encode(Net.DropTake, w =>
        {
            w.Write(uid);
            w.Write(map);
            w.Write(taker);
            w.Write(kind);
            w.Write(id);
            w.Write(count);
        }));

    // One judged hit to every client (basic swings and skills share this wire shape).
    // A killing blow also ticks the attacker's quests (serverside copy) and
    // pushes the bumped counters back to the killer only.
    private void BroadcastHit(MobWorld.Hit h, int attacker)
    {
        Broadcast(Net.Encode(Net.MobHit, w =>
        {
            w.Write(h.Uid);
            w.Write(h.Map);
            w.Write(h.Damage);
            w.Write(h.HpLeft);
            w.Write(attacker);
            w.Write(h.Exp);
            w.Write(h.Skill);
        }));
        if (h.HpLeft == 0)
            PushQuestKills(attacker, h.Mob);
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
                Send(client, MobSpawnFrame(s));
            foreach (var d in _mobs.DropsOn(map))
                Send(client, DropSpawnFrame(d));
        }
    }

    // Spawn frames, built once: the tick loop and map entry share them.
    private static byte[] MobSpawnFrame(MobWorld.Spawn s) =>
        Net.Encode(Net.MobSpawn, w =>
        {
            w.Write(s.Uid);
            w.Write(s.Mob);
            w.Write(s.Map);
            w.Write(s.X);
            w.Write(s.Y);
            w.Write(s.MaxHp);
            w.Write(s.Hp);
        });

    private static byte[] DropSpawnFrame(MobWorld.Drop d) =>
        Net.Encode(Net.DropSpawn, w =>
        {
            w.Write(d.Uid);
            w.Write(d.Map);
            w.Write(d.X);
            w.Write(d.Y);
            w.Write(d.Kind);
            w.Write(d.Id);
            w.Write(d.Count);
        });

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
        _responder?.Dispose();
        foreach (var kv in _clients) kv.Value.Tcp.Close();
    }
}
