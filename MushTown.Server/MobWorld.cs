using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Server;

// The mob side of the world: spawns, HP, hit judging. The client never runs
// this logic (Q9: movement is client-side, mobs are server-side). Solo play
// drives one directly; net play lets the GameServer drive it for everyone.
public sealed class MobWorld
{
    // One spawn pushed to clients entering the map (live Hp, not MaxHp: a
    // late joiner must see wounded mobs as wounded).
    public sealed record Spawn(int Uid, int Mob, int Map, float X, float Y, int MaxHp, int Hp);

    // One mob caught by a swing. HpLeft 0 means it died (no separate die message).
    public sealed record Hit(int Uid, int Map, int Damage, int HpLeft);

    // Swing reach in pixels: a mob is hit when roughly at swing height and
    // within arm's length ahead (a little behind counts, for jumps landing short).
    private const int ReachX = 90;
    private const int ReachY = 60;
    private const int BehindX = 10;

    private sealed class Live
    {
        public int Uid;
        public int Mob;
        public int Map;
        public float X;
        public float Y;
        public int Hp;
        public int MaxHp;
        public double Def;
    }

    private readonly string _assetsDir;
    private readonly Dictionary<int, MapData> _maps = new();
    private readonly Dictionary<int, (int hp, double def)> _stats = new();
    private readonly List<Live> _lives = new();
    private readonly HashSet<int> _spawned = new();
    private int _nextUid = 1;

    public MobWorld(string assetsDir)
    {
        _assetsDir = assetsDir;
    }

    private static T LoadJson<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream)
            ?? throw new InvalidDataException($"{path} is empty or broken");
    }

    // Spawns a map's life mobs, once per session (re-entry keeps the old
    // bodies: killed mobs stay dead until W13 adds respawn timers). The map
    // counts as spawned only after its lives exist: a failed load must fail
    // again on retry, not go quiet with zero mobs.
    public void EnterMap(int map)
    {
        if (_spawned.Contains(map)) return;
        if (!_maps.TryGetValue(map, out var data))
        {
            data = LoadJson<MapData>(Path.Combine(_assetsDir, "maps", $"{map}.json"));
            _maps[map] = data;
        }
        // All lives join at once: a failed load must leave zero behind, so a
        // retry never duplicates the mobs that loaded before the bad one.
        var fresh = new List<Live>();
        foreach (var life in data.Life)
        {
            if (!_stats.TryGetValue(life.MobId, out var st))
            {
                var mob = LoadJson<MobData>(Path.Combine(_assetsDir, "mob", $"{life.MobId}.json"));
                if (mob.MaxHp <= 0 || mob.Def is < 0 or > 1)
                    throw new InvalidDataException($"mob {life.MobId} has bad stats (hp {mob.MaxHp}, def {mob.Def})");
                st = (mob.MaxHp, mob.Def);
                _stats[life.MobId] = st;
            }
            fresh.Add(new Live
            {
                Uid = _nextUid++,
                Mob = life.MobId,
                Map = map,
                X = life.X,
                Y = life.Cy,
                Hp = st.hp,
                MaxHp = st.hp,
                Def = st.def
            });
        }
        _lives.AddRange(fresh);
        _spawned.Add(map);
    }

    // Live mobs on one map, for clients entering it.
    public List<Spawn> MobsOn(int map)
    {
        var list = new List<Spawn>();
        foreach (var m in _lives)
            if (m.Map == map)
                list.Add(new Spawn(m.Uid, m.Mob, m.Map, m.X, m.Y, m.MaxHp, m.Hp));
        return list;
    }

    // One basic-attack swing at (x, y): every mob in reach takes one damage
    // roll, and the dead are removed. rng comes from the caller (tests pass a seed).
    public List<Hit> Strike(int map, float x, float y, bool facingRight, Random rng)
    {
        var hits = new List<Hit>();
        for (int i = _lives.Count - 1; i >= 0; i--)
        {
            var m = _lives[i];
            if (m.Map != map) continue;
            float dx = m.X - x;
            bool ahead = facingRight ? dx >= -BehindX : dx <= BehindX;
            if (!ahead || Math.Abs(dx) > ReachX || Math.Abs(m.Y - y) > ReachY) continue;
            int damage = Damage.Calc(rng.NextDouble(), m.Def);
            m.Hp = Math.Max(0, m.Hp - damage);
            hits.Add(new Hit(m.Uid, m.Map, damage, m.Hp));
            if (m.Hp == 0) _lives.RemoveAt(i);
        }
        return hits;
    }
}
