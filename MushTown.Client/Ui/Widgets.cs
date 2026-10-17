using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Hit + layout rect. Numbers are copied from the legacy rect helpers
// (pixel parity first); the helpers stay the single source.
public sealed class Rect : Component
{
    public Rectangle Box;
    public Rect(Rectangle box) => Box = box;
    public bool Contains(int gx, int gy) => Box.Contains(gx, gy);
}

// 3-slice stretched background. Children (added after) draw on top.
public sealed class Panel : Component, IDrawable
{
    private readonly string _top, _mid, _bot;
    public Panel(string top, string mid, string bot) { _top = top; _mid = mid; _bot = bot; }
    public void Draw(UiGfx gfx)
    {
        var p = Owner.Get<Rect>()!.Box;
        var t0 = gfx.Tex(_top); var t1 = gfx.Tex(_mid); var t2 = gfx.Tex(_bot);
        gfx.Sb.Draw(t0, new Vector2(p.X, p.Y), Color.White);
        gfx.Sb.Draw(t1, new Rectangle(p.X, p.Y + t0.Height, p.Width, p.Height - t0.Height - t2.Height), Color.White);
        gfx.Sb.Draw(t2, new Vector2(p.X, p.Y + p.Height - t2.Height), Color.White);
    }
}

// Clickable rect: hit via Rect, action via OnClick. The Scene resolves
// the hovered Button and plays the hover sfx (LegacyId bridges the old
// _hoverId world; gone at the W5 cleanup).
public enum ButtonKind { Silent, Click } // click sfx via ClickTree (conditional sounds stay explicit in callbacks)

public class Button : Component, IDrawable
{
    public string LegacyId = "";
    public ButtonKind Kind; // Silent by default (slots, titles, furniture, gated clicks)
    public bool HoverFill; // white-30 overlay when hovered (text rows)
    public Action? OnClick;
    public void Press() => OnClick?.Invoke();
    public virtual void Draw(UiGfx gfx)
    {
        if (HoverFill && gfx.HoverButton == this)
            gfx.Sb.Draw(gfx.Pixel, Owner.Get<Rect>()!.Box, new Color(255, 255, 255, 30));
    }
}

// Wooden-button answer: hover 0.9, pressed 0.6 (one spelling for all menu art).
internal static class MenuAlpha
{
    public const float Hover = 0.9f, Pressed = 0.6f;
}

// State art: stem + _n/_h/_p by hover/pressed (the HUD buttons moved here from DrawHudButton).
// Stretches to the rect: callers size rects from the _n art, so a future art resize would stretch, not overflow.
public sealed class Image : Component, IDrawable
{
    private readonly string _stem;
    public Image(string stem) => _stem = stem;
    public void Draw(UiGfx gfx)
    {
        string key = _stem + (gfx.Hovered == Owner ? (gfx.Pressed ? "_p.png" : "_h.png") : "_n.png");
        gfx.Sb.Draw(gfx.Tex(key), Owner.Get<Rect>()!.Box, Color.White);
    }
}

// Bar visual: full backing, partial fill by live value/max, full cover
// on top. The fill starts at the rect's top-left (matches DrawExpBar).
public sealed class Bar : Component, IDrawable
{
    private readonly string _back, _fill, _cover;
    private readonly Func<(int Value, int Max)> _amount;
    public Bar(string back, string fill, string cover, Func<(int Value, int Max)> amount)
    {
        _back = back; _fill = fill; _cover = cover; _amount = amount;
    }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex(_back), r, Color.White);
        var f = gfx.Tex(_fill);
        var (v, m) = _amount();
        int w = SceneMath.BarWidth(f.Width, v, m);
        if (w > 0)
            gfx.Sb.Draw(f, new Vector2(r.X, r.Y), new Rectangle(0, 0, w, f.Height), Color.White);
        gfx.Sb.Draw(gfx.Tex(_cover), new Vector2(r.X, r.Y), Color.White);
    }
}

// Fixed art: stretched to the Owner rect, or raw at an offset.
public sealed class Picture : Component, IDrawable
{
    private readonly string? _key;
    private readonly Texture2D? _tex; // prebuilt backing (stat/skill/quest/settings bodies): no art key
    private readonly int _dx, _dy;
    private readonly bool _stretch;
    public Color Tint = Color.White;
    public float HoverAlpha = 1f, PressAlpha = 1f; // wooden buttons answer with transparency (0.9/0.6)
    public Picture(string key) { _key = key; _stretch = true; }
    public Picture(string key, int dx, int dy) { _key = key; _dx = dx; _dy = dy; }
    public Picture(Texture2D tex, int dx, int dy) { _tex = tex; _dx = dx; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        float a = gfx.Hovered == Owner ? gfx.Pressed ? PressAlpha : HoverAlpha : 1f;
        var c = Tint * a;
        if (_stretch) gfx.Sb.Draw(gfx.Tex(_key!), r, c);
        else gfx.Sb.Draw(_tex ?? gfx.Tex(_key!), new Vector2(r.X + _dx, r.Y + _dy), c);
    }
}

// Baked-text sizes a LiveLabel can ask for.
public enum LabelFont { Small, Mid }

// Live baked text at a rect offset (UiText caches by string, so steady
// text costs one lookup; changing text adds entries like the old draws).
public sealed class LiveLabel : Component, IDrawable
{
    private readonly Func<string> _text;
    private readonly int _dx, _dy;
    private readonly Color _color;
    private readonly LabelFont _font;
    public LiveLabel(Func<string> text, int dx, int dy, Color color, LabelFont font = LabelFont.Small) { _text = text; _dx = dx; _dy = dy; _color = color; _font = font; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var tex = _font switch
        {
            LabelFont.Mid => gfx.Ui.GetMid(_text()),
            _ => gfx.Ui.GetSmall(_text()),
        };
        gfx.Sb.Draw(tex, new Vector2(r.X + _dx, r.Y + _dy), _color);
    }
}

// Plate level digits: WZ levelPos stepping (matches DrawPlate).
public sealed class PlateLevel : Component, IDrawable
{
    private readonly Func<int> _level;
    public PlateLevel(Func<int> level) => _level = level;
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        int x = r.X + 28; // WZ levelPos; digits step 10 (numInterval)
        foreach (char ch in _level().ToString())
        {
            gfx.Sb.Draw(gfx.Tex($"ui/hud_lv{ch}.png"), new Vector2(x, r.Y + 11), Color.White);
            x += 10;
        }
    }
}

// Centered art-digit number (was DrawBarNumber). The "/" is baked 12px
// (font:number is Arial 12; the WZ separator name breaks path lookup).
public sealed class BarNumber : Component, IDrawable
{
    private readonly Func<string> _text;
    private readonly int _centerDx, _dy;
    public BarNumber(Func<string> text, int centerDx, int dy) { _text = text; _centerDx = centerDx; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        string text = _text();
        int dh = gfx.Tex("ui/hud_num0.png").Height;
        int w = 0;
        foreach (char ch in text) w += ch == '/' ? gfx.Ui.GetSmall("/").Width : gfx.Tex($"ui/hud_num{ch}.png").Width;
        int x = r.X + _centerDx - w / 2;
        foreach (char ch in text)
        {
            if (ch == '/')
            {
                var s = gfx.Ui.GetSmall("/");
                gfx.Sb.Draw(s, new Vector2(x, r.Y + _dy + (dh - s.Height) / 2), Color.White);
                x += s.Width;
            }
            else
            {
                var d = gfx.Tex($"ui/hud_num{ch}.png");
                gfx.Sb.Draw(d, new Vector2(x, r.Y + _dy), Color.White);
                x += d.Width;
            }
        }
    }
}

// Quest tracker lines (matches DrawTracker).
public sealed class Tracker : Component, IDrawable
{
    private readonly Func<List<string>> _lines;
    public Tracker(Func<List<string>> lines) => _lines = lines;
    public void Draw(UiGfx gfx)
    {
        int y = 10;
        var lines = _lines();
        for (int i = 0; i < lines.Count; i++) // name/detail pairs: even lines are gold names
        {
            var t = gfx.Ui.GetSmall(lines[i]);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(6, y - 2, t.Width + 8, 16), new Color(0, 0, 0, 120));
            gfx.Sb.Draw(t, new Vector2(10, y), i % 2 == 0 ? new Color(255, 220, 0) : Color.White);
            y += 16;
        }
    }
}

// Boss HP bar (matches DrawBossBar).
public sealed class BossBar : Component, IDrawable
{
    private readonly int _w;
    private readonly Func<(string Name, int Hp, int MaxHp)?> _boss;
    public BossBar(int width, Func<(string Name, int Hp, int MaxHp)?> boss) { _w = width; _boss = boss; }
    public void Draw(UiGfx gfx)
    {
        var b = _boss();
        if (b == null) return;
        int x = (MushTownGame.GameWidth - _w) / 2;
        var head = gfx.Ui.GetSmall($"{b.Value.Name}  {b.Value.Hp} / {b.Value.MaxHp}");
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x - 4, 8, _w + 8, 32), new Color(0, 0, 0, 120));
        gfx.Sb.Draw(head, new Vector2(x + (_w - head.Width) / 2, 10), new Color(255, 220, 0));
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x, 28, _w, 8), Color.Black);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x + 1, 29, Math.Max(1, SceneMath.BarWidth(_w - 2, b.Value.Hp, b.Value.MaxHp)), 6), new Color(220, 60, 60));
    }
}

// Minimap (matches DrawMinimap).
public sealed class Minimap : Component, IDrawable
{
    private readonly Func<MapData> _map;
    private readonly Func<float> _px, _py;
    public Minimap(Func<MapData> map, Func<float> px, Func<float> py) { _map = map; _px = px; _py = py; }
    public void Draw(UiGfx gfx)
    {
        var map = _map();
        var tex = gfx.Tex(map.MiniMap.Image);
        float s = Math.Min(220f / tex.Width, 160f / tex.Height);
        int w = (int)(tex.Width * s), h = (int)(tex.Height * s);
        int x = MushTownGame.GameWidth - 8 - w, y = 8;
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x - 2, y - 2, w + 4, h + 4), new Color(0, 0, 0, 160));
        gfx.Sb.Draw(tex, new Rectangle(x, y, w, h), Color.White);
        foreach (var p in map.Portals)
        {
            var (dx, dy) = map.MiniMap.ToDot(p.X, p.Y, w, h);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(x + dx - 1, y + dy - 1, 3, 3), new Color(80, 160, 255));
        }
        var (px, py) = map.MiniMap.ToDot(_px(), _py(), w, h);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x + px - 1, y + py - 1, 3, 3), new Color(255, 220, 0));
    }
}

// An item cell: a Button that reports kind + index to ClickSlot
// instead of acting directly. Silent: the grab cursor alone
// answers hovers (LegacyId stays "").
public sealed class Slot : Button
{
    public string ItemKind = ""; // carry-match kind (use/worn/bag); sfx Kind stays Silent
    public int Index;
}

// Baked small text at a rect offset (matches the submenu text rows).
public sealed class Label : Component, IDrawable
{
    private readonly string _text;
    private readonly int _dx, _dy;
    public Color Tint = Color.White;
    public Label(string text, int dx, int dy) { _text = text; _dx = dx; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_text), new Vector2(r.X + _dx, r.Y + _dy), Tint);
    }
}

// The menu sign board: header, 3-slice body (the cream center
// stretches), base (matches DrawSign). Needs no rect.
public sealed class Sign : Component, IDrawable
{
    private readonly int _bodyH;
    public Sign(int bodyH) { _bodyH = bodyH; }
    public void Draw(UiGfx gfx)
    {
        var mid = gfx.Tex("menu/sign_mid.png");
        var head = gfx.Tex("menu/sign_top.png");
        var foot = gfx.Tex("menu/sign_bot.png");
        float ms = MenuLayout.Scale;
        int top = (MushTownGame.GameHeight - (head.Height + _bodyH + foot.Height - MenuLayout.OverlapTop - MenuLayout.BotUp)) / 2;
        int body = top + head.Height - MenuLayout.OverlapTop;
        int topS = (MushTownGame.GameHeight - (int)((head.Height + _bodyH + foot.Height - MenuLayout.OverlapTop - MenuLayout.BotUp) * ms)) / 2;
        int msy(int y) => topS + (int)((y - top) * ms);
        int x = (MushTownGame.GameWidth - mid.Width) / 2;
        int y0 = msy(body), y1 = y0 + (int)(MenuLayout.Slice * ms);
        int y3 = msy(body + _bodyH), y2 = y3 - (int)(MenuLayout.Slice * ms);
        int dx = MushTownGame.GameWidth / 2 + (int)((x - MushTownGame.GameWidth / 2) * ms), dw = (int)(mid.Width * ms);
        gfx.Sb.Draw(mid, new Rectangle(dx, y0, dw, y1 - y0),
            new Rectangle(0, 0, mid.Width, MenuLayout.Slice), Color.White);
        gfx.Sb.Draw(mid, new Rectangle(dx, y1, dw, y2 - y1),
            new Rectangle(0, MenuLayout.Slice, mid.Width, mid.Height - MenuLayout.Slice * 2), Color.White);
        gfx.Sb.Draw(mid, new Rectangle(dx, y2, dw, y3 - y2),
            new Rectangle(0, mid.Height - MenuLayout.Slice, mid.Width, MenuLayout.Slice), Color.White);
        gfx.Sb.Draw(head, new Rectangle(MushTownGame.GameWidth / 2 + (int)(((MushTownGame.GameWidth - head.Width) / 2 - MushTownGame.GameWidth / 2) * ms), msy(top),
            (int)(head.Width * ms), (int)(head.Height * ms)), Color.White);
        gfx.Sb.Draw(foot, new Rectangle(MushTownGame.GameWidth / 2 + (int)(((MushTownGame.GameWidth - foot.Width) / 2 - MushTownGame.GameWidth / 2) * ms), msy(body + _bodyH - MenuLayout.BotUp),
            (int)(foot.Width * ms), (int)(foot.Height * ms)), Color.White);
    }
}

// One server row: blank pill plus name left + IP right (matches
// DrawServerRow). Sits on the row button (hover dims the pill).
public sealed class ServerRow : Component, IDrawable
{
    private readonly string _name, _ip;
    private readonly Color _ink, _gray;
    public ServerRow(string name, string ip, Color ink, Color gray) { _name = name; _ip = ip; _ink = ink; _gray = gray; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        float a = gfx.Hovered == Owner ? gfx.Pressed ? MenuAlpha.Pressed : MenuAlpha.Hover : 1f;
        gfx.Sb.Draw(gfx.Tex("menu/btn_blank.png"), r, Color.White * a);
        var tr = gfx.Ui.GetList(_ip);
        int m = (int)(MenuLayout.TextMargin * MenuLayout.Scale);
        var tl = gfx.Ui.GetList(Truncate(gfx, _name, r.Width - m - tr.Width - m - (int)(MenuLayout.TextGap * MenuLayout.Scale)));
        int cy = r.Y + (r.Height - tl.Height) / 2 + (int)(MenuLayout.TextDown * MenuLayout.Scale);
        gfx.Sb.Draw(tl, new Vector2(r.X + m, cy), _ink);
        gfx.Sb.Draw(tr, new Vector2(r.Right - m - tr.Width, cy), _gray);
    }
    private static string Truncate(UiGfx gfx, string text, int maxWidth)
    {
        if (gfx.Ui.GetList(text).Width <= maxWidth) return text;
        while (text.Length > 0 && gfx.Ui.GetList(text + "…").Width > maxWidth) text = text[..^1];
        return text + "…";
    }
}

// Cancel plaque: blank pill plus centered label (matches DrawCancelRow).
public sealed class CancelRow : Component, IDrawable
{
    private readonly Color _ink;
    public CancelRow(Color ink) { _ink = ink; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        float a = gfx.Hovered == Owner ? gfx.Pressed ? MenuAlpha.Pressed : MenuAlpha.Hover : 1f;
        gfx.Sb.Draw(gfx.Tex("menu/btn_blank.png"), r, Color.White * a);
        var t = gfx.Ui.GetListCancel("Cancel");
        int cy = r.Y + (r.Height - t.Height) / 2 + (int)(MenuLayout.CancelDown * MenuLayout.Scale);
        gfx.Sb.Draw(t, new Vector2(r.X + (r.Width - t.Width) / 2, cy), _ink);
    }
}

// Live text centered on cx (matches DrawTextCentered/DrawSmallCentered).
public sealed class CenterText : Component, IDrawable
{
    private readonly Func<string> _text;
    private readonly int _cx, _y;
    private readonly Color _color;
    private readonly bool _small;
    public CenterText(Func<string> text, int cx, int y, Color color, bool small = false) { _text = text; _cx = cx; _y = y; _color = color; _small = small; }
    public void Draw(UiGfx gfx)
    {
        var t = _small ? gfx.Ui.GetSmall(_text()) : gfx.Ui.Get(_text());
        gfx.Sb.Draw(t, new Vector2(_cx - t.Width / 2, _y), _color);
    }
}

// One pager cell: thumb under the art on the current page, then the
// 3-state digit (matches the pager loop). Edge cells pin to the rect,
// digits center (the art overflows the bar, like the official look).
public sealed class PagerCell : Component, IDrawable
{
    private readonly string _stem;
    private readonly bool _current, _centered;
    public PagerCell(string stem, bool current, bool centered) { _stem = stem; _current = current; _centered = centered; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        string st = _current ? "p" : gfx.Hovered == Owner ? gfx.Pressed ? "p" : "h" : "n";
        if (_current) Round.Fill(gfx, new Rectangle(r.X + (r.Width - 30) / 2, r.Y + (r.Height - 30) / 2, 30, 30), 7, new Color(63, 195, 220));
        var t = gfx.Tex($"ui/cs_{_stem}_{st}.png");
        int dx = _centered ? r.X + (r.Width - t.Width) / 2 : r.X;
        int dy = _centered ? r.Y + (r.Height - t.Height) / 2 : r.Y;
        gfx.Sb.Draw(t, new Vector2(dx, dy), Color.White);
    }
}

// Rounded fill over the Owner rect (matches DrawRound).
public sealed class Round : Component, IDrawable
{
    private readonly int _radius;
    private readonly Color _color;
    public Round(int radius, Color color) { _radius = radius; _color = color; }
    public void Draw(UiGfx gfx) => Fill(gfx, Owner.Get<Rect>()!.Box, _radius, _color);
    public static void Fill(UiGfx gfx, Rectangle r, int radius, Color color)
    {
        for (int y = 0; y < r.Height; y++)
        {
            int d = y < radius ? radius - y : y >= r.Height - radius ? y - (r.Height - radius - 1) : 0;
            int inset = d == 0 ? 0 : radius - (int)Math.Sqrt(radius * radius - d * d);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X + inset, r.Y + y, r.Width - inset * 2, 1), color);
        }
    }
}

// Fullscreen dim (matches DrawDim). Needs no rect.
public sealed class Dim : Component, IDrawable
{
    private readonly int _alpha;
    public Dim(int alpha) { _alpha = alpha; }
    public void Draw(UiGfx gfx) =>
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(0, 0, MushTownGame.GameWidth, MushTownGame.GameHeight), new Color(0, 0, 0, _alpha));
}

// White box plus gray border (matches DrawWhitePanel).
public sealed class WhitePanel : Component, IDrawable
{
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Pixel, r, Color.White);
        Border.Draw(gfx, r, new Color(150, 150, 150));
    }
}

// Bridge: runs Menu draws inside the tree (game art the tree does not
// own yet — the chars dolls now, job/setup previews next; gone when
// the sim phase owns the doll).
public sealed class Invoke : Component, IDrawable
{
    private readonly Action<UiGfx> _draw;
    public Invoke(Action<UiGfx> draw) { _draw = draw; }
    public void Draw(UiGfx gfx) => _draw(gfx);
}

// Art button: the png carries its own label, hover is a white overlay
// (matches DrawImgButton). Sits on the button (hover lights it).
public sealed class ImgButton : Component, IDrawable
{
    private readonly string _key;
    public ImgButton(string key) { _key = key; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex(_key), new Vector2(r.X, r.Y), Color.White);
        if (gfx.Hovered == Owner) gfx.Sb.Draw(gfx.Pixel, r, new Color(255, 255, 255, 40));
    }
}

// 2px box border (was DrawBoxBorder; WhitePanel and UiBox reuse the static).
public sealed class Border : Component, IDrawable
{
    private readonly Color _color;
    public Border(Color color) { _color = color; }
    public void Draw(UiGfx gfx) => Draw(gfx, Owner.Get<Rect>()!.Box, _color);
    public static void Draw(UiGfx gfx, Rectangle r, Color color)
    {
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y, r.Width, 2), color);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), color);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y, 2, r.Height), color);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), color);
    }
}

// Name box: white fill, gold when focused, live text or hint (matches
// DrawUiBox). Sits on the name field (focus and text read live).
public sealed class UiBox : Component, IDrawable
{
    private readonly Func<string> _text;
    private readonly string _hint;
    private readonly Func<bool> _focused;
    private readonly Color _ink, _hintInk;
    public UiBox(Func<string> text, string hint, Func<bool> focused, Color ink, Color hintInk)
    {
        _text = text; _hint = hint; _focused = focused; _ink = ink; _hintInk = hintInk;
    }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Pixel, r, Color.White);
        Border.Draw(gfx, r, _focused() ? Color.Gold : new Color(160, 160, 160));
        string text = _text(), show = text == "" ? _hint : text;
        if (show == "") return;
        var tex = gfx.Ui.Get(show);
        gfx.Sb.Draw(tex, new Vector2(r.X + 10, r.Y + (r.Height - tex.Height) / 2), text == "" ? _hintInk : _ink);
    }
}

// Static big text at a fixed spot (setup labels).
public sealed class Text : Component, IDrawable
{
    private readonly string _text;
    private readonly int _x, _y;
    private readonly Color _color;
    public Text(string text, int x, int y, Color color) { _text = text; _x = x; _y = y; _color = color; }
    public void Draw(UiGfx gfx) =>
        gfx.Sb.Draw(gfx.Ui.Get(_text), new Vector2(_x, _y), _color);
}

// Panel title strip: stretched hilong plus the lime caption (stat,
// skill, quest, settings, shop share it). Reads the Owner body rect.
public sealed class TitleBar : Component, IDrawable
{
    private readonly string _title;
    public TitleBar(string title) { _title = title; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex("ui/hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        gfx.Sb.Draw(gfx.Ui.GetSmall(_title), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
    }
}

// One stat row: gray label left, live value right-aligned at +432
// (matches DrawStatRow). Sits on the body with a row dy.
public sealed class StatRow : Component, IDrawable
{
    private readonly string _label;
    private readonly Func<string> _value;
    private readonly int _dy;
    public StatRow(string label, Func<string> value, int dy) { _label = label; _value = value; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_label), new Vector2(r.X + 16, r.Y + _dy), new Color(50, 50, 60));
        var v = gfx.Ui.GetSmall(_value());
        gfx.Sb.Draw(v, new Vector2(r.X + 432 - v.Width, r.Y + _dy), Color.Black);
    }
}

// Right-aligned live text at a body offset (meso coin total, stat
// combat power). The bake picks the font (Meso vs Small).
public sealed class RightLabel : Component, IDrawable
{
    private readonly Func<UiText, string, Texture2D> _bake;
    private readonly Func<string> _text;
    private readonly int _dx, _dy;
    private readonly Color _color;
    public RightLabel(Func<UiText, string, Texture2D> bake, Func<string> text, int dx, int dy, Color color)
    {
        _bake = bake; _text = text; _dx = dx; _dy = dy; _color = color;
    }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var t = _bake(gfx.Ui, _text());
        gfx.Sb.Draw(t, new Vector2(r.X + _dx - t.Width, r.Y + _dy), _color);
    }
}

// Live skill numbers per draw (level/SP/cooldown/buff/orbs change
// without a scene rebuild, so the row reads them every frame).
public readonly record struct SkillLive(int Lv, int Sp, int Level, float CdMs, float BuffMs, int Orbs);

// One skill row: icon (gray when locked/cooling), name, sub line,
// right side (req level / live seconds / key / [+]). Matches
// DrawSkillRow; reads the Owner row rect.
public sealed class SkillRow : Component, IDrawable
{
    private readonly SkillData _skill;
    private readonly string? _key;
    private readonly string _blurb;
    private readonly Func<SkillLive> _live;
    public SkillRow(SkillData skill, string? key, string blurb, Func<SkillLive> live)
    {
        _skill = skill; _key = key; _blurb = blurb; _live = live;
    }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var s = _live();
        bool locked = s.Level < _skill.ReqLevel;
        bool maxed = s.Lv >= _skill.MaxLevel;
        bool learnable = !locked && !maxed && s.Sp > 0;
        bool cooling = !locked && s.CdMs > 0;
        bool live = s.BuffMs > 0;
        gfx.Sb.Draw(gfx.Tex(locked || cooling ? _skill.Disabled : _skill.Icon), new Vector2(r.X + 10, r.Y + 8), Color.White);
        var ink = locked ? new Color(130, 140, 150) : Color.White;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_skill.Name), new Vector2(r.X + 50, r.Y + 4), ink);
        string sub = _skill.Kind == SkillData.KindPassive
            ? $"Lv.{s.Lv}/{_skill.MaxLevel}  {_blurb}"
            : _skill.Kind == SkillData.KindBuff
            ? $"Lv.{s.Lv}/{_skill.MaxLevel}  MP {_skill.Mp}  {_skill.BuffMs / 1000}s{(_skill.SkillId == SkillData.ComboAttack && live ? $"  {s.Orbs} orbs" : "")}"
            : $"Lv.{s.Lv}/{_skill.MaxLevel}  MP {_skill.Mp}  CD {_skill.CooldownMs / 1000}s";
        gfx.Sb.Draw(gfx.Ui.GetSmall(sub), new Vector2(r.X + 50, r.Y + 24), ink);
        string right = locked ? $"Lv.{_skill.ReqLevel}"
            : live ? $"{(int)Math.Ceiling(s.BuffMs / 1000)}s"
            : cooling ? $"{(int)Math.Ceiling(s.CdMs / 1000)}s"
            : _key ?? $"{s.Lv}/{_skill.MaxLevel}";
        var rc = locked ? new Color(255, 120, 120)
            : live || cooling ? new Color(255, 220, 0) : Color.White;
        if (learnable) { right += " [+]"; rc = new Color(120, 255, 120); }
        var rt = gfx.Ui.GetSmall(right);
        gfx.Sb.Draw(rt, new Vector2(r.X + r.Width - 8 - rt.Width, r.Y + 4), rc);
    }
}

// One inventory cell: gold tint when worn, icon centered, count
// bottom-right (matches DrawWornSlot/DrawInvSlot). Empty cells draw
// nothing (counts read live: drops and drinks skip scene rebuilds).
public sealed class InvSlot : Component, IDrawable
{
    private readonly string _image;
    private readonly Func<int> _count;
    private readonly bool _worn;
    public InvSlot(string image, Func<int> count, bool worn) { _image = image; _count = count; _worn = worn; }
    public void Draw(UiGfx gfx)
    {
        int n = _count();
        if (n <= 0) return;
        var r = Owner.Get<Rect>()!.Box;
        if (_worn) gfx.Sb.Draw(gfx.Pixel, r, new Color(255, 220, 0, 60));
        var icon = gfx.Tex(_image);
        gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
        var t = gfx.Ui.GetSmall(n.ToString());
        gfx.Sb.Draw(t, new Vector2(r.X + r.Width - t.Width - 2, r.Y + r.Height - t.Height - 2), Color.Black);
    }
}

// Plain body fill under the title strip (shop + talk bodies follow
// the content height, so they take no fixed rounded backing).
public sealed class Fill : Component, IDrawable
{
    private readonly Color _color;
    private readonly int _top;
    public Fill(Color color, int top) { _color = color; _top = top; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y + _top, r.Width, r.Height - _top), _color);
    }
}

// Hover-aware flat button (was DrawShopButton + the talk buttons).
// Reads the legacy hover id, not gfx.HoverButton: a front panel wins
// the hover loop and the scenes behind keep stale hover state, so
// the per-scene value would light covered buttons (legacy-exact).
public sealed class FlatButton : Component, IDrawable
{
    private readonly string _label, _id;
    private readonly Func<string> _hover;
    public FlatButton(string label, string id, Func<string> hover) { _label = label; _id = id; _hover = hover; }
    public void Draw(UiGfx gfx)
    {
        var b = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Pixel, b, _hover() == _id ? new Color(52, 88, 110) : new Color(30, 50, 66));
        var t = gfx.Ui.GetSmall(_label);
        gfx.Sb.Draw(t, new Vector2(b.X + (b.Width - t.Width) / 2, b.Y + 5), Color.White);
    }
}

// Settings [-]/[+] box: dark fill, glyph centered at +4 (matches DrawSettingsPanel).
public sealed class StepBox : Component, IDrawable
{
    private readonly string _glyph;
    public StepBox(string glyph) => _glyph = glyph;
    public void Draw(UiGfx gfx)
    {
        var b = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Pixel, b, new Color(0, 0, 0, 120));
        var t = gfx.Ui.GetSmall(_glyph);
        gfx.Sb.Draw(t, new Vector2(b.X + (b.Width - t.Width) / 2, b.Y + 4), Color.White);
    }
}

// One shop row: icon centered in a 32px cell, name + price at +52
// (matches DrawShopIcon + the row text). Strings bake at build: the
// sync gate rebuilds when stock or sell rows change.
public sealed class ShopRow : Component, IDrawable
{
    private readonly string _image, _line1, _line2;
    public ShopRow(string image, string line1, string line2) { _image = image; _line1 = line1; _line2 = line2; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var icon = gfx.Tex(_image);
        gfx.Sb.Draw(icon, new Vector2(r.X + 12 + (32 - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
        gfx.Sb.Draw(gfx.Ui.GetSmall(_line1), new Vector2(r.X + 52, r.Y + 4), Color.White);
        gfx.Sb.Draw(gfx.Ui.GetSmall(_line2), new Vector2(r.X + 52, r.Y + 20), new Color(130, 140, 150));
    }
}

// Live text in a live color (quest rows answer to select/done, talk
// entries to offer/ready; both change without a scene rebuild).
public sealed class LiveTint : Component, IDrawable
{
    private readonly Func<(string Text, Color Ink)> _row;
    private readonly int _dx, _dy;
    public LiveTint(Func<(string Text, Color Ink)> row, int dx, int dy) { _row = row; _dx = dx; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var (text, ink) = _row();
        gfx.Sb.Draw(gfx.Ui.GetSmall(text), new Vector2(r.X + _dx, r.Y + _dy), ink);
    }
}

// Selected-quest block per draw (null when the block hides: no chara,
// unknown sel, or sel not accepted). Lines ride the Hud wrap cache.
public readonly record struct QuestLive(List<string> Lines, List<(string Text, bool Done)> Kills, string TurnIn);

// Selected-quest detail: wrapped lines yield to kill rows (the block
// holds 10), then the turn-in line (matches DrawQuestPanel).
public sealed class QuestDetail : Component, IDrawable
{
    private readonly Func<QuestLive?> _data;
    private readonly int _dy;
    public QuestDetail(Func<QuestLive?> data, int dy) { _data = data; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var d = _data();
        if (d == null) return;
        var r = Owner.Get<Rect>()!.Box;
        int y = r.Y + _dy;
        foreach (string line in d.Value.Lines.Take(10 - d.Value.Kills.Count))
        {
            gfx.Sb.Draw(gfx.Ui.GetSmall(line), new Vector2(r.X + 12, y), Color.White);
            y += 16;
        }
        foreach (var k in d.Value.Kills)
        {
            gfx.Sb.Draw(gfx.Ui.GetSmall(k.Text), new Vector2(r.X + 12, y), k.Done ? new Color(120, 255, 120) : Color.White);
            y += 16;
        }
        gfx.Sb.Draw(gfx.Ui.GetSmall(d.Value.TurnIn), new Vector2(r.X + 12, y), new Color(255, 220, 0));
    }
}

// Talk body lines at +16, step 16 (reads the Hud wrap cache).
public sealed class TalkBody : Component, IDrawable
{
    private readonly Func<List<string>> _lines;
    private readonly int _dy;
    public TalkBody(Func<List<string>> lines, int dy) { _lines = lines; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        int y = r.Y + _dy;
        foreach (string line in _lines())
        {
            gfx.Sb.Draw(gfx.Ui.GetSmall(line), new Vector2(r.X + 16, y), Color.White);
            y += 16;
        }
    }
}
