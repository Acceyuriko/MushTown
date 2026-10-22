using MushTown.Shared;

namespace MushTown.Client;

// Shot staging: frozen local worlds for --hit/--skill/--drop/--aiticks.
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
        SetupShotQuests(); // quest/talk screens run the real accept/open path (other shots no-op)
        if (_shotWear) // --wear runs the real WearEquip path on the demo (plain shots grow one, like --skill buffs)
        {
            _save ??= new SaveData();
            var chara = _save.Slots[_slot] ??= DemoChara();
            // The demo bag holds one item per slot: the doll wears them all.
            foreach (int id in chara.EquipBag!.Keys.ToList())
                Progression.WearEquip(chara, id, _items);
            BuildStatScene(); // the equip page shows the worn items: bake them in
        }
        if (_shotSkill != 0 && !_skills.ContainsKey(_shotSkill))
            throw new InvalidDataException($"unknown skill: {_shotSkill}");
        if (_shotSkill != 0 && _skills[_shotSkill].Kind == SkillData.KindBuff)
        {
            // buff shots run the real path (learn, pay, cast): a demo that
            // cannot afford the pose fails loud instead of faking it
            _save ??= new SaveData();
            var chara = _save.Slots[_slot] ??= DemoChara();
            var data = _skills[_shotSkill];
            if (!chara.SkillLv.ContainsKey(_shotSkill) && !Progression.LearnSkill(chara, data))
                throw new InvalidDataException($"--skill {_shotSkill} cannot learn: demo Lv.{chara.Level} needs Lv.{data.ReqLevel}");
            if (!TryPaySkill(_shotSkill))
                throw new InvalidDataException($"--skill {_shotSkill} cannot cast: needs Lv.{data.ReqLevel} and {data.Mp} MP");
            CastBuff(_shotSkill);
            RunShotAi();
            SnapCamera();
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
            _mover.Action = "swingO1";
            _frame = 1;
            // fixed seed: the evidence shot shows the same number every run
            if (_shotSkill != 0)
            {
                if (_skills[_shotSkill].Kind == SkillData.KindPassive)
                    throw new InvalidDataException($"--skill {_shotSkill} is passive (no art to pose)");
                var chara = _save?.Slots[_slot];
                if (chara != null) // window shots learn and pay best-effort (cooldowns show); plain shots have no one
                {
                    if (!chara.SkillLv.ContainsKey(_shotSkill)) Progression.LearnSkill(chara, _skills[_shotSkill]);
                    TryPaySkill(_shotSkill);
                }
                SpawnCastFx(_shotSkill, _mover.X, _mover.Y, true);
                int n = _skills[_shotSkill].Effect.Count;
                if (n <= 5) throw new InvalidDataException($"--skill {_shotSkill} needs 6+ effect frames, got {n}");
                _fx[0].Frame = 5; // mid-effect: the small first frames prove nothing
                if (world.IsDamageSkill(_shotSkill))
                {
                    int pct = Progression.SkillDamage(_skills[_shotSkill], 1);
                    foreach (var h in world.SkillStrike(_mapId, _mover.X, _mover.Y, true, new Random(0), OwnBuild(), _shotSkill, pct))
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                    DrainDrops(world);
                }
            }
            else if (_shotDrop)
            {
                // swing until the first kill (one fixed seed: same corpse, same loot)
                var rng = new Random(0);
                bool killed = false;
                for (int i = 0; i < 20 && !killed; i++)
                    foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, rng, OwnBuild()))
                    {
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                        killed |= h.HpLeft == 0;
                    }
                if (!killed) throw new InvalidDataException($"--drop killed nothing in 20 swings, map {_mapId}");
                DrainDrops(world);
            }
            else
            {
                foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, new Random(0), OwnBuild()))
                    ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                DrainDrops(world);
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
