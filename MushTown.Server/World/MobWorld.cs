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
    public sealed record Spawn(int Uid, int Mob, int Map, float X, float Y, long MaxHp, long Hp);

    // One mob caught by a swing. HpLeft 0 means it died (no separate die message).
    // Exp is the mob's kill award (the killer's client claims it).
    // Skill is 0 for a basic swing, else the skill whose hit spark to play.
    // Crit picks the critical damage-number art on every client.
    // Settle: this hit carries the mob's HP, death and EXP for the whole cast. A multi-segment skill sends one hit per
    // segment (each with its own number) but only the first settles: HpLeft is the HP after ALL the segments, and the
    // other segments are numbers only (no flinch, no death sound, no EXP again). Every other hit settles.
    // MpGain: MP Eater absorbed this many MP from the mob on this hit (0 = none); it rides the first hit of the mob and the
    // attacker's client adds it to its own MP.
    public sealed record Hit(int Uid, int Map, long Damage, long HpLeft, long Exp, int Skill, int Mob, bool Crit, bool Settle = true, int MpGain = 0, bool Dot = false);

    // One mob step (10Hz on the wire): Uid now stands at X/Y, facing along.
    public sealed record Move(int Uid, int Map, float X, float Y, bool FacingRight);

    // One touch that landed: Mob's Uid dealt Damage to Victim. The victim's
    // client folds DEF and drains HP (death respawns at the map spawn).
    public sealed record Hurt(int Victim, int Uid, int Mob, int Map, long Damage);

    // One ground drop: Kind is Mesos (Amount) or Item (Id + Count). Tab is the
    // bag a taken item returns to ("" for mesos). FromX/FromY is where the icon
    // pops from (monster centre or player feet); X/Y is the landing spot.
    // Fresh false is an entry snapshot: the client shows it at rest.
    public sealed record Drop(int Uid, int Map, float X, float Y, int Kind, int Id, int Count, long Amount,
        float FromX = 0, float FromY = 0, bool Fresh = true, string Tab = "");

    // One tick's events, in wire order: strikes are the damage of the balls that arrived (with the caster
    // who threw them), moves snap poses, hurts drain HP, spawns re-plant the respawned, takes clear expired
    // drops. Kill drops ride TakeDrops instead (they land at judge time, not tick time).
    public sealed record TickEvents(List<(Hit hit, int attacker)> Strikes, List<Move> Moves, List<Hurt> Hurts, List<Spawn> Spawns,
        List<(int uid, int map)> Takes);

    // Swing reach in pixels: a mob is hit when roughly at swing height and
    // within arm's length ahead (a little behind counts, for jumps landing short).
    private const int ReachX = 90;
    private const int ReachY = 60;
    private const int BehindX = 10;
    public const int TickMs = 100; // the server tick: a ball lands on the first tick after it arrived, never before
    private const int PushPx = 60; // Rush knocks mobs this far along the dash
    private const int LaunchDx = 40; // a ball starts this far in front of the caster...
    private const int LaunchDy = 40; // ...at the torso (the height of the cast effect); a proposal, WZ has no start point
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

    // A ball in the air, or a cast with no ball that waits for its attack frame: it waits here for its delay (and flight), then the box judges.
    private sealed class Pending
    {
        public int Map;
        public float X; // the center of the box (the mob feet, or the end of the range)
        public float Y;
        public float CasterX; // the caster's feet at the cast (Elemental Drain counts nearby mobs around the caster)
        public float CasterY;
        public bool FacingRight;
        public int MsLeft;
        public Random Rng = null!;
        public Build Build;
        public WzSkillData Wz = null!;
        public AttackElement Element; // read at the cast: the landing in the tick loop must not throw
        public int MobCount;
        public int Segments;
        public int Pct;
        public int Attacker;
        public Dot? Dot; // the DoT this cast puts on each mob it leaves alive (a copy for each mob), built at the cast
    }

    // A ground zone and the cast that made it: the zone judges the mobs in its box with these numbers (read at the cast).
    private sealed class LiveZone
    {
        public GroundZone Zone = null!;
        public Pending Cast = null!; // only its attack numbers are used (no box, no wait)
    }

    // A summon and the pool zone it made (0: none), so that a summon that ends takes its pool with it. Its pools are the pieces of the pool (ToxinPoolLayout) that blow up one by one.
    // Pools and ZoneUid are for Creeping Toxin only: Ifrit does not use them, and the next summon ticket will split them from the general part.
    private sealed class LiveSummon
    {
        public SummonUnit Unit = null!;
        public int ZoneUid;
        public WzSkillData Wz = null!;
        public Build Build;
        public Random Rng = null!;
        public List<LivePool> Pools = new();
    }

    // One pool: where it stands (the middle and the width of its puddle), when it is there (Born, ms of the zone's age: the spreading reaches it, or it grows back after a blast) and
    // whether it is already set to blow up.
    private sealed class LivePool
    {
        public float Center;
        public int Width;
        public int Born;
        public bool Queued;
    }

    // A pool that a blast next to it set off: it blows up after DueMs (the chain wait, WZ y of the blast).
    private sealed class PendingBlast
    {
        public LiveSummon Summon = null!;
        public int Index;
        public int DueMs;
        public int Attacker;
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
        public long Amount;
        public string Tab = "";
        public float FromX;
        public float FromY;
        public float MsLeft;
    }

    private readonly string _assetsDir;
    private readonly Dictionary<int, MapData> _maps = new();
    private readonly Dictionary<int, (long hp, double def, long exp, long touch, int speed, int aggro, bool boss, int level, string elemAttr, int bodyH, long maxMp)> _stats = new();
    private readonly Dictionary<int, (int kind, int dmgLo, int mobCount, int range)> _skills = new();
    private readonly Dictionary<int, WzSkillData> _wzSkills = new(); // the Fire/Poison skills (skill/wz/<id>.json), loaded when first needed
    private readonly Dictionary<int, DropTable> _dropTables = new();
    private readonly Dictionary<int, ItemData> _itemDefs = new();
    private readonly List<Mob> _lives = new();
    private readonly List<Grave> _graves = new();
    private readonly List<GroundDrop> _drops = new();
    private readonly List<Drop> _fresh = new(); // kill drops waiting for broadcast
    private readonly List<Pending> _pending = new(); // balls in the air
    private readonly List<LiveZone> _zones = new();
    private readonly List<ZoneEvent> _zoneEvents = new(); // zones started or taken away, waiting for broadcast
    private readonly List<LiveSummon> _summons = new();
    private readonly List<SummonEvent> _summonEvents = new(); // summons started or taken away, waiting for broadcast
    private readonly List<PoolBlastEvent> _poolEvents = new(); // pools that blew up, waiting for broadcast
    private readonly List<PendingBlast> _pendingBlasts = new();
    private readonly Dictionary<(int Attacker, int Uid), long> _lastBlast = new(); // (caster, mob uid) -> the world time a pool blast of that caster last hit it (the blasts of one caster cut each other, not those of another: 未核实, the developer's reading)
    private long _nowMs; // the world time: it runs with Tick
    private readonly List<BallEvent> _launches = new(); // balls thrown, waiting for broadcast (the flight time is Projectile.FlightMs of the four numbers, the same rule on the client)
    private readonly List<(int attacker, int uid, int stacks)> _markDirty = new(); // Empirical Knowledge stack changes to send to their owner
    private readonly List<(int uid, int skill, bool on)> _dotDirty = new(); // DoT skills that came on a mob or left it, to send to everyone
    private readonly HashSet<int> _spawned = new();
    private int _nextUid = 1;
    private int _nextZone = 1;
    private int _nextSummon = 1;
    private int _nextDrop = 1;

    private readonly HitPipeline _pipeline; // every hit on a mob (swing, skill hit, DoT tick) is calculated here, in one order

    public MobWorld(string assetsDir)
    {
        _assetsDir = assetsDir;
        _pipeline = new HitPipeline(Wz, _lives, (m, rng, attacker) => { _lives.Remove(m); Bury(m, rng, attacker); }, FlushMarks);
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
                int bodyH = mob.Actions.FirstOrDefault(a => a.Name == "stand")?.Frames.FirstOrDefault()?.OriginY ?? 0;
                st = (mob.MaxHp, mob.Def, mob.Exp, mob.Touch, mob.Speed, mob.Aggro, mob.Boss, mob.Level, mob.ElemAttr, bodyH, mob.MaxMp);
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
        (long hp, double def, long exp, long touch, int speed, int aggro, bool boss, int level, string elemAttr, int bodyH, long maxMp) st) => new()
        {
            Uid = uid,
            MobId = mob,
            Map = map,
            X = x,
            Y = y,
            HomeX = x,
            HomeY = y,
            BodyH = st.bodyH,
            Level = st.level,
            ElemAttr = st.elemAttr,
            Hp = st.hp,
            MaxHp = st.hp,
            Mp = st.maxMp,
            MaxMp = st.maxMp,
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
                list.Add(new Drop(d.Uid, d.Map, d.X, d.Y, d.Kind, d.Id, d.Count, d.Amount, d.X, d.Y, false, d.Tab));
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
        var strikes = new List<(Hit hit, int attacker)>();
        _nowMs += ms;
        for (int i = 0; i < _pending.Count;) // the balls land in the order they were thrown
        {
            var p = _pending[i];
            p.MsLeft -= ms;
            if (p.MsLeft > 0)
            {
                i++;
                continue;
            }
            _pending.RemoveAt(i);
            foreach (var h in WzHits(p.Map, p.X, p.Y, p.FacingRight, p.Rng, p.Build, p.Wz, p.Element, p.MobCount, p.Segments, p.Pct, p.Attacker, p.Dot, p.CasterX, p.CasterY))
                strikes.Add((h, p.Attacker));
        }
        var onMap = new HashSet<int>();
        foreach (var p in players) onMap.Add(p.map);
        var moves = new List<Move>();
        var hurts = new List<Hurt>();
        foreach (var m in _lives)
        {
            m.Step(ms, _maps[m.Map], players); // lives only exist after EnterMap loaded it
            FlushMarks(m); // the marks that ran out this step tell their owner to drop them
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
        for (int i = 0; i < _zones.Count;) // the zones that run: every tick, after the mobs moved, they judge the mobs inside (a mob that steps in is poisoned in that very tick), in the order the zones were cast
        {
            var z = _zones[i];
            bool up = z.Zone.Step(ms);
            if (z.Zone.Over) _zones.RemoveAt(i); // the clients end their copy by the time they were given
            else i++;
            if (!up) continue;
            foreach (var h in ZoneTick(z))
                strikes.Add((h, z.Cast.Attacker));
        }
        for (int i = _summons.Count - 1; i >= 0; i--) // the summons run their time; one that is over goes (its pool has the same time and ends in the zone loop above)
        {
            var u = _summons[i].Unit;
            u.Step(ms);
            if (!u.Over) continue;
            _pendingBlasts.RemoveAll(b => b.Summon == _summons[i]);
            _summonEvents.Add(new SummonEvent(u.Uid, u.Skill, u.Map, u.X, u.Y, 0, 0, true));
            _summons.RemoveAt(i);
        }
        var due = new List<PendingBlast>(); // the pools a blast set off blow up now, in the order they were set (their own neighbours blow one chain wait later)
        foreach (var b in _pendingBlasts)
        {
            b.DueMs -= ms;
            if (b.DueMs <= 0) due.Add(b);
        }
        foreach (var b in due)
        {
            _pendingBlasts.Remove(b);
            foreach (var h in ExplodePool(b.Summon, b.Index, b.Attacker, false)) strikes.Add((h, b.Attacker));
        }
        for (int i = 0; i < _lives.Count;) // the DoT ticks that came due in the steps above (a kill removes the mob from _lives, so the index stays)
        {
            var m = _lives[i];
            FlushDots(m); // the DoT skills that came on (the zones above) or ran out (the steps above) tell everyone
            foreach (var dot in m.Statuses.TakeDue())
            {
                foreach (var h in DotTick(m, dot)) strikes.Add((h, dot.Attacker)); // one hit for each segment of the tick
                if (m.Hp == 0) break; // the dead mob takes no more ticks
            }
            if (m.Hp > 0) i++;
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
        return new TickEvents(strikes, moves, hurts, spawns, takes);
    }

    // The balls of one caster that are still in the air vanish with no damage (the caster left the game or changed map).
    public void CancelBalls(int attacker) => _pending.RemoveAll(p => p.Attacker == attacker);

    // The zones of one caster vanish with the caster too (it left the game or the map): every client on the map is told to take them away.
    public void CancelZones(int attacker)
    {
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            var z = _zones[i].Zone;
            if (z.Attacker != attacker) continue;
            _zoneEvents.Add(new ZoneEvent(z.Uid, _zones[i].Cast.Wz.SkillId, z.Map, z.FullLeft, z.FullRight, z.OriginY, 0, 0, true));
            _zones.RemoveAt(i);
        }
    }

    // The summons of one caster vanish with the caster too (and the pools they made): every client on the map is told to take them away.
    public void CancelSummons(int attacker)
    {
        for (int i = _summons.Count - 1; i >= 0; i--)
            if (_summons[i].Unit.Attacker == attacker) RemoveSummon(i);
    }

    private void RemoveSummon(int i)
    {
        var s = _summons[i];
        _pendingBlasts.RemoveAll(b => b.Summon == s);
        _summonEvents.Add(new SummonEvent(s.Unit.Uid, s.Unit.Skill, s.Unit.Map, s.Unit.X, s.Unit.Y, 0, 0, true));
        _summons.RemoveAt(i);
        int z = _zones.FindIndex(l => l.Zone.Uid == s.ZoneUid);
        if (z < 0) return;
        var zone = _zones[z].Zone;
        _zoneEvents.Add(new ZoneEvent(zone.Uid, _zones[z].Cast.Wz.SkillId, zone.Map, zone.FullLeft, zone.FullRight, zone.OriginY, 0, 0, true));
        _zones.RemoveAt(z);
    }

    // The summons that stand on one map, for clients entering it (the time they have left; one still waiting keeps its wait).
    public List<SummonEvent> SummonsOn(int map) =>
        _summons.Where(s => s.Unit.Map == map).Select(s => new SummonEvent(s.Unit.Uid, s.Unit.Skill, map, s.Unit.X, s.Unit.Y, s.Unit.MsLeft, s.Unit.WaitMs)).ToList();

    // Pools that blew up since the last call (the caller broadcasts them to the map).
    public List<PoolBlastEvent> TakePoolBlasts()
    {
        var list = new List<PoolBlastEvent>(_poolEvents);
        _poolEvents.Clear();
        return list;
    }

    // Summons started or taken away since the last call (the caller broadcasts them to the map).
    public List<SummonEvent> TakeSummons()
    {
        var list = new List<SummonEvent>(_summonEvents);
        _summonEvents.Clear();
        return list;
    }

    // The zones that run on one map, for clients entering it (the time they have left; one still waiting keeps its wait).
    public List<ZoneEvent> ZonesOn(int map)
    {
        var list = new List<ZoneEvent>();
        foreach (var z in _zones)
            if (z.Zone.Map == map)
                list.Add(new ZoneEvent(z.Zone.Uid, z.Cast.Wz.SkillId, map, z.Zone.FullLeft, z.Zone.FullRight, z.Zone.OriginY, z.Zone.MsLeft, z.Zone.WaitMs, false, z.Zone.OriginX, z.Zone.StepMs, z.Zone.AgeMs));
        return list;
    }

    // Zones started or taken away since the last call (the caller broadcasts them to the map).
    public List<ZoneEvent> TakeZones()
    {
        var list = new List<ZoneEvent>(_zoneEvents);
        _zoneEvents.Clear();
        return list;
    }

    // The DoTs of one caster vanish with the caster: a disconnect, a map change or a cash-shop entry removes every
    // effect he put on a mob. It walks every live mob and removes the ones this source owns (a source stands on one map,
    // so only that map's mobs hold his DoTs, but the loop does not filter by map). His Empirical Knowledge marks go the
    // same way (he only ever sees his own).
    public void CancelDoTs(int attacker)
    {
        foreach (var m in _lives)
        {
            m.Statuses.RemoveDotsOf(attacker);
            m.Statuses.RemoveMarksOf(attacker);
            FlushMarks(m);
        }
    }

    // Elemental Drain: how many layers count near one point (the pipeline counts them for a hit; the server also asks once a tick,
    // to tell each caster the layers it displays). 0 when not learned (max 0).
    public int DrainLayers(int map, float x, float y, int max, int attacker) => _pipeline.DrainLayers(map, x, y, max, attacker);

    // The Empirical Knowledge stacks one attacker has on one mob (0 = none), for the client's own view and the shot staging.
    public int MarkStacks(int uid, int attacker)
    {
        foreach (var m in _lives)
            if (m.Uid == uid) return m.Statuses.MarkStacks(attacker);
        return 0;
    }

    // Set one attacker's stacks on one mob outright (shot staging and tests): 0 clears them.
    public void SetMarkStacks(int uid, int attacker, int stacks, int timeMs)
    {
        foreach (var m in _lives)
            if (m.Uid == uid) { m.Statuses.SetMark(attacker, stacks, timeMs); FlushMarks(m); return; }
    }

    // The mark changes since the last call, to send to the owning client only (attack, uid, new stacks).
    public IReadOnlyList<(int attacker, int uid, int stacks)> TakeMarkChanges()
    {
        if (_markDirty.Count == 0) return Array.Empty<(int, int, int)>(); // most steps change no mark
        var list = new List<(int, int, int)>(_markDirty);
        _markDirty.Clear();
        return list;
    }

    // The DoT skills that came on a mob or left it since the last call (uid, skill, on), to send to everyone.
    public IReadOnlyList<(int uid, int skill, bool on)> TakeDotChanges()
    {
        if (_dotDirty.Count == 0) return Array.Empty<(int, int, bool)>(); // most steps change no DoT
        var list = new List<(int, int, bool)>(_dotDirty);
        _dotDirty.Clear();
        return list;
    }

    // The DoT skills on each live mob of a map (uid, skill), for a client that enters it.
    public List<(int uid, int skill)> DotsOn(int map)
    {
        var list = new List<(int, int)>();
        foreach (var m in _lives)
            if (m.Map == map)
                foreach (int skill in m.Statuses.DotSkills()) list.Add((m.Uid, skill));
        return list;
    }

    // The skills that have a DoT on one mob now, for the client's own view of a local world and the shot staging.
    public IReadOnlyList<int> DotSkills(int uid)
    {
        foreach (var m in _lives)
            if (m.Uid == uid) return m.Statuses.DotSkills();
        return Array.Empty<int>();
    }

    // Drops the mark and DoT changes nobody reads (a local world, a shot, the cash shop: the client shows its own stacks and the DoT icons from the world
    // directly, so the queues would only grow).
    public void DiscardChangeQueues()
    {
        _markDirty.Clear();
        _dotDirty.Clear();
    }

    // Balls thrown by the casts since the last call (the caller broadcasts them to the map).
    public List<BallEvent> TakeLaunches()
    {
        var list = new List<BallEvent>(_launches);
        _launches.Clear();
        return list;
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
            return new Drop(d.Uid, d.Map, d.X, d.Y, d.Kind, d.Id, d.Count, d.Amount, d.FromX, d.FromY, true, d.Tab);
        }
        return null;
    }

    // One player-dropped item or meso pile at the player's feet. The map must
    // already be loaded (a State entered it); tab is the bag it came from
    // ("" for mesos), the icon pops from the feet and lands on the ground below.
    // The caller broadcasts the returned Drop.
    public Drop DropItem(int map, float x, float y, string tab, int kind, int id, int count, long amount = 0)
    {
        var data = _maps[map];
        return AddDrop(data, map, x, y, x, y, tab, kind, id, count, amount);
    }

    // Quest-scripted bonus lines per kill: (mob, attacker) -> extra payouts
    // (kind/id/count/amount, placed in the same row as table rolls). Null in
    // v1 (the bag holds no quest items yet); the hook stays so the first
    // scripted drop needs no rewire. attacker 0 is the local player.
    public Func<int, int, List<(int kind, int id, int count, long amount)>>? BonusDrops { get; set; }

    // One basic-attack swing at (x, y): every mob in reach takes one damage
    // roll, and the dead are removed. rng comes from the caller (tests pass a seed).
    // build is the attacker's combat stats (the server stores one per client). The swing is one attack of the hit pipeline (physical, one segment).
    public List<Hit> Strike(int map, float x, float y, bool facingRight, Random rng, Build build, int attacker = 0)
    {
        _pipeline.Prepare(build); // read the status numbers now, in the cast
        var targets = new List<Mob>();
        for (int i = _lives.Count - 1; i >= 0; i--)
        {
            var m = _lives[i];
            if (m.Map != map) continue;
            float dx = m.X - x;
            bool ahead = facingRight ? dx >= -BehindX : dx <= BehindX;
            if (!ahead || Math.Abs(dx) > ReachX || Math.Abs(m.Y - y) > ReachY) continue;
            targets.Add(m);
        }
        return _pipeline.Resolve(targets, new HitPipeline.Attack { Build = build, Rng = rng, Attacker = attacker, Map = map, CasterX = x, CasterY = y });
    }

    // Cast routing: damaging actives are judged, everything else (War Leap
    // utility, buffs) only relays its effect. Unknown ids mean a foreign
    // client. Files load once and cache like mob stats. A skill is a warrior
    // skill (skill/<id>.json) or a WZ skill (skill/wz/<id>.json); a WZ skill is a
    // damage skill when its common has a damage line.
    public bool IsDamageSkill(int skill)
    {
        if (_wzSkills.TryGetValue(skill, out var wz)) return IsWzDamage(wz);
        if (!_skills.TryGetValue(skill, out var st))
        {
            string path = Path.Combine(_assetsDir, "skill", $"{skill}.json");
            if (!File.Exists(path))
            {
                if (!File.Exists(WzPath(skill)))
                    throw new InvalidDataException($"bad skill id {skill}");
                return IsWzDamage(Wz(skill));
            }
            var data = LoadJson<SkillData>(path);
            st = (data.Kind, data.DmgLo, data.MobCount, data.Range);
            _skills[skill] = st;
        }
        return st.kind == SkillData.KindActive && st.dmgLo > 0;
    }

    // Teleport Mastery is a passive that the client casts at the Teleport landing: the server judges it like an active.
    private static bool IsWzDamage(WzSkillData wz) => (wz.Kind == SkillData.KindActive || wz.SkillId == WzSkillData.TeleportMastery) && wz.Common.ContainsKey("damage");

    private string WzPath(int skill) => Path.Combine(_assetsDir, "skill", "wz", $"{skill}.json");

    private WzSkillData Wz(int skill)
    {
        if (!_wzSkills.TryGetValue(skill, out var wz))
            _wzSkills[skill] = wz = LoadJson<WzSkillData>(WzPath(skill));
        return wz;
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
        _pipeline.Prepare(build); // the status numbers are read now, in the cast (a ball lands later, in the tick loop)
        if (build.IgniteProp > 0) Wz(WzSkillData.Ignite); // the wall numbers too: the hit that starts it may land in the tick loop, which must not read files
        if (_wzSkills.TryGetValue(skill, out var wz)) return WzStrike(map, x, y, facingRight, rng, build, wz, pct, attacker);
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
        var targets = inRing.Take(st.mobCount).Select(r => r.m).ToList();
        var hits = _pipeline.Resolve(targets, new HitPipeline.Attack { Build = build, Rng = rng, Attacker = attacker, Skill = skill, Pct = pct, Map = map, CasterX = x, CasterY = y });
        Mob? firstAlive = null;
        foreach (var m in targets)
        {
            if (skill == SkillData.Rush && m.Hp > 0) // the living slide along the dash
                m.X = Math.Clamp(m.X + (facingRight ? PushPx : -PushPx),
                    _maps[map].Info.VrLeft, _maps[map].Info.VrRight);
            if (m.Hp > 0) firstAlive ??= m;
        }
        if (firstAlive != null && rng.NextDouble() * 100 < build.FinalPct)
            hits.AddRange(_pipeline.Resolve(new[] { firstAlive }, new HitPipeline.Attack
            { Build = build, Rng = rng, Attacker = attacker, Skill = SkillData.FinalAttack, Pct = pct, Map = map, CasterX = x, CasterY = y }));
        return hits;
    }

    // One WZ damage skill cast at (x, y). A skill with a ball (BallFrames) throws it: the ball appears at the body
    // action's attack frame, then flies to the end of the range ahead, or to the nearest mob ahead within range (its
    // body overlapping the box height) when the skill is rectBasedOnTarget (Energy Bolt). The damage lands only after
    // the attack-frame delay and the flight (the first tick after both, so a client never sees a number before the
    // explosion). The target is picked at the cast: the box stays where the ball was sent even if that mob moves or
    // dies on the way. The cast returns no hits for a ball; they come out of Tick. A skill with no ball judges with the
    // full lt..rb box on the caster (Flame Orb, Poison Breath), so it can hit a mob behind the caster: no target is picked
    // and no range is read, whatever the file's rectBasedOnTarget says. A skill with no
    // ball and a body action (AttackDelayMs) judges at the action's attack frame, on the first tick after it, from Tick like a ball
    // (the box stays where the caster cast it): the cast effect has reached its main part by then. A skill with no action judges at once.
    private List<Hit> WzStrike(int map, float x, float y, bool facingRight, Random rng, Build build, WzSkillData wz, int pct, int attacker)
    {
        // Every number the damage needs is read here, in the cast, where a bad skill file fails the caster's call. A ball
        // lands later in the tick loop, which must not throw (it would end the loop for everyone).
        var element = wz.Element();
        // The DoT of the cast (dot, dotInterval, dotTime at max level), read here too: Tick must not read the WZ numbers.
        Dot? dot = !wz.Common.ContainsKey("dot") ? null : new Dot
        {
            Skill = wz.SkillId,
            Attacker = attacker,
            Build = build,
            Pct = (int)wz.Value("dot", wz.MaxLevel),
            NormalPct = NormalPctOf(wz),
            Element = element,
            Rng = rng,
            Map = map,
            CasterX = x,
            CasterY = y,
            IntervalMs = (int)(wz.Value("dotInterval", wz.MaxLevel) * 1000),
            MsLeft = (int)(wz.Value("dotTime", wz.MaxLevel) * 1000 * (100 + build.DotTimePct) / 100), // Burning Magic: a longer DoT
        };
        if (wz.IsGroundZone)
        {
            StartZone(map, x, y, facingRight, rng, build, wz, element, pct, attacker, dot, (int)(wz.Value("time", wz.MaxLevel) * 1000), wz.AttackDelayMs + TickMs, 0);
            return new List<Hit>();
        }
        if (wz.IsSummon)
        {
            StartSummon(map, x, y, facingRight, rng, build, wz, element, attacker, dot);
            return new List<Hit>();
        }
        int mobCount = (int)wz.Value("mobCount", wz.MaxLevel);
        int segments = wz.Common.ContainsKey("attackCount") ? (int)wz.Value("attackCount", wz.MaxLevel) : 1; // no attackCount: one hit (Teleport Mastery)
        bool ball = wz.BallFrames.Count > 0;
        int attackDelay = wz.AttackDelayMs; // the ball appears when the body action reaches its attack frame
        // A skill with no ball judges with the full lt..rb box on the caster (Flame Orb, Poison Breath), on both sides: the
        // box is not moved onto a target and no range is read, so a mob behind the caster is hit too.
        if (!ball)
        {
            if (attackDelay <= 0) return WzHits(map, x, y, facingRight, rng, build, wz, element, mobCount, segments, pct, attacker, dot, x, y);
            Wait(x, y, attackDelay + TickMs);
            return new List<Hit>();
        }
        bool onTarget = wz.Info.GetValueOrDefault("rectBasedOnTarget") == "1";
        float range = wz.Reach(); // a ball reads the range: the end of the flight, or the search for a target
        Mob? first = null;
        if (onTarget)
            foreach (var m in _lives)
            {
                float dx = m.X - x;
                bool ahead = facingRight ? dx >= -BehindX : dx <= BehindX;
                if (m.Map != map || !ahead || Math.Abs(dx) > range || !wz.BodyOverlapsY(m.Y - y, m.BodyH)) continue;
                if (first == null || Math.Abs(dx) < Math.Abs(first.X - x)) first = m;
            }
        float boxX = first?.X ?? x + (facingRight ? range : -range);
        float boxY = first?.Y ?? y;
        float toX = boxX;
        float toY = first == null ? y - LaunchDy : first.Y - first.BodyH / 2f; // the explosion is at the mob body
        float fromX = facingRight ? Math.Min(x + LaunchDx, toX) : Math.Max(x - LaunchDx, toX); // a mob right in front: the ball does not fly back
        float fromY = y - LaunchDy;
        _launches.Add(new BallEvent(wz.SkillId, map, fromX, fromY, toX, toY, attackDelay));
        Wait(boxX, boxY, attackDelay + Projectile.FlightMs(fromX, fromY, toX, toY) + TickMs);
        return new List<Hit>();

        // The box (center bx, by) judges after msLeft, in Tick, with the numbers read above.
        void Wait(float bx, float by, int msLeft) => _pending.Add(new Pending
        {
            Map = map,
            X = bx,
            Y = by,
            CasterX = x,
            CasterY = y,
            FacingRight = facingRight,
            MsLeft = msLeft,
            Rng = rng,
            Build = build,
            Wz = wz,
            Element = element,
            MobCount = mobCount,
            Segments = segments,
            Pct = pct,
            Attacker = attacker,
            Dot = dot,
        });
    }

    // The damage of one WZ cast whose box has its origin at (ox, oy): the mobs whose body overlaps the box lt..rb,
    // mirrored by facing (WzSkillData.InBodyRect), taken nearest first, at most mobCount. Each takes attackCount separate rolls
    // (a hit each, so the numbers equal the segments). The mob loses the TOTAL of its segments once, and that total
    // decides its death (a drop and EXP once): a segment never kills the mob in the middle, so every segment keeps its
    // number. The first hit of the mob settles (Hit.Settle), the others only show.
    // The cast is one attack of the hit pipeline (HitPipeline, five steps, statuses first): the element comes from the skill; the mark of
    // Elemental Weakness, the Empirical Knowledge stack and the DoT of the skill (common dot, dotInterval, dotTime at max level; DotTick does the ticks)
    // are on the mob before the first segment is rolled. Elemental Drain, MP Eater, Element Amplification and Burning Magic are in HitPipeline too.
    // The WZ fields u, x and w of Energy Bolt are not read (their meaning is not known).
    private List<Hit> WzHits(int map, float ox, float oy, bool facingRight, Random rng, Build build, WzSkillData wz,
        AttackElement element, int mobCount, int segments, int pct, int attacker, Dot? dot, float casterX, float casterY)
    {
        var inBox = new List<(float dist2, Mob m)>();
        foreach (var m in _lives)
        {
            float dx = m.X - ox, dy = m.Y - oy;
            if (m.Map == map && wz.InBodyRect(dx, dy, m.BodyH, facingRight)) inBox.Add((dx * dx + dy * dy, m));
        }
        inBox.Sort((a, b) => a.dist2.CompareTo(b.dist2));
        var hits = ResolveWz(inBox.Take(mobCount).Select(t => t.m).ToList(), map, rng, build, wz, element, segments, pct, attacker, dot, casterX, casterY);
        if (element == AttackElement.Fire) hits.AddRange(DetonatePool(map, wz, ox, oy, facingRight, attacker)); // here, not in ResolveWz: the box exists with no mob in it
        return hits;
    }

    // One WZ cast on the chosen mobs: one attack of the hit pipeline (a skill hit and, with a dot, the DoT put on the mobs it leaves alive).
    // Teleport Mastery also stuns each mob (WZ subProp: the chance, time: the seconds, at max level). A fire hit may start Ignite's wall (StartIgnite).
    private List<Hit> ResolveWz(List<Mob> targets, int map, Random rng, Build build, WzSkillData wz, AttackElement element, int segments, int pct,
        int attacker, Dot? dot, float casterX, float casterY)
    {
        bool stuns = wz.SkillId == WzSkillData.TeleportMastery;
        var hits = _pipeline.Resolve(targets, new HitPipeline.Attack
        {
            Build = build,
            Rng = rng,
            Attacker = attacker,
            Skill = wz.SkillId,
            Pct = pct,
            Element = element,
            Segments = segments,
            Magic = true,
            Dot = dot,
            StunProp = stuns ? (int)wz.Value("subProp", wz.MaxLevel) : 0,
            StunMs = stuns ? (int)(wz.Value("time", wz.MaxLevel) * 1000) : 0,
            Map = map,
            CasterX = casterX,
            CasterY = casterY,
        });
        if (element == AttackElement.Fire && build.IgniteProp > 0 && targets.Count > 0 && rng.NextDouble() * 100 < build.IgniteProp)
            StartIgnite(targets[0], rng, build, attacker, casterX, casterY);
        return hits;
    }

    // Creeping Toxin's blast, the rule the user gave (2026-11-21): the box of a fire WZ skill (lt..rb at (ox, oy), mirrored by facing, the same box that finds its mobs) that touches
    // a pool of the caster's Creeping Toxin blows that pool up, whether or not a mob stands in it. A pool can blow up once it is `w2` ms old (1.5 s: the time since it came up). One cast
    // blows up every ripe pool its box touches, all in this tick, and pays the MP once (the first blast says paid; the rest, like the chain, are free). The box is read here in WzHits
    // (the one place every WZ cast with a box passes, mobs or not) and not in ResolveWz, which only sees the mobs; Ignite's wall and every DoT tick never come here (a wall is not
    // a fire skill's hit). The attacks of a summon must not come here either (Ifrit's attack will have to skip this).
    // Vertical (未核实, the developer's reading): the box touches a pool when it overlaps the cast area of Creeping Toxin round the totem (zone Top..Bottom: 85 up and 60 down from the feet), not only the floor line.
    private List<Hit> DetonatePool(int map, WzSkillData fire, float ox, float oy, bool facingRight, int attacker)
    {
        var lt = fire.Vectors["lt"];
        var rb = fire.Vectors["rb"];
        float left = facingRight ? ox - rb[0] : ox + lt[0], right = facingRight ? ox - lt[0] : ox + rb[0];
        float top = oy + lt[1], bottom = oy + rb[1];
        var touched = new List<(LiveSummon Summon, int Index)>();
        foreach (var s in _summons)
        {
            if (s.Unit.Attacker != attacker || s.Unit.Map != map) continue;
            var zone = _zones.Find(z => z.Zone.Uid == s.ZoneUid)!.Zone; // a totem and its pool go together
            if (zone.Bottom < top || zone.Top > bottom) continue;
            for (int i = 0; i < s.Pools.Count; i++)
            {
                var pool = s.Pools[i];
                if (IsRipe(s, zone, pool) && pool.Center + pool.Width / 2f >= left && pool.Center - pool.Width / 2f <= right) touched.Add((s, i));
            }
        }
        foreach (var (s, i) in touched) s.Pools[i].Queued = true; // all set first: the blast of one does not set off another that this hit blows up itself
        var hits = new List<Hit>();
        for (int k = 0; k < touched.Count; k++) hits.AddRange(ExplodePool(touched[k].Summon, touched[k].Index, attacker, k == 0));
        return hits;
    }

    // A pool is ripe when it has been there `w2` ms (WZ w2, 1500) and is not spent or set off already.
    private static bool IsRipe(LiveSummon s, GroundZone zone, LivePool pool) =>
        !pool.Queued && zone.AgeMs - pool.Born >= (int)s.Wz.Value("w2", s.Wz.MaxLevel);

    // One pool blows up (the child skill 2111014 of Creeping Toxin): the mobs in the box lt..rb of the blast round the middle of the pool take its damage %, attackCount hits, at most
    // mobCount (the nearest first). The hits belong to Creeping Toxin (its hit art and its id), the element is the one of the skill (poison), no DoT, and the numbers of the pool's
    // Creeping Toxin (nbdR: +% against a mob that is not a boss). A mob that a pool blast hit within `t` seconds before (WZ t 0.4: the time is the world time of the last blast hit
    // on that mob) takes u2 percent less FINAL damage from this blast (WZ u2 60: the final damage zone of this attack only); every blast hit sets the time again. Then every ripe pool of the caster's whose puddle touches the box of this blast is set to blow up one chain wait later (WZ y of the blast, 60 x 10 ms, 未核实 unit), for free (the blast event says paid false). The blasted pool itself grows back: it is there again
    // after the wait of WZ y of Creeping Toxin (120 x 10 ms, 未核实 unit) and the steps from the totem, and ripe w2 ms after that. "Next to" is the developer's reading (未核实: WZ only says
    // "nearby"): a pool whose puddle comes within the half width of the blast box (rb 190) of the middle of this pool. 未核实 too (the developer's readings, WZ and the user say nothing): the chain
    // runs only between pools of the same caster; the cut window of u2 is kept per caster (see _lastBlast); the damage uses the Build of the cast of Creeping Toxin (the totem's moment),
    // not the attributes of the fire hit that sets the blast off.
    private List<Hit> ExplodePool(LiveSummon s, int index, int attacker, bool paid)
    {
        var child = s.Wz.Children.Single(c => c.SkillId == WzSkillData.CreepingToxinBlast);
        var u = s.Unit;
        var pool = s.Pools[index];
        var zone = _zones.Find(z => z.Zone.Uid == s.ZoneUid)!.Zone;
        pool.Queued = false; // it grows back: after the wait (WZ y) and the spreading from the totem, and it is ripe again w2 ms later
        pool.Born = zone.AgeMs + (int)s.Wz.Value("y", s.Wz.MaxLevel) * WzSkillData.ToxinWzUnitMs + ToxinPoolLayout.SpreadMs(pool.Center, u.X, zone.StepMs, zone.StepPx);
        _poolEvents.Add(new PoolBlastEvent(s.ZoneUid, index, u.Map, attacker, paid));
        var targets = _lives.Where(m => m.Map == u.Map && child.InBodyRect(m.X - pool.Center, m.Y - u.Y, m.BodyH, true))
            .OrderBy(m => (m.X - pool.Center) * (m.X - pool.Center) + (m.Y - u.Y) * (m.Y - u.Y)).Take((int)child.Value("mobCount", child.MaxLevel)).ToList();
        long window = (long)(s.Wz.Value("t", s.Wz.MaxLevel) * 1000);
        double cut = s.Wz.Value("u2", s.Wz.MaxLevel);
        foreach (var old in _lastBlast.Where(kv => _nowMs - kv.Value > window).Select(kv => kv.Key).ToList()) _lastBlast.Remove(old); // a time past the window cuts nothing: drop it
        var chained = targets.Where(m => _lastBlast.ContainsKey((attacker, m.Uid))).Select(m => m.Uid).ToHashSet();
        var hits = _pipeline.Resolve(targets, new HitPipeline.Attack
        {
            Build = s.Build,
            Rng = s.Rng,
            Attacker = attacker,
            Skill = s.Wz.SkillId,
            Pct = (int)child.Value("damage", child.MaxLevel),
            Element = s.Wz.Element(),
            Segments = (int)child.Value("attackCount", child.MaxLevel),
            Magic = true,
            NormalPct = NormalPctOf(s.Wz),
            FinalPctOf = m => chained.Contains(m.Uid) ? -cut : 0,
            Map = u.Map,
            CasterX = u.X,
            CasterY = u.Y,
        });
        foreach (var m in targets) _lastBlast[(attacker, m.Uid)] = _nowMs;
        float reach = child.Vectors["rb"][0];
        int chainMs = (int)child.Value("y", child.MaxLevel) * WzSkillData.ToxinWzUnitMs;
        foreach (var other in _summons)
        {
            if (other.Unit.Attacker != attacker || other.Unit.Map != u.Map) continue;
            var otherZone = _zones.Find(z => z.Zone.Uid == other.ZoneUid)!.Zone;
            for (int j = 0; j < other.Pools.Count; j++)
            {
                var near = other.Pools[j];
                if (!IsRipe(other, otherZone, near) || Math.Abs(near.Center - pool.Center) - near.Width / 2f > reach) continue;
                near.Queued = true;
                _pendingBlasts.Add(new PendingBlast { Summon = other, Index = j, DueMs = chainMs, Attacker = attacker });
            }
        }
        return hits;
    }

    // The damage % a skill adds against a mob that is not a boss (WZ nbdR, "Normal Enemy Damage"); 0 for a skill that has none.
    private static int NormalPctOf(WzSkillData wz) => wz.Common.ContainsKey("nbdR") ? (int)wz.Value("nbdR", wz.MaxLevel) : 0;

    // Ignite: a fire hit started a wall of fire at its first target (one roll for the whole cast, WZ: "when casting Fire magic"). The wall is a ground zone
    // (WZ lt..rb round the target feet, `x` seconds at max level) that puts a DoT of Ignite on every mob in its box: damage % (WZ damage), attackCount hits
    // each time, one time every subTime ms (the interval: the developer's reading of subTime, 未核实), at most mobCount mobs (the nearest). The zone refreshes
    // the DoT every tick and the DoT lasts one interval after the last refresh, so a mob that leaves the wall is hit at most once more. The element is
    // fire (WZ elemAttr), the numbers are the caster's at the cast (the Build). The wall is up after one tick (no body action).
    private void StartIgnite(Mob target, Random rng, Build build, int attacker, float casterX, float casterY)
    {
        var wz = Wz(WzSkillData.Ignite); // loaded in the cast (SkillStrike)
        int interval = (int)wz.Value("subTime", wz.MaxLevel);
        var dot = new Dot
        {
            Skill = wz.SkillId,
            Attacker = attacker,
            Build = build,
            Pct = (int)wz.Value("damage", wz.MaxLevel),
            Segments = (int)wz.Value("attackCount", wz.MaxLevel),
            Element = AttackElement.Fire,
            Rng = rng,
            Map = target.Map,
            CasterX = casterX,
            CasterY = casterY,
            IntervalMs = interval,
            MsLeft = interval,
        };
        // facingRight (true here) does not matter: the wall box (lt/rb) is symmetric left and right
        StartZone(target.Map, target.X, target.Y, true, rng, build, wz, AttackElement.Fire, 0, attacker, dot, (int)(wz.Value("x", wz.MaxLevel) * 1000), TickMs, (int)wz.Value("mobCount", wz.MaxLevel));
    }

    // A ground zone cast at the caster's feet (x, y): the WZ box lt..rb (mirrored by facing, like every box) with its origin on the caster's feet (not
    // on the ground below: a caster in the air has the box around him), fixed there for `time` seconds at max level, starting at the body action's attack
    // frame (the first tick after it, like a ball's landing). It is judged every tick: see ZoneTick. The clients get the zone as one event.
    // lifeMs is how long it stays and waitMs the time before it starts; mobCount caps the mobs it works on (0 = no cap).
    // A zone that spreads (stepMs > 0) starts as a line at x and widens to its box in steps (GroundZone.Spread: the caller sets StepPx and FirstMs); clipLeft and clipRight cut that box (the edges of a platform).
    // Returns the uid of the zone.
    private int StartZone(int map, float x, float y, bool facingRight, Random rng, Build build, WzSkillData wz, AttackElement element, int pct, int attacker, Dot? dot,
        int lifeMs, int waitMs, int mobCount, float clipLeft = float.NegativeInfinity, float clipRight = float.PositiveInfinity, int stepMs = 0, float reach = 0)
    {
        var lt = wz.Vectors["lt"];
        var rb = wz.Vectors["rb"];
        var zone = new GroundZone
        {
            Uid = _nextZone++,
            Map = map,
            Attacker = attacker,
            Left = Math.Max(clipLeft, reach > 0 ? x - reach : facingRight ? x - rb[0] : x + lt[0]), // reach: a zone that spreads over this far to each side (instead of the box lt/rb)
            Right = Math.Min(clipRight, reach > 0 ? x + reach : facingRight ? x - lt[0] : x + rb[0]),
            Top = y + lt[1],
            Bottom = y + rb[1],
            OriginY = y,
            LifeMs = lifeMs,
            WaitMs = waitMs,
            OriginX = x,
            StepMs = stepMs,
        };
        zone.FullLeft = zone.Left;
        zone.FullRight = zone.Right;
        if (stepMs > 0) zone.Left = zone.Right = x;
        _zones.Add(new LiveZone
        {
            Zone = zone,
            Cast = new Pending { Wz = wz, Rng = rng, Build = build, Element = element, Pct = pct, Attacker = attacker, Dot = dot, MobCount = mobCount, CasterX = x, CasterY = y, Map = map },
        });
        _zoneEvents.Add(new ZoneEvent(zone.Uid, wz.SkillId, map, zone.FullLeft, zone.FullRight, y, zone.LifeMs, zone.WaitMs, false, x, stepMs, 0));
        return zone.Uid;
    }

    // Creeping Toxin: the totem (a summon, SummonUnit) stands on the floor ToxinFrontPx in front of the caster for `time` seconds, and its pool (a spreading ground zone with the same
    // wait and life) widens from the totem to both sides in steps (GroundZone.Spread: the first at WzSkillData.ToxinFirstPoolMs, then WZ u px every WZ subTime ms, all 未核实 readings) as far as
    // ToxinPoolLayout.Reach and no further than the platform goes (SceneMath.PlatformSpan). The totem never blows up and the pool keeps its poison to the end. The pool does the work of a zone:
    // no direct hit when it starts (the user, 2026-11-21) and no mob cap: its damage is the DoT on every mob in it and the blasts.
    // The pool keeps its poison when pools blow up (未核实: the user has not said). The pool is cut into pools (ToxinPoolLayout) that a fire hit blows
    // up (DetonatePool): a pool is there when the spreading reaches its middle (Born, ms of the zone's age). A caster has one totem of a skill: a new one takes the old one away (未核实).
    // No floor under the totem: nothing is made (the client does not cast then and pays nothing).
    private void StartSummon(int map, float x, float y, bool facingRight, Random rng, Build build, WzSkillData wz, AttackElement element, int attacker, Dot? dot)
    {
        var footholds = _maps[map].Footholds;
        x = ToxinPoolLayout.TotemX(x, facingRight);
        var ground = SceneMath.GroundBelow(footholds, (int)x, (int)y);
        if (ground == null) return;
        int gy = SceneMath.FootholdY(ground, (int)x);
        for (int i = _summons.Count - 1; i >= 0; i--)
            if (_summons[i].Unit.Attacker == attacker && _summons[i].Unit.Skill == wz.SkillId) RemoveSummon(i);
        int life = (int)(wz.Value("time", wz.MaxLevel) * 1000), wait = wz.AttackDelayMs + TickMs, step = (int)wz.Value("subTime", wz.MaxLevel), stepPx = ToxinPoolLayout.StepPx(wz);
        int reach = ToxinPoolLayout.Reach(wz);
        var (edgeLeft, edgeRight) = SceneMath.PlatformSpan(footholds, (int)x, gy, reach);
        int zoneUid = StartZone(map, x, gy, true, rng, build, wz, element, 0, attacker, dot, life, wait, 0,
            clipLeft: edgeLeft, clipRight: edgeRight, stepMs: step, reach: reach); // mobCount 0: no cap, the poison reaches every mob in the pool (WZ mobCount, attackCount and damage of Creeping Toxin are its blast's: see ExplodePool)
        var zone = _zones.Find(z => z.Zone.Uid == zoneUid)!.Zone;
        zone.StepPx = stepPx;
        zone.FirstMs = WzSkillData.ToxinFirstPoolMs;
        var unit = new SummonUnit { Uid = _nextSummon++, Map = map, Attacker = attacker, Skill = wz.SkillId, X = x, Y = gy, LifeMs = life, WaitMs = wait };
        var summon = new LiveSummon { Unit = unit, ZoneUid = zoneUid, Wz = wz, Build = build, Rng = rng };
        foreach (var (center, kind) in ToxinPoolLayout.Pieces(x, zone.FullLeft, zone.FullRight))
            summon.Pools.Add(new LivePool { Center = center, Width = WzSkillData.ToxinPoolWidths[kind], Born = WzSkillData.ToxinFirstPoolMs + ToxinPoolLayout.SpreadMs(center, x, step, stepPx) });
        _summons.Add(summon);
        _summonEvents.Add(new SummonEvent(unit.Uid, wz.SkillId, map, x, gy, life, wait));
    }

    // One tick of a zone that is up. Every mob in the box is poisoned: one that has no DoT of this skill from this caster gets the cast's DoT, one that has it only
    // gets it refreshed (its time starts again, the beat of its ticks stays). So a mob that walks in is poisoned in that very tick, and many zones of one caster
    // give a mob one DoT between them; a DoT of another skill or of another caster does not count. In the tick the zone starts, the cast also hits every mob in the box
    // once (the cast's damage %, one segment, no mob cap: WZ has no mobCount); the mist itself does no damage, only its poison does, through the pipeline in DotTick.
    // A zone with a mob cap (Ignite's wall, which has no cast hit) works on the nearest mobs of its box only. The pool of Creeping Toxin has no cast hit and no mob cap: it poisons every mob in it.
    private List<Hit> ZoneTick(LiveZone z)
    {
        var c = z.Cast;
        var mobs = _lives.Where(m => m.Map == z.Zone.Map && z.Zone.Contains(m.X, m.Y, m.BodyH)).ToList();
        var inBox = c.MobCount <= 0 || mobs.Count <= c.MobCount ? mobs : mobs.OrderBy(m => Math.Abs(m.X - (z.Zone.Left + z.Zone.Right) / 2)).Take(c.MobCount).ToList(); // the nearest to the middle of the box
        foreach (var m in inBox)
        {
            if (MobElemAttr.Multiplier(m.ElemAttr, c.Dot!.Element) <= 0) continue; // immune to the element of the DoT: it is neither put on nor refreshed (as in HitPipeline.Collect)
            if (m.Statuses.HasDot(c.Attacker, c.Wz.SkillId)) m.Statuses.RefreshDot(c.Attacker, c.Wz.SkillId, c.Dot!.MsLeft);
            else m.Statuses.AddDot(c.Dot!.Clone());
        }
        if (!z.Zone.JustStarted || !c.Wz.IsGroundZone) return new List<Hit>(); // the pool of a summon (Creeping Toxin) has no cast hit: its damage is its poison and its blasts
        return ResolveWz(inBox, c.Map, c.Rng, c.Build, c.Wz, c.Element, 1, c.Pct, c.Attacker, null, c.CasterX, c.CasterY);
    }

    // One tick of a DoT: one attack of the hit pipeline with the caster's snapshot (Build), the DoT's damage % and element (the mob already has
    // the DoT, so Burning Magic counts; a tick takes every bonus a hit takes, Elemental Drain read around the place where the DoT was cast). The tick is one hit per segment of the DoT (Ignite: 3), the first settled, all flagged Dot: HP and death are set, the client skips the flinch and the spark (Ignite's hit art excepted).
    // A kill buries the mob and its statuses go with it, and the kill is credited to the DoT's own source (Attacker). A DoT is removed when its
    // source disconnects or leaves the map (CancelDoTs), so a tick never belongs to a source that is gone.
    private List<Hit> DotTick(Mob m, Dot dot) =>
        _pipeline.Resolve(new[] { m }, new HitPipeline.Attack
        {
            Build = dot.Build,
            Rng = dot.Rng,
            Attacker = dot.Attacker,
            Skill = dot.Skill,
            Pct = dot.Pct,
            Element = dot.Element,
            Magic = true,
            NormalPct = dot.NormalPct,
            Segments = dot.Segments,
            Tick = true,
            Map = dot.Map,
            CasterX = dot.CasterX,
            CasterY = dot.CasterY,
        });

    // One mob's DoT skill changes gathered into the broadcast queue (everyone draws the icons).
    private void FlushDots(Mob m)
    {
        foreach (var (skill, on) in m.Statuses.TakeDotChanges()) _dotDirty.Add((m.Uid, skill, on));
    }

    // One mob's mark changes gathered into the broadcast queue (the caller sends each to its own attacker's client).
    private void FlushMarks(Mob m)
    {
        var changes = m.Statuses.TakeMarkChanges();
        if (changes.Count == 0) return;
        foreach (var (attacker, stacks) in changes) _markDirty.Add((attacker, m.Uid, stacks));
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
    // append, then the whole payout lays out as one row around the corpse
    // (EnterMap loads every spawned mob's table; lives only exist after it).
    private void DropKill(Mob m, Random rng, int attacker)
    {
        var payouts = _dropTables[m.MobId].Roll(rng);
        if (BonusDrops != null) payouts.AddRange(BonusDrops(m.MobId, attacker));
        PlacePayouts(m, payouts, rng);
    }

    // One kill's payouts: icons left-to-right with no gap, the row centred on
    // the corpse. A row wider than the map VR puts every icon at a random x
    // inside the VR (overlap allowed). Each icon lands on the ground below.
    private void PlacePayouts(Mob m, List<(int kind, int id, int count, long amount)> payouts, Random rng)
    {
        var data = _maps[m.Map];
        float width = 0;
        foreach (var p in payouts) width += DropWidth(p.kind, p.id, p.amount);
        float vrLeft = data.Info.VrLeft, vrRight = data.Info.VrRight;
        // The first meso's art hangs left of its anchor by its WZ origin.
        float overhang = payouts.Count > 0 && payouts[0].kind == DropKind.Mesos
            ? -MesoOriginX(payouts[0].amount) : 0f;
        if (width + overhang > vrRight - vrLeft)
        {
            foreach (var p in payouts)
            {
                float x = vrLeft + (float)rng.NextDouble() * (vrRight - vrLeft);
                AddDrop(data, m.Map, x, m.Y, m.X, m.Y - m.BodyH / 2f, DropBag(p.kind, p.id), p.kind, p.id, p.count, p.amount);
            }
            return;
        }
        float left = Math.Clamp(m.X - width / 2f, vrLeft + overhang, vrRight - width);
        float cursor = left;
        foreach (var p in payouts)
        {
            float w = DropWidth(p.kind, p.id, p.amount);
            // Meso art is placed by its WZ origin; shift its anchor so the art
            // fills its slot and the next item starts after the picture.
            float x = p.kind == DropKind.Mesos ? cursor + MesoOriginX(p.amount) : cursor + w / 2f;
            AddDrop(data, m.Map, x, m.Y, m.X, m.Y - m.BodyH / 2f, DropBag(p.kind, p.id), p.kind, p.id, p.count, p.amount);
            cursor += w;
        }
    }

    private float DropWidth(int kind, int id, long amount) => kind == DropKind.Mesos
        ? MesoArt.Widths[MesoArt.Tier(amount)]
        : IconWidth(id);

    // The WZ origin x of the meso picture of this amount.
    private static int MesoOriginX(long amount) => MesoArt.Origins[MesoArt.Tier(amount)].X;

    private string DropBag(int kind, int id) =>
        kind == DropKind.Mesos ? "" : DropBagRule.ForMonster(ItemDef(id));

    // The item def and its info/icon width, cached per id. Item defs are repo
    // data; a missing or old def fails loud so the row never guesses.
    private ItemData ItemDef(int id)
    {
        if (_itemDefs.TryGetValue(id, out var data)) return data;
        string path = Path.Combine(_assetsDir, "item", $"{id}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing item def for drop {id}: {Path.GetFullPath(path)}", path);
        data = LoadJson<ItemData>(path);
        if (data.IconW <= 0)
            throw new InvalidDataException($"item {id} has no icon width (rerun the exporter)");
        _itemDefs[id] = data;
        return data;
    }

    private int IconWidth(int id) => ItemDef(id).IconW;

    // One loot drop at (x, y): clamped inside the VR, lands on the ground
    // below. Meso art hangs right of its anchor by the WZ origin, so its
    // picture edge is clamped too. FromX/FromY is the pop-out source; tab is
    // the bag it returns to.
    private Drop AddDrop(MapData data, int map, float x, float y, float fromX, float fromY, string tab,
        int kind, int id, int count, long amount)
    {
        float cx;
        if (kind == DropKind.Mesos)
        {
            float w = DropWidth(kind, id, amount);
            float ox = MesoOriginX(amount);
            float maxLeft = Math.Max(data.Info.VrLeft, data.Info.VrRight - w);
            float artLeft = Math.Clamp(x - ox, data.Info.VrLeft, maxLeft);
            cx = Math.Clamp(artLeft + ox, data.Info.VrLeft, data.Info.VrRight);
        }
        else
        {
            cx = Math.Clamp(x, data.Info.VrLeft, data.Info.VrRight);
        }
        var d = new GroundDrop
        {
            Uid = _nextDrop++,
            Map = map,
            X = cx,
            Y = LandY(data.Footholds, cx, y),
            Kind = kind,
            Id = id,
            Count = count,
            Amount = amount,
            Tab = tab,
            FromX = fromX,
            FromY = fromY,
            MsLeft = DropMs
        };
        _drops.Add(d);
        var drop = new Drop(d.Uid, d.Map, d.X, d.Y, d.Kind, d.Id, d.Count, d.Amount, d.FromX, d.FromY, true, d.Tab);
        _fresh.Add(drop);
        return drop;
    }

    // Drop landing height: the first ground at or below the kill, else the
    // kill height itself (a void drop fades where it fell).
    private static float LandY(List<FootholdItem> fh, float x, float y)
    {
        var g = SceneMath.GroundBelow(fh, (int)Math.Floor(x), (int)Math.Ceiling(y));
        return g == null ? y : SceneMath.FootholdY(g, (int)Math.Floor(x));
    }
}
