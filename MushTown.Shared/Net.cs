namespace MushTown.Shared;

// W5 net protocol (extended in W8): TCP, one 4-byte little-endian length header, then a payload
// of type byte + body. Both ends run the same build, so no versioning yet.
public static class Net
{
    public const int Port = 16631;

    public const byte Welcome = 2; // server -> client: int id ("you are N")
    public const byte State = 3;   // both ways: int id, int map, float x, float y, byte action, byte frame, byte facing
    public const byte Leave = 4;   // server -> client: int id (a peer is gone)
    public const byte Attack = 5;  // client -> server: no body (server judges from the last State)
    public const byte MobSpawn = 6; // server -> client: int uid, int mob, int map, float x, float y, int maxHp, int hp
    public const byte MobHit = 7;  // server -> client: int uid, int map, int damage, int hpLeft (0 = dead), int attacker, int exp, int skill (0 = basic)
    public const byte Build = 8;   // client -> server: int str, int dex, int weaponAtk, int dmgPct, int critPct, int finalPct (resent on level-up/learn/buff)
    public const byte SkillCast = 9; // client -> server: int skill, int pct (cast-time damage pct, orbs folded in; buffs send 0)
    public const byte SkillFx = 10;  // server -> client: int attacker, int skill, int map, float x, float y, byte facing
    public const byte MobMove = 11;  // server -> client: int uid, int map, float x, float y, byte facing (10Hz, occupied maps only)
    public const byte Hurt = 12;     // server -> client: int uid, int mob, int map, int damage (sent to the victim only, so no victim id rides along)
    public const byte DropSpawn = 13; // server -> client: int uid, int map, float x, float y, int kind, int id, int count
    public const byte DropTake = 14; // server -> client: int uid, int map, int taker, int kind, int id, int count (taker 0 = expired)
    public const byte Pickup = 15;   // client -> server: int uid (the drop to grab; server checks map + reach)

    public const byte ActStand = 0;
    public const byte ActWalk = 1;
    public const byte ActJump = 2;
    public const byte ActLadder = 3;
    public const byte ActRope = 4;
    public const byte ActProne = 5;
    public const byte ActSwing = 6;

    // Every character action, in load order. Exporter and client init reuse
    // this so a new action needs one list edit (plus its code switch arms).
    public static readonly string[] Actions = { "stand2", "walk2", "jump", "ladder", "rope", "prone", "swingP1" };

    // Action name <-> code. Both throw on bad input (fail loud, never guess).
    public static byte ActionCode(string action) => action switch
    {
        "stand2" => ActStand,
        "walk2" => ActWalk,
        "jump" => ActJump,
        "ladder" => ActLadder,
        "rope" => ActRope,
        "prone" => ActProne,
        "swingP1" => ActSwing,
        _ => throw new InvalidDataException($"bad action name {action}"),
    };

    public static string ActionName(byte action) => action switch
    {
        ActStand => "stand2",
        ActWalk => "walk2",
        ActJump => "jump",
        ActLadder => "ladder",
        ActRope => "rope",
        ActProne => "prone",
        ActSwing => "swingP1",
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
}

// One event from the network for the game loop: a peer's fresh state,
// or Gone when a peer left (then only Id means anything).
public readonly record struct RemoteEvent(
    bool Gone, int Id, int Map, float X, float Y, byte Action, byte Frame, bool FacingRight);

// One mob event from the server: a spawn (Spawn true, Mob/MaxHp set, HpLeft
// is the live HP) or a hit (Spawn false, Damage/HpLeft set, HpLeft 0 means dead).
// A hit also carries its Attacker id (kill credit goes to that client only),
// the mob's Exp (awarded on the killing blow), and the Skill id (0 = basic
// swing, else the skill whose hit spark each client plays on the mob).
public readonly record struct MobEvent(
    bool Spawn, int Uid, int Mob, int Map, float X, float Y, int MaxHp, int Damage, int HpLeft,
    int Attacker, int Exp, int Skill);

// One skill effect from the server: Attacker cast Skill at X/Y facing right
// or left. The caster plays its own effect live and never gets this echo.
// Clients drop effects for other maps, like states.
public readonly record struct SkillFxEvent(int Attacker, int Skill, int Map, float X, float Y, bool FacingRight);

// One mob step from the server: Uid now stands at X/Y facing right or left.
// Clients drop moves for other maps and for mobs mid-flinch (hit1/die1 own
// the pose until they finish).
public readonly record struct MoveEvent(int Uid, int Map, float X, float Y, bool FacingRight);

// One touch that landed on us: Mob's Uid dealt Damage (pre-DEF). Sent to
// the victim only, so no victim id rides along.
public readonly record struct HurtEvent(int Uid, int Mob, int Map, int Damage);

// One drop event: a spawn (Spawn true, Kind/Id/Count set) or a take (Spawn
// false). The taker's client claims Kind/Id/Count; taker 0 means expired.
public readonly record struct DropEvent(
    bool Spawn, int Uid, int Map, float X, float Y, int Kind, int Id, int Count, int Taker);

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
