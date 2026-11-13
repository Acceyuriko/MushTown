using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// Combat: swings, skills, casts, hurt/death, and the hit feedback pools.
public partial class MushTownGame
{
    private const int DashPx = 220; // Rush/Flash Blade carry the caster this far along facing
    private const float DiveV = 900f; // Leap Attack slams down this fast (past MaxFall on purpose)
    private const int LeapRange = 400; // Leap Attack needs a platform within this far below

    // Pooled spawn: a number is a node now, so a pop puts it back in the tree
    // and a fresh one gets its node once. Tree and list move together here,
    // so a new spawn site cannot leak a node that only the list could reach.
    // Only call this outside the tree step: adding to a node being walked throws.
    private FloatingNumber TakeNumber()
    {
        var n = _numberPool.TryPop(out var p) ? p : new GameObject().Add(new FloatingNumber());
        n.Reset();
        _world.AddChild(n.Owner);
        _numbers.Add(n);
        return n;
    }

    // Same contract as TakeNumber: the node enters the tree and the list here.
    private SkillFx TakeFx()
    {
        var f = _fxPool.TryPop(out var p) ? p : new GameObject().Add(new SkillFx());
        f.Reset();
        _world.AddChild(f.Owner);
        _fx.Add(f);
        return f;
    }

    private void DropNumbers() => PoolSweep.DropAll(_numbers, _world, _numberPool);

    private void DropFx() => PoolSweep.DropAll(_fx, _world, _fxPool);

    // Skill effects: bare frames centered on their anchor, flipped like
    // avatars (raw art faces left). Cast effects and hit sparks sit where they
    // spawned; only a buff loop keeps following the player.
    private void DrawFx()
    {
        foreach (var f in _fx)
        {
            var tex = _textures[f.Frames[f.Frame]];
            var o = new Vector2(tex.Width / 2f, tex.Height / 2f);
            _spriteBatch.Draw(tex, new Vector2(f.X - _camera.X, f.Y - _camera.Y), null, Color.White,
                0f, o, 1f, f.FacingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }

    // Damage numbers: the digits of the number's skin, rising and fading
    // over FloatingNumber.LifeMs.
    private void DrawNumbers()
    {
        if (_numbers.Count == 0) return;
        foreach (var n in _numbers)
        {
            var dmg = LoadDmg(n.Skin);
            float k = n.Age / FloatingNumber.LifeMs;
            int alpha = k < 0.7f ? 255 : (int)(255 * (1 - (k - 0.7f) / 0.3f));
            var tint = new Color(255, 255, 255, alpha);
            int totalW = 0;
            foreach (char c in n.Text) totalW += _textures[dmg.Digits[c - '0'].Image].Width;
            float x = n.X - totalW / 2f - _camera.X;
            float baseY = n.Y - FloatingNumber.RisePx * k - _camera.Y;
            foreach (char c in n.Text)
            {
                var d = dmg.Digits[c - '0'];
                var tex = _textures[d.Image];
                _spriteBatch.Draw(tex, new Vector2(x, baseY - d.Oy), null, tint,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                x += tex.Width;
            }
        }
    }

    // One damage skin: ten digits plus their baseline offsets, loaded once
    // per key. Skin 0 keeps the original file name; a new skin is the same
    // file with its key appended.
    private DmgData LoadDmg(int skin)
    {
        if (_dmg.TryGetValue(skin, out var data)) return data;
        string file = skin == 0 ? "dmg.json" : $"dmg{skin}.json";
        data = LoadJson<DmgData>(Path.Combine(_assetsDir, "effect", file));
        if (data.Digits.Count != 10)
            throw new InvalidDataException($"damage skin {skin} has {data.Digits.Count} digits, want 10");
        foreach (var d in data.Digits) LoadTexture(d.Image);
        _dmg[skin] = data;
        return data;
    }

    // One touch that landed on us: DEF folds in, the number shows what got
    // through, death respawns at the map spawn with full pools (buffs stay).
    // Returns true on death: the caller drops the rest of the frame's touches
    // (judged at the corpse, not the spawn).
    private bool ApplyHurt(long damage)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false; // CLI sessions have no one to hurt
        long dealt = Progression.ApplyHurt(chara, damage, OwnDef());
        if (dealt == 0) return false; // harmless touch: no number, no death
        var num = TakeNumber();
        num.Text = dealt.ToString(); num.X = _mover.X; num.Y = _mover.Y - 90;
        if (chara.Hp != 0) return false;
        Die(chara);
        return true;
    }

    // Death: full pools, feet back on the spawn ground, motion cleared.
    private void Die(CharaSlot chara)
    {
        chara.Hp = MaxHpNow();
        chara.Mp = MaxMpNow();
        _mover.X = _map.Info.SpawnX;
        _mover.Ground = SceneMath.GroundBelow(_map.Footholds, _map.Info.SpawnX, _map.Info.SpawnY)
            ?? throw new InvalidDataException($"map {_mapId} has no ground under spawn");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, _map.Info.SpawnX);
        ResetMotion();
        SnapCamera();
        _talkNpc = 0; _talkQuest = 0; _talkReward = 0; _talkPage = 0; // death teleports: no reload on the same map, so end talk here
        _showShop = false; _shopQtyId = 0; _shopNotice = ""; _shopQtyField.Unfocus(); // and the NPC-bound shop
        LogEvent("you died");
    }

    // Swings and damaging casts need a judge: net play sends, the local
    // world strikes right here. The disconnect frame has neither yet.
    private bool HasJudge() => (_net != null && _net.Connected) || _localWorld != null;

    // The server owns quests only on a live link (dropped links go solo and
    // trust the save again, like movement and drops).
    private bool QuestServer() => _net != null && _net.Connected && _localWorld == null;

    // One cast's client-side checks: learned, level, MP, cooldown. CLI
    // sessions carry no character (no pools), so they cannot cast, like potions.
    private bool TryPaySkill(int id) =>
        _skillCost.Pay(_save?.Slots[_slot], id, SkillLv(id), _fighter.Cooldowns);

    private int SkillLv(int id)
    {
        if (_save?.Slots[_slot] is not CharaSlot chara) return 0;
        return chara.SkillLv.TryGetValue(id, out int lv) ? lv : 0;
    }

    // One fresh edge for a key (Esc, the MENU arrows).
    private static bool FreshKey(KeyboardState kb, Keys key, ref bool was)
    {
        bool down = kb.IsKeyDown(key);
        bool fresh = down && !was;
        was = down;
        return fresh;
    }

    // One skill, whatever its shape: the binding table only says which skill
    // fired; the conditions stay here (the old per-key branches, id-keyed).
    private void CastSkill(int id)
    {
        switch (id)
        {
            case SkillData.WarLeap:
                // Once per airtime, mid-swing allowed (no attack-lock check).
                if (_mover.Ground == null && _mover.Climb == null && _canLeap && TryPaySkill(id))
                {
                    _mover.Vy = OwnJump();
                    _canLeap = false;
                    PlaySfx("jump");
                    SpawnCastFx(id, _mover.X, _mover.Y, _mover.FacingRight);
                    if (_net != null && _net.Connected) _net.SendSkillCast(id, 0);
                }
                return;
            case SkillData.LeapAttack:
                if (_mover.Ground == null && _mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge()
                    && DiveGround() && TryPaySkill(id))
                {
                    _mover.Vy = DiveV;
                    CastActive(id);
                }
                return;
            case SkillData.IronBody:
            case SkillData.SpiritBlade:
                if (_mover.Climb == null && _fighter.AttackLock <= 0 && TryPaySkill(id)) CastBuff(id);
                return;
            case SkillData.ComboAttack:
                if (_mover.Climb != null || _fighter.AttackLock > 0) return;
                if (_fighter.Buffs.ContainsKey(id)) EndBuff(id);
                else if (TryPaySkill(id)) CastBuff(id);
                return;
            case SkillData.FlashBlade:
            case SkillData.Rush:
                if (_mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge() && TryPaySkill(id))
                {
                    Dash();
                    CastActive(id);
                }
                return;
            default:
                // A bound passive is ignored and a bound buff casts as a
                // buff: only an active skill may strike (the server's
                // SkillStrike rejects zero mob count / range).
                if (!_skills.TryGetValue(id, out var data)) return;
                if (data.Kind == SkillData.KindPassive) return;
                if (data.Kind == SkillData.KindBuff)
                {
                    if (_mover.Climb == null && _fighter.AttackLock <= 0 && TryPaySkill(id)) CastBuff(id);
                    return;
                }
                if (_mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge() && TryPaySkill(id))
                    CastActive(id);
                return;
        }
    }

    // A paid damaging cast: lock the pose, play the effect, judge (local) or
    // send (net). The cast-time pct folds skill level and combo orbs.
    private void CastActive(int id)
    {
        _fighter.AttackLock = _skills[id].CastLockMs;
        PlaySfx(SwingKey()); // v1 skills whoosh like swings (no per-skill sounds yet)
        SpawnCastFx(id, _mover.X, _mover.Y, _mover.FacingRight);
        int pct = CastPct(id);
        if (_net != null && _net.Connected) _net.SendSkillCast(id, pct);
        else if (_localWorld != null)
        {
            foreach (var h in _localWorld.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, Random.Shared, CurrentBuild(), id, pct))
                ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
            DrainDrops(_localWorld);
        }
    }

    // Cast-time damage pct: the skill's level curve plus combo orbs (each orb
    // pays Combo Attack's rate plus Combo Synergy's, both at their levels).
    private int CastPct(int id) =>
        _skillDmg.Pct(id, SkillLv(id), _fighter.Orbs, SkillLv(SkillData.ComboAttack), SkillLv(SkillData.ComboSynergy));

    // A paid buff cast: start the timer (recast refreshes), loop its art,
    // relay the effect, push the new build (buff stats feed damage).
    private void CastBuff(int id)
    {
        _fighter.Buffs[id] = _skills[id].BuffMs;
        PlaySfx("buff");
        StripLoopFx(id); // recasts re-hang one loop, never stack two
        SpawnLoopFx(id);
        SpawnCastFx(id, _mover.X, _mover.Y, _mover.FacingRight);
        if (_net != null && _net.Connected) _net.SendSkillCast(id, 0);
        PushBuild();
    }

    // Buff loops are singletons per buff (cast and end both strip first).
    private void StripLoopFx(int id) => SkillFx.StripLoops(_fx, _world, _fxPool, id);

    // A buff ends (expiry, toggle, session): timer, loop, and orbs all go,
    // and the build drops back (pools clamp next frame, never heal).
    private void EndBuff(int id)
    {
        _fighter.Buffs.Remove(id);
        StripLoopFx(id);
        if (id == SkillData.ComboAttack) _fighter.Orbs = 0;
        PushBuild();
    }

    // Map switches clear one-shot effects but keep buff timers: re-hang loops.
    private void RespawnBuffLoops()
    {
        foreach (int id in _fighter.Buffs.Keys) SpawnLoopFx(id);
    }

    // Buff loop art, riding the player (buffs without art skip silently).
    private void SpawnLoopFx(int id)
    {
        if (_skills[id].Affected.Count == 0) return;
        var loop = TakeFx();
        loop.Skill = id; loop.Loop = true; loop.Frames = _skills[id].Affected; loop.Rider = _mover;
        loop.X = _mover.X; loop.Y = _mover.Y - SkillFx.CastDy; loop.FacingRight = _mover.FacingRight;
    }

    // Rush/Flash Blade carry: shift along facing (staying inside the map),
    // then re-ground below (over a gap the dash ends airborne and physics
    // takes it from there).
    private void Dash()
    {
        _mover.X = Math.Clamp(_mover.X + (_mover.FacingRight ? DashPx : -DashPx), _map.Info.VrLeft, _map.Info.VrRight);
        _mover.Ground = _mover.Ground == null ? null
            : SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)_mover.Y + 2);
        if (_mover.Ground != null) _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
    }

    // Leap Attack's floor check: a platform within range below the feet.
    private bool DiveGround()
    {
        var land = SceneMath.GroundBelow(_map.Footholds, (int)Math.Floor(_mover.X), (int)Math.Ceiling(_mover.Y));
        return land != null && SceneMath.FootholdY(land, (int)_mover.X) - _mover.Y <= LeapRange;
    }

    // Self Recovery pays out on its tick (both pools, capped at buffed max).
    private void PayoutRegen()
    {
        var chara = _save?.Slots[_slot];
        int lv = SkillLv(SkillData.SelfRecovery);
        if (chara == null || lv <= 0) return;
        chara.Hp = Math.Min(chara.Hp + _skills[SkillData.SelfRecovery].RegenHp * lv, MaxHpNow());
        chara.Mp = Math.Min(chara.Mp + _skills[SkillData.SelfRecovery].RegenMp * lv, MaxMpNow());
    }

    // Our own cast or a peer's echo: the effect centers on the torso.
    // Casts without art (Iron Body, Combo Attack) skip silently.
    private void SpawnCastFx(int skill, float x, float y, bool facingRight)
    {
        if (_skills[skill].Effect.Count == 0) return;
        var fx = TakeFx();
        fx.Frames = _skills[skill].Effect;
        fx.X = x; fx.Y = y - SkillFx.CastDy; fx.FacingRight = facingRight;
    }

    // Ctrl is a weapon swing: no weapon in hand means no swing at all.
    // The attack art follows the appearance: cash weapon first, then gear.
    private string AttackAction() => Progression.AttackAction(Hero, _items);

    // Our name + plate, so peers draw our tag (CLI sessions have no chara: skip).
    private void PushName()
    {
        var chara = _save?.Slots[_slot];
        if (chara != null) _net?.SendName(chara.Name, PlateFor(chara.Equips));
    }

    // Our quest state, so the server judges from its own copy (same skip).
    private void PushQuestSync()
    {
        var chara = _save?.Slots[_slot];
        if (chara != null) _net?.SendQuestSync(chara);
    }

    // A kill we landed (net or local): claim the EXP. A level-up pushes our
    // new build and quest mirror to the server (the local judge reads the
    // build fresh every swing; level is the one quest field the server
    // cannot evolve itself).
    private void GainExp(long exp)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions have no one to level
        int ups = Progression.ApplyExp(chara, exp, MaxHpNow());
        if (ups > 0)
        {
            LogEvent($"level up! now Lv.{chara.Level} (STR {chara.Str} DEX {chara.Dex})");
            PlaySfx("levelup");
            PushBuild();
            chara.Hp = MaxHpNow();
            chara.Mp = MaxMpNow();
            PushQuestSync();
        }
    }
}
