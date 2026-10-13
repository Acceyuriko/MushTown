using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// W9 bottom bar + item/stat panels. Art is official (StatusBar3, UIInventory,
// UICharacterInfo); all text baked. Plate/slot positions are WZ vectors;
// the rest is tuned rects. HP/MP numbers and EXP are live (W10 leveling);
// nothing drains HP until W13 mob hits, so the bars always draw full.
public partial class MushTownGame
{
    // Must match Export.cs ExportHud: every name here must be exported, or LoadTexture throws.
    private static readonly string[] HudFiles = new[]
    {
        "hud_plate.png", "hud_lvtag.png",
        "hud_hpbg.png", "hud_hpcover.png", "hud_hpfill.png", "hud_mpfill.png",
        "hud_expback.png", "hud_expcover.png", "hud_expgauge.png",
        "hud_btn_char_n.png", "hud_btn_char_h.png", "hud_btn_char_p.png",
        "hud_btn_menu_n.png", "hud_btn_menu_h.png", "hud_btn_menu_p.png",
        "hud_sub_char_n.png", "hud_sub_char_h.png", "hud_sub_char_p.png",
        "hud_sub_item_n.png", "hud_sub_item_h.png", "hud_sub_item_p.png",
        "hud_sub_bg0.png", "hud_sub_bg1.png", "hud_sub_bg2.png",
        "hud_qslot.png", "hud_qslotcover.png",
        "hud_key_ctrl.png", "hud_key_alt.png", "hud_key_i.png", "hud_key_c.png",
        "hud_inv.png",
        "hud_itab_s0.png", "hud_itab_s1.png", "hud_itab_s2.png",
        "hud_itab_s3.png", "hud_itab_s4.png", "hud_itab_s5.png",
        "hud_itab_n0.png", "hud_itab_n1.png", "hud_itab_n2.png",
        "hud_itab_n3.png", "hud_itab_n4.png", "hud_itab_n5.png",
        "hud_coin.png", "hud_stat.png", "hud_stathilong.png", "hud_statx.png",
    };
    private const int TabCount = 6;
    private const int SkillW = 320; // skill window: tabs + SP line + 8 rows
    private const int SkillRowH = 52;
    private const int SkillRowsY = 48; // rows start below the tab strip + SP line
    private const int SkillBodyH = SkillRowsY + 8 * SkillRowH + 8; // strip + SP + rows + bottom pad

    // USE tab slots, fixed order: red potion, blue potion (starters and W13 drops share them).
    private static readonly int[] UseIds = new[] { ItemData.RedPotion, ItemData.BluePotion };
    private readonly Dictionary<int, ItemData> _items = new();

    // Skill window tabs (one per job) + rows: skill id + fixed cast key
    // (null = passive, no key; rebinds wait for the settings week).
    private static readonly string[] SkillTabNames = { "Warrior", "Fighter", "Crusader" };
    private static readonly (int id, string? key)[][] SkillTabs = new[]
    {
        new (int id, string? key)[]
        {
            (SkillData.SlashBlast, "Q"), (SkillData.IronBody, "R"), (SkillData.WarLeap, "W"),
            (SkillData.WarriorMastery, null), (SkillData.LeapAttack, "E"), (SkillData.GuardianArmor, null),
        },
        new (int id, string? key)[]
        {
            (SkillData.Brandish, "T"), (SkillData.WeaponMastery, null), (SkillData.SpiritBlade, "U"),
            (SkillData.ComboAttack, "F"), (SkillData.FlashBlade, "Y"), (SkillData.AgileArms, null),
            (SkillData.FinalAttack, null), (SkillData.PhysicalTraining, null),
        },
        new (int id, string? key)[]
        {
            (SkillData.IntrepidSlash, "G"), (SkillData.BeamBlade, "H"), (SkillData.Rush, "J"),
            (SkillData.ScarringSword, "L"), (SkillData.ComboSynergy, null), (SkillData.SelfRecovery, null),
            (SkillData.ChanceAttack, null), (SkillData.Endure, null),
        },
    };
    // One-line passive blurbs (per skill level): the window's only help text.
    private static readonly Dictionary<int, string> PassiveBlurb = new()
    {
        [SkillData.WarriorMastery] = "+HP/speed",
        [SkillData.GuardianArmor] = "+attack/DEF",
        [SkillData.WeaponMastery] = "+damage/crit",
        [SkillData.AgileArms] = "+STR",
        [SkillData.FinalAttack] = "bonus hit",
        [SkillData.PhysicalTraining] = "+STR/DEX",
        [SkillData.ComboSynergy] = "+orb gain/dmg",
        [SkillData.SelfRecovery] = "HP/MP regen",
        [SkillData.ChanceAttack] = "+crit",
        [SkillData.Endure] = "+Max HP/DEF",
    };
    private readonly Dictionary<int, SkillData> _skills = new();
    private int _skillTab; // 0 Warrior, 1 Fighter, 2 Crusader
    private bool _showInv, _showStat, _showSubmenu, _showSkill;
    private int _invTab; // 0 Equip 1 Use 2 Etc 3 Set-up 4 Cash 5 Dec
    private bool _iWasDown, _cWasDown, _kWasDown;

    private static Rectangle PlateRect() => new(Layout.Plate.X, Layout.Plate.Y, 250, 35);
    private static Rectangle HpRect() => new(Layout.HpMp.X, Layout.HpMp.Y, 205, 46); // cover bottom flush with EXP top
    private static Rectangle MenuBtnRect() => new(Layout.MenuBtn.X, Layout.MenuBtn.Y, 34, 37);
    private static Rectangle CharBtnRect() => new(Layout.CharBtn.X, Layout.CharBtn.Y, 34, 37);
    private static Rectangle QslotRect() => new(Layout.Qslot.X, Layout.Qslot.Y, 557, 67);
    private static Rectangle ExpRect() => new(0, 758, 1366, 10);
    private Point _invPos = new(Layout.Inv.X, Layout.Inv.Y), _statPos = new(Layout.Stat.X, Layout.Stat.Y);
    private Point _skillPos = new(Layout.Skill.X, Layout.Skill.Y);
    private Point? _dragInv, _dragStat, _dragSkill; // title grab offsets while held
    private int _topPanel = 1; // the front panel: 0 = inventory, 1 = stat, 2 = skill
    private Texture2D _statBack = null!; // navy body backing, corners rounded to match the art
    private Texture2D _skillBack = null!; // same navy, skill window size

    private Rectangle InvRect() => new(_invPos.X, _invPos.Y, 242, 593);
    private Rectangle StatRect() => new(_statPos.X, _statPos.Y, 448, 422); // title 16 + body 406
    private Rectangle SkillRect() => new(_skillPos.X, _skillPos.Y, SkillW, 16 + SkillBodyH); // title 16 + tabs + SP + rows
    private Rectangle InvTitleRect() => new(_invPos.X, _invPos.Y, 242, 28);
    private Rectangle StatTitleRect() => new(_statPos.X, _statPos.Y, 448, 16);
    private Rectangle SkillTitleRect() => new(_skillPos.X, _skillPos.Y, SkillW, 16);

    private Texture2D Tex(string file) => _textures["ui/" + file];

    private void UpdateHud(KeyboardState kb, bool click, bool held, int gx, int gy)
    {
        bool i = kb.IsKeyDown(Keys.I);
        if (i && !_iWasDown) { _showInv = !_showInv; _dragInv = null; if (_showInv) _topPanel = 0; } // hiding mid-drag drops the grab
        _iWasDown = i;
        bool c = kb.IsKeyDown(Keys.C);
        if (c && !_cWasDown) { _showStat = !_showStat; _dragStat = null; if (_showStat) _topPanel = 1; } // hiding mid-drag drops the grab
        _cWasDown = c;
        bool k = kb.IsKeyDown(Keys.K);
        if (k && !_kWasDown) { _showSkill = !_showSkill; _dragSkill = null; if (_showSkill) _topPanel = 2; } // hiding mid-drag drops the grab
        _kWasDown = k;
        // Title drag runs on held (down starts it in the chain below).
        if (_dragInv.HasValue)
        {
            if (!held) _dragInv = null;
            else _invPos = ClampPanel(new Point(gx - _dragInv.Value.X, gy - _dragInv.Value.Y), 242);
        }
        if (_dragStat.HasValue)
        {
            if (!held) _dragStat = null;
            else _statPos = ClampPanel(new Point(gx - _dragStat.Value.X, gy - _dragStat.Value.Y), 448);
        }
        if (_dragSkill.HasValue)
        {
            if (!held) _dragSkill = null;
            else _skillPos = ClampPanel(new Point(gx - _dragSkill.Value.X, gy - _dragSkill.Value.Y), SkillW);
        }
        if (!click) return;
        bool onInv = _showInv && HitRect(InvRect(), gx, gy);
        bool onStat = _showStat && HitRect(StatRect(), gx, gy);
        bool onSkill = _showSkill && HitRect(SkillRect(), gx, gy);
        // Overlap: the frontmost panel owns the click. The front panel wins
        // every tie; the back two keep inv < stat < skill order. Pairwise
        // elimination leaves the highest z standing, and the survivor comes
        // to front (a click brings its panel to front, even on overlap).
        int z(int p) => p == _topPanel ? 3 : p;
        if (onInv && onStat) { if (z(0) < z(1)) onInv = false; else onStat = false; }
        if (onInv && onSkill) { if (z(0) < z(2)) onInv = false; else onSkill = false; }
        if (onStat && onSkill) { if (z(1) < z(2)) onStat = false; else onSkill = false; }
        if (onInv) _topPanel = 0;
        else if (onStat) _topPanel = 1;
        else if (onSkill) _topPanel = 2;
        // Panels draw on top, so a panel click never reaches the bottom bar behind it.
        if (onInv || onStat || onSkill)
        {
            _showSubmenu = false;
            if (onInv && HitRect(InvXRect(), gx, gy)) _showInv = false;
            else if (onStat && HitRect(StatXRect(), gx, gy)) _showStat = false;
            else if (onSkill && HitRect(SkillXRect(), gx, gy)) _showSkill = false;
            else if (onInv && HitRect(InvTitleRect(), gx, gy)) _dragInv = new Point(gx - _invPos.X, gy - _invPos.Y);
            else if (onStat && HitRect(StatTitleRect(), gx, gy)) _dragStat = new Point(gx - _statPos.X, gy - _statPos.Y);
            else if (onSkill && HitRect(SkillTitleRect(), gx, gy)) _dragSkill = new Point(gx - _skillPos.X, gy - _skillPos.Y);
            else if (onInv)
            {
                for (int t = 0; t < TabCount; t++)
                    if (HitRect(TabRect(t), gx, gy)) { _invTab = t; break; }
                if (_invTab == 1) // USE tab: click a potion to drink it
                    for (int s = 0; s < UseIds.Length; s++)
                        if (HitRect(UseSlotRect(s), gx, gy)) DrinkPotion(UseIds[s]);
            }
            else if (onSkill)
            {
                for (int t = 0; t < SkillTabs.Length; t++)
                    if (HitRect(SkillTabRect(t), gx, gy)) { _skillTab = t; break; }
                var tab = SkillTabs[_skillTab]; // [+] spends one SP (LearnSkill re-checks everything)
                for (int row = 0; row < tab.Length; row++)
                    if (HitRect(SkillPlusRect(row), gx, gy) && LearnSkill(tab[row].id)) PushBuild();
            }
            return;
        }
        if (HitRect(MenuBtnRect(), gx, gy)) _showSubmenu = !_showSubmenu;
        else if (HitRect(CharBtnRect(), gx, gy)) { _showStat = !_showStat; _showSubmenu = false; if (_showStat) _topPanel = 1; }
        else if (_showSubmenu && HitRect(SubRowRect(0), gx, gy)) { _showStat = true; _showSubmenu = false; _topPanel = 1; }
        else if (_showSubmenu && HitRect(SubRowRect(1), gx, gy)) { _showInv = true; _showSubmenu = false; _topPanel = 0; }
        else if (_showSubmenu && HitRect(SubPopupRect(), gx, gy)) { } // popup padding swallows the click
        else if (_showSubmenu) _showSubmenu = false; // outside click closes (no panel behind to fall through to)
    }

    // Panels stay reachable: 80px on screen sideways, title never above the top.
    private static Point ClampPanel(Point p, int w) =>
        new(Math.Clamp(p.X, -w + 80, 1366 - 80), Math.Clamp(p.Y, 0, 768 - 40));

    // Submenu popup: 3-slice bg sized by its own caps, two 109x26 rows inside.
    private const int SubPopupW = 121;
    private Rectangle SubPopupRect()
    {
        int top = Tex("hud_sub_bg0.png").Height;
        int bottom = Tex("hud_sub_bg2.png").Height;
        int h = top + 2 * 26 + 3 * 4 + bottom;
        int x = MenuBtnRect().X + (MenuBtnRect().Width - SubPopupW) / 2;
        return new Rectangle(x, MenuBtnRect().Y - 4 - h, SubPopupW, h);
    }

    private Rectangle SubRowRect(int i)
    {
        var p = SubPopupRect();
        int y = p.Y + Tex("hud_sub_bg0.png").Height + 4 + i * (26 + 4);
        return new Rectangle(p.X + 6, y, 109, 26);
    }

    // Official tabs are fixed 35x22 art at pitch 37 (tab:category lt/pitch).
    private Rectangle TabRect(int t) =>
        new(InvRect().X + 11 + 37 * t, InvRect().Y + 31, 35, 22);

    // USE tab potion cells: aligned to the first inset row of the art grid
    // (local y 95..143, measured off the USE shot; x pitch 56).
    private Rectangle UseSlotRect(int i)
    {
        var r = InvRect();
        return new Rectangle(r.X + 14 + i * 56, r.Y + 95, 48, 48);
    }

    // Click a USE slot: drink one. Empty slots and full pools do nothing.
    private void DrinkPotion(int id)
    {
        var chara = _account?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no bag
        var item = _items[id];
        Progression.UsePotion(chara, id, item.Hp, item.Mp, OwnHpPct());
    }

    // Skill window job tabs (3 x 100px under the title).
    private Rectangle SkillTabRect(int t)
    {
        var r = SkillRect();
        return new Rectangle(r.X + 8 + t * 104, r.Y + 16 + 4, 100, 18);
    }

    // [+] zone: right end of a skill row (spends one SP on click).
    private Rectangle SkillPlusRect(int i)
    {
        var r = SkillRect();
        return new Rectangle(r.X + SkillW - 44, r.Y + 16 + SkillRowsY + i * SkillRowH + 4, 36, 26);
    }

    // Click a [+]: learn one level. CLI sessions carry no character.
    private bool LearnSkill(int id)
    {
        var chara = _account?.Slots[_slot];
        return chara != null && Progression.LearnSkill(chara, _skills[id]);
    }

    private Rectangle InvXRect()
    {
        var r = InvRect();
        return new Rectangle(r.X + r.Width - 11 - 6, r.Y + 6, 11, 11);
    }

    private Rectangle StatXRect()
    {
        var r = StatRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private Rectangle SkillXRect()
    {
        var r = SkillRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private void DrawHud(int gx, int gy, bool pressed)
    {
        DrawExpBar();
        DrawPlate();
        DrawHpMp();
        DrawHudButton(MenuBtnRect(), "hud_btn_menu", gx, gy, pressed);
        DrawHudButton(CharBtnRect(), "hud_btn_char", gx, gy, pressed);
        if (_showSubmenu) DrawSubmenu(gx, gy, pressed);
        DrawQslot();
        // The front panel draws last (on top); the back two keep inv < stat < skill.
        if (_topPanel == 0) { if (_showStat) DrawStatPanel(); if (_showSkill) DrawSkillPanel(); if (_showInv) DrawInvPanel(); }
        else if (_topPanel == 1) { if (_showInv) DrawInvPanel(); if (_showSkill) DrawSkillPanel(); if (_showStat) DrawStatPanel(); }
        else { if (_showInv) DrawInvPanel(); if (_showStat) DrawStatPanel(); if (_showSkill) DrawSkillPanel(); }
    }

    private void DrawHudButton(Rectangle r, string stem, int gx, int gy, bool pressed)
    {
        bool hover = HitRect(r, gx, gy);
        string file = hover ? pressed ? $"{stem}_p.png" : $"{stem}_h.png" : $"{stem}_n.png";
        _spriteBatch.Draw(Tex(file), r, Color.White);
    }

    private void DrawExpBar()
    {
        var r = ExpRect();
        _spriteBatch.Draw(Tex("hud_expback.png"), r, Color.White);
        var chara = _account?.Slots[_slot];
        if (chara != null && chara.Exp > 0) // CLI sessions have no EXP: the bar stays empty
        {
            var gauge = Tex("hud_expgauge.png");
            int w = gauge.Width * chara.Exp / Progression.ExpNext(chara.Level);
            if (w > 0)
                _spriteBatch.Draw(gauge, new Vector2(r.X, r.Y), new Rectangle(0, 0, w, gauge.Height), Color.White);
        }
        _spriteBatch.Draw(Tex("hud_expcover.png"), new Vector2(r.X, r.Y), Color.White);
    }

    private void DrawPlate()
    {
        var r = PlateRect();
        _spriteBatch.Draw(Tex("hud_plate.png"), r, Color.White);
        var chara = _account?.Slots[_slot];
        int x = r.X + 28; // WZ levelPos; digits step 10 (numInterval)
        foreach (char ch in (chara?.Level ?? 1).ToString())
        {
            _spriteBatch.Draw(Tex($"hud_lv{ch}.png"), new Vector2(x, r.Y + 11), Color.White);
            x += 10;
        }
        _spriteBatch.Draw(Tex("hud_lvtag.png"), new Vector2(r.X + 8, r.Y + 13), Color.White);
        if (chara != null) // WZ namePos; CLI sessions have no name
            _spriteBatch.Draw(_ui.GetSmall(chara.Name), new Vector2(r.X + 67, r.Y + 13), Color.White);
    }

    private void DrawHpMp()
    {
        var r = HpRect();
        _spriteBatch.Draw(Tex("hud_hpbg.png"), r, Color.White);
        // main/status/normal anchors (canvas draws at anchor minus origin):
        // fills at bg+(4,4)/(4,25), cover at bg+(3,3), numbers centered on
        // x+101 (vector:hp/mp), tops y+7/y+28. Bars always full: nothing
        // drains HP/MP until W13 mob hits (W13 adds the clip then).
        var chara = _account?.Slots[_slot];
        int maxHp = Progression.BuffedMaxHp(chara?.Level ?? 1, OwnHpPct()), maxMp = Progression.MaxMp(chara?.Level ?? 1);
        _spriteBatch.Draw(Tex("hud_hpfill.png"), new Vector2(r.X + 4, r.Y + 4), Color.White);
        _spriteBatch.Draw(Tex("hud_mpfill.png"), new Vector2(r.X + 4, r.Y + 25), Color.White);
        DrawBarNumber($"{chara?.Hp ?? maxHp}/{maxHp}", r.X + 101, r.Y + 7);
        DrawBarNumber($"{chara?.Mp ?? maxMp}/{maxMp}", r.X + 101, r.Y + 28);
        _spriteBatch.Draw(Tex("hud_hpcover.png"), new Vector2(r.X + 3, r.Y + 3), Color.White);
    }

    // Art digits centered on the official x; the "/" is baked 12px
    // (font:number is Arial 12; the WZ separator name breaks path lookup).
    private void DrawBarNumber(string text, int centerX, int topY)
    {
        int dh = Tex("hud_num0.png").Height;
        int w = 0;
        foreach (char ch in text) w += ch == '/' ? _ui.GetSmall("/").Width : Tex($"hud_num{ch}.png").Width;
        int x = centerX - w / 2;
        foreach (char ch in text)
        {
            if (ch == '/')
            {
                var s = _ui.GetSmall("/");
                _spriteBatch.Draw(s, new Vector2(x, topY + (dh - s.Height) / 2), Color.White);
                x += s.Width;
            }
            else
            {
                var d = Tex($"hud_num{ch}.png");
                _spriteBatch.Draw(d, new Vector2(x, topY), Color.White);
                x += d.Width;
            }
        }
    }

    private void DrawSubmenu(int gx, int gy, bool pressed)
    {
        var p = SubPopupRect();
        var bg0 = Tex("hud_sub_bg0.png");
        var bg1 = Tex("hud_sub_bg1.png");
        var bg2 = Tex("hud_sub_bg2.png");
        _spriteBatch.Draw(bg0, new Vector2(p.X, p.Y), Color.White);
        _spriteBatch.Draw(bg1, new Rectangle(p.X, p.Y + bg0.Height, p.Width, p.Height - bg0.Height - bg2.Height), Color.White);
        _spriteBatch.Draw(bg2, new Vector2(p.X, p.Y + p.Height - bg2.Height), Color.White);
        DrawHudButton(SubRowRect(0), "hud_sub_char", gx, gy, pressed);
        DrawHudButton(SubRowRect(1), "hud_sub_item", gx, gy, pressed);
    }

    private void DrawQslot()
    {
        var r = QslotRect();
        _spriteBatch.Draw(Tex("hud_qslot.png"), r, Color.White);
        // Fixed W9 hotkeys: official key art top-left (icons arrive with skills/potions).
        (string file, string action)[] slots = new[] { ("hud_key_ctrl.png", "Attack"), ("hud_key_alt.png", "Jump"), ("hud_key_i.png", "Items"), ("hud_key_c.png", "Stats") };
        for (int i = 0; i < slots.Length; i++)
        {
            var s = QslotCell(i);
            var k = Tex(slots[i].file);
            var a = _ui.GetSmall(slots[i].action);
            _spriteBatch.Draw(k, new Vector2(s.X + 2, s.Y + 2), Color.White);
            _spriteBatch.Draw(a, new Vector2(s.X + (s.Width - a.Width) / 2, s.Y + 2 + k.Height), Color.Gray);
        }
        _spriteBatch.Draw(Tex("hud_qslotcover.png"), r, Color.White); // gloss stretched onto the cells: bottom flush with EXP top
    }

    private static Rectangle QslotCell(int i)
    {
        var r = QslotRect();
        return new Rectangle(r.X + 6 + i * 34, r.Y + 2, 34, 31);
    }

    private void DrawInvPanel()
    {
        var r = InvRect();
        _spriteBatch.Draw(Tex("hud_inv.png"), r, Color.White);
        for (int t = 0; t < TabCount; t++) DrawTab(t);
        _spriteBatch.Draw(Tex("hud_statx.png"), InvXRect(), Color.White);
        var chara = _account?.Slots[_slot];
        if (_invTab == 1 && chara?.Use != null) // USE tab: owned potions only (other tabs stay empty until W13+)
            for (int i = 0; i < UseIds.Length; i++)
                if (chara.Use.TryGetValue(UseIds[i], out int count) && count > 0)
                    DrawUseSlot(i, UseIds[i], count);
        // Official numbers: Arial Bold 12 #606871, right end x+213 top y+484 (font:Meso, vector:Meso).
        _spriteBatch.Draw(Tex("hud_coin.png"), new Vector2(r.X + Layout.Coin.X, r.Y + Layout.Coin.Y), Color.White);
        var meso = _ui.GetMeso((chara?.Mesos ?? 0).ToString("#,##0"));
        _spriteBatch.Draw(meso, new Vector2(r.X + Layout.Meso.X - meso.Width, r.Y + Layout.Meso.Y), new Color(96, 104, 113));
    }

    private void DrawTab(int t)
    {
        var tr = TabRect(t);
        string file = t == _invTab ? $"hud_itab_s{t}.png" : $"hud_itab_n{t}.png";
        _spriteBatch.Draw(Tex(file), new Vector2(tr.X, tr.Y), Color.White);
    }

    // One USE slot: icon centered in the cell, count bottom-right.
    private void DrawUseSlot(int i, int id, int count)
    {
        var s = UseSlotRect(i);
        var icon = _textures[_items[id].Image];
        _spriteBatch.Draw(icon, new Vector2(s.X + (s.Width - icon.Width) / 2, s.Y + (s.Height - icon.Height) / 2), Color.White);
        var n = _ui.GetSmall(count.ToString());
        _spriteBatch.Draw(n, new Vector2(s.X + s.Width - n.Width - 2, s.Y + s.Height - n.Height - 2), Color.Black);
    }

    // Solid backing with rounded corners (the art's own radius is ~3px, measured).
    private Texture2D RoundedBacking(int w, int h, Color color, int r)
    {
        var tex = new Texture2D(GraphicsDevice, w, h);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
                bool cut = dx < r && dy < r && (dx - r) * (dx - r) + (dy - r) * (dy - r) > r * r;
                px[y * w + x] = cut ? Color.Transparent : color;
            }
        tex.SetData(px);
        return tex;
    }

    // Rounds the title strip's top corners in place (drawn stretched ~2x wide,
    // so the native ellipse is rx=2 by ry=3).
    private static void RoundTopCorners(Texture2D tex, int rx, int ry)
    {
        Color[] px = new Color[tex.Width * tex.Height];
        tex.GetData(px);
        for (int y = 0; y < ry; y++)
            for (int x = 0; x < tex.Width; x++)
            {
                int dx = Math.Min(x, tex.Width - 1 - x);
                if (dx < rx && (dx - rx) * (dx - rx) * ry * ry + (y - ry) * (y - ry) * rx * rx > rx * rx * ry * ry)
                    px[y * tex.Width + x] = Color.Transparent;
            }
        tex.SetData(px);
    }

    private void DrawStatPanel()
    {
        var r = StatRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("STAT"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), StatXRect(), Color.White);
        int by = r.Y + 16;
        _spriteBatch.Draw(_statBack, new Vector2(r.X, by), Color.White);
        _spriteBatch.Draw(Tex("hud_stat.png"), new Vector2(r.X, by), Color.White);
        var chara = _account?.Slots[_slot];
        var build = OwnBuild(); // STR/DEX/range show skilled values (passives and buffs feed combat)
        int maxHp = Progression.BuffedMaxHp(chara?.Level ?? 1, OwnHpPct()), maxMp = Progression.MaxMp(chara?.Level ?? 1);
        var cp = _ui.GetSmall("0"); // value only; "Combat Power" is baked in the art (no formula yet)
        _spriteBatch.Draw(cp, new Vector2(r.X + 432 - cp.Width, by + 8), Color.White);
        int y = by + 132; // panel 1: vitals + 4 stats (name/level live on the plate)
        DrawStatRow("HP", $"{chara?.Hp ?? maxHp} / {maxHp}", r.X, y); y += 22;
        DrawStatRow("MP", $"{chara?.Mp ?? maxMp} / {maxMp}", r.X, y); y += 22;
        DrawStatRow("STR", $"{build.Str}", r.X, y); y += 22;
        DrawStatRow("DEX", $"{build.Dex}", r.X, y); y += 22;
        DrawStatRow("INT", $"{chara?.Int ?? 4}", r.X, y); y += 22;
        DrawStatRow("LUK", $"{chara?.Luk ?? 4}", r.X, y);
        y = by + 320; // panel 2 is only ~80px: 5 tight rows (official rows are tight too)
        (int lo, int hi) = Progression.PanelRange(build.Str, build.Dex, build.WeaponAtk);
        DrawStatRow("Attack Power", $"{lo}~{hi}", r.X, y); y += 16;
        DrawStatRow("Magic ATT", "0", r.X, y); y += 16;
        DrawStatRow("Defense", $"{OwnDef()}", r.X, y); y += 16;
        DrawStatRow("Speed", "100", r.X, y); y += 16;
        DrawStatRow("Jump", "100", r.X, y);
    }

    private void DrawStatRow(string label, string value, int x, int y)
    {
        _spriteBatch.Draw(_ui.GetSmall(label), new Vector2(x + 16, y), new Color(50, 50, 60));
        var v = _ui.GetSmall(value);
        _spriteBatch.Draw(v, new Vector2(x + 432 - v.Width, y), Color.Black);
    }

    // Skill window: one tab per job (icon, name, level, costs, key).
    // Locked skills show the required level in red; cooling skills and live
    // buffs count seconds; learnable rows grow a green [+] (one SP a click).
    private void DrawSkillPanel()
    {
        var r = SkillRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("SKILL"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), SkillXRect(), Color.White);
        int by = r.Y + 16;
        _spriteBatch.Draw(_skillBack, new Vector2(r.X, by), Color.White);
        for (int t = 0; t < SkillTabs.Length; t++)
        {
            var tab = SkillTabRect(t);
            var ink = t == _skillTab ? new Color(255, 220, 0) : new Color(130, 140, 150);
            _spriteBatch.Draw(_ui.GetSmall(SkillTabNames[t]), new Vector2(tab.X + 8, tab.Y + 2), ink);
        }
        int sp = _account?.Slots[_slot]?.Sp ?? 0;
        _spriteBatch.Draw(_ui.GetSmall(sp > 0 ? $"SP: {sp} (click + to learn)" : $"SP: {sp}"),
            new Vector2(r.X + 16, by + 26), Color.White);
        var rows = SkillTabs[_skillTab];
        for (int i = 0; i < rows.Length; i++)
            DrawSkillRow(rows[i].id, rows[i].key, r.X, by + SkillRowsY + i * SkillRowH);
    }

    private void DrawSkillRow(int id, string? key, int x, int y)
    {
        var skill = _skills[id];
        var chara = _account?.Slots[_slot];
        int level = chara?.Level ?? 1;
        int lv = 0;
        chara?.SkillLv.TryGetValue(id, out lv);
        bool locked = level < skill.ReqLevel;
        bool maxed = lv >= skill.MaxLevel;
        bool learnable = !locked && !maxed && (chara?.Sp ?? 0) > 0;
        float left = 0;
        bool cooling = !locked && _skillCdMs.TryGetValue(id, out left) && left > 0;
        float buffLeft = 0;
        bool live = _buffMs.TryGetValue(id, out buffLeft) && buffLeft > 0;
        _spriteBatch.Draw(_textures[locked || cooling ? skill.Disabled : skill.Icon], new Vector2(x + 10, y + 8), Color.White);
        var ink = locked ? new Color(130, 140, 150) : Color.White;
        _spriteBatch.Draw(_ui.GetSmall(skill.Name), new Vector2(x + 50, y + 4), ink);
        string sub = skill.Kind == SkillData.KindPassive
            ? $"Lv.{lv}/{skill.MaxLevel}  {PassiveBlurb[id]}"
            : skill.Kind == SkillData.KindBuff
            ? $"Lv.{lv}/{skill.MaxLevel}  MP {skill.Mp}  {skill.BuffMs / 1000}s{(id == SkillData.ComboAttack && live ? $"  {_orbs} orbs" : "")}"
            : $"Lv.{lv}/{skill.MaxLevel}  MP {skill.Mp}  CD {skill.CooldownMs / 1000}s";
        _spriteBatch.Draw(_ui.GetSmall(sub), new Vector2(x + 50, y + 24), ink);
        string right = locked ? $"Lv.{skill.ReqLevel}"
            : live ? $"{(int)Math.Ceiling(buffLeft / 1000)}s"
            : cooling ? $"{(int)Math.Ceiling(left / 1000)}s"
            : key ?? $"{lv}/{skill.MaxLevel}";
        var rc = locked ? new Color(255, 120, 120)
            : live || cooling ? new Color(255, 220, 0) : Color.White;
        if (learnable) { right += " [+]"; rc = new Color(120, 255, 120); }
        var rt = _ui.GetSmall(right);
        _spriteBatch.Draw(rt, new Vector2(x + SkillW - 8 - rt.Width, y + 4), rc);
    }
}
