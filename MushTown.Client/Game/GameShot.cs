using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// Shot staging: frozen local worlds for --hit/--skill/--drop/--dmg/--hurt/--hurtblink/--hurtmiss/--hurtpre/--teleport/--aiticks.
public partial class MushTownGame
{
    // Shot mode: a frozen local world. --hit plants the player mid-swing next
    // to the first mob and lands one real strike (numbers and all); --skill
    // poses a skill at lv 1: a buff casts for real (loop + timer, no mobs
    // needed), a damage skill freezes mid-effect with judged hit sparks, a
    // utility (leap) only poses its effect and judges nothing.
    private void SetupShotMobs()
    {
        StartLocalWorld();
        if (_shotJob != 0) { _save ??= new SaveData(); (_save.Slots[_slot] ??= DemoChara()).Job = _shotJob; } // --job on a stat shot: the demo plays that class
        RefreshStats();
        if (_shotScreen == "stat200" || (_shotScreen == null && _shotJob != 0)) ClampPoolsToMax(); // shots never update: the new hero's first frame is done here
        if (_shotHit && _shotDmgSkin is { } hitSkin) Hero!.DamageSkin = hitSkin; // --dmgskin: the --hit number reads the demo's own skin
        if (_shotDmg > 0)
        {
            var num = TakeNumber();
            num.Text = _shotDmg.ToString();
            num.Skin = _shotDmgSkin ?? 0;
            num.Crit = _shotCrit;
            num.X = _mover.X;
            num.Y = _mover.Y - 90;
        }
        if (_shotScreen == "death")
        {
            _save ??= new SaveData();
            _save.Slots[_slot] ??= DemoChara();
            var dead = _save.Slots[_slot]!;
            dead.Hp = 0;
            if (_shotWear) // the demo bag has no hat: --wear also puts the Pilgrim Hat on, to prove the head gear stays on the ghost
            {
                Inventory.Add(dead, "equip", 1000025, 1);
                Progression.WearEquip(dead, 1000025, _items);
            }
            Die();
            for (int i = 0; i < 5; i++) _death.Tick(1000f); // lands, then rests
            SnapCamera();
            return;
        }
        SetupShotQuests(); // quest/talk screens run the real accept/open path (other shots no-op)
        if (_shotWear) // --wear runs the real WearEquip path on the demo (plain shots grow one, like --skill buffs)
        {
            _save ??= new SaveData();
            var chara = _save.Slots[_slot] ??= DemoChara();
            // The demo bag holds one item per slot: the doll wears them all.
            var ids = chara.EquipBag!.Keys.AsEnumerable();
            if (!string.IsNullOrEmpty(_shotWearOnly))
            {
                var slots = _shotWearOnly.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                ids = ids.Where(id => _items.TryGetValue(id, out var data) && data.Slot != null && slots.Contains(data.Slot));
            }
            foreach (int id in ids.ToList())
                Progression.WearEquip(chara, id, _items);
            RefreshStats();
            BuildStatScene(); // the Character Info portrait shows the worn items: bake them in
        }
        bool wzSkill = _wzSkills.ContainsKey(_shotSkill); // a Fire/Poison skill: its own table
        if (_shotSkill != 0 && !wzSkill && !_skills.ContainsKey(_shotSkill))
            throw new InvalidDataException($"unknown skill: {_shotSkill}");
        if (_shotSkill != 0 && !wzSkill && _skills[_shotSkill].Kind == SkillData.KindBuff)
        {
            // buff shots run the real path (pay, cast): a demo that
            // cannot afford the pose fails loud instead of faking it
            _save ??= new SaveData();
            _save.Slots[_slot] ??= DemoChara();
            var data = _skills[_shotSkill];
            if (!TryPaySkill(_shotSkill))
                throw new InvalidDataException($"--skill {_shotSkill} cannot cast: needs {data.Mp} MP");
            CastBuff(_shotSkill);
            RunShotAi();
            SnapCamera();
            return;
        }
        if (wzSkill && _wzSkills[_shotSkill].Kind == SkillData.KindBuff)
        {
            // a WZ buff (Meditation) runs the real path too: pay, cast (timer, loop, the stats); --flyms <ms> then ages the effects, so
            // the cast effect and the affected loop are shown mid-play. The demo must be a Fire/Poison hero (--job 210).
            _save ??= new SaveData();
            _save.Slots[_slot] ??= DemoChara();
            var buff = _wzSkills[_shotSkill];
            if (!TryPayWzSkill(buff))
                throw new InvalidDataException($"--skill {_shotSkill} cannot cast: the demo needs the job (--job 210) and {(int)buff.Value("mpCon", buff.MaxLevel)} MP");
            CastBuff(_shotSkill);
            RunShotFly(_shotFlyms);
            SnapCamera();
            return;
        }
        if (_shotTeleport != null)
        {
            PoseTeleport();
            return;
        }
        if (_shotManaWave != null)
        {
            PoseManaWave();
            return;
        }
        if (_shotHurt)
        {
            var world = _localWorld!; // StartLocalWorld just built it
            var first = world.MobsOn(_mapId).FirstOrDefault();
            _save ??= new SaveData();
            _save.Slots[_slot] ??= DemoChara();
            RefreshStats();

            if (first == null) throw new InvalidDataException($"--hurt needs a mob, map {_mapId} has none");
            _mover.X = first.X - 180; // the mob stands to the right
            _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)first.Y)
                ?? throw new InvalidDataException($"--hurt found no ground at ({_mover.X},{first.Y})");
            _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
            ResetMotion();
            _mover.FacingRight = false; // faces away from the mob: the shove still goes away from it
            _mover.Action = "stand1";
            _frame = 0;
            SnapCamera(); // --hurtpre and --hurt share this camera; the arc must not move it
            if (_shotHurtPre) return;
            var standGround = _mover.Ground;
            float standY = _mover.Y;
            ApplyHurt(_mobData[first.Mob].Touch, first.Uid, first.Mob, _shotHurtMiss ? 0.0 : 1.0); // the real formula; the dodge roll is fixed so a shot never flips: 0 dodges, 1 never does
            if (_shotAction != "stand1") // --action: the pose a moving or swinging player keeps while hurt, on the ground
            {
                _mover.Reset();
                _mover.Ground = standGround;
                _mover.Y = standY;
                for (int i = _shotHurtBlink ? 5 : 11; i > 0; i--) _hurt.Tick(); // 5 frames = purple step, 11 frames = normal step
                _mover.Action = _shotAction;
                _shownAction = _shotAction;
                _frame = _shotFrame;
                return;
            }
            // Real physics, 60 steps a second: 5 steps = 83ms (purple flash step),
            // 8 steps = 133ms (normal step, near the top of the arc).
            _mover.Input = new PlayerInput { Walk = OwnWalk(), Jump = OwnJump() };
            for (int i = _shotHurtBlink ? 5 : 8; i > 0; i--)
            {
                _mover.Update(1f / 60);
                _hurt.Tick();
            }
            _shownAction = _mover.Action; // "jump": in the air the hurt state keeps the jump pose
            _frame = 0;
            return;
        }
        if (_shotHit || _shotSkill != 0 || _shotDrop)
        {
            var world = _localWorld!; // StartLocalWorld just built it
            var first = world.MobsOn(_mapId).FirstOrDefault()
                ?? throw new InvalidDataException($"--hit/--skill/--drop needs mobs, map {_mapId} has none");
            bool right = !wzSkill || _shotFacingRight; // the warrior poses face right; a WZ skill follows --facing
            int standOff = wzSkill ? (_shotStandoff > 0 ? _shotStandoff : 250) : 50; // a ball needs room to fly: a WZ skill stands 250 px from the first mob
            _mover.X = first.X + (right ? -standOff : standOff);
            _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)first.Y)
                ?? throw new InvalidDataException($"--hit/--skill/--drop found no ground near mob {first.Mob}");
            _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
            ResetMotion();
            _mover.FacingRight = right;
            _mover.Action = RollSwing();
            _frame = 1;
            if (wzSkill)
            {
                PoseWzSkill(world, _wzSkills[_shotSkill]);
                SnapCamera();
                return;
            }
            // fixed seed: the evidence shot shows the same number every run
            if (_shotSkill != 0)
            {
                if (_skills[_shotSkill].Kind == SkillData.KindPassive)
                    throw new InvalidDataException($"--skill {_shotSkill} is passive (no art to pose)");
                var chara = _save?.Slots[_slot];
                if (chara != null) TryPaySkill(_shotSkill); // window shots pay best-effort (cooldowns show); plain shots have no one
                SpawnCastFx(_shotSkill, _mover.X, _mover.Y, true);
                int n = _skills[_shotSkill].Effect.Count;
                if (n <= 5) throw new InvalidDataException($"--skill {_shotSkill} needs 6+ effect frames, got {n}");
                _fx[0].Frame = 5; // mid-effect: the small first frames prove nothing
                if (world.IsDamageSkill(_shotSkill))
                {
                    int pct = Progression.SkillDamage(_skills[_shotSkill], 1);
                    foreach (var h in world.SkillStrike(_mapId, _mover.X, _mover.Y, true, new Random(0), ShotBuild(), _shotSkill, pct))
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
                    DrainDrops(world, animate: false);
                }
            }
            else if (_shotDrop)
            {
                // Fixed extra payouts so the shot proves several icons lay out
                // in one row; source data still comes from the exported defs.
                // Map 20000's table already pays the copper pile; the three extra
                // meso piles complete the four tiers in its evidence shot only.
                var bonus = new List<(int kind, int id, int count, long amount)>();
                if (_mapId == 20000)
                    bonus.AddRange(new[] { (DropKind.Mesos, 0, 0, 60L), (DropKind.Mesos, 0, 0, 500L), (DropKind.Mesos, 0, 0, 5000L) });
                bonus.Add((DropKind.Item, 2000002, 1, 0));
                bonus.Add((DropKind.Item, 2000006, 1, 0));
                bonus.Add((DropKind.Item, 1302007, 1, 0));
                world.BonusDrops = (_, _) => bonus;
                // swing until the first kill (one fixed seed: same corpse, same loot)
                var rng = new Random(0);
                bool killed = false;
                for (int i = 0; i < 20 && !killed; i++)
                    foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, rng, ShotBuild()))
                    {
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
                        killed |= h.HpLeft == 0;
                    }
                if (!killed) throw new InvalidDataException($"--drop killed nothing in 20 swings, map {_mapId}");
                DrainDrops(world, animate: false);
                // the corpse would cover the row: this shot only proves the drops
                if (_mobs.Remove(first.Uid, out var corpse)) _world.RemoveChild(corpse.Owner);
                // stand clear of the row, so every payout stays visible
                _mover.X -= 200;
                _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)_mover.Y)
                    ?? throw new InvalidDataException($"--drop found no ground 200px left of mob {first.Mob}");
                _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
                ResetMotion();
                _mover.FacingRight = true;
                _mover.Action = "stand1";
                _frame = 0;
            }
            else
            {
                foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, new Random(0), ShotBuild()))
                    ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
                DrainDrops(world, animate: false);
            }
        }
        RunShotAi();
        SnapCamera();
    }

    // --teleport <dir> --at <x>[,<y>]: the hero stands at feet x on the ground nearest below feet height y (default: the
    // map's spawn height), facing the way of a horizontal cast, the direction key held, and casts Teleport through
    // CastTeleport (the landing rules, the MP, the effect). --hold <ms> then steps the mover that long with the key held. The camera is fixed
    // before the cast and stays, so the pre and the post shot show the same scene. The effect is posed at its fourth frame (a thin one: the hero stays visible).
    private void PoseTeleport()
    {
        _mover.X = _shotAt!.Value.X;
        _mover.Ground = SceneMath.GroundBelow(_map.Footholds, (int)_mover.X, _shotAt.Value.Y ?? _map.Info.SpawnY)
            ?? throw new InvalidDataException($"--teleport found no ground at x {_mover.X}");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
        ResetMotion();
        _mover.FacingRight = _shotTeleport != "left";
        _mover.Action = "stand1";
        _frame = 0;
        if (_shotTeleportFrom == "rope") // hangs on the rope or ladder at --at (its x, the --at height)
        {
            var rope = SceneMath.FindLadder(_map.Ladders, _mover.X, _shotAt!.Value.Y ?? _map.Info.SpawnY)
                ?? throw new InvalidDataException("--teleportfrom rope found no ladder or rope at --at");
            _mover.Climb = rope;
            _mover.Ground = null;
            _mover.X = rope.X;
            _mover.Y = _shotAt!.Value.Y ?? _map.Info.SpawnY;
            _mover.Action = rope.Ladder ? "ladder" : "rope";
        }
        if (_shotTeleportFrom == "swing") // the swing is on: the attack lock holds the pose
        {
            string swing = RollSwing();
            _fighter.StartAttack(Delays(swing, Hero?.Skin ?? 0).Sum(), AttackScale());
            _mover.AttackAction = swing;
            _mover.Action = swing;
        }
        _shownAction = _mover.Action;
        SnapCamera();
        if (_shotTeleportPre && _shotHoldMs == 0) return;
        _upNow = _shotTeleport == "up";
        _downNow = _shotTeleport == "down";
        _dirNow = _shotTeleport == "left" ? -1 : _shotTeleport == "right" ? 1 : 0;
        if (!_shotTeleportPre) // --teleportpre --hold only walks: no cast
        {
            CastTeleport(_wzSkills[WzSkillData.Teleport]);
            foreach (var f in _fx) f.Frame = 3;
        }
        if (_shotTeleportFrom != null) // one step: the pose of the new place (a swing keeps its pose, a rope hero stands now)
        {
            _mover.Input = new PlayerInput { Locked = _fighter.AttackLock > 0, Walk = OwnWalk(), Jump = OwnJump() };
            _mover.Update(1f / 60);
            _shownAction = _mover.Action;
            _frame = _fighter.AttackLock > 0 ? 1 : 0;
        }
        if (_shotHoldMs == 0) return;
        _mover.Input = new PlayerInput { Dir = _dirNow, Walk = OwnWalk(), Jump = OwnJump() };
        for (int i = _shotHoldMs * 60 / 1000; i > 0; i--) _mover.Update(1f / 60);
        _shownAction = _mover.Action;
    }

    // --manawave <boost|air|float|release|timeout> --at <x>[,<y>] [--hold <ms>]: the hero stands as for --teleport and plays Mana Wave through the
    // Client's own check (UpdateManaWave: the MP, the effect) and the air state and mover steps at 60 a second (see Program.cs).
    private void PoseManaWave()
    {
        _mover.X = _shotAt!.Value.X;
        _mover.Ground = SceneMath.GroundBelow(_map.Footholds, (int)_mover.X, _shotAt.Value.Y ?? _map.Info.SpawnY)
            ?? throw new InvalidDataException($"--manawave found no ground at x {_mover.X}");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
        float standY = _mover.Y;
        ResetMotion();
        _mover.FacingRight = true;
        void Step(int frames, bool up, bool alt, bool fresh)
        {
            for (int i = 0; i < frames; i++)
            {
                UpdateManaWave(fresh && i == 0, up);
                _mover.Input = new PlayerInput { Up = up, JumpHeld = alt, JumpPressed = fresh && i == 0, Walk = OwnWalk(), Jump = OwnJump() };
                _air.Update(1f / 60);
                _mover.Update(1f / 60);
                _bubble.Active = _air.Floating; // what the tree step and the end of Update do in the game
                _bubble.Update(1f / 60);
                SpawnFloatEnds();
                foreach (var f in _fx) f.Update(1f / 60);
                PoolSweep.CollectDead(_fx, _world, _fxPool); // a finished effect leaves, as in the game
                _frame = PoseFrame(_mover.Action, 0);
            }
        }
        int hold = _shotHoldMs > 0 ? _shotHoldMs : _shotManaWave == "float" ? 1000 : _shotManaWave is "release" or "timeout" ? 240 : 250;
        int frames = Math.Max(1, hold * 60 / 1000);
        switch (_shotManaWave)
        {
            case "boost":
                Step(frames, true, true, true);
                break;
            case "air":
                Step(6, false, true, true); // the plain jump (no Up: nothing to cast on the ground)
                Step(frames, true, true, true); // Up and Jump together in the air
                break;
            case "release":
                Step(40, true, true, true); // the boost, to its top
                Step(2, false, false, false);
                Step(36, false, true, true); // the slow fall for 600 ms
                Step(frames, false, false, false); // Jump let go: the fall is free again, the end effect plays
                break;
            case "timeout":
                Step(40, true, true, true); // the boost, to its top
                Step(2, false, false, false);
                Step(5 * 60 + 2, false, true, true); // Jump held through the 5 s limit
                Step(frames, false, true, false); // still held: the limit ended it, the end effect plays
                break;
            default:
                Step(40, true, true, true); // the boost, to its top (a plain jump is only 75 px: the slow fall needs height)
                Step(2, false, false, false); // the keys let go
                Step(frames, false, true, true); // pressed again and held: the slow fall
                break;
        }
        Console.WriteLine($"manawave {_shotManaWave}: {hold} ms in, feet height {_mover.Y:F1} (stand {standY:F0}, so {standY - _mover.Y:F1} px up), speed {_mover.Vy:F1}, MP {_save!.Slots[_slot]!.Mp}");
        _shownAction = _mover.Action;
        SnapCamera();
    }

    // A WZ damage skill (Energy Bolt, Flame Orb): pay best-effort, let the mobs gather (--aiticks), then cast with a fixed
    // seed, so the numbers are the same every run. A skill with a ball judges when the ball arrives, and one with no ball but
    // a body action (Flame Orb) at the action's attack frame (300 ms), so --flyms <ms>
    // runs the world that long after the cast (16 ms steps): the cast effect starts at once, the ball appears at the
    // body action's attack frame (Energy Bolt: 390 ms), then it flies, explodes, and the numbers rise on arrival.
    // Without --flyms the shot is the cast the moment it is thrown, with the cast effect posed mid-play. The shown cast
    // does not roll the mark itself (chance 0), so the plain shot has no mark. --weak casts once before with a 100%
    // Elemental Weakness chance (another seed; the shot ticks the world until that cast's first strike lands, up to
    // 3 s, so its ball is unseen and its numbers are not shown): the shown cast then has the same rolls as the plain
    // shot, with the mark on the mobs (+20 damage %).
    private void PoseWzSkill(MobWorld world, WzSkillData wz)
    {
        if (wz.Kind != SkillData.KindActive || !wz.Common.ContainsKey("damage"))
            throw new InvalidDataException($"--skill {wz.SkillId} is not a WZ damage skill (no pose)");
        TryPayWzSkill(wz); // best-effort, like the window shots: the demo has no one to fail
        RunShotAi();
        int pct = (int)wz.Value("damage", wz.MaxLevel);
        var build = ShotBuild() with { WeaknessProp = 0 };
        if (_shotWeak)
        {
            world.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, new Random(1), build with { WeaknessProp = 100 }, wz.SkillId, pct);
            world.TakeLaunches(); // unseen: the ball is not drawn
            for (int i = 0; i < 30; i++)
                if (ShotTick(100, showStrikes: false).Count > 0) break;
        }
        SpawnCastFx(wz.SkillId, _mover.X, _mover.Y, _mover.FacingRight);
        if (_shotFlyms == 0) _fx[0].Frame = 5; // mid-effect: the small first frames prove nothing
        foreach (var h in world.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, new Random(0), build, wz.SkillId, pct))
            ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
        foreach (var l in world.TakeLaunches()) SpawnBall(l.Skill, l.FromX, l.FromY, l.ToX, l.ToY, l.DelayMs);
        DrainDrops(world, animate: false);
        RunShotFly(_shotFlyms);
        if (_shotRecastMs > 0) // --recast: the same cast again at the same mobs, never a crit, while the numbers of the first are still up
        {
            foreach (var h in world.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, new Random(1), build with { CritPct = 0 }, wz.SkillId, pct))
                ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
            foreach (var l in world.TakeLaunches()) SpawnBall(l.Skill, l.FromX, l.FromY, l.ToX, l.ToY, l.DelayMs);
            DrainDrops(world, animate: false);
            RunShotFly(_shotRecastMs);
        }
    }

    // The world runs ms after a cast, in 16 ms steps: the effects and the numbers age; the mobs and the hero stay posed.
    private void RunShotFly(int totalMs)
    {
        for (int left = totalMs; left > 0; left -= 16)
        {
            int ms = Math.Min(16, left);
            foreach (var f in _balls) TreeUpdate.UpdateObject(f.Owner, ms / 1000f);
            foreach (var f in _fx) TreeUpdate.UpdateObject(f.Owner, ms / 1000f);
            foreach (var n in _numbers) TreeUpdate.UpdateObject(n.Owner, ms / 1000f);
            SpawnImpacts();
            PoolSweep.CollectDead(_balls, _world, _ballPool);
            PoolSweep.CollectDead(_fx, _world, _fxPool);
            PoolSweep.CollectDead(_numbers, _world, _numberPool);
            ShotTick(ms);
        }
    }

    // --aiticks <n>: n 100ms AI steps at the posed feet. Moves and respawns
    // show; touches don't (the demo must not die mid-pose). Zero ticks skips.
    private void RunShotAi()
    {
        for (int i = 0; i < _shotAiticks; i++) ShotTick(100);
    }

    // The posed build; --crit forces a 100% crit rate so the real chain shows the critical number.
    private Build ShotBuild() => _shotCrit ? CurrentBuild() with { CritPct = 100 } : CurrentBuild();

    // One world step at the posed feet: the balls that arrived strike (shown unless asked not to), the mobs move.
    private List<(MobWorld.Hit hit, int attacker)> ShotTick(int ms, bool showStrikes = true)
    {
        var world = _localWorld!; // StartLocalWorld just built it
        var players = new List<(int id, int map, float x, float y)> { (0, _mapId, _mover.X, _mover.Y) };
        var ev = world.Tick(ms, players);
        if (showStrikes)
        {
            foreach (var s in ev.Strikes) ApplyHit(s.hit.Uid, s.hit.Damage, s.hit.HpLeft, true, s.hit.Exp, s.hit.Skill, s.hit.Crit, s.hit.Settle, s.hit.MpPct, s.hit.Dot);
            DrainDrops(world, animate: false);
        }
        foreach (var mv in ev.Moves) ApplyMove(mv.Uid, mv.X, mv.Y, mv.FacingRight);
        ApplySpawns(ev.Spawns);
        foreach (var t in ev.Takes) if (_drops.Remove(t.uid, out var gone)) _world.RemoveChild(gone.Owner);
        return ev.Strikes;
    }
}
