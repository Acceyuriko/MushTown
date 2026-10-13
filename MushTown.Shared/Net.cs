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
    public const byte MobHit = 7;  // server -> client: int uid, int map, int damage, int hpLeft (0 = dead)

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
public readonly record struct MobEvent(
    bool Spawn, int Uid, int Mob, int Map, float X, float Y, int MaxHp, int Damage, int HpLeft);

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
