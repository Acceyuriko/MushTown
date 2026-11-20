namespace MushTown.Shared;

// W5 net protocol (extended through W13): TCP, one 4-byte little-endian length header, then a payload
// of type byte + body. Both ends run the same build, so no versioning yet.
public static class Net
{
    public const int Port = 16631;

    public const byte Welcome = 2; // server -> client: int id ("you are N")
    public const byte State = 3;   // both ways: int id, int map, float x, float y, byte action, byte frame, byte facing
    public const byte Leave = 4;   // server -> client: int id (a peer is gone)
    public const byte Attack = 5;  // client -> server: no body (server judges from the last State)
    public const byte MobSpawn = 6; // server -> client: int uid, int mob, int map, float x, float y, long maxHp, long hp
    public const byte MobHit = 7;  // server -> client: int uid, int map, long damage, long hpLeft (0 = dead), int attacker, long exp, int skill (0 = basic), byte crit, byte settle, int mpGain (MP Eater: MP absorbed from the mob, the attacker's client adds it), byte dot (1 = a tick of a damage-over-time effect: it sets HP and death but plays no flinch and no spark)
    public const byte Build = 8;   // client -> server: Build fields (resent on level-up/learn/buff)
    public const byte SkillCast = 9; // client -> server: int skill, int pct (cast-time damage pct, orbs folded in; buffs send 0)
    public const int BlockPct = 1;   // the pct a non-damage SkillCast carries when it reports a blocked status (Elemental Adaptation); a plain cast sends 0
    public const byte SkillFx = 10;  // server -> client: int attacker, int skill, int map, float x, float y, byte facing, byte block (1 = a status was blocked: play the block art, not the cast)
    public const byte MobMove = 11;  // server -> client: int uid, int map, float x, float y, byte facing (10Hz, occupied maps only)
    public const byte Hurt = 12;     // server -> client: int uid, int mob, int map, long damage (sent to the victim only, so no victim id rides along)
    public const byte DropSpawn = 13; // server -> client: int uid, int map, float x, float y, float fromX, float fromY, int kind, int id, int count, long amount, byte fresh, byte tab
    public const byte DropTake = 14; // server -> client: int uid, int map, int taker, int kind, int id, int count, long amount (taker 0 = expired)
    public const byte Pickup = 15;   // client -> server: int uid (the drop to grab; server checks map + reach)
    public const byte Name = 16;     // client -> server: string name, int plate; relay adds int id first
    public const byte QuestSync = 17; // client -> server: int level, int n + n quest ids, int n + n done ids, int n + n (string key, int count) kills
    public const byte QuestAccept = 18; // client -> server: int quest (accept intent; server confirms with QuestOk)
    public const byte QuestComplete = 19; // client -> server: int quest (turn-in intent; server confirms with QuestOk)
    public const byte QuestOk = 20;  // server -> client: int quest, byte completed (the intent passed on the server's copy)
    public const byte QuestKill = 21; // server -> client: int quest, int index, int count (authoritative counter)
    public const byte Chat = 22; // client -> server: string text; the server relays int id, string name, string text

    public const byte PartyMake = 23;   // client -> server: make a party (the sender leads it)
    public const byte PartyInvite = 24; // client -> server: int target (the target gets the ask)
    public const byte PartyAsk = 25;    // server -> client: int from, string name (the invite)
    public const byte PartyAnswer = 26; // client -> server: byte accept
    public const byte PartyInfo = 27;   // server -> client: int n + n * (int id, string name, int level, int hpPct)
    public const byte PartyLeave = 28;  // client -> server: leave the party
    public const byte PartyChat = 29;   // client -> server: string text; the server relays string name, string text to the party
    public const byte PartyList = 30;   // client -> server: the players of my map (the invite list)
    public const byte PartyPlayers = 31; // server -> client: int n + n * (int id, string name, int level, int hpPct)

    public const byte ExpShare = 32; // server -> client: long exp (a party member's share of a kill)
    public const byte QuestAbandon = 33; // client -> server: int quest (forfeit intent; server confirms with QuestGone)
    public const byte QuestGone = 34; // server -> client: int quest (the forfeit landed on the server's copy)
    public const byte PartyHp = 35; // client -> server: byte HP percent (0..100)
    public const byte CashShop = 36; // client -> server: no body (the player stepped out of the map; the next State returns)
    public const byte DropItem = 37; // client -> server: int itemId, int count, byte tab (drop at the last State's map/x/y; tab 4 = mesos, the two ints are a long amount)
    public const byte Appearance = 38; // client -> server: appearance body; the server relays int id + body to the same map
    public const byte Ball = 39; // server -> client: int skill, int map, float fromX, fromY, toX, toY, int delayMs (the attack-frame delay before the ball; its damage comes as MobHit when it arrives)
    public const byte MarkStack = 40; // server -> client (the owner only): int uid, int stacks (Empirical Knowledge on that mob; 0 = gone)
    public const byte DrainStacks = 41; // server -> client (the owner only): byte layers (Elemental Drain layers near the player; 0 = none)
    public const byte Zone = 42; // server -> client: int uid, int skill, int map, float left, float right, float y (the caster's feet), int lifeMs, int delayMs, byte gone (a ground zone appears: lifeMs of it left after delayMs of waiting; gone 1 = take it away now)
    public const byte MobDot = 43; // server -> client (everyone): int uid, int skill, byte on (a skill's DoT is on that mob = 1, or has left it = 0: the client draws that skill's own head icon)

    // A meso drop rides DropItem with tab 4: itemId holds the high 32 bits and
    // count the low 32, so the amount stays one long without a new command.
    public static (int High, int Low) SplitDropAmount(long amount) =>
        ((int)(amount >> 32), (int)(uint)amount);

    public static long JoinDropAmount(int high, int low) => ((long)high << 32) | (uint)low;

    public const byte ActStand = 0;
    public const byte ActWalk = 1;
    public const byte ActJump = 2;
    public const byte ActLadder = 3;
    public const byte ActRope = 4;
    public const byte ActProne = 5;
    public const byte ActSwing = 6;
    public const byte ActSwingT = 7;
    public const byte ActSwingP = 8;
    public const byte ActStabO = 9;
    public const byte ActStabT = 10;
    public const byte ActShoot = 11;
    public const byte ActAlert = 12;
    public const byte ActGhostStand = 13; // the dead character
    public const byte ActSwingO2 = 14;
    public const byte ActSwingO3 = 15;
    public const byte ActFly = 16; // the floating pose (Mana Wave's slow fall: WZ manaWaveFloat is fly, frame 0)
    public const byte ActStabO2 = 17; // the last step of the Flame Orb and Explosion scripts (WZ stabO2: two frames); a State with this action byte is dropped whole by a client without it

    // Every character action, in load order. Exporter and client init reuse
    // this so a new action needs one list edit (plus its code switch arms).
    // 18 actions: the six body poses (stand1 to prone), the six attack
    // motions (swingO1, swingT1, swingP1, stabO1, stabT1, shoot1), alert (the
    // hit pose), ghoststand (the dead character), swingO2 and swingO3
    // (the other two swings of the wand and staff pool), then fly (the pose
    // of a float), then stabO2 (the stab of the Flame Orb and Explosion body scripts). The code of each action is its index here.
    // A new action is a new State action byte value: NetClient.Enqueue only queues a State whose action byte is below Actions.Length, so a client without the new action
    // silently drops that whole State (the player's place is not updated either). Host and joiners must run the same build.
    public static readonly string[] Actions =
    {
        "stand1", "walk1", "jump", "ladder", "rope", "prone",
        "swingO1", "swingT1", "swingP1", "stabO1", "stabT1", "shoot1",
        "alert", "ghoststand", "swingO2", "swingO3", "fly", "stabO2",
    };

    // Action name <-> code. Both throw on bad input (fail loud, never guess).
    public static byte ActionCode(string action) => action switch
    {
        "stand1" => ActStand,
        "walk1" => ActWalk,
        "jump" => ActJump,
        "ladder" => ActLadder,
        "rope" => ActRope,
        "prone" => ActProne,
        "swingO1" => ActSwing,
        "swingT1" => ActSwingT,
        "swingP1" => ActSwingP,
        "stabO1" => ActStabO,
        "stabT1" => ActStabT,
        "shoot1" => ActShoot,
        "alert" => ActAlert,
        "ghoststand" => ActGhostStand,
        "swingO2" => ActSwingO2,
        "swingO3" => ActSwingO3,
        "fly" => ActFly,
        "stabO2" => ActStabO2,
        _ => throw new InvalidDataException($"bad action name {action}"),
    };

    public static string ActionName(byte action) => action switch
    {
        ActStand => "stand1",
        ActWalk => "walk1",
        ActJump => "jump",
        ActLadder => "ladder",
        ActRope => "rope",
        ActProne => "prone",
        ActSwing => "swingO1",
        ActSwingT => "swingT1",
        ActSwingP => "swingP1",
        ActStabO => "stabO1",
        ActStabT => "stabT1",
        ActShoot => "shoot1",
        ActAlert => "alert",
        ActGhostStand => "ghoststand",
        ActSwingO2 => "swingO2",
        ActSwingO3 => "swingO3",
        ActFly => "fly",
        ActStabO2 => "stabO2",
        _ => throw new InvalidDataException($"bad action code {action}"),
    };

    // One send-ready frame: length header + type byte + body.
    public static byte[] Encode(byte type, Action<BinaryWriter> writeBody)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(type);
        writeBody(w);
        return Frame(ms.ToArray());
    }

    // Wrap a bare payload (type byte + body) with the length header.
    public static byte[] Frame(byte[] payload)
    {
        byte[] frame = new byte[4 + payload.Length];
        BitConverter.GetBytes(payload.Length).CopyTo(frame, 0);
        payload.CopyTo(frame, 4);
        return frame;
    }

    // The appearance body, without a player id: the sender writes it, the
    // server reads it, prepends the id, and relays it.
    public static void WriteAppearance(BinaryWriter w, int skin, int hair, int face,
        IReadOnlyList<int> cashWorn, IReadOnlyList<int> equips)
    {
        w.Write((byte)skin);
        w.Write(hair);
        w.Write(face);
        w.Write(cashWorn.Count);
        foreach (int id in cashWorn) w.Write(id);
        w.Write(equips.Count);
        foreach (int id in equips) w.Write(id);
    }

    public static AppearanceEvent ReadAppearance(BinaryReader r, int id)
    {
        int skin = r.ReadByte();
        int hair = r.ReadInt32();
        int face = r.ReadInt32();
        int cashCount = ReadCount(r, "cash worn");
        var cashWorn = new int[cashCount];
        for (int i = 0; i < cashCount; i++) cashWorn[i] = r.ReadInt32();
        int equipCount = ReadCount(r, "equips");
        var equips = new int[equipCount];
        for (int i = 0; i < equipCount; i++) equips[i] = r.ReadInt32();
        return new AppearanceEvent(id, skin, hair, face, cashWorn, equips);
    }

    // A bad count must fail before anything allocates: a 1KB frame can hold
    // hundreds of ids, but a doll has at most two dozen pieces.
    private static int ReadCount(BinaryReader r, string name)
    {
        int count = r.ReadInt32();
        if (count is < 0 or > 64) throw new FormatException($"bad appearance {name} count {count}");
        return count;
    }

    // The relayed frame: the sender id, then the same body.
    public static byte[] AppearanceFrame(AppearanceEvent a) =>
        Encode(Appearance, w =>
        {
            w.Write(a.Id);
            WriteAppearance(w, a.Skin, a.Hair, a.Face, a.CashWorn, a.Equips);
        });
}

// One event from the network for the game loop: a peer's fresh state,
// or Gone when a peer left (then only Id means anything).
public readonly record struct RemoteEvent(
    bool Gone, int Id, int Map, float X, float Y, byte Action, byte Frame, bool FacingRight);

// One peer's name for the game loop: sent on connect and re-sent when the
// plate changes (ring worn or taken off). Plate is the NameTag node (0 = plain).
public readonly record struct NameEvent(int Id, string Name, int Plate);

// One peer's look: skin, hair, face and the two worn lists. The client sends
// it on connect and after every look change; the server relays it with the
// sender id. Missing lists are empty, never null.
public readonly record struct AppearanceEvent(int Id, int Skin, int Hair, int Face, int[] CashWorn, int[] Equips);

// One mob event from the server: a spawn (Spawn true, Mob/MaxHp set, HpLeft
// is the live HP) or a hit (Spawn false, Damage/HpLeft set, HpLeft 0 means dead).
// A hit also carries its Attacker id (kill credit goes to that client only),
// the mob's Exp (awarded on the killing blow), the Skill id (0 = basic swing,
// else the skill whose hit spark each client plays on the mob), and whether
// the roll critted (the critical damage-number art). Settle: the hit carries the mob's HP, death and EXP (a
// multi-segment cast settles on its first segment only; the rest are numbers). Dot: a tick of a DoT (a settled hit with no flinch and no spark).
public readonly record struct MobEvent(
    bool Spawn, int Uid, int Mob, int Map, float X, float Y, long MaxHp, long Damage, long HpLeft,
    int Attacker, long Exp, int Skill, bool Crit = false, bool Settle = true, int MpGain = 0, bool Dot = false);

// One skill effect from the server: Attacker cast Skill at X/Y facing right
// or left (Block: a blocked status, Elemental Adaptation's block art, not its cast). The caster plays its own effect live and never gets this echo.
// Clients drop effects for other maps, like states.
public readonly record struct SkillFxEvent(int Attacker, int Skill, int Map, float X, float Y, bool FacingRight, bool Block = false);

// One ball thrown by a cast (Energy Bolt): Skill's art flies from From to To on Map after DelayMs, the body
// action's attack-frame delay. Every client on the map gets it, the caster too. The flight time is
// Projectile.FlightMs of the four numbers.
public readonly record struct BallEvent(int Skill, int Map, float FromX, float FromY, float ToX, float ToY, int DelayMs = 0);

// One ground zone (Poison Mist): Skill's art fills the box Left..Right (the vertical size is the skill's lt/rb) round Y of Map (the caster's feet at the cast) for LifeMs after DelayMs (the body action's attack frame;
// a client that enters later gets the time that is left). Gone: the caster left, take the zone away now. Every client on the map gets it, the caster too.
public readonly record struct ZoneEvent(int Uid, int Skill, int Map, float Left, float Right, float Y, int LifeMs, int DelayMs, bool Gone = false);

// One mob step from the server: Uid now stands at X/Y facing right or left.
// Clients drop moves for other maps and for mobs mid-flinch (hit1/die1 own
// the pose until they finish).
public readonly record struct MoveEvent(int Uid, int Map, float X, float Y, bool FacingRight);

// One DoT skill that came on a mob (On) or left it, sent to everyone: the client keeps one head icon for each skill on the mob.
public readonly record struct MobDotEvent(int Uid, int Skill, bool On);

// One Empirical Knowledge stack change on a mob, sent only to its owner: Uid now has Stacks (0 = the marks are gone).
public readonly record struct MarkEvent(int Uid, int Stacks);

// One Elemental Drain layer update, sent only to the caster: Stacks layers count near the player now (0 = none).
public readonly record struct DrainEvent(int Stacks);

// One touch that landed on us: Mob's Uid dealt Damage (pre-DEF). Sent to
// the victim only, so no victim id rides along.
public readonly record struct HurtEvent(int Uid, int Mob, int Map, long Damage);

// One drop event: a spawn (Spawn true, Kind/Id/Count/Amount, From, and the
// return bag Tab set; Fresh false is a map-entry snapshot) or a take (Spawn
// false, Tab empty: the client reads the tab from its own drop view). The
// taker's client claims the loot; taker 0 means expired.
public readonly record struct DropEvent(
    bool Spawn, int Uid, int Map, float X, float Y, float FromX, float FromY, int Kind, int Id, int Count, long Amount, int Taker, bool Fresh, string Tab = "");

// One quest event from the server: a confirmed intent (Kill false, Quest +
// Completed set) or an authoritative kill counter (Kill true, Quest/Index/Count).
public readonly record struct QuestEvent(bool Kill, int Quest, int Index, int Count, bool Completed, bool Abandoned = false);

// One chat line from the server: who said what (the sender name and the
// text). The server adds the name to every line.
public readonly record struct ChatEvent(string Name, string Text);

// One party member: the id, the name, the level and the HP percent. A player
// that has not reported HP yet is at 100.
public readonly record struct PartyMember(int Id, string Name, int Level, int HpPct = 100);

// The party member list from the server. An empty list means no party. The
// first member is the leader.
public readonly record struct PartyEvent(PartyMember[] Members);

// The players of the map, for the invite list: the id, the name, the level and
// the HP percent.
public readonly record struct PartyPlayersEvent(PartyMember[] Players);

// An invite to join a party: who asked. The client answers the server.
public readonly record struct PartyAskEvent(int Id, string Name);

// Accumulates raw TCP bytes and yields whole payloads (header eaten).
// TCP splits and merges at will, so one Feed can yield zero, one, or more.
public sealed class NetReader
{
    private readonly List<byte> _buf = new();

    public List<byte[]> Feed(byte[] chunk, int count)
    {
        _buf.AddRange(new ArraySegment<byte>(chunk, 0, count));
        var out_ = new List<byte[]>();
        while (_buf.Count >= 4)
        {
            int len = _buf[0] | (_buf[1] << 8) | (_buf[2] << 16) | (_buf[3] << 24);
            if (len < 1 || len > 1024)
                throw new InvalidDataException($"bad net frame length: {len}");
            if (_buf.Count < 4 + len) break;
            byte[] payload = new byte[len];
            _buf.CopyTo(4, payload, 0, len);
            _buf.RemoveRange(0, 4 + len);
            out_.Add(payload);
        }
        return out_;
    }
}
