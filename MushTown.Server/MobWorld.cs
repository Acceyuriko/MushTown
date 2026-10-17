using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Server;

// The mob side of the world: spawns, HP, hit judging, AI, drops. The client
// never runs this logic (Q9: movement is client-side, mobs are server-side).
// Local worlds (shots, dropped links) drive one directly; net play lets the
// GameServer drive it for everyone.
public sealed class MobWorld
{
    // One spawn pushed to clients entering the map (live Hp, not MaxHp: a
    // late joiner must see wounded mobs as wounded). Respawns reuse it.
    public sealed record Spawn(int Uid, int Mob, int Map, float X, float Y, int MaxHp, int Hp);

    // One mob caught by a swing. HpLeft 0 means it died (no separate die message).
    // Exp is the mob's kill award (the killer's client claims it).
    // Skill is 0 for a basic swing, else the skill whose hit spark to play.
    public sealed record Hit(int Uid, int Map, int Damage, int HpLeft, int Exp, int Skill, int Mob);

    // One mob step (10Hz on the wire): Uid now stands at X/Y, facing along.
    public sealed record Move(int Uid, int Map, float X, float Y, bool FacingRight);

    // One touch that landed: Mob's Uid dealt Damage to Victim. The victim's
    // client folds DEF and drains HP (death respawns at the map spawn).
    public sealed record Hurt(int Victim, int Uid, int Mob, int Map, int Damage);

    // One ground drop: Kind is Mesos (Id = amount) or Item (Id + Count).
    public sealed record Drop(int Uid, int Map, float X, float Y, int Kind, int Id, int Count);

    // One tick's events, in wire order: moves snap poses, hurts drain HP,
    // spawns re-plant the respawned, takes clear expired drops. Kill drops
    // ride TakeDrops instead (they land at judge time, not tick time).
    public sealed record TickEvents(List<Move> Moves, List<Hurt> Hurts, List<Spawn> Spawns,
        List<(int uid, int map)> Takes);

    // Swing reach in pixels: a mob is hit when roughly at swing height and
    // within arm's length ahead (a little behind counts, for jumps landing short).
    private const int ReachX = 90;
    private const int ReachY = 60;
    private const int BehindX = 10;
    private const int PushPx = 60; // Rush knocks mobs this far along the dash
    private const int RespawnMs = 10000; // graves re-plant after 10s
    private const int BossRespawnMs = 300000; // the boss rests 5 min between fights
    private const int DropMs = 60000; // drops fade after 60s unclaimed
    public const int PickDx = 90; // pickup reach, x half-box (the client filters Z with these)
    public const int PickDy = 90; // ... and the server judges with them (a miss sends nothing)

    private sealed class Grave
    {
        public int Map;
        public int Mob;
        public float X;
        public float Y;
        public float MsLeft;
    }

    private sealed class GroundDrop
    {
        public int Uid;
        public int Map;
        public float X;
        public float Y;
        public int Kind;
        public int Id;
        public int Count;
        public float MsLeft;
    }

    private readonly string _assetsDir;
    private readonly Dictionary<int, MapData> _maps = new();
    private readonly Dictionary<int, (int hp, double def, int exp, int touch, int speed, int aggro, bool boss)> _stats = new();
    private readonly Dictionary<int, (int kind, int dmgLo, int mobCount, int range)> _skills = new();
    private readonly Dictionary<int, DropTable> _dropTables = new();
    private readonly List<Mob> _lives = new();
    private readonly List<Grave> _graves = new();
    private readonly List<GroundDrop> _drops = new();
    private readonly List<Drop> _fresh = new(); // kill drops waiting for broadcast
    private readonly HashSet<int> _spawned = new();
    private int _nextUid = 1;
    private int _nextDrop = 1;

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
    // bodies: the dead re-plant on respawn timers, not on entry). The map
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
        var fresh = new List<Mob>();
        foreach (var life in data.Life)
        {
            if (!_stats.TryGetValue(life.MobId, out var st))
            {
                var mob = LoadJson<MobData>(Path.Combine(_assetsDir, "mob", $"{life.MobId}.json"));
                if (mob.MaxHp <= 0 || mob.Def is < 0 or > 1 || mob.Exp < 0)
                    throw new InvalidDataException($"mob {life.MobId} has bad stats (hp {mob.MaxHp}, def {mob.Def}, exp {mob.Exp})");
                if (mob.Touch < 0 || mob.Speed <= 0 || mob.Aggro < 0)
                    throw new InvalidDataException($"mob {life.MobId} has bad ai stats (touch {mob.Touch}, speed {mob.Speed}, aggro {mob.Aggro})");
                st = (mob.MaxHp, mob.Def, mob.Exp, mob.Touch, mob.Speed, mob.Aggro, mob.Boss);
                _stats[life.MobId] = st;
                _dropTables[life.MobId] = new DropTable(LoadDropTable(life.MobId));
            }
            fresh.Add(Plant(_nextUid++, life.MobId, map, life.X, life.Cy, st));
        }
        _lives.AddRange(fresh);
        _spawned.Add(map);
    }

    // One fresh body at the spawn feet (first plant and respawns share it).
    // Mobs face left until the AI turns them.
    private static Mob Plant(int uid, int mob, int map, float x, float y,
        (int hp, double def, int exp, int touch, int speed, int aggro, bool boss) st) => new()
        {
            Uid = uid,
            MobId = mob,
            Map = map,
            X = x,
            Y = y,
            HomeX = x,
            HomeY = y,
            Hp = st.hp,
            MaxHp = st.hp,
            Def = st.def,
            Exp = st.exp,
            Touch = st.touch,
            Speed = st.speed,
            Aggro = st.aggro,
            Boss = st.boss
        };

    // Live mobs on one map, for clients entering it.
    public List<Spawn> MobsOn(int map)
    {
        var list = new List<Spawn>();
        foreach (var m in _lives)
            if (m.Map == map)
                list.Add(new Spawn(m.Uid, m.MobId, m.Map, m.X, m.Y, m.MaxHp, m.Hp));
        return list;
    }

    // Ground drops on one map, for clients entering it (drops land between
    // pushes, so entry re-lists them like mobs).
    public List<Drop> DropsOn(int map)
    {
        var list = new List<Drop>();
        foreach (var d in _drops)
            if (d.Map == map)
                list.Add(new Drop(d.Uid, d.Map, d.X, d.Y, d.Kind, d.Id, d.Count));
        return list;
    }

    // One AI step (net: 100ms server ticks; local: every frame). Each mob
    // chases its nearest in-range player or wanders its platform, turns at
    // edges and VR walls, falls when pushed off, and touches what it
    // reaches. Graves re-plant, drops expire. Moves only cover maps with a
    // player on them (empty maps sleep: nobody watches). Fully deterministic
    // (no random turns or pauses): the same ticks always walk the same line.
    public TickEvents Tick(int ms, List<(int id, int map, float x, float y)> players)
    {
        var onMap = new HashSet<int>();
        foreach (var p in players) onMap.Add(p.map);
        var moves = new List<Move>();
        var hurts = new List<Hurt>();
        foreach (var m in _lives)
        {
            m.Step(ms, _maps[m.Map], players); // lives only exist after EnterMap loaded it
            foreach (var p in players)
            {
                if (p.map != m.Map) continue;
                if (!m.TryTouch(p.x, p.y)) continue;
                hurts.Add(new Hurt(p.id, m.Uid, m.MobId, m.Map, m.Touch));
                break; // one victim per tick per mob
            }
            if (onMap.Contains(m.Map))
                moves.Add(new Move(m.Uid, m.Map, m.X, m.Y, m.FacingRight));
        }
        var spawns = new List<Spawn>();
        for (int i = _graves.Count - 1; i >= 0; i--)
        {
            var g = _graves[i];
            g.MsLeft -= ms;
            if (g.MsLeft > 0) continue;
            _graves.RemoveAt(i);
            var live = Plant(_nextUid++, g.Mob, g.Map, g.X, g.Y, _stats[g.Mob]);
            _lives.Add(live);
            spawns.Add(new Spawn(live.Uid, live.MobId, live.Map, live.X, live.Y, live.MaxHp, live.Hp));
        }
        var takes = new List<(int uid, int map)>();
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            d.MsLeft -= ms;
            if (d.MsLeft > 0) continue;
            _drops.RemoveAt(i);
            takes.Add((d.Uid, d.Map));
        }
        return new TickEvents(moves, hurts, spawns, takes);
    }

    // Kill drops waiting for broadcast (the judge fills it, the caller drains it).
    public List<Drop> TakeDrops()
    {
        var list = new List<Drop>(_fresh);
        _fresh.Clear();
        return list;
    }

    // One pickup: the drop must exist, sit on the taker's map, and be near.
    // Null means no (gone, far, or another map): the client just misses.
    public Drop? Pickup(int uid, int map, float x, float y)
    {
        for (int i = 0; i < _drops.Count; i++)
        {
            var d = _drops[i];
            if (d.Uid != uid) continue;
            if (d.Map != map || Math.Abs(x - d.X) > PickDx || Math.Abs(y - d.Y) > PickDy)
                return null;
            _drops.RemoveAt(i);
            return new Drop(d.Uid, d.Map, d.X, d.Y, d.Kind, d.Id, d.Count);
        }
        return null;
    }

    // Quest-scripted bonus lines per kill: (mob, attacker) -> extra payouts
    // (kind/id/count/dx, placed like table rolls). Null in v1 (the bag
    // holds no quest items yet); the hook stays so the first scripted
    // drop needs no rewire. attacker 0 is the local player.
    public Func<int, int, List<(int kind, int id, int count, float dx)>>? BonusDrops { get; set; }

    // One basic-attack swing at (x, y): every mob in reach takes one damage
    // roll, and the dead are removed. rng comes from the caller (tests pass a seed).
    // build is the attacker's combat stats (the server stores one per client).
    public List<Hit> Strike(int map, float x, float y, bool facingRight, Random rng, Build build, int attacker = 0)
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
            hits.Add(new Hit(m.Uid, m.Map, damage, m.Hp, m.Exp, 0, m.MobId));
            if (m.Hp != 0) continue;
            _lives.RemoveAt(i);
            Bury(m, rng, attacker);
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
    // shoves its victims along the dash (live moves sync them, gravity
    // lands them). Final Attack may land one bonus hit on the nearest
    // survivor (a number, no spark: passives have no art).
    public List<Hit> SkillStrike(int map, float x, float y, bool facingRight, Random rng, Build build, int skill, int pct, int attacker = 0)
    {
        IsDamageSkill(skill); // loads and caches (a buff id here is a caller bug, caught below)
        var st = _skills[skill];
        if (st.mobCount <= 0 || st.range <= 0)
            throw new InvalidDataException($"skill {skill} has bad damage stats");
        var inRing = new List<(float dist2, Mob m)>();
        foreach (var m in _lives)
        {
            if (m.Map != map) continue;
            float dx = m.X - x, dy = m.Y - y;
            float dist2 = dx * dx + dy * dy;
            if (dist2 <= st.range * st.range) inRing.Add((dist2, m));
        }
        inRing.Sort((a, b) => a.dist2.CompareTo(b.dist2)); // nearest first: the cap keeps the close ones
        var hits = new List<Hit>();
        Mob? firstAlive = null;
        foreach ((float _, Mob m) in inRing)
        {
            if (hits.Count >= st.mobCount) break;
            hits.Add(HitOne(m, skill, rng, build, pct, attacker));
            if (skill == SkillData.Rush && m.Hp > 0) // the living slide along the dash
                m.X = Math.Clamp(m.X + (facingRight ? PushPx : -PushPx),
                    _maps[map].Info.VrLeft, _maps[map].Info.VrRight);
            if (m.Hp > 0) firstAlive ??= m;
        }
        if (firstAlive != null && rng.NextDouble() * 100 < build.FinalPct)
            hits.Add(HitOne(firstAlive, SkillData.FinalAttack, rng, build, pct, attacker));
        return hits;
    }

    // One damage roll applied to one live mob. The dead leave the world, a
    // grave (respawn), and their drops: mesos always, a potion sometimes
    // (the boss always pays its purse).
    private Hit HitOne(Mob m, int tag, Random rng, Build build, int pct, int attacker)
    {
        int damage = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), m.Def, build, pct);
        m.Hp = Math.Max(0, m.Hp - damage);
        if (m.Hp == 0)
        {
            _lives.Remove(m);
            Bury(m, rng, attacker);
        }
        return new Hit(m.Uid, m.Map, damage, m.Hp, m.Exp, tag, m.MobId);
    }

    // One death's aftermath (swings and skills share it): a grave at home
    // for the respawn timer, plus the kill's drops.
    private void Bury(Mob m, Random rng, int attacker)
    {
        _graves.Add(new Grave { Map = m.Map, Mob = m.MobId, X = m.HomeX, Y = m.HomeY, MsLeft = m.Boss ? BossRespawnMs : RespawnMs });
        DropKill(m, rng, attacker);
    }

    // One mob's drop table (drops/{mob}.json, repo data): a meso range plus
    // per-item chances. Ranges fail loud here; the client proves item ids
    // against its defs at boot (join mode trusts the host's rolls like
    // every other server-sent id). The pre-check names the hand-data fix
    // (restore from git): a bare missing-file error would not say where
    // the file comes from.
    private DropData LoadDropTable(int mobId)
    {
        string path = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "drops", $"{mobId}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing drop table for mob {mobId}: {Path.GetFullPath(path)} (repo data: restore it from git)", path);
        var table = LoadJson<DropData>(path);
        if (table.MobId != mobId)
            throw new InvalidDataException($"drop table {mobId} names mob {table.MobId}");
        if (table.MesoMin < 0 || table.MesoMax < table.MesoMin)
            throw new InvalidDataException($"drop table {mobId} has a bad meso range ({table.MesoMin}-{table.MesoMax})");
        foreach (var d in table.Items)
            if (d.Chance <= 0 || d.Chance > 1 || d.Count <= 0)
                throw new InvalidDataException($"drop table {mobId} has a bad entry (item {d.Item}, chance {d.Chance}, count {d.Count})");
        return table;
    }

    // One kill's loot (the Loot act): the table rolls once, bonus lines
    // append, the world places every payout (EnterMap loads every spawned
    // mob's table; lives only exist after EnterMap loaded it).
    private void DropKill(Mob m, Random rng, int attacker)
    {
        var data = _maps[m.Map];
        var table = _dropTables[m.MobId];
        foreach (var p in table.Roll(rng))
            DropOne(data, m, m.X + p.dx, p.kind, p.id, p.count);
        if (BonusDrops != null)
            foreach (var p in BonusDrops(m.MobId, attacker))
                DropOne(data, m, m.X + p.dx, p.kind, p.id, p.count);
    }

    // One loot drop: clamped inside the VR, lands on the ground below.
    private void DropOne(MapData data, Mob m, float x, int kind, int id, int count)
    {
        float cx = Math.Clamp(x, data.Info.VrLeft, data.Info.VrRight);
        var d = new GroundDrop
        {
            Uid = _nextDrop++,
            Map = m.Map,
            X = cx,
            Y = LandY(data.Footholds, cx, m.Y),
            Kind = kind,
            Id = id,
            Count = count,
            MsLeft = DropMs
        };
        _drops.Add(d);
        _fresh.Add(new Drop(d.Uid, d.Map, d.X, d.Y, d.Kind, d.Id, d.Count));
    }

    // Drop landing height: the first ground at or below the kill, else the
    // kill height itself (a void drop fades where it fell).
    private static float LandY(List<FootholdItem> fh, float x, float y)
    {
        var g = SceneMath.GroundBelow(fh, (int)Math.Floor(x), (int)Math.Ceiling(y));
        return g == null ? y : SceneMath.FootholdY(g, (int)Math.Floor(x));
    }
}
