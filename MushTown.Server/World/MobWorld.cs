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
    private readonly List<BallEvent> _launches = new(); // balls thrown, waiting for broadcast (the flight time is Projectile.FlightMs of the four numbers, the same rule on the client)
    private readonly List<(int attacker, int uid, int stacks)> _markDirty = new(); // Empirical Knowledge stack changes to send to their owner
    private readonly HashSet<int> _spawned = new();
    private int _nextUid = 1;
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
        for (int i = 0; i < _lives.Count;) // the DoT ticks that came due in the steps above (a kill removes the mob from _lives, so the index stays)
        {
            var m = _lives[i];
            foreach (var dot in m.Statuses.TakeDue())
            {
                strikes.Add((DotTick(m, dot), dot.Attacker));
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
    public int DrainLayers(int map, float x, float y, int max) => _pipeline.DrainLayers(map, x, y, max);

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

    // Drops the mark changes nobody reads (a local world, a shot, the cash shop: the client shows its own stacks from the world
    // directly, so the queue would only grow).
    public void DiscardMarkChanges() => _markDirty.Clear();

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

    private static bool IsWzDamage(WzSkillData wz) => wz.Kind == SkillData.KindActive && wz.Common.ContainsKey("damage");

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
        int mobCount = (int)wz.Value("mobCount", wz.MaxLevel);
        int segments = (int)wz.Value("attackCount", wz.MaxLevel);
        // The DoT of the cast (dot, dotInterval, dotTime at max level), read here too: Tick must not read the WZ numbers.
        Dot? dot = !wz.Common.ContainsKey("dot") ? null : new Dot
        {
            Skill = wz.SkillId,
            Attacker = attacker,
            Build = build,
            Pct = (int)wz.Value("dot", wz.MaxLevel),
            Element = element,
            Rng = rng,
            IntervalMs = (int)(wz.Value("dotInterval", wz.MaxLevel) * 1000),
            MsLeft = (int)(wz.Value("dotTime", wz.MaxLevel) * 1000 * (100 + build.DotTimePct) / 100), // Burning Magic: a longer DoT
        };
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
        var targets = inBox.Take(mobCount).Select(t => t.m).ToList();
        return _pipeline.Resolve(targets, new HitPipeline.Attack
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
            Map = map,
            CasterX = casterX,
            CasterY = casterY,
        });
    }

    // One tick of a DoT: one attack of the hit pipeline with the caster's snapshot (Build), the DoT's damage % and element (the mob already has
    // the DoT, so Burning Magic counts). The tick is a settled hit flagged Dot: HP and death are set, the client skips the flinch and the spark.
    // A kill buries the mob and its statuses go with it, and the kill is credited to the DoT's own source (Attacker). A DoT is removed when its
    // source disconnects or leaves the map (CancelDoTs), so a tick never belongs to a source that is gone.
    private Hit DotTick(Mob m, Dot dot) =>
        _pipeline.Resolve(new[] { m }, new HitPipeline.Attack
        {
            Build = dot.Build,
            Rng = dot.Rng,
            Attacker = dot.Attacker,
            Skill = dot.Skill,
            Pct = dot.Pct,
            Element = dot.Element,
            Magic = true,
            Tick = true,
        })[0];

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
