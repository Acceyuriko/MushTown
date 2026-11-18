using MushTown.Shared;

namespace MushTown.Client;

// Shot staging: frozen local worlds for --hit/--skill/--drop/--dmg/--hurt/--hurtblink/--hurtpre/--aiticks.
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
        if (_shotScreen == "stat200") ClampPoolsToMax(); // shots never update: the new hero's first frame is done here
        if (_shotHit && _shotDmgSkin is { } hitSkin) Hero!.DamageSkin = hitSkin; // --dmgskin: the --hit number reads the demo's own skin
        if (_shotDmg > 0)
        {
            var num = TakeNumber();
            num.Text = _shotDmg.ToString();
            num.Skin = _shotDmgSkin ?? 0;
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
        if (_shotSkill != 0 && !_skills.ContainsKey(_shotSkill))
            throw new InvalidDataException($"unknown skill: {_shotSkill}");
        if (_shotSkill != 0 && _skills[_shotSkill].Kind == SkillData.KindBuff)
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
            ApplyHurt(_mobData[first.Mob].Touch, first.Uid, first.Mob); // the real formula: the shot shows the real number
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
            _mover.X = first.X - 50;
            _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)first.Y)
                ?? throw new InvalidDataException($"--hit/--skill/--drop found no ground near mob {first.Mob}");
            _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
            ResetMotion();
            _mover.FacingRight = true;
            _mover.Action = RollSwing();
            _frame = 1;
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
                    foreach (var h in world.SkillStrike(_mapId, _mover.X, _mover.Y, true, new Random(0), CurrentBuild(), _shotSkill, pct))
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
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
                    foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, rng, CurrentBuild()))
                    {
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
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
                foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, new Random(0), CurrentBuild()))
                    ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                DrainDrops(world, animate: false);
            }
        }
        RunShotAi();
        SnapCamera();
    }

    // --aiticks <n>: n 100ms AI steps at the posed feet. Moves and respawns
    // show; touches don't (the demo must not die mid-pose). Zero ticks skips.
    private void RunShotAi()
    {
        var world = _localWorld!; // StartLocalWorld just built it
        var players = new List<(int id, int map, float x, float y)> { (0, _mapId, _mover.X, _mover.Y) };
        for (int i = 0; i < _shotAiticks; i++)
        {
            var ev = world.Tick(100, players);
            foreach (var mv in ev.Moves) ApplyMove(mv.Uid, mv.X, mv.Y, mv.FacingRight);
            ApplySpawns(ev.Spawns);
            foreach (var t in ev.Takes) if (_drops.Remove(t.uid, out var gone)) _world.RemoveChild(gone.Owner);
        }
    }
}
