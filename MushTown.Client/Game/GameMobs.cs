using Microsoft.Xna.Framework;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// One idle animation: the picture keys in the order they play, how long each
// stays on screen, and the origin each one is anchored by.
internal sealed record IdleArt(
    IReadOnlyList<string> Images, IReadOnlyList<int> Delays, IReadOnlyList<Origin> Origins);

// Mobs: art loading, spawn/hit/move application, local world lifecycle.
public partial class MushTownGame
{
    private const int BarWidth = 40; // mob HP bar width, px
    private const int BossBarW = 400; // boss HP bar width, px
    private readonly Dictionary<int, IdleArt> _npcIdle = new(); // the idle of an NPC, built once
    private readonly Dictionary<int, IdleArt> _mobIdle = new(); // the idle of a monster, built once

    // Batch clear (a map switch): the Owner leaves the tree with the dict
    // entry. A single death goes through MobArt.CollectDead instead.
    private void ClearMobs()
    {
        foreach (var m in _mobs.Values) _world.RemoveChild(m.Owner);
        _mobs.Clear();
        _markStacks.Clear();
        _drainLayers = 0;
    }

    private void DrawMob(int uid, MobArt m)
    {
        var frame = m.Frames[m.Frame];
        var tex = _textures[frame.Image];
        // The WZ origin is the anchor; a flipped draw mirrors it around the feet,
        // so the left edge comes from the shared flip math (same as NpcFrame).
        int feetX = (int)Math.Floor(m.X);
        int x = m.FacingRight
            ? SceneMath.FlipDrawX(feetX, -frame.OriginX, tex.Width)
            : feetX - frame.OriginX;
        int y = (int)Math.Floor(m.Y) - frame.OriginY;
        DrawTexture(frame.Image, x, y, m.FacingRight); // raw art faces left; the AI faces along
        DrawMark(uid, m); // my own Empirical Knowledge stacks over the head
        if (m.Hp >= m.MaxHp || m.Hp <= 0) return; // fresh and dying mobs hide their bar
        int px = (int)Math.Floor(m.X) - BarWidth / 2 - _camera.X, py = y - 8 - _camera.Y;
        _spriteBatch.Draw(_pixel, new Rectangle(px, py, BarWidth, 5), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(px + 1, py + 1, Math.Max(1, (int)((BarWidth - 2) * (float)m.Hp / m.MaxHp)), 3), new Color(80, 220, 80));
    }

    // The Empirical Knowledge stacks the local player has on this mob: the local world reads its own status list (the single
    // player is attacker 0); net play reads the marks the server sent to this client only.
    private int LocalMarkStacks(int uid) => _localWorld != null
        ? _localWorld.MarkStacks(uid, 0)
        : _markStacks.TryGetValue(uid, out var s) ? s : 0;

    // Elemental Drain layers around the player: the local world reads its own mobs (attacker 0 is the single player); net play
    // reads the count the server last sent. A shot stages a fixed count (--drain).
    private int CurrentDrainLayers()
    {
        if (_shotDrain >= 0) return _shotDrain;
        if (_localWorld != null) return _localWorld.DrainLayers(_mapId, _mover.X, _mover.Y, DrainMaxNow());
        return _drainLayers;
    }

    // Elemental Drain's numbers at max level for a Fire/Poison Mage (0 when another job, or the skill table has no file).
    private int DrainPctNow() => DrainNumber().pct;
    private int DrainMaxNow() => DrainNumber().max;
    private (int pct, int max) DrainNumber()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || chara.Job != StatTables.FirePoisonMage || !_wzSkills.ContainsKey(WzSkillData.ElementalDrain)) return (0, 0);
        return WzSkillStats.ElementalDrain(_wzSkills);
    }

    // The mob/0, mob/1, mob/2 loops (1/2/3 stacks), built once from the exported skill art.
    private void EnsureMarkArt()
    {
        if (_markArt != null) return;
        _markArt = new IdleArt?[3];
        if (!_wzSkills.TryGetValue(WzSkillData.EmpiricalKnowledge, out var wz)) return; // no export: no marks drawn
        for (int k = 0; k < 3; k++)
        {
            if (!wz.Art.TryGetValue($"mob{k}", out var art) || art.Frames.Count == 0) continue;
            foreach (string f in art.Frames) LoadTexture(f);
            _markArt[k] = new IdleArt(art.Frames, art.Delays, art.Origins.Select(o => new Origin(o[0], o[1])).ToList());
        }
    }

    // The stack mark over a mob's head: 1, 2 or 3 stacks play mob/0, mob/1, mob/2. Anchored by the art's origin at the head top.
    private void DrawMark(int uid, MobArt m)
    {
        if (m.Hp <= 0) return; // a dead mob shows no mark
        int stacks = LocalMarkStacks(uid);
        if (stacks <= 0) return;
        EnsureMarkArt();
        var art = _markArt![Math.Min(stacks, 3) - 1];
        if (art == null) return;
        int frame = Idle.At(art.Delays, (float)_timeMs);
        var org = art.Origins[frame];
        int headTop = (int)Math.Floor(m.Y) - m.Frames[0].OriginY; // origin to feet: the top of the head
        int ax = (int)Math.Floor(m.X), ay = headTop - 6;
        DrawTexture(art.Images[frame], ax - org.X, ay - org.Y, false);
    }

    // One mob's art: json plus textures, loaded once.
    private MobData LoadMob(int mob)
    {
        if (!_mobData.TryGetValue(mob, out var data))
        {
            data = LoadJson<MobData>(Path.Combine(_assetsDir, "mob", $"{mob}.json"));
            if (!data.Actions.Any(a => a.Name == data.Idle))
                throw new InvalidDataException($"mob {mob} has no {data.Idle} action");
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
        return data;
    }

    // The idle of one NPC or monster: the picture keys in the order they play,
    // how long each stays, and the origin each is anchored by. The left column
    // of the world map and the NPC list of the minimap play the same one.
    internal IdleArt LifeIdle(int kind, int id)
    {
        if (kind == 1)
        {
            if (_mobIdle.TryGetValue(id, out var mob)) return mob;
            var data = LoadMob(id);
            var frames = data.Actions.First(a => a.Name == data.Idle).Frames;
            return _mobIdle[id] = new IdleArt(
                frames.Select(f => f.Image).ToList(),
                frames.Select(f => f.Delay).ToList(),
                frames.Select(f => new Origin(f.OriginX, f.OriginY)).ToList());
        }
        if (_npcIdle.TryGetValue(id, out var npc)) return npc;
        var npcData = _npcs[id];
        var delays = npcData.StandDelays; // LoadQuestData proved it has stand frames
        var images = delays.Select((_, i) => i == 0 ? $"npc/{id}.png" : $"npc/{id}_{i}.png").ToList();
        return _npcIdle[id] = new IdleArt(images, delays, npcData.StandOrigins);
    }

    // A spawn the server pushed (or the local world listed): the view starts
    // at the live HP, so late joiners see wounded mobs as wounded.
    private void ApplySpawn(int uid, int mob, float x, float y, long maxHp, long hp)
    {
        var o = new GameObject();
        var art = o.Add(new MobArt { Mob = mob, Data = LoadMob(mob), X = x, Y = y, Hp = hp, MaxHp = maxHp });
        art.SetAction(art.Idle);
        _mobs[uid] = art;
        _world.AddChild(o);
    }

    // A hit the server judged (or the local world struck): flinch or die, plus
    // a number. A hit for a view we never saw is dropped: a kill in flight
    // across a remap, or a join racing a swing on another thread.
    // mine + dead means we landed the killing blow: claim the EXP.
    // A skill hit also plants the skill's spark on the mob's body (skills
    // without spark art, like Scarring Sword, skip it). Our own hits feed
    // the combo orb while Combo Attack lives (its bonus hits don't recurse).
    // The hits of one cast must be taken in the same frame (they find each other by Age == 0 and the mob), or the stack
    // is wrong.
    // settle false: a later segment of a multi-segment cast, which only shows its number (the first segment already set
    // the mob's HP, its flinch or death, the sound and the EXP for the whole cast).
    // dot: a tick of a damage-over-time effect (MobWorld.DotTick): it sets HP and death and shows its number, but plays no flinch,
    // no sound and no spark (a kill still dies as usual).
    // mpGain: an MP Eater proc of this hit (the server rolled it and took that MP from the mob, see MobWorld.WzHits): our own
    // client adds that much MP (the MP stays inside our max).
    private void ApplyHit(int uid, long damage, long hpLeft, bool mine, long exp, int skill, bool crit, bool settle, int mpGain = 0, bool dot = false)
    {
        if (!_mobs.TryGetValue(uid, out var m)) return;
        if (mine && mpGain > 0 && _save?.Slots[_slot] is { } chara)
            chara.Mp = Math.Min(chara.Mp + mpGain, MaxMpNow());
        if (settle)
        {
            m.Hp = hpLeft;
            if (hpLeft == 0 || !dot) // a DoT tick that does not kill makes no flinch and no sound
            {
                m.SetAction(hpLeft == 0 ? "die1" : "hit1");
                PlaySfx(hpLeft == 0 ? SoundKey.MobDie(m.Mob, m.Data.Boss) : "hit");
            }
        }
        float bodyH = m.Frames[0].OriginY; // origin to feet: where the head is
        float headY = m.Y - bodyH;
        // The hits of one multi-hit cast land in the same frame: each number of this mob goes one step above the last
        // and appears after it (other mobs' numbers do not count: every mob has its own stack from its own head), and
        // only the first of a mob plants the spark. The rule does not look at the job or the skill: any hit stacks.
        int layer = FloatingNumber.Layer(_numbers, uid);
        bool firstOfMob = layer == 0;
        var num = TakeNumber();
        num.Text = damage.ToString(); num.X = m.X; num.MobUid = uid;
        num.Y = headY - layer * FloatingNumber.StackPx; // where it stands
        num.DelayMs = layer * FloatingNumber.AppearStepMs; // when it appears (timing: FloatingNumber)
        num.Crit = crit;
        if (mine) num.Skin = Hero?.DamageSkin ?? 0;
        // A skill with a ball plays its special once at the landing (SpawnImpacts); each mob it hits gets its hit art.
        // Another WZ skill plants its hit art on each mob.
        _wzSkills.TryGetValue(skill, out var wz);
        var hitArt = skill == 0 ? null : wz == null ? _skills[skill].Hit : ArtFrames(wz, "hit");
        if (hitArt is { Count: > 0 } && firstOfMob && !dot)
        {
            var spark = TakeFx();
            if (wz != null) UseArt(spark, wz, "hit");
            else spark.Frames = hitArt;
            spark.X = m.X; spark.Y = m.Y - bodyH / 2; spark.FacingRight = true;
        }
        if (settle && mine && skill != SkillData.FinalAttack && _fighter.Buffs.ContainsKey(SkillData.ComboAttack))
        {
            int chance = _skills[SkillData.ComboAttack].OrbChance * SkillLv(SkillData.ComboAttack)
                + _skills[SkillData.ComboSynergy].SynChance * SkillLv(SkillData.ComboSynergy);
            if (Random.Shared.NextDouble() * 100 < chance) // visible count: rolled client-side, shown in the window
                _fighter.Orbs = Math.Min(_fighter.Orbs + 1, _skills[SkillData.ComboAttack].OrbMax);
        }
        if (settle && hpLeft == 0 && mine) { GainExp(exp); if (!QuestServer()) BumpQuestKills(m.Mob); } // net kills come back as QuestKill deltas
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
        if (m.Action != "move") m.SetAction("move");
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
        _localWorld!.CancelBalls(0); // the balls in the air did not cross the map change (DropFx took their art)
        _localWorld!.CancelDoTs(0); // and the DoTs the player left on the old map do not tick
        _localWorld.EnterMap(_mapId);
        foreach (var s in _localWorld.MobsOn(_mapId))
            ApplySpawn(s.Uid, s.Mob, s.X, s.Y, s.MaxHp, s.Hp);
        foreach (var d in _localWorld.DropsOn(_mapId))
            ApplyDropSpawn(d.Uid, d.X, d.Y, d.FromX, d.FromY, d.Tab, d.Kind, d.Id, d.Count, d.Amount, false);
    }
}
