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
    private readonly ConcurrentQueue<MobEvent> _mobEvents = new();
    private readonly ConcurrentQueue<SkillFxEvent> _fxEvents = new();
    private readonly ConcurrentQueue<MoveEvent> _moveEvents = new();
    private readonly ConcurrentQueue<HurtEvent> _hurtEvents = new();
    private readonly ConcurrentQueue<DropEvent> _dropEvents = new();
    private readonly ConcurrentQueue<NameEvent> _nameEvents = new();
    private readonly ConcurrentQueue<QuestEvent> _questEvents = new();
    private readonly ConcurrentQueue<ChatEvent> _chatEvents = new();
    private readonly ConcurrentQueue<PartyEvent> _partyEvents = new();
    private readonly ConcurrentQueue<PartyPlayersEvent> _partyPlayersEvents = new();
    private readonly ConcurrentQueue<PartyAskEvent> _partyAskEvents = new();
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
        SendFrame(Net.Encode(Net.State, w =>
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

    // One basic-attack swing: the body is empty, the server judges from our last State.
    public void SendAttack()
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.Attack, w => { }));
    }

    // Our combat stats, so the server judges our swings on the real attacker.
    // Sent on connect and resent whenever they change (level-up, learn, buff).
    public void SendBuild(Build build)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.Build, w =>
        {
            w.Write(build.Str);
            w.Write(build.Dex);
            w.Write(build.WeaponAtk);
            w.Write(build.DmgPct);
            w.Write(build.CritPct);
            w.Write(build.FinalPct);
        }));
    }

    // One skill cast: skill id plus the cast-time damage pct (skill level
    // and combo orbs folded in; buffs send 0). The server judges damage
    // skills and relays every cast's effect.
    public void SendSkillCast(int skill, int pct)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.SkillCast, w => { w.Write(skill); w.Write(pct); }));
    }

    // One pickup: the drop uid under our Z key. The server checks map +
    // reach and answers with a DropTake (a miss answers nothing).
    public void SendPickup(int uid)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.Pickup, w => w.Write(uid)));
    }

    // Our name + plate, so peers draw our tag (sent on connect, re-sent on ring change).
    public void SendName(string name, int plate)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.Name, w => { w.Write(name); w.Write(plate); }));
    }

    // Our quest state, so the server judges from its own copy (sent on
    // connect, re-sent on level-up: level is the only field the server
    // cannot evolve itself). Small: a dozen quests fit the 1024 frame.
    public void SendQuestSync(CharaSlot chara)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.QuestSync, w =>
        {
            w.Write(chara.Level);
            w.Write(chara.Quests.Count);
            foreach (int q in chara.Quests) w.Write(q);
            w.Write(chara.QuestsDone.Count);
            foreach (int q in chara.QuestsDone) w.Write(q);
            w.Write(chara.QuestKills.Count);
            foreach (var kv in chara.QuestKills) { w.Write(kv.Key); w.Write(kv.Value); }
        }));
    }

    // Accept/turn-in intents: the server runs the act on its copy and
    // answers QuestOk (silence means the act failed there).
    public void SendQuestAccept(int quest)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.QuestAccept, w => w.Write(quest)));
    }

    public void SendQuestComplete(int quest)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.QuestComplete, w => w.Write(quest)));
    }

    // One chat line: only the text goes out. The server adds the player id
    // and the name and relays the line to every client.
    public void SendChat(string text)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.Chat, w => w.Write(text)));
    }

    // Make a party (we lead it).
    public void SendPartyMake()
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.PartyMake, w => { }));
    }

    // Ask another player to join our party.
    public void SendPartyInvite(int target)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.PartyInvite, w => w.Write(target)));
    }

    // The answer to an invite we got.
    public void SendPartyAnswer(bool accept)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.PartyAnswer, w => w.Write((byte)(accept ? 1 : 0))));
    }

    // Leave the party.
    public void SendPartyLeave()
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.PartyLeave, w => { }));
    }

    // One party chat line: the server relays it to the party only.
    public void SendPartyChat(string text)
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.PartyChat, w => w.Write(text)));
    }

    // Ask for the players of our map (the invite list).
    public void SendPartyList()
    {
        if (!Connected) return;
        SendFrame(Net.Encode(Net.PartyList, w => { }));
    }

    private void SendFrame(byte[] frame)
    {
        try
        {
            lock (_sendLock)
                _tcp!.GetStream().Write(frame);
        }
        catch (Exception ex)
        {
            _log($"send failed: {ex.Message}");
            Connected = false;
        }
    }

    public bool TryTake(out RemoteEvent e) => _events.TryDequeue(out e);

    public bool TryTakeMob(out MobEvent e) => _mobEvents.TryDequeue(out e);

    public bool TryTakeFx(out SkillFxEvent e) => _fxEvents.TryDequeue(out e);

    public bool TryTakeMove(out MoveEvent e) => _moveEvents.TryDequeue(out e);

    public bool TryTakeHurt(out HurtEvent e) => _hurtEvents.TryDequeue(out e);

    public bool TryTakeDrop(out DropEvent e) => _dropEvents.TryDequeue(out e);

    public bool TryTakeName(out NameEvent e) => _nameEvents.TryDequeue(out e);

    public bool TryTakeQuest(out QuestEvent e) => _questEvents.TryDequeue(out e);

    public bool TryTakeChat(out ChatEvent e) => _chatEvents.TryDequeue(out e);

    public bool TryTakeParty(out PartyEvent e) => _partyEvents.TryDequeue(out e);

    public bool TryTakePartyPlayers(out PartyPlayersEvent e) => _partyPlayersEvents.TryDequeue(out e);

    public bool TryTakePartyAsk(out PartyAskEvent e) => _partyAskEvents.TryDequeue(out e);

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
        else if (p[0] == Net.MobSpawn && p.Length == 29)
            _mobEvents.Enqueue(new MobEvent(true,
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5), BitConverter.ToInt32(p, 9),
                BitConverter.ToSingle(p, 13), BitConverter.ToSingle(p, 17),
                BitConverter.ToInt32(p, 21), 0, BitConverter.ToInt32(p, 25), 0, 0, 0));
        else if (p[0] == Net.MobHit && p.Length == 29)
            _mobEvents.Enqueue(new MobEvent(false,
                BitConverter.ToInt32(p, 1), 0, BitConverter.ToInt32(p, 5), 0, 0, 0,
                BitConverter.ToInt32(p, 9), BitConverter.ToInt32(p, 13),
                BitConverter.ToInt32(p, 17), BitConverter.ToInt32(p, 21), BitConverter.ToInt32(p, 25)));
        else if (p[0] == Net.SkillFx && p.Length == 22)
            _fxEvents.Enqueue(new SkillFxEvent(
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5), BitConverter.ToInt32(p, 9),
                BitConverter.ToSingle(p, 13), BitConverter.ToSingle(p, 17), p[21] != 0));
        else if (p[0] == Net.MobMove && p.Length == 18)
            _moveEvents.Enqueue(new MoveEvent(
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5),
                BitConverter.ToSingle(p, 9), BitConverter.ToSingle(p, 13), p[17] != 0));
        else if (p[0] == Net.Hurt && p.Length == 17)
            _hurtEvents.Enqueue(new HurtEvent(
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5), BitConverter.ToInt32(p, 9),
                BitConverter.ToInt32(p, 13)));
        else if (p[0] == Net.DropSpawn && p.Length == 29)
            _dropEvents.Enqueue(new DropEvent(true,
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5),
                BitConverter.ToSingle(p, 9), BitConverter.ToSingle(p, 13),
                BitConverter.ToInt32(p, 17), BitConverter.ToInt32(p, 21), BitConverter.ToInt32(p, 25), 0));
        else if (p[0] == Net.DropTake && p.Length == 25)
            _dropEvents.Enqueue(new DropEvent(false,
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5), 0, 0,
                BitConverter.ToInt32(p, 13), BitConverter.ToInt32(p, 17), BitConverter.ToInt32(p, 21),
                BitConverter.ToInt32(p, 9)));
        else if (p[0] == Net.QuestOk && p.Length == 6)
            _questEvents.Enqueue(new QuestEvent(false,
                BitConverter.ToInt32(p, 1), 0, 0, p[5] != 0));
        else if (p[0] == Net.QuestKill && p.Length == 13)
            _questEvents.Enqueue(new QuestEvent(true,
                BitConverter.ToInt32(p, 1), BitConverter.ToInt32(p, 5), BitConverter.ToInt32(p, 9), false));
        else if (p[0] == Net.Name && p.Length > 9)
        {
            try
            {
                using var ms = new MemoryStream(p, 1, p.Length - 1);
                using var r = new BinaryReader(ms);
                _nameEvents.Enqueue(new NameEvent(r.ReadInt32(), r.ReadString(), r.ReadInt32()));
            }
            catch (Exception ex) when (ex is IOException or FormatException) // malformed frames are ignored, never fatal
            {
                _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
            }
        }
        else if (p[0] == Net.Chat)
        {
            try
            {
                using var ms = new MemoryStream(p, 1, p.Length - 1);
                using var r = new BinaryReader(ms);
                _chatEvents.Enqueue(new ChatEvent(r.ReadString(), r.ReadString()));
            }
            catch (Exception ex) when (ex is IOException or FormatException) // malformed frames are ignored, never fatal
            {
                _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
            }
        }
        else if (p[0] == Net.PartyAsk)
        {
            try
            {
                using var ms = new MemoryStream(p, 1, p.Length - 1);
                using var r = new BinaryReader(ms);
                _partyAskEvents.Enqueue(new PartyAskEvent(r.ReadInt32(), r.ReadString()));
            }
            catch (Exception ex) when (ex is IOException or FormatException) // malformed frames are ignored, never fatal
            {
                _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
            }
        }
        else if (p[0] == Net.PartyInfo)
        {
            try
            {
                using var ms = new MemoryStream(p, 1, p.Length - 1);
                using var r = new BinaryReader(ms);
                int n = r.ReadInt32();
                if (n < 0 || n > 64) throw new FormatException("bad party count");
                var members = new PartyMember[n];
                for (int i = 0; i < n; i++) members[i] = new PartyMember(r.ReadInt32(), r.ReadString(), r.ReadInt32());
                _partyEvents.Enqueue(new PartyEvent(members));
            }
            catch (Exception ex) when (ex is IOException or FormatException) // malformed frames are ignored, never fatal
            {
                _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
            }
        }
        else if (p[0] == Net.PartyPlayers)
        {
            try
            {
                using var ms = new MemoryStream(p, 1, p.Length - 1);
                using var r = new BinaryReader(ms);
                int n = r.ReadInt32();
                if (n < 0 || n > 64) throw new FormatException("bad player count");
                var players = new PartyMember[n];
                for (int i = 0; i < n; i++) players[i] = new PartyMember(r.ReadInt32(), r.ReadString(), r.ReadInt32());
                _partyPlayersEvents.Enqueue(new PartyPlayersEvent(players));
            }
            catch (Exception ex) when (ex is IOException or FormatException) // malformed frames are ignored, never fatal
            {
                _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
            }
        }
        else if (p[0] == Net.PartyChat)
        {
            try
            {
                using var ms = new MemoryStream(p, 1, p.Length - 1);
                using var r = new BinaryReader(ms);
                // a party line is a normal chat line: the log shows it in the
                // All tab, next to the lines of the world
                _chatEvents.Enqueue(new ChatEvent(r.ReadString(), r.ReadString()));
            }
            catch (Exception ex) when (ex is IOException or FormatException) // malformed frames are ignored, never fatal
            {
                _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
            }
        }
        else
            _log($"bad message (type {p[0]}, {p.Length} bytes), ignored");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _tcp?.Close();
    }
}
