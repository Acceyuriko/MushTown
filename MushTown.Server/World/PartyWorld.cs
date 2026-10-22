using MushTown.Shared;

namespace MushTown.Server;

// The party state: who is in which party. The server owns it, the clients
// only ask. A party holds up to six members; the leader is the first one in
// the list, so the leader leaving promotes the member that comes next.
public sealed class PartyWorld
{
    public const int MaxMembers = 6;

    private readonly object _lock = new();
    private readonly Dictionary<int, List<int>> _parties = new(); // party id -> the members
    private readonly Dictionary<int, int> _partyOf = new(); // member id -> party id
    private readonly Dictionary<int, (int From, int Party)> _invites = new(); // the invited member -> the inviter and the party
    private int _nextId = 1;

    // The party of a member (0 = no party).
    public int PartyOf(int member)
    {
        lock (_lock) return _partyOf.TryGetValue(member, out int p) ? p : 0;
    }

    // The members of a party, the leader first.
    public List<int> Members(int party)
    {
        lock (_lock) return _parties.TryGetValue(party, out var list) ? new List<int>(list) : new List<int>();
    }

    // The party of a member and its member list, read in one go: a member
    // that leaves between two reads must not split its exp with the list it
    // just left.
    public (int Party, List<int> Members) PartyAndMembers(int member)
    {
        lock (_lock)
        {
            if (!_partyOf.TryGetValue(member, out int party)) return (0, new List<int>());
            return (party, new List<int>(_parties[party]));
        }
    }

    // A new party with one member (the leader). The caller holds the lock.
    private int NewParty(int member)
    {
        int id = _nextId++;
        _parties[id] = new List<int> { member };
        _partyOf[member] = id;
        return id;
    }

    // A member without a party gets one and leads it. A member with a party
    // keeps it. A pending invite stays: the answer tells the invited player
    // that they are already in a party.
    public int Make(int member)
    {
        lock (_lock)
        {
            if (_partyOf.TryGetValue(member, out int existing)) return existing;
            return NewParty(member);
        }
    }

    // The party of the member is full: nobody else can join.
    public bool Full(int member)
    {
        lock (_lock)
        {
            if (!_partyOf.TryGetValue(member, out int party)) return false;
            return _parties[party].Count >= MaxMembers;
        }
    }

    // The leader asks another member to join. The answer comes later.
    public bool Invite(int from, int to)
    {
        lock (_lock)
        {
            if (_partyOf.ContainsKey(to)) return false; // already in a party
            if (!_partyOf.TryGetValue(from, out int party)) return false; // the inviter has no party
            if (_parties[party][0] != from) return false; // only the leader invites
            if (_parties[party].Count >= MaxMembers) return false;
            if (_invites.TryGetValue(to, out var pending))
            {
                // another leader is already asking: the first invite stays
                if (pending.From != from) return false;
                return true;
            }
            _invites[to] = (from, party);
            return true;
        }
    }

    // The invited member answers. Accept joins the inviter's party; a
    // decline only drops the invite. The result carries the inviter (0 =
    // there was no invite) and whether the member joined.
    public (int Inviter, bool Joined) Answer(int member, bool accept)
    {
        lock (_lock)
        {
            if (!_invites.Remove(member, out var invite)) return (0, false);
            if (!accept) return (invite.From, false);
            if (_parties[invite.Party].Count >= MaxMembers || _partyOf.ContainsKey(member)) return (invite.From, false);
            _parties[invite.Party].Add(member);
            _partyOf[member] = invite.Party;
            return (invite.From, true);
        }
    }

    // A member leaves the party. The leader leaving promotes the member
    // that comes next (the list keeps its order).
    public void Leave(int member)
    {
        lock (_lock)
        {
            // the invites of a member who left are dead: the ones they sent
            // and the one they got
            foreach (int invited in _invites.Where(kv => kv.Value.From == member || kv.Key == member).Select(kv => kv.Key).ToList())
                _invites.Remove(invited);
            if (!_partyOf.Remove(member, out int party)) return;
            _parties[party].Remove(member);
            if (_parties[party].Count == 0) _parties.Remove(party);
        }
    }
}
