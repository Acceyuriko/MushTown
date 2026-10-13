using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Server;

// The mob side of the world: spawns, HP, hit judging. The client never runs
// this logic (Q9: movement is client-side, mobs are server-side). Local
// worlds (shots, dropped links) drive one directly; net play lets the
// GameServer drive it for everyone.
public sealed class MobWorld
{
    // One spawn pushed to clients entering the map (live Hp, not MaxHp: a
    // late joiner must see wounded mobs as wounded).
    public sealed record Spawn(int Uid, int Mob, int Map, float X, float Y, int MaxHp, int Hp);

    // One mob caught by a swing. HpLeft 0 means it died (no separate die message).
    // Exp is the mob's kill award (the killer's client claims it).
    // Skill is 0 for a basic swing, else the skill whose hit spark to play.
    public sealed record Hit(int Uid, int Map, int Damage, int HpLeft, int Exp, int Skill);

    // Swing reach in pixels: a mob is hit when roughly at swing height and
    // within arm's length ahead (a little behind counts, for jumps landing short).
    private const int ReachX = 90;
    private const int ReachY = 60;
    private const int BehindX = 10;
    private const int PushPx = 60; // Rush knocks mobs this far along the dash

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
        public int Exp;
    }

    private readonly string _assetsDir;
    private readonly Dictionary<int, MapData> _maps = new();
    private readonly Dictionary<int, (int hp, double def, int exp)> _stats = new();
    private readonly Dictionary<int, (int kind, int dmgLo, int mobCount, int range)> _skills = new();
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
                if (mob.MaxHp <= 0 || mob.Def is < 0 or > 1 || mob.Exp < 0)
                    throw new InvalidDataException($"mob {life.MobId} has bad stats (hp {mob.MaxHp}, def {mob.Def}, exp {mob.Exp})");
                st = (mob.MaxHp, mob.Def, mob.Exp);
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
                Def = st.def,
                Exp = st.exp
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
    // build is the attacker's combat stats (the server stores one per client).
    public List<Hit> Strike(int map, float x, float y, bool facingRight, Random rng, Build build)
    {
        var hits = new List<Hit>();
        for (int i = _lives.Count - 1; i >= 0; i--)
        {
            var m = _lives[i];
            if (m.Map != map) continue;
            float dx = m.X - x;
            bool ahead = facingRight ? dx >= -BehindX : dx <= BehindX;
            if (!ahead || Math.Abs(dx) > ReachX || Math.Abs(m.Y - y) > ReachY) continue;
            int damage = Damage.Calc(rng.NextDouble(), rng.NextDouble(), m.Def, build);
            m.Hp = Math.Max(0, m.Hp - damage);
            hits.Add(new Hit(m.Uid, m.Map, damage, m.Hp, m.Exp, 0));
            if (m.Hp == 0) _lives.RemoveAt(i);
        }
        return hits;
    }

    // Cast routing: damaging actives are judged, everything else (War Leap
    // utility, buffs) only relays its effect. Unknown ids mean a foreign
    // client. Files load once and cache like mob stats.
    public bool IsDamageSkill(int skill)
    {
        if (!_skills.TryGetValue(skill, out var st))
        {
            string path = Path.Combine(_assetsDir, "skill", $"{skill}.json");
            if (!File.Exists(path))
                throw new InvalidDataException($"bad skill id {skill}");
            var data = LoadJson<SkillData>(path);
            st = (data.Kind, data.DmgLo, data.MobCount, data.Range);
            _skills[skill] = st;
        }
        return st.kind == SkillData.KindActive && st.dmgLo > 0;
    }

    // One damaging skill cast at (x, y): every live mob inside the skill's
    // ring takes one scaled roll, nearest first, capped at the skill's mob
    // count. pct is the cast-time damage pct (skill level and combo orbs
    // folded in by the caster); the attacker's +damage% adds on top. Rush
    // shoves its victims along the dash (clients resync positions on map
    // entry; W13's AI syncs them live). Final Attack may land one bonus
    // hit on the nearest survivor (a number, no spark: passives have no art).
    public List<Hit> SkillStrike(int map, float x, float y, bool facingRight, Random rng, Build build, int skill, int pct)
    {
        IsDamageSkill(skill); // loads and caches (a buff id here is a caller bug, caught below)
        var st = _skills[skill];
        if (st.mobCount <= 0 || st.range <= 0)
            throw new InvalidDataException($"skill {skill} has bad damage stats");
        var inRing = new List<(float dist2, Live m)>();
        foreach (var m in _lives)
        {
            if (m.Map != map) continue;
            float dx = m.X - x, dy = m.Y - y;
            float dist2 = dx * dx + dy * dy;
            if (dist2 <= st.range * st.range) inRing.Add((dist2, m));
        }
        inRing.Sort((a, b) => a.dist2.CompareTo(b.dist2)); // nearest first: the cap keeps the close ones
        var hits = new List<Hit>();
        Live? firstAlive = null;
        foreach ((float _, Live m) in inRing)
        {
            if (hits.Count >= st.mobCount) break;
            hits.Add(HitOne(m, skill, rng, build, pct));
            if (skill == SkillData.Rush) m.X += facingRight ? PushPx : -PushPx;
            if (m.Hp > 0) firstAlive ??= m;
        }
        if (firstAlive != null && rng.NextDouble() * 100 < build.FinalPct)
            hits.Add(HitOne(firstAlive, SkillData.FinalAttack, rng, build, pct));
        return hits;
    }

    // One damage roll applied to one live mob (dead mobs leave the world).
    private Hit HitOne(Live m, int tag, Random rng, Build build, int pct)
    {
        int damage = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), m.Def, build, pct);
        m.Hp = Math.Max(0, m.Hp - damage);
        if (m.Hp == 0) _lives.Remove(m);
        return new Hit(m.Uid, m.Map, damage, m.Hp, m.Exp, tag);
    }
}
