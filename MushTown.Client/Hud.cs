using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MushTown.Client;

// W9 bottom bar + item/stat panels. Art is official (StatusBar3, UIInventory,
// UICharacterInfo, Basic tabs); all text baked. Plate/slot positions are WZ
// vectors; the rest is tuned rects. HP full + EXP 0 are real: nothing changes
// them until W10 leveling and W13 mob hits.
public partial class MushTownGame
{
    // Must match Export.cs ExportHud: every name here must be exported, or LoadTexture throws.
    private static readonly string[] HudFiles = new[]
    {
        "hud_plate.png", "hud_lvtag.png",
        "hud_hpbg.png", "hud_hpcover.png", "hud_hpfill.png", "hud_mpfill.png",
        "hud_expback.png", "hud_expcover.png",
        "hud_btn_char_n.png", "hud_btn_char_h.png", "hud_btn_char_p.png",
        "hud_btn_menu_n.png", "hud_btn_menu_h.png", "hud_btn_menu_p.png",
        "hud_sub_char_n.png", "hud_sub_char_h.png", "hud_sub_char_p.png",
        "hud_sub_item_n.png", "hud_sub_item_h.png", "hud_sub_item_p.png",
        "hud_sub_bg0.png", "hud_sub_bg1.png", "hud_sub_bg2.png",
        "hud_qslot.png", "hud_qslotcover.png",
        "hud_inv.png",
        "hud_itab_s0.png", "hud_itab_s1.png", "hud_itab_s2.png",
        "hud_itab_s3.png", "hud_itab_s4.png", "hud_itab_s5.png",
        "hud_itab_n0.png", "hud_itab_n1.png", "hud_itab_n2.png",
        "hud_itab_n3.png", "hud_itab_n4.png", "hud_itab_n5.png",
        "hud_coin.png", "hud_stat.png", "hud_stathilong.png", "hud_statx.png",
    };
    private const int TabCount = 6;

    // Beginner vitals: no formula yet (W10), so the bar shows full.
    private const int MaxHp = 50;
    private const int MaxMp = 5;

    private bool _showInv, _showStat, _showSubmenu;
    private int _invTab; // 0 Equip 1 Use 2 Etc 3 Set-up 4 Cash 5 Dec
    private bool _iWasDown, _cWasDown;

    private static Rectangle PlateRect() => new(0, 723, 250, 35);
    private static Rectangle HpRect() => new(584, 720, 198, 37); // cover bottom flush with EXP top
    private static Rectangle MenuBtnRect() => new(266, 724, 34, 37);
    private static Rectangle CharBtnRect() => new(304, 724, 34, 37);
    private static Rectangle QslotRect() => new(810, 691, 557, 67);
    private static Rectangle ExpRect() => new(0, 758, 1366, 10);
    private Point _invPos = new(1114, 45), _statPos = new(656, 130);
    private Point? _dragInv, _dragStat; // title grab offsets while held
    private bool _statOnTop = true; // false = inventory is the active (front) panel
    private Texture2D _statBack = null!; // navy body backing, corners rounded to match the art

    private Rectangle InvRect() => new(_invPos.X, _invPos.Y, 242, 593);
    private Rectangle StatRect() => new(_statPos.X, _statPos.Y, 448, 422); // title 16 + body 406
    private Rectangle InvTitleRect() => new(_invPos.X, _invPos.Y, 242, 28);
    private Rectangle StatTitleRect() => new(_statPos.X, _statPos.Y, 448, 16);

    private Texture2D Tex(string file) => _textures["ui/" + file];

    private void UpdateHud(KeyboardState kb, bool click, bool held, int gx, int gy)
    {
        bool i = kb.IsKeyDown(Keys.I);
        if (i && !_iWasDown) { _showInv = !_showInv; _dragInv = null; if (_showInv) _statOnTop = false; } // hiding mid-drag drops the grab
        _iWasDown = i;
        bool c = kb.IsKeyDown(Keys.C);
        if (c && !_cWasDown) { _showStat = !_showStat; _dragStat = null; if (_showStat) _statOnTop = true; } // hiding mid-drag drops the grab
        _cWasDown = c;
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
        if (!click) return;
        bool onInv = _showInv && HitRect(InvRect(), gx, gy);
        bool onStat = _showStat && HitRect(StatRect(), gx, gy);
        if (onInv && !onStat) _statOnTop = false; // a click brings its panel to front
        else if (onStat && !onInv) _statOnTop = true;
        if (onInv && onStat) // overlap: the top panel owns the click
        {
            if (_statOnTop) onInv = false;
            else onStat = false;
        }
        // Panels draw on top, so a panel click never reaches the bottom bar behind it.
        if (onInv || onStat)
        {
            _showSubmenu = false;
            if (onInv && HitRect(InvXRect(), gx, gy)) _showInv = false;
            else if (onStat && HitRect(StatXRect(), gx, gy)) _showStat = false;
            else if (onInv && HitRect(InvTitleRect(), gx, gy)) _dragInv = new Point(gx - _invPos.X, gy - _invPos.Y);
            else if (onStat && HitRect(StatTitleRect(), gx, gy)) _dragStat = new Point(gx - _statPos.X, gy - _statPos.Y);
            else if (onInv)
            {
                for (int t = 0; t < TabCount; t++)
                    if (HitRect(TabRect(t), gx, gy)) { _invTab = t; break; }
            }
            return;
        }
        if (HitRect(MenuBtnRect(), gx, gy)) _showSubmenu = !_showSubmenu;
        else if (HitRect(CharBtnRect(), gx, gy)) { _showStat = !_showStat; _showSubmenu = false; if (_showStat) _statOnTop = true; }
        else if (_showSubmenu && HitRect(SubRowRect(0), gx, gy)) { _showStat = true; _showSubmenu = false; _statOnTop = true; }
        else if (_showSubmenu && HitRect(SubRowRect(1), gx, gy)) { _showInv = true; _showSubmenu = false; _statOnTop = false; }
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

    private void DrawHud(int gx, int gy, bool pressed)
    {
        DrawExpBar();
        DrawPlate();
        DrawHpMp();
        DrawHudButton(MenuBtnRect(), "hud_btn_menu", gx, gy, pressed);
        DrawHudButton(CharBtnRect(), "hud_btn_char", gx, gy, pressed);
        if (_showSubmenu) DrawSubmenu(gx, gy, pressed);
        DrawQslot();
        // The active panel draws last (on top).
        if (_statOnTop) { if (_showInv) DrawInvPanel(); if (_showStat) DrawStatPanel(); }
        else { if (_showStat) DrawStatPanel(); if (_showInv) DrawInvPanel(); }
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
        // EXP is always 0 in W9: nothing fills the bar yet (W10 leveling).
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
        // Official anchor math (canvas draws at anchor minus origin): fills at
        // bg+(22,4)/(22,20), cover at bg+(-3,-24). Bar always full: nothing
        // drains HP/MP until W13 mob hits (W13 adds the clip then).
        _spriteBatch.Draw(Tex("hud_hpfill.png"), new Vector2(r.X + 22, r.Y + 4), Color.White);
        _spriteBatch.Draw(Tex("hud_mpfill.png"), new Vector2(r.X + 22, r.Y + 20), Color.White);
        DrawBarNumber($"{MaxHp}/{MaxHp}", r.X + 22, r.Y + 4);
        DrawBarNumber($"{MaxMp}/{MaxMp}", r.X + 22, r.Y + 20);
        _spriteBatch.Draw(Tex("hud_hpcover.png"), new Vector2(r.X - 3, r.Y - 24), Color.White);
    }

    // Art digits centered on the 171px fill; the "/" is baked (see ExportHud).
    private void DrawBarNumber(string text, int rowX, int rowY)
    {
        int w = 0;
        foreach (char ch in text) w += ch == '/' ? _ui.GetSmall("/").Width : Tex($"hud_num{ch}.png").Width;
        int x = rowX + (171 - w) / 2;
        int y = rowY + (13 - 9) / 2;
        foreach (char ch in text)
        {
            if (ch == '/')
            {
                var s = _ui.GetSmall("/");
                _spriteBatch.Draw(s, new Vector2(x, rowY + (13 - s.Height) / 2), Color.White);
                x += s.Width;
            }
            else
            {
                var d = Tex($"hud_num{ch}.png");
                _spriteBatch.Draw(d, new Vector2(x, y), Color.White);
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
        // Fixed W9 hotkeys: only live actions get a label (icons arrive with skills/potions).
        (string key, string action)[] slots = new[] { ("Ctrl", "Attack"), ("Alt", "Jump"), ("I", "Items"), ("C", "Stats") };
        for (int i = 0; i < slots.Length; i++)
        {
            var s = QslotCell(i);
            var k = _ui.GetSmall(slots[i].key);
            var a = _ui.GetSmall(slots[i].action);
            _spriteBatch.Draw(k, new Vector2(s.X + (s.Width - k.Width) / 2, s.Y + 2), Color.White);
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
        // Slots stay empty: no items exist until W13 drops.
        var chara = _account?.Slots[_slot];
        // Official numbers: Arial Bold 12 #606871, right end x+213 top y+484 (font:Meso, vector:Meso).
        _spriteBatch.Draw(Tex("hud_coin.png"), new Vector2(r.X + 24, r.Y + 484), Color.White);
        var meso = _ui.GetMeso((chara?.Mesos ?? 0).ToString("#,##0"));
        _spriteBatch.Draw(meso, new Vector2(r.X + 213 - meso.Width, r.Y + 484), new Color(96, 104, 113));
    }

    private void DrawTab(int t)
    {
        var tr = TabRect(t);
        string file = t == _invTab ? $"hud_itab_s{t}.png" : $"hud_itab_n{t}.png";
        _spriteBatch.Draw(Tex(file), new Vector2(tr.X, tr.Y), Color.White);
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
        var cp = _ui.GetSmall("0"); // value only; "Combat Power" is baked in the art (no formula yet)
        _spriteBatch.Draw(cp, new Vector2(r.X + 432 - cp.Width, by + 8), Color.White);
        int y = by + 132; // panel 1: vitals + 4 stats (name/level live on the plate)
        DrawStatRow("HP", $"{MaxHp} / {MaxHp}", r.X, y); y += 22;
        DrawStatRow("MP", $"{MaxMp} / {MaxMp}", r.X, y); y += 22;
        DrawStatRow("STR", $"{chara?.Str ?? 4}", r.X, y); y += 22;
        DrawStatRow("DEX", $"{chara?.Dex ?? 4}", r.X, y); y += 22;
        DrawStatRow("INT", $"{chara?.Int ?? 4}", r.X, y); y += 22;
        DrawStatRow("LUK", $"{chara?.Luk ?? 4}", r.X, y);
        y = by + 320; // panel 2 is only ~80px: 5 tight rows (official rows are tight too)
        DrawStatRow("Attack Power", "32", r.X, y); y += 16; // polearm incPAD (W8); formula in W10
        DrawStatRow("Magic ATT", "0", r.X, y); y += 16;
        DrawStatRow("Defense", "0", r.X, y); y += 16;
        DrawStatRow("Speed", "100", r.X, y); y += 16;
        DrawStatRow("Jump", "100", r.X, y);
    }

    private void DrawStatRow(string label, string value, int x, int y)
    {
        _spriteBatch.Draw(_ui.GetSmall(label), new Vector2(x + 16, y), new Color(50, 50, 60));
        var v = _ui.GetSmall(value);
        _spriteBatch.Draw(v, new Vector2(x + 432 - v.Width, y), Color.Black);
    }
}
