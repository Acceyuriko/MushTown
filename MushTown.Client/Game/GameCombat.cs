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
    private static readonly Color HurtPurple = new(200, 130, 255);

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
    // over FloatingNumber.LifeMs. Skin 0 and the hurt skin keep the original
    // left-to-right layout; collected skins use their origins and spacing.
    private void DrawNumbers()
    {
        if (_numbers.Count == 0) return;
        foreach (var n in _numbers)
        {
            var dmg = LoadDmg(n.Skin);
            float k = n.Age / FloatingNumber.LifeMs;
            int alpha = k < 0.7f ? 255 : (int)(255 * (1 - (k - 0.7f) / 0.3f));
            var tint = new Color(255, 255, 255, alpha);
            float baseY = n.Y - FloatingNumber.RisePx * k - _camera.Y;
            if (n.Skin <= 0)
            {
                DrawBasicNumbers(n.Text, dmg, n.X, baseY, tint);
                continue;
            }
            if (dmg.Words.FirstOrDefault(w => w.Name == n.Text) is { } word)
            {
                var wordTex = _textures[word.Image];
                _spriteBatch.Draw(wordTex, new Vector2(n.X - word.Ox - _camera.X, baseY - word.Oy),
                    null, tint, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                continue;
            }
            if (!long.TryParse(n.Text, out long damage)) continue;
            DrawSkinNumbers(dmg.Unit != null ? DmgUnit.Format(damage) : n.Text, dmg, n.X, baseY, tint);
        }
    }

    // The original base-skin layout: pack the raw picture widths from the
    // left edge; Ox stays 0 in dmg.json, so it would shift these digits.
    private void DrawBasicNumbers(string text, DmgData dmg, float centerX, float baseY, Color tint)
    {
        int totalW = 0;
        foreach (char c in text) totalW += _textures[dmg.Digits[c - '0'].Image].Width;
        float x = centerX - totalW / 2f - _camera.X;
        foreach (char c in text)
        {
            var d = dmg.Digits[c - '0'];
            var tex = _textures[d.Image];
            _spriteBatch.Draw(tex, new Vector2(x, baseY - d.Oy), null, tint,
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            x += tex.Width;
        }
    }

    // Collected skins: each picture's origin lands in the middle of its
    // advance box (left = pen + width / 2 - Ox); NumberSpace widens digits.
    // A unit skin's "." and K/M/B come from DmgUnit: DotSpace after the dot,
    // and the unit letter follows its number with no extra gap, bottom-aligned
    // with the digits.
    private void DrawSkinNumbers(string text, DmgData dmg, float centerX, float baseY, Color tint)
    {
        var glyphs = new List<(Texture2D Tex, int Ox, int Oy, int SpaceBefore, int SpaceAfter)>();
        foreach (char c in text)
        {
            if (c is >= '0' and <= '9')
            {
                var d = dmg.Digits[c - '0'];
                glyphs.Add((_textures[d.Image], d.Ox, d.Oy, 0, dmg.NumberSpace));
            }
            else if (c == '.' && dmg.Unit != null)
            {
                var d = dmg.Unit.Images[0];
                glyphs.Add((_textures[d.Image], d.Ox, d.Oy, 0, dmg.Unit.DotSpace));
            }
            else if (dmg.Unit != null)
            {
                int unit = c switch { 'K' => 1, 'M' => 2, 'B' => 3, _ => -1 };
                if (unit < 0) continue;
                var d = dmg.Unit.Images[unit];
                var tex = _textures[d.Image];
                // The letter's bottom edge sits on the digits' bottom edge.
                var digit = dmg.Digits[0];
                int oy = tex.Height - (_textures[digit.Image].Height - digit.Oy);
                glyphs.Add((tex, d.Ox, oy, 0, 0));
            }
        }
        float totalW = 0;
        for (int i = 0; i < glyphs.Count; i++)
            totalW += glyphs[i].SpaceBefore + glyphs[i].Tex.Width
                + (i < glyphs.Count - 1 ? glyphs[i].SpaceAfter : 0);
        float x = centerX - totalW / 2f - _camera.X;
        foreach (var g in glyphs)
        {
            x += g.SpaceBefore;
            _spriteBatch.Draw(g.Tex, new Vector2(x + g.Tex.Width / 2f - g.Ox, baseY - g.Oy),
                null, tint, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            x += g.Tex.Width + g.SpaceAfter;
        }
    }

    // One damage skin: ten digits plus their baseline offsets, loaded once
    // per key. Skin 0 keeps the original file name; a new skin is the same
    // file with its key appended.
    private DmgData LoadDmg(int skin)
    {
        if (_dmg.TryGetValue(skin, out var data)) return data;
        string file = skin == FloatingNumber.HurtSkin ? "hurtdmg.json" : skin == 0 ? "dmg.json" : $"dmg{skin}.json";
        data = LoadJson<DmgData>(Path.Combine(_assetsDir, "effect", file));
        if (data.Digits.Count != 10)
            throw new InvalidDataException($"damage skin {skin} has {data.Digits.Count} digits, want 10");
        foreach (var d in data.Digits) LoadTexture(d.Image);
        foreach (var w in data.Words) LoadTexture(w.Image);
        if (data.Unit != null)
            foreach (var u in data.Unit.Images) LoadTexture(u.Image);
        if (data.Sample != null) LoadTexture(data.Sample.Image);
        _dmg[skin] = data;
        return data;
    }

    // The coupon tooltip's preview picture; a skin without one has no preview.
    internal Texture2D? DamageSample(int skin)
    {
        return LoadDmg(skin).Sample is { } sample ? _textures[sample.Image] : null;
    }

    // The player's Arcane Force. The ARC system (T-20261114000523) is not made, so this is 0 for now
    // (so a map with a barrierArc is the hardest tier, x2.8); read the real value here when it exists.
    private static int PlayerArc => 0;

    // One touch that landed on us: the mob's attack goes through DEF (MobTouch) and the damage-taken
    // zone, the number shows what got through, death respawns at the map spawn with full pools (buffs
    // stay). Returns true on death: the caller drops the rest of the frame's touches (judged at the
    // corpse, not the spawn). A Touch-0 mob is harmless.
    private bool ApplyHurt(long mobAtk, int mobUid, int mobId)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || _death.Dead) return false; // CLI sessions have no one to hurt, and a tombstone cannot be hit
        long damage = mobAtk <= 0 ? 0 : DamageChain.TakenDamage(_stats,
            MobTouch.Damage(mobAtk, _mobData[mobId].Level, chara.Level, OwnDef(), _map.Info.BarrierArc, PlayerArc, Random.Shared.NextDouble()));
        long dealt = _hurt.Apply(chara, damage);
        if (dealt == 0) return false; // invincible or harmless touch: no number, no death
        var num = TakeNumber();
        num.Text = dealt.ToString(); num.Skin = FloatingNumber.HurtSkin;
        num.X = _mover.X; num.Y = _mover.Y - 90;
        if (chara.Hp == 0)
        {
            Die();
            return true;
        }
        if (_mobs.TryGetValue(mobUid, out var source)
            && HurtGuard.KnocksBack(100 - OwnKnockbackResistance(), Random.Shared.Next(100)))
            _mover.Knock(HurtGuard.KnockDir(_mover.X, source.X));
        return false;
    }

    // The hit pose loops the two alert frames of the WZ invincible action
    // (frame 1 for 90ms, frame 0 for 360ms), counted in game frames: 5 + 22.
    private int HurtFrame() => _hurt.ElapsedFrames % 27 < 5 ? 1 : 0;

    // Purple on the first flash step, normal on the next, for the whole hurt state.
    private Color? HurtTint =>
        _hurt.Invincible && (_hurt.ElapsedFrames / HurtGuard.FlashFrames & 1) == 0 ? HurtPurple : null;

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

    // Every skill of the character's own job stands at its max level (no skill points); a skill of
    // another job is not learned. CLI sessions have no character.
    private int SkillLv(int id) => _save?.Slots[_slot] is { } chara ? JobSkills.Level(chara.Job, _skills[id]) : 0;

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
    private void CastSkill(int id, bool fresh)
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
                if (_fighter.Buffs.ContainsKey(id)) return;
                if (_mover.Climb == null && _fighter.AttackLock <= 0 && TryPaySkill(id)) CastBuff(id);
                return;
            case SkillData.ComboAttack:
                if (_mover.Climb != null || _fighter.AttackLock > 0) return;
                if (_fighter.Buffs.ContainsKey(id))
                {
                    if (fresh) EndBuff(id);
                }
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
                    if (_fighter.Buffs.ContainsKey(id)) return;
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
        RollSwing(); // the cast poses with a swing too
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
    // The swing is rolled from the pool of the appearance: cash weapon first, then gear.
    private string RollSwing() => _swing.Roll(Progression.SwingPool(Hero, _items));

    // Our look, so peers draw our skin, hair, face and worn pieces (CLI
    // sessions have no chara: skip).
    private void PushAppearance()
    {
        var chara = _save?.Slots[_slot];
        if (chara != null) _net?.SendAppearance(chara);
    }

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
        int ups = Progression.ApplyExp(chara, exp, MaxHpNow(), MaxMpNow());
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
