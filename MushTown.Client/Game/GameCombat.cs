using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// Combat: swings, skills, casts, hurt/death, and the hit feedback pools.
public partial class MushTownGame
{
    private const float NumberMs = 800; // damage numbers rise and fade over this long
    private const int NumberRise = 60; // px a damage number floats up
    private const int FxDelay = 100; // skill effect frames share one delay (WZ ships no delays for them)
    private const int FxCastDy = 40; // cast effects center on the torso (bare frames: no WZ origins)
    private const int DashPx = 220; // Rush/Flash Blade carry the caster this far along facing
    private const float DiveV = 900f; // Leap Attack slams down this fast (past MaxFall on purpose)
    private const int LeapRange = 400; // Leap Attack needs a platform within this far below

    private sealed class DamageNumber
    {
        public string Text = "";
        public float X; // head position when the hit landed
        public float Y;
        public float Age;
    }

    // One skill effect in flight: a cast effect (centered on the caster), a
    // hit spark (centered on the mob's body), or a buff loop (riding the
    // player until the buff ends). One-shots play once, then are removed.
    private sealed class SkillFx
    {
        public int Skill;
        public bool Hit;
        public bool Loop;
        public float X;
        public float Y;
        public bool FacingRight;
        public int Frame;
        public float Timer;
    }

    // Pooled spawn: pop or make, reset the fields the spawn site does not
    // set (sites always set Text/X/Y and Skill/X/Y/FacingRight; spark and
    // loop sites add Hit/Loop).
    private DamageNumber TakeNumber()
    {
        var n = _numberPool.TryPop(out var p) ? p : new DamageNumber();
        n.Age = 0;
        return n;
    }

    private SkillFx TakeFx()
    {
        var f = _fxPool.TryPop(out var p) ? p : new SkillFx();
        f.Hit = false;
        f.Loop = false;
        f.Frame = 0;
        f.Timer = 0;
        return f;
    }

    private void DropNumbers()
    {
        foreach (var n in _numbers) _numberPool.Push(n);
        _numbers.Clear();
    }

    private void DropFx()
    {
        foreach (var f in _fx) _fxPool.Push(f);
        _fx.Clear();
    }

    // Skill effects: bare frames centered on their anchor, flipped like
    // avatars (raw art faces left). Cast effects ride the caster, hit sparks
    // the mob's body, buff loops the player; all were centered when spawned.
    private void DrawFx()
    {
        foreach (var f in _fx)
        {
            var frames = f.Loop ? _skills[f.Skill].Affected : f.Hit ? _skills[f.Skill].Hit : _skills[f.Skill].Effect;
            var tex = _textures[frames[f.Frame]];
            var o = new Vector2(tex.Width / 2f, tex.Height / 2f);
            _spriteBatch.Draw(tex, new Vector2(f.X - _camera.X, f.Y - _camera.Y), null, Color.White,
                0f, o, 1f, f.FacingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }

    // Damage numbers: basic skin digits, rising and fading over NumberMs.
    private void DrawNumbers()
    {
        if (_numbers.Count == 0) return;
        var dmg = LoadDmg();
        foreach (var n in _numbers)
        {
            float k = n.Age / NumberMs;
            int alpha = k < 0.7f ? 255 : (int)(255 * (1 - (k - 0.7f) / 0.3f));
            var tint = new Color(255, 255, 255, alpha);
            int totalW = 0;
            foreach (char c in n.Text) totalW += _textures[dmg.Digits[c - '0'].Image].Width;
            float x = n.X - totalW / 2f - _camera.X;
            float baseY = n.Y - NumberRise * k - _camera.Y;
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

    // Basic damage skin: ten digits plus their baseline offsets, loaded once.
    private DmgData LoadDmg()
    {
        if (_dmg == null)
        {
            var data = LoadJson<DmgData>(Path.Combine(_assetsDir, "effect", "dmg.json"));
            if (data.Digits.Count != 10)
                throw new InvalidDataException($"damage skin has {data.Digits.Count} digits, want 10");
            foreach (var d in data.Digits) LoadTexture(d.Image);
            _dmg = data;
        }
        return _dmg;
    }

    // One touch that landed on us: DEF folds in, the number shows what got
    // through, death respawns at the map spawn with full pools (buffs stay).
    // Returns true on death: the caller drops the rest of the frame's touches
    // (judged at the corpse, not the spawn).
    private bool ApplyHurt(int damage)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false; // CLI sessions have no one to hurt
        int dealt = Progression.ApplyHurt(chara, damage, OwnDef());
        if (dealt == 0) return false; // harmless touch: no number, no death
        var num = TakeNumber();
        num.Text = dealt.ToString(); num.X = _mover.X; num.Y = _mover.Y - 90;
        _numbers.Add(num);
        if (chara.Hp != 0) return false;
        Die(chara);
        return true;
    }

    // Death: full pools, feet back on the spawn ground, motion cleared.
    private void Die(CharaSlot chara)
    {
        chara.Hp = Progression.BuffedMaxHp(chara.Level, OwnHpPct());
        chara.Mp = Progression.MaxMp(chara.Level);
        _mover.X = _map.Info.SpawnX;
        _mover.Ground = SceneMath.GroundBelow(_map.Footholds, _map.Info.SpawnX, _map.Info.SpawnY)
            ?? throw new InvalidDataException($"map {_mapId} has no ground under spawn");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, _map.Info.SpawnX);
        ResetMotion();
        SnapCamera();
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0; // death teleports: no reload on the same map, so end talk here
        LogEvent("you died");
    }

    // DEF% from every source: worn gear, Iron Body while live, Guardian
    // Armor and Endure always. Mob touches are the only thing it blocks.
    private int OwnDef() =>
        ItemData.EquipBonus(_save?.Slots[_slot]?.Equips, _items).def
        + (_fighter.Buffs.ContainsKey(SkillData.IronBody) ? _skills[SkillData.IronBody].DefPct * SkillLv(SkillData.IronBody) : 0)
        + _skills[SkillData.GuardianArmor].DefPct * SkillLv(SkillData.GuardianArmor)
        + _skills[SkillData.Endure].DefPct * SkillLv(SkillData.Endure);

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

    // One fresh edge per skill key (twelve now: a helper, not twelve blocks).
    private static bool FreshKey(KeyboardState kb, Keys key, ref bool was)
    {
        bool down = kb.IsKeyDown(key);
        bool fresh = down && !was;
        was = down;
        return fresh;
    }

    // Menu trips don't track game keys: seed every game edge from live state.
    private void SeedGameKeys(KeyboardState kb)
    {
        _altWasDown = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        _upWasDown = kb.IsKeyDown(Keys.Up);
        _ctrlWasDown = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        _zWasDown = kb.IsKeyDown(Keys.Z);
        _qWasDown = kb.IsKeyDown(Keys.Q);
        _wWasDown = kb.IsKeyDown(Keys.W);
        _eWasDown = kb.IsKeyDown(Keys.E);
        _rWasDown = kb.IsKeyDown(Keys.R);
        _tWasDown = kb.IsKeyDown(Keys.T);
        _yWasDown = kb.IsKeyDown(Keys.Y);
        _uWasDown = kb.IsKeyDown(Keys.U);
        _fWasDown = kb.IsKeyDown(Keys.F);
        _gWasDown = kb.IsKeyDown(Keys.G);
        _hWasDown = kb.IsKeyDown(Keys.H);
        _jWasDown = kb.IsKeyDown(Keys.J);
        _lWasDown = kb.IsKeyDown(Keys.L);
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
            foreach (var h in _localWorld.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, Random.Shared, OwnBuild(), id, pct))
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
    private void StripLoopFx(int id)
    {
        for (int i = _fx.Count - 1; i >= 0; i--)
            if (_fx[i].Loop && _fx[i].Skill == id)
            {
                _fxPool.Push(_fx[i]);
                _fx.RemoveAt(i);
            }
    }

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
        loop.Skill = id; loop.Loop = true; loop.X = _mover.X; loop.Y = _mover.Y - FxCastDy; loop.FacingRight = _mover.FacingRight;
        _fx.Add(loop);
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
        chara.Hp = Math.Min(chara.Hp + _skills[SkillData.SelfRecovery].RegenHp * lv,
            Progression.BuffedMaxHp(chara.Level, OwnHpPct()));
        chara.Mp = Math.Min(chara.Mp + _skills[SkillData.SelfRecovery].RegenMp * lv,
            Progression.MaxMp(chara.Level));
    }

    // Our own cast or a peer's echo: the effect centers on the torso.
    // Casts without art (Iron Body, Combo Attack) skip silently.
    private void SpawnCastFx(int skill, float x, float y, bool facingRight)
    {
        if (_skills[skill].Effect.Count == 0) return;
        var fx = TakeFx();
        fx.Skill = skill; fx.X = x; fx.Y = y - FxCastDy; fx.FacingRight = facingRight;
        _fx.Add(fx);
    }

    // Our combat stats for the server (Build) and the local judge. CLI
    // sessions have no character, so they swing as beginners. Passives and
    // worn gear fold in by level; Spirit Blade multiplies the weapon live.
    private Build OwnBuild()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return Progression.Beginner;
        var worn = ItemData.EquipBonus(chara.Equips, _items);
        int str = chara.Str + worn.str
            + _skills[SkillData.PhysicalTraining].Str * SkillLv(SkillData.PhysicalTraining)
            + _skills[SkillData.AgileArms].Str * SkillLv(SkillData.AgileArms);
        int dex = chara.Dex + worn.dex
            + _skills[SkillData.PhysicalTraining].Dex * SkillLv(SkillData.PhysicalTraining);
        int atk = (worn.weapon > 0 ? worn.weapon : Progression.WeaponAtk)
            + _skills[SkillData.GuardianArmor].Atk * SkillLv(SkillData.GuardianArmor);
        if (_fighter.Buffs.ContainsKey(SkillData.SpiritBlade))
            atk = atk * (100 + _skills[SkillData.SpiritBlade].AtkPct * SkillLv(SkillData.SpiritBlade)) / 100;
        int dmg = _skills[SkillData.WeaponMastery].DmgPct * SkillLv(SkillData.WeaponMastery);
        int crit = _skills[SkillData.WeaponMastery].CritPct * SkillLv(SkillData.WeaponMastery)
            + _skills[SkillData.ChanceAttack].CritPct * SkillLv(SkillData.ChanceAttack);
        int final = _skills[SkillData.FinalAttack].ProcPct * SkillLv(SkillData.FinalAttack);
        return new Build(str, dex, atk, dmg, crit, final);
    }

    // +MaxHP% from every source: Iron Body while live, Warrior Mastery and
    // Endure always. Pools, potions, and refills all cap at the buffed max.
    private int OwnHpPct() =>
        (_fighter.Buffs.ContainsKey(SkillData.IronBody) ? _skills[SkillData.IronBody].HpPct * SkillLv(SkillData.IronBody) : 0)
        + _skills[SkillData.WarriorMastery].HpPct * SkillLv(SkillData.WarriorMastery)
        + _skills[SkillData.Endure].HpPct * SkillLv(SkillData.Endure);

    // Warrior Mastery speeds run and jump together (one column, both axes).
    private float OwnWalk() =>
        PlayerMover.WalkSpeed * (100 + _skills[SkillData.WarriorMastery].MovePct * SkillLv(SkillData.WarriorMastery)) / 100;
    private float OwnJump() =>
        PlayerMover.JumpV0 * (100 + _skills[SkillData.WarriorMastery].MovePct * SkillLv(SkillData.WarriorMastery)) / 100;

    // Builds change on level-up, learn, and buff edges: push them all.
    private void PushBuild() => _net?.SendBuild(OwnBuild());

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
    private void GainExp(int exp)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions have no one to level
        int ups = Progression.ApplyExp(chara, exp, OwnHpPct());
        if (ups > 0)
        {
            LogEvent($"level up! now Lv.{chara.Level} (STR {chara.Str} DEX {chara.Dex})");
            PlaySfx("levelup");
            PushBuild();
            PushQuestSync();
        }
    }
}
