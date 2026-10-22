using MushTown.Shared;

namespace MushTown.Server;

// The party messages: make, invite, answer, leave, chat and the invite list,
// plus the kill exp split. The server owns the party (PartyWorld); the
// clients only ask. The member list goes to every member of the party, so
// all the windows agree.
public sealed partial class GameServer
{
    private readonly PartyWorld _party = new();
    // The party changes and the member list that goes with them sit in one
    // lock: two members leaving at the same time must not push their lists
    // in the wrong order (the client would keep a member who left).
    private readonly object _partyOps = new();
    private const int LevelGate = 5; // the official gate: five levels below the killer

    // The player makes a party and leads it.
    private void OnPartyMake(int id)
    {
        lock (_partyOps) PushParty(_party.Make(id));
    }

    // The leader asks another player to join: the target gets the ask and
    // answers it later. Only a player on the same map can be invited.
    private void OnPartyInvite(int id, int target)
    {
        if (!_clients.TryGetValue(target, out var client) || !SameMap(id, target))
        {
            // the player is gone or on another map (the invite list is a
            // snapshot): the leader hears that the click did nothing
            SystemLine(id, "the invite failed.");
            return;
        }
        if (_party.Full(id))
        {
            // the party has six members: the invite cannot work, so the
            // leader gets a line instead of nothing
            SystemLine(id, "the party is full.");
            return;
        }
        if (!_party.Invite(id, target))
        {
            // the player is in another party or another leader is already
            // asking them: the leader hears that the click did nothing
            SystemLine(id, "the invite failed.");
            return;
        }
        Send(client, Net.Encode(Net.PartyAsk, w =>
        {
            w.Write(id);
            w.Write(PlayerName(id));
        }));
    }

    // Two players are on the same map when their last state says so (a
    // player that never sent one is on map 0).
    private bool SameMap(int a, int b)
    {
        lock (_mobLock)
        {
            int ma = _pos.TryGetValue(a, out var pa) ? pa.map : 0;
            int mb = _pos.TryGetValue(b, out var pb) ? pb.map : 0;
            return ma == mb;
        }
    }

    // The invited player answers: accept joins the inviter's party, a
    // decline drops the invite. The party gets the fresh list.
    private void OnPartyAnswer(int id, bool accept)
    {
        lock (_partyOps)
        {
            var (inviter, joined) = _party.Answer(id, accept);
            if (inviter != 0)
            {
                if (!accept)
                    SystemLine(inviter, PlayerName(id) + " declined the party invite.");
                else if (!joined)
                    // the party filled up while the invite waited
                    SystemLine(id, _party.PartyOf(id) != 0 ? "you are already in a party." : "the party is full.");
            }
            else if (accept)
                // the invite is dead (the inviter left the game): the invited
                // player gets a line, so the click is not lost
                SystemLine(id, "the party invite expired.");
            int party = _party.PartyOf(id);
            if (party != 0) PushParty(party);
        }
    }

    // A line in the chat log without a name: the client draws it as a
    // system line.
    private void SystemLine(int id, string text)
    {
        if (!_clients.TryGetValue(id, out var client)) return;
        Send(client, Net.Encode(Net.Chat, w =>
        {
            w.Write("");
            w.Write(text);
        }));
    }

    // A member leaves: the rest of the party gets the new list, the leaver
    // gets an empty one (no party any more).
    private void OnPartyLeave(int id)
    {
        lock (_partyOps)
        {
            int party = _party.PartyOf(id);
            _party.Leave(id);
            if (party != 0) PushParty(party);
        }
        Send(_clients[id], PartyEmptyFrame());
    }

    // A party chat line: the server adds the name and relays it to the
    // party only. A player without a party sends nothing.
    private void OnPartyChat(int id, byte[] payload)
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
            throw new InvalidDataException($"bad party chat from {id} ({ex.Message})");
        }
        int party = _party.PartyOf(id);
        if (party == 0) return;
        var frame = Net.Encode(Net.PartyChat, w =>
        {
            w.Write(PlayerName(id));
            w.Write(text);
        });
        foreach (int m in _party.Members(party))
            if (_clients.TryGetValue(m, out var c)) Send(c, frame);
    }

    // The invite list: the other players of the requester's map.
    private void OnPartyList(int id)
    {
        int[] others;
        lock (_mobLock)
        {
            int map = _pos.TryGetValue(id, out var p) ? p.map : 0;
            others = _pos.Where(kv => kv.Value.map == map && kv.Key != id).Select(kv => kv.Key).ToArray();
        }
        var players = new List<PartyMember>();
        foreach (int other in others)
        {
            if (_party.PartyOf(other) != 0) continue; // a player in a party cannot be invited
            players.Add(new PartyMember(other, PlayerName(other), PlayerLevel(other)));
        }
        Send(_clients[id], Net.Encode(Net.PartyPlayers, w => WriteMembers(w, players)));
    }

    // The member list to every member of the party (the leader first).
    private void PushParty(int party)
    {
        var members = _party.Members(party);
        var list = new List<PartyMember>();
        foreach (int m in members) list.Add(new PartyMember(m, PlayerName(m), PlayerLevel(m)));
        var frame = Net.Encode(Net.PartyInfo, w => WriteMembers(w, list));
        foreach (int m in members)
            if (_clients.TryGetValue(m, out var c)) Send(c, frame);
    }

    // The member list in a frame: the count and every member.
    private static void WriteMembers(BinaryWriter w, List<PartyMember> members)
    {
        w.Write(members.Count);
        foreach (var m in members)
        {
            w.Write(m.Id);
            w.Write(m.Name);
            w.Write(m.Level);
        }
    }

    // The list of a player without a party: the window goes back to its
    // "no party" state.
    private static byte[] PartyEmptyFrame() => Net.Encode(Net.PartyInfo, w => w.Write(0));

    // The name a player is known by (the chat and the party list use it).
    private string PlayerName(int id)
    {
        lock (_mobLock) return _names.TryGetValue(id, out var n) && n.name != "" ? n.name : "Player " + id;
    }

    // The level a player is known by (0 until the client syncs its quest log).
    private int PlayerLevel(int id)
    {
        lock (_mobLock) return _questLogs.TryGetValue(id, out var q) ? q.chara.Level : 0;
    }

    // The kill exp goes to the party: every member on the same map whose
    // level is close to the killer gets an equal share, the rest stays with
    // the killer. A solo kill (or a party without levels yet) keeps the whole
    // award. The recipients are picked once, so a member that leaves in the
    // middle never eats a share. A share that cannot be sent goes back to
    // the killer: the exp must not vanish.
    private int ShareKillExp(int exp, int killer, int map)
    {
        var (party, members) = _party.PartyAndMembers(killer);
        if (party == 0) return exp;
        var sharing = new List<Client>();
        lock (_mobLock)
        {
            if (!_questLogs.TryGetValue(killer, out var killerLog)) return exp; // no level yet
            // the levels are the server's copy: a level-up reaches the server
            // with the quest sync, so the floor is one round trip behind
            int floor = killerLog.chara.Level - LevelGate;
            foreach (int m in members)
            {
                if (m == killer) continue;
                if (!_clients.TryGetValue(m, out var c)) continue; // gone already
                if (!_pos.TryGetValue(m, out var p) || p.map != map) continue;
                if (!_questLogs.TryGetValue(m, out var q) || q.chara.Level < floor) continue;
                sharing.Add(c);
            }
        }
        if (sharing.Count == 0) return exp;
        int each = exp / (sharing.Count + 1);
        if (each == 0) return exp; // too small to split
        var frame = Net.Encode(Net.ExpShare, w => w.Write(each));
        int sent = 0;
        foreach (var c in sharing)
            if (Send(c, frame)) sent++;
        return exp - each * sent; // the rest stays with the killer
    }
}
