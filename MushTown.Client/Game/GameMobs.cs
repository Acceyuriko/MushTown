using Microsoft.Xna.Framework;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// Mobs: art loading, spawn/hit/move application, local world lifecycle.
public partial class MushTownGame
{
    private const int BarWidth = 40; // mob HP bar width, px
    private const int BossBarW = 400; // boss HP bar width, px

    // Art clears: the Owner leaves the tree with the dict entry (single
    // removals pair the same two calls inline, like the S4 pools).
    private void ClearMobs()
    {
        foreach (var m in _mobs.Values) _world.RemoveChild(m.Owner);
        _mobs.Clear();
    }

    private void DrawMob(MobArt m)
    {
        var frame = MobFrames(m.Mob, m.Action)[m.Frame];
        var tex = _textures[frame.Image];
        // WZ mob art has no origin: center-bottom anchor at the feet
        int x = (int)Math.Floor(m.X) - tex.Width / 2;
        int y = (int)Math.Floor(m.Y) - tex.Height;
        DrawTexture(frame.Image, x, y, m.FacingRight); // raw art faces left; the AI faces along
        if (m.Hp >= m.MaxHp || m.Hp <= 0) return; // fresh and dying mobs hide their bar
        int px = (int)Math.Floor(m.X) - BarWidth / 2 - _camera.X, py = y - 8 - _camera.Y;
        _spriteBatch.Draw(_pixel, new Rectangle(px, py, BarWidth, 5), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(px + 1, py + 1, Math.Max(1, (int)((BarWidth - 2) * (float)m.Hp / m.MaxHp)), 3), new Color(80, 220, 80));
    }

    // One mob's art: json plus textures, loaded once. The idle is stand, or
    // move when the mob has no stand (the blue snail crawls everywhere).
    private string LoadMob(int mob)
    {
        if (!_mobData.TryGetValue(mob, out var data))
        {
            data = LoadJson<MobData>(Path.Combine(_assetsDir, "mob", $"{mob}.json"));
            if (!data.Actions.Any(a => a.Name is "stand" or "move"))
                throw new InvalidDataException($"mob {mob} has no idle action");
            foreach (string need in new[] { "hit1", "die1" })
                if (!data.Actions.Any(a => a.Name == need))
                    throw new InvalidDataException($"mob {mob} has no {need} action");
            foreach (var a in data.Actions)
            {
                if (a.Frames.Count == 0)
                    throw new InvalidDataException($"mob {mob} {a.Name} has no frames");
                if (a.Frames.Any(f => f.Delay <= 0))
                    throw new InvalidDataException($"mob {mob} {a.Name} has a bad delay");
            }
            foreach (var a in data.Actions)
                foreach (var f in a.Frames)
                    LoadTexture(f.Image);
            _mobData[mob] = data;
        }
        return MobIdle(mob);
    }

    private string MobIdle(int mob) =>
        _mobData[mob].Actions.Any(a => a.Name == "stand") ? "stand" : "move";

    private List<MobFrame> MobFrames(int mob, string state) =>
        _mobData[mob].Actions.FirstOrDefault(a => a.Name == state)?.Frames
            ?? throw new InvalidDataException($"mob {mob} has no {state} action");

    // A spawn the server pushed (or the local world listed): the view starts
    // at the live HP, so late joiners see wounded mobs as wounded.
    private void ApplySpawn(int uid, int mob, float x, float y, int maxHp, int hp)
    {
        string idle = LoadMob(mob);
        var o = new GameObject();
        _mobs[uid] = o.Add(new MobArt { Mob = mob, X = x, Y = y, Hp = hp, MaxHp = maxHp, Action = idle });
        _world.AddChild(o);
    }

    // A hit the server judged (or the local world struck): flinch or die, plus
    // a number. A hit for a view we never saw is dropped: a kill in flight
    // across a remap, or a join racing a swing on another thread.
    // mine + dead means we landed the killing blow: claim the EXP.
    // A skill hit also plants the skill's spark on the mob's body (skills
    // without spark art, like Scarring Sword, skip it). Our own hits feed
    // the combo orb while Combo Attack lives (its bonus hits don't recurse).
    private void ApplyHit(int uid, int damage, int hpLeft, bool mine, int exp, int skill)
    {
        if (!_mobs.TryGetValue(uid, out var m)) return;
        m.Hp = hpLeft;
        m.Action = hpLeft == 0 ? "die1" : "hit1";
        m.Frame = 0;
        m.Timer = 0;
        PlaySfx(hpLeft == 0 ? SoundKey.MobDie(m.Mob, _mobData[m.Mob].Boss) : "hit");
        float bodyH = _textures[MobFrames(m.Mob, m.Action)[0].Image].Height;
        float headY = m.Y - bodyH;
        var num = TakeNumber();
        num.Text = damage.ToString(); num.X = m.X; num.Y = headY;
        _numbers.Add(num);
        if (skill != 0 && _skills[skill].Hit.Count > 0)
        {
            var spark = TakeFx();
            spark.Skill = skill; spark.Hit = true; spark.X = m.X; spark.Y = m.Y - bodyH / 2; spark.FacingRight = true;
            _fx.Add(spark);
        }
        if (mine && skill != SkillData.FinalAttack && _fighter.Buffs.ContainsKey(SkillData.ComboAttack))
        {
            int chance = _skills[SkillData.ComboAttack].OrbChance * SkillLv(SkillData.ComboAttack)
                + _skills[SkillData.ComboSynergy].SynChance * SkillLv(SkillData.ComboSynergy);
            if (Random.Shared.NextDouble() * 100 < chance) // visible count: rolled client-side, shown in the window
                _fighter.Orbs = Math.Min(_fighter.Orbs + 1, _skills[SkillData.ComboAttack].OrbMax);
        }
        if (hpLeft == 0 && mine) { GainExp(exp); if (!QuestServer()) BumpQuestKills(m.Mob); } // net kills come back as QuestKill deltas
    }

    // One AI step: snap the pose, face along. hit1/die1 own the pose until
    // they finish (a move never steals a flinch or mirrors a corpse), but
    // the feet still follow.
    // A move for a view we never saw is dropped, like a hit. A changed
    // action restarts its frames (stand/move counts differ per mob).
    private void ApplyMove(int uid, float x, float y, bool facingRight)
    {
        if (!_mobs.TryGetValue(uid, out var m)) return;
        m.X = x;
        m.Y = y;
        if (m.Action == "hit1" || m.Action == "die1") return; // flinch/death own the pose
        m.FacingRight = facingRight;
        if (m.Action != "move")
        {
            m.Action = "move";
            m.Frame = 0;
            m.Timer = 0;
        }
    }

    // Respawn arrivals for this view (other maps' graves tick too, but their
    // spawns are not ours to show).
    private void ApplySpawns(IEnumerable<MobWorld.Spawn> spawns)
    {
        foreach (var s in spawns)
        {
            if (s.Map != _mapId) continue;
            ApplySpawn(s.Uid, s.Mob, s.X, s.Y, s.MaxHp, s.Hp);
        }
    }

    // Local mobs (shots and dropped links): a fresh world once per session,
    // then re-list the view (map switches re-list; kills re-plant on timers).
    private void StartLocalWorld()
    {
        _localWorld = new MobWorld(_assetsDir);
        RefreshLocalMobs();
    }

    private void RefreshLocalMobs()
    {
        ClearMobs();
        ClearDrops();
        DropNumbers();
        DropFx();
        RespawnBuffLoops(); // buff timers survive the switch, so their loops re-hang
        _localWorld!.EnterMap(_mapId);
        foreach (var s in _localWorld.MobsOn(_mapId))
            ApplySpawn(s.Uid, s.Mob, s.X, s.Y, s.MaxHp, s.Hp);
        foreach (var d in _localWorld.DropsOn(_mapId))
            ApplyDropSpawn(d.Uid, d.X, d.Y, d.Kind, d.Id, d.Count);
    }
}
