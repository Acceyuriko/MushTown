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

    private void DropFx()
    {
        PoolSweep.DropAll(_fx, _world, _fxPool);
        PoolSweep.DropAll(_balls, _world, _ballPool);
    }

    // A ball in the air: one node with a Projectile (the attack-frame delay and flight) and a SkillFx (the looping art
    // that follows it). Our own cast, a peer's cast and the shots all come through here (the local world or the Ball message).
    private void SpawnBall(int skill, float fromX, float fromY, float toX, float toY, int delayMs)
    {
        if (!_ballPool.TryPop(out var fx))
        {
            var o = new GameObject();
            var flight = o.Add(new Projectile());
            fx = o.Add(new SkillFx());
            fx.Flight = flight;
        }
        fx.Reset();
        _world.AddChild(fx.Owner);
        _balls.Add(fx);
        fx.Skill = skill;
        fx.Loop = true;
        UseArt(fx, _wzSkills[skill], "ball");
        fx.FacingRight = toX >= fromX;
        fx.Flight!.Launch(fromX, fromY, toX, toY, delayMs);
    }

    // Plays the frames of one WZ art (hit, ball, special) with the origins and delays the WZ gives it.
    private static List<string> ArtFrames(WzSkillData wz, string art) =>
        art switch { "ball" => wz.BallFrames, "special" => wz.SpecialFrames, _ => wz.HitFrames };

    private static void UseArt(SkillFx fx, WzSkillData wz, string art)
    {
        fx.Frames = ArtFrames(wz, art);
        fx.Origins = wz.Art[art].Origins;
        fx.Delays = wz.Art[art].Delays;
    }

    // The landing special of every ball that arrived this step: one explosion at the end point (also when
    // nothing was there). The damage numbers come from the judge in the same step or later, never before.
    // Only call this outside the tree step (it adds nodes).
    private void SpawnImpacts()
    {
        foreach (var b in _balls)
        {
            if (!b.Flight!.Arrived) continue;
            var spark = TakeFx();
            UseArt(spark, _wzSkills[b.Skill], "special");
            spark.X = b.Flight.ToX; spark.Y = b.Flight.ToY; spark.FacingRight = true;
        }
    }

    // Skill effects: WZ art uses its origin, bare frames stay centered, and both flip like avatars (raw art faces
    // left). Cast effects, hit sparks and landing specials sit where they spawned; only a buff loop keeps following the player.
    private void DrawFx()
    {
        DrawFxList(_balls); // the balls first: the explosion sparks cover them
        DrawFxList(_fx);
    }

    private void DrawFxList(List<SkillFx> list)
    {
        foreach (var f in list)
        {
            if (f.Flight is { Started: false }) continue; // the body action has not reached its attack frame
            DrawFxFrame(f.Frames[f.Frame], f.Origins?[f.Frame], f.X, f.Y, f.FacingRight);
        }
    }

    // One frame of an effect: its WZ origin on the anchor (x, y in the world), or centered when it has none.
    private void DrawFxFrame(string frame, int[]? origin, float x, float y, bool facingRight)
    {
        var tex = _textures[frame];
        var ox = origin == null ? tex.Width / 2f : origin[0];
        if (facingRight) ox = tex.Width - ox;
        var o = new Vector2(ox, origin == null ? tex.Height / 2f : origin[1]);
        _spriteBatch.Draw(tex, new Vector2(x - _camera.X, y - _camera.Y), null, Color.White,
            0f, o, 1f, facingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    // The slow fall's bubble (WZ 2001012 specialAffected, three lists that play together, z -1): drawn behind the actor,
    // before its avatar, with the WZ origin on the feet. It loops while the actor floats.
    private void DrawFloatBubble(FloatBubble b, float feetX, float feetY, bool facingRight)
    {
        if (!b.Active) return;
        foreach (var art in FloatWz.FloatArt("specialAffected"))
        {
            int i = Idle.At(art.Delays, b.Ms);
            DrawFxFrame(art.Frames[i], art.Origins[i], feetX, feetY, facingRight);
        }
    }

    // The end of a slow fall (WZ 2001012 special, three lists that play together) where the actor is, once. It is drawn in
    // front of the avatar like every cast effect: in WZ every special frame has z 0 and the lists have no z of their own,
    // while specialAffected lists have z -1 (behind), which DrawFloatBubble follows. Only call this outside the tree step
    // (it adds nodes).
    private void SpawnFloatEnd(float x, float y, bool facingRight)
    {
        foreach (var art in FloatWz.FloatArt("special"))
        {
            var fx = TakeFx();
            fx.Frames = art.Frames;
            fx.Origins = art.Origins;
            fx.Delays = art.Delays;
            fx.X = x; fx.Y = y; fx.FacingRight = facingRight;
        }
    }

    // The end effects of this step: the hero's float ended by the Jump key or the time limit (a landing is no end), and each
    // peer whose fly pose ended into a jump (the pose is all a peer sends: a landing shows as a stand, walk, prone or ladder).
    private void SpawnFloatEnds()
    {
        if (_bubble.TakeEnded() && _air.EndedBy != FloatEnd.Landed) SpawnFloatEnd(_mover.X, _mover.Y, _mover.FacingRight);
        foreach (var r in _remotes.Values)
            if (r.Bubble.TakeEnded() && r.Action == Net.ActJump) SpawnFloatEnd(r.X, r.Y, r.FacingRight);
    }

    private WzSkillData FloatWz => _wzSkills[WzSkillData.ManaWave].Children.Single(c => c.SkillId == WzSkillData.ManaWaveFloat); // the start check proves it is there

    // Damage numbers: the digits of the number's skin, rising and fading
    // over FloatingNumber.LifeMs. Every skin lays its digits out by their WZ
    // origins (DrawSkinNumbers). A critical number uses the NoCri sets plus the
    // skin's effect3 prefix.
    private void DrawNumbers()
    {
        if (_numbers.Count == 0) return;
        foreach (var n in _numbers)
        {
            if (!n.Visible) continue; // waiting for its turn in the cast
            var dmg = LoadDmg(n.Skin);
            var tint = new Color(255, 255, 255, n.Alpha);
            float baseY = n.Y - n.Rise - _camera.Y;
            if (dmg.Words.FirstOrDefault(w => w.Name == n.Text) is { } word) // the hurt skin has Miss; the base skin has no words
            {
                var wordTex = _textures[word.Image];
                _spriteBatch.Draw(wordTex, new Vector2(n.X - word.Ox - _camera.X, baseY - word.Oy),
                    null, tint, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                continue;
            }
            if (!long.TryParse(n.Text, out long damage)) continue;
            DrawSkinNumbers(dmg.Unit != null ? DmgUnit.Format(damage) : n.Text, dmg, n.X, baseY, tint, n.Crit);
        }
    }

    // The WZ holds two sets of digits per style: NoRed1 (big) and NoRed0 (small) for a normal number, NoCri1 (big) and
    // NoCri0 (small) for a critical one. The first character of a number takes the big set; every later character
    // (digits, the dot and the unit letter count too) takes the small set. The user's finding in the WZ, and the same
    // as the reference pictures of the basic skin with units and of the new video.
    internal static IReadOnlyList<DmgDigit> DigitSet(DmgData dmg, bool crit, int index) =>
        index == 0 ? (crit ? dmg.Cri1 : dmg.Red1) : (crit ? dmg.Cri0 : dmg.Digits);

    // Every skin lays its characters out by DamageNumberLayout (steps, overlap, the critical mark in the first cell).
    // A unit skin's "." and K/M/B come from DmgUnit; a critical number takes the small critical letters (CriImages,
    // NoCustom/NoCri0) and CriDotSpace. The mark is drawn before the digits, so the first digit covers it.
    private void DrawSkinNumbers(string text, DmgData dmg, float centerX, float baseY, Color tint, bool crit)
    {
        var textures = new List<Texture2D>();
        var pics = new List<GlyphPic>();
        var unitImages = crit ? dmg.Unit?.CriImages : dmg.Unit?.Images;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            DmgDigit d;
            GlyphKind kind;
            if (c is >= '0' and <= '9') { d = DigitSet(dmg, crit, i)[c - '0']; kind = GlyphKind.Digit; }
            else if (c == '.' && dmg.Unit != null) { d = unitImages![0]; kind = GlyphKind.Dot; }
            else { d = unitImages![c switch { 'K' => 1, 'M' => 2, _ => 3 }]; kind = GlyphKind.Unit; }
            textures.Add(_textures[d.Image]);
            pics.Add(new GlyphPic(kind, textures[^1].Width, d.Ox, d.Oy));
        }
        var star = crit ? dmg.Effect3 : null;
        var layout = DamageNumberLayout.Place(pics, crit ? dmg.Unit?.CriDotSpace ?? 0 : dmg.Unit?.DotSpace ?? 0,
            star is { } e ? new GlyphPic(GlyphKind.Digit, _textures[e.Image].Width, e.Ox, e.Oy) : null);
        var origin = new Vector2(centerX - layout.Width / 2f - _camera.X, baseY);
        if (layout.Star is { } place)
            _spriteBatch.Draw(_textures[star!.Image], origin + new Vector2(place.Left, place.Top), null, tint, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        for (int i = 0; i < textures.Count; i++)
            _spriteBatch.Draw(textures[i], origin + new Vector2(layout.Places[i].Left, layout.Places[i].Top), null, tint, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
    }

    // One damage skin: ten digits plus their baseline offsets, loaded once
    // per key. Skin 0 keeps the original file name; a new skin is the same
    // file with its key appended.
    private DmgData LoadDmg(int skin)
    {
        if (_dmg.TryGetValue(skin, out var data)) return data;
        string file = skin == FloatingNumber.HurtSkin ? "hurtdmg.json" : skin == 0 ? "dmg.json" : $"dmg{skin}.json";
        data = LoadJson<DmgData>(Path.Combine(_assetsDir, "effect", file));
        if (data.Digits.Count != 10 || data.Red1.Count != 10
            || (skin != FloatingNumber.HurtSkin && (data.Cri0.Count != 10 || data.Cri1.Count != 10)))
            throw new InvalidDataException($"damage skin {skin} lacks a digit set (normal {data.Digits.Count}/{data.Red1.Count}, critical {data.Cri0.Count}/{data.Cri1.Count}, want 10): run `panel damageskin` again");
        foreach (var d in data.Digits.Concat(data.Red1).Concat(data.Cri0).Concat(data.Cri1)) LoadTexture(d.Image);
        foreach (var w in data.Words) LoadTexture(w.Image);
        if (data.Unit != null)
        {
            if (data.Unit.CriImages.Count != 4)
                throw new InvalidDataException($"damage skin {skin} has no critical unit letters, run `panel damageskin` again");
            foreach (var u in data.Unit.Images.Concat(data.Unit.CriImages)) LoadTexture(u.Image);
        }
        if (data.Effect3 != null) LoadTexture(data.Effect3.Image);
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
    // The touch rolls the dodge first (MobTouch.DodgeChance): a dodged touch does no damage, no knockback and
    // no hit pose, but it starts the same invincible window as a hit; the word Miss rises. dodgeRoll (0..1) is the roll; the shots pass one.
    private bool ApplyHurt(long mobAtk, int mobUid, int mobId, double? dodgeRoll = null)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || _death.Dead || _hurt.Invincible) return false; // CLI sessions have no one to hurt, a tombstone cannot be hit, and an invincible player is not hit (so no dodge either)
        var mob = _mobData[mobId];
        if (mobAtk > 0 && (dodgeRoll ?? Random.Shared.NextDouble()) * 100.0
            < MobTouch.DodgeChance(_stats.IntValue(StatKey.DEX), _stats.IntValue(StatKey.LUK), mob.Acc, mob.Level, chara.Level))
        {
            var miss = TakeNumber();
            miss.Text = "Miss"; miss.Skin = FloatingNumber.HurtSkin;
            miss.X = _mover.X; miss.Y = _mover.Y - 90;
            _hurt.Dodge(); // the invincible time of a hit, without its pose
            return false;
        }
        long damage = mobAtk <= 0 ? 0 : DamageChain.TakenDamage(_stats,
            MobTouch.Damage(mobAtk, mob.Level, chara.Level, OwnDef(), _map.Info.BarrierArc, PlayerArc, Random.Shared.NextDouble()));
        long dealt = _hurt.Apply(chara, damage, MagicGuardPct(chara)); // Magic Guard: MP pays its share, HP the rest
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

    // Magic Guard: the share of a touch that MP pays (0 = the character does not have the skill).
    private int MagicGuardPct(CharaSlot chara) =>
        JobSkills.Has(chara.Job, WzSkillData.MagicGuard) ? WzSkillStats.MagicGuardPct(_wzSkills) : 0;

    // The time scale of a swing or cast, read from the panel's attack speed stage (so a buff or a potion that
    // changes the stage needs nothing more).
    private float AttackScale() => (float)StatTables.DelayScale(_stats.Value(StatKey.ATTACK_SPEED));

    // The hit pose loops the two alert frames of the WZ invincible action
    // (frame 1 for 90ms, frame 0 for 360ms), counted in game frames: 5 + 22.
    private int HurtFrame() => _hurt.ElapsedFrames % 27 < 5 ? 1 : 0;

    // Purple on the first flash step, normal on the next, for the whole hurt state.
    private Color? HurtTint =>
        _hurt.HitShown && (_hurt.ElapsedFrames / HurtGuard.FlashFrames & 1) == 0 ? HurtPurple : null;

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

    // The same for a WZ skill: it stands at its max level when the character's job has it.
    private bool TryPayWzSkill(WzSkillData wz)
    {
        var chara = _save?.Slots[_slot];
        int lv = chara != null && JobSkills.Has(chara.Job, wz.SkillId) ? wz.MaxLevel : 0;
        return _skillCost.Pay(chara, wz, lv, _fighter.Cooldowns);
    }

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
            case WzSkillData.Teleport:
                CastTeleport(_wzSkills[id]); // the only skill that may fire during a swing or a cast, and on a ladder or rope (the user's rule)
                return;
            default:
                if (_wzSkills.TryGetValue(id, out var wz))
                {
                    // Only a damage skill casts so far; a passive and the move Mana Wave are ignored.
                    if (wz.Kind == SkillData.KindActive && wz.Common.ContainsKey("damage") && _mover.Climb == null
                        && _fighter.AttackLock <= 0 && HasJudge() && TryPayWzSkill(wz))
                        CastWzActive(wz);
                    return;
                }
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

    // Teleport: the direction keys of this frame decide (_upNow/_downNow/_dirNow: a key pressed together with the
    // Teleport key in one frame counts; the macro cast reads the same fields). Up or Down is the vertical jump (the y
    // field, Up wins over Down, and over Left/Right), Left or Right the horizontal one (the x field). No direction key
    // held: nothing happens (no cast, no MP, no cooldown, no effect). The landing is TeleportRules' (wall first, then the
    // platform); a refused landing costs no MP. It works in the middle of a swing or another cast (the attack lock only
    // holds the pose) and on a ladder or rope: the landing is the same, and the feet leave the ladder (PlayerMover.Land).
    // The feet move at once and the pose stays as it was (WZ has no action node). The cooldown (teleportCooltime, 480 ms) is checked by TryPayWzSkill: while it runs nothing moves,
    // nothing is paid and no effect plays. The server learns the new place first, so the others see the effect where we land.
    private void CastTeleport(WzSkillData wz)
    {
        var land = TeleportRules.ForKeys(_map, _mover.X, _mover.Y, _upNow, _downNow, _dirNow, (int)wz.Value("x", wz.MaxLevel), (int)wz.Value("y", wz.MaxLevel));
        if (land is not { } to || !TryPayWzSkill(wz)) return;
        if (_mover.Climb != null && !_upNow && !_downNow) _mover.FacingRight = _dirNow > 0; // the facing does not follow the keys on a ladder: turn to the cast
        _mover.Land(to.X, to.Y, to.Ground);
        SpawnCastFx(wz.SkillId, _mover.X, _mover.Y, _mover.FacingRight);
        if (_net == null || !_net.Connected) return;
        _net.SendState(_mapId, _mover.X, _mover.Y, Net.ActionCode(_mover.Action), (byte)_frame, _mover.FacingRight);
        _net.SendSkillCast(wz.SkillId, 0);
    }

    // Mana Wave (WZ 2001011) has no key of its own: Jump pressed with Up held boosts (mpCon MP), on the ground (instead of
    // the plain jump) or in the air; Jump pressed again in the air without Up slows the fall for as long as it is held, up
    // to time seconds (the child 2001012, mpCon MP, paid once when it starts). Each once per airtime (AirMovement). Like the
    // other skills (but Teleport) it does not fire during a swing or on a ladder.
    // The poses: the boost plays WZ manaWave (alert frame 2 for 30 ms, then the jump pose) and the slow fall WZ manaWaveFloat
    // (fly frame 0), see PoseFrame. The slow fall has no message of its own: its bubble (specialAffected) and its end
    // (special) are drawn by every client from the State messages, the bubble while the action is fly and the end when it
    // turns into a jump. The boost effect goes by SkillCast.
    // Unverified (the WZ gives no unit): y 1200 is taken as the take-off speed in px per second (a peak of y^2 / 2 / gravity
    // = 400 px, the plain jump is 75); the child's u 25 as how fast the fall speeds up (px per second squared) and v 95 as
    // the fastest fall (px per second); time 5 is seconds. w 180 is not used.
    private void UpdateManaWave(bool freshAlt, bool up)
    {
        if (!freshAlt || _mover.Climb != null || _fighter.AttackLock > 0 || !_wzSkills.TryGetValue(WzSkillData.ManaWave, out var wave)) return;
        if (up)
        {
            if (!_air.CanBoost || !TryPayWzSkill(wave)) return;
            _air.Boost((float)wave.Value("y", wave.MaxLevel));
            PlaySfx("jump");
            SpawnCastFx(wave.SkillId, _mover.X, _mover.Y, _mover.FacingRight);
            if (_net != null && _net.Connected) _net.SendSkillCast(wave.SkillId, 0);
            return;
        }
        var chara = _save?.Slots[_slot];
        if (!_air.CanFloat || chara == null || !JobSkills.Has(chara.Job, wave.SkillId)) return;
        var hold = FloatWz;
        int lv = wave.MaxLevel; // the skill stands at its max level; the child has no level of its own to learn
        if (!_skillCost.Pay(chara, hold, lv, _fighter.Cooldowns)) return;
        _air.Float((float)hold.Value("time", lv), (float)hold.Value("v", lv), (float)hold.Value("u", lv));
    }

    // A paid damaging cast: lock the pose, play the effect, judge (local) or
    // send (net). The cast-time pct folds skill level and combo orbs.
    private void CastActive(int id)
    {
        RollSwing(); // the cast poses with a swing too
        _fighter.StartAttack(_skills[id].CastLockMs, AttackScale());
        PlaySfx(SwingKey()); // v1 skills whoosh like swings (no per-skill sounds yet)
        SpawnCastFx(id, _mover.X, _mover.Y, _mover.FacingRight);
        int pct = CastPct(id);
        if (_net != null && _net.Connected) _net.SendSkillCast(id, pct);
        else if (_localWorld != null)
        {
            foreach (var h in _localWorld.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, Random.Shared, CurrentBuild(), id, pct))
                ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
            DrainDrops(_localWorld);
        }
    }

    // A paid WZ damage cast: the pose is a weapon swing and its lock is that swing's time x the attack speed
    // scale, like a basic swing. The cast-time pct is the skill's damage at its level. The judge is the
    // local world or the server (rectangle and segments, MobWorld.WzStrike). A skill with a ball judges when the
    // ball arrives: the local world hands the ball out here and the hits later through its tick; the server sends
    // the ball to everyone (Net.Ball) and the hits later as MobHit.
    private void CastWzActive(WzSkillData wz)
    {
        string action = RollSwing();
        _fighter.StartAttack(Delays(action, Hero?.Skin ?? 0).Sum(), AttackScale());
        PlaySfx(SwingKey());
        SpawnCastFx(wz.SkillId, _mover.X, _mover.Y, _mover.FacingRight);
        int pct = (int)wz.Value("damage", wz.MaxLevel);
        if (_net != null && _net.Connected) _net.SendSkillCast(wz.SkillId, pct);
        else if (_localWorld != null)
        {
            foreach (var h in _localWorld.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, Random.Shared, CurrentBuild(), wz.SkillId, pct))
                ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill, h.Crit, h.Settle, h.MpPct);
            foreach (var l in _localWorld.TakeLaunches()) SpawnBall(l.Skill, l.FromX, l.FromY, l.ToX, l.ToY, l.DelayMs);
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

    // Our own cast or a peer's echo: a WZ effect anchors its WZ origin at the caster's feet; an old bare effect
    // keeps the centered torso rule. Casts without art (Iron Body, Combo Attack) skip silently.
    private void SpawnCastFx(int skill, float x, float y, bool facingRight)
    {
        List<string> frames;
        int[][]? origins = null;
        int[]? delays = null;
        if (_wzSkills.TryGetValue(skill, out var wz))
        {
            frames = wz.CastFrames;
            if (wz.Art.TryGetValue("effect", out var art))
            {
                origins = art.Origins;
                delays = art.Delays;
            }
        }
        else frames = _skills[skill].Effect;
        if (frames.Count == 0) return;
        var fx = TakeFx();
        fx.Frames = frames;
        fx.Origins = origins;
        fx.Delays = delays;
        fx.X = x; fx.Y = origins == null ? y - SkillFx.CastDy : y; fx.FacingRight = facingRight;
    }

    // The hero's frame as the WZ poses of Mana Wave show it: the boost is alert frame 2 (in the air alert is only that pose) and the
    // slow fall is fly frame 0, held. Every other pose keeps its own frame. Peers get this frame in the State message.
    private int PoseFrame(string action, int frame) =>
        action == "fly" ? 0 : action == "alert" && _mover.Ground == null ? 2 : frame;

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
