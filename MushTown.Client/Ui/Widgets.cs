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

// The official nine piece frame: the corners stay, the edges and the center
// stretch. The art names the pieces (ui/<stem>_nw.png and so on) and the
// corner piece gives the border size.
public static class NineSlice
{
    public static void Draw(UiGfx gfx, string stem, Rectangle r)
    {
        var nw = gfx.Tex("ui/" + stem + "_nw.png"); var ne = gfx.Tex("ui/" + stem + "_ne.png");
        var sw = gfx.Tex("ui/" + stem + "_sw.png"); var se = gfx.Tex("ui/" + stem + "_se.png");
        var n = gfx.Tex("ui/" + stem + "_n.png"); var s = gfx.Tex("ui/" + stem + "_s.png");
        var w = gfx.Tex("ui/" + stem + "_w.png"); var e = gfx.Tex("ui/" + stem + "_e.png");
        var c = gfx.Tex("ui/" + stem + "_c.png");
        gfx.Sb.Draw(nw, new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(ne, new Vector2(r.Right - ne.Width, r.Y), Color.White);
        gfx.Sb.Draw(sw, new Vector2(r.X, r.Bottom - sw.Height), Color.White);
        gfx.Sb.Draw(se, new Vector2(r.Right - se.Width, r.Bottom - se.Height), Color.White);
        gfx.Sb.Draw(n, new Rectangle(r.X + nw.Width, r.Y, r.Width - nw.Width - ne.Width, n.Height), Color.White);
        gfx.Sb.Draw(s, new Rectangle(r.X + sw.Width, r.Bottom - s.Height, r.Width - sw.Width - se.Width, s.Height), Color.White);
        gfx.Sb.Draw(w, new Rectangle(r.X, r.Y + nw.Height, w.Width, r.Height - nw.Height - sw.Height), Color.White);
        gfx.Sb.Draw(e, new Rectangle(r.Right - e.Width, r.Y + ne.Height, e.Width, r.Height - ne.Height - se.Height), Color.White);
        gfx.Sb.Draw(c, new Rectangle(r.X + w.Width, r.Y + n.Height, r.Width - w.Width - e.Width, r.Height - n.Height - s.Height), Color.White);
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
    public bool HoverFill; // white-30 overlay when hovered (the hotkey cell)
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

// Quest tracker lines (matches DrawTracker). The lines are right aligned:
// the right edge comes from the layout file.
public sealed class Tracker : Component, IDrawable
{
    private readonly Func<List<string>> _lines;
    private readonly int _right, _top;
    public Tracker(Func<List<string>> lines, int right, int top) { _lines = lines; _right = right; _top = top; }
    public void Draw(UiGfx gfx)
    {
        int y = _top;
        var lines = _lines();
        for (int i = 0; i < lines.Count; i++) // name/detail pairs: even lines are gold names
        {
            var t = gfx.Ui.GetSmall(lines[i]);
            int x = _right - t.Width;
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(x - 4, y - 2, t.Width + 8, 16), new Color(0, 0, 0, 120));
            gfx.Sb.Draw(t, new Vector2(x, y), i % 2 == 0 ? new Color(255, 220, 0) : Color.White);
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

// The map dots: the portals (blue) and the player (yellow). The minimap
// window and the big map window draw the same dots.
public static class MapDots
{
    public static void Draw(UiGfx gfx, MapData map, float playerX, float playerY, int x, int y, int w, int h)
    {
        foreach (var p in map.Portals)
        {
            var (dx, dy) = map.MiniMap.ToDot(p.X, p.Y, w, h);
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(x + dx - 1, y + dy - 1, 3, 3), new Color(80, 160, 255));
        }
        var (ux, uy) = map.MiniMap.ToDot(playerX, playerY, w, h);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x + ux - 1, y + uy - 1, 3, 3), new Color(255, 220, 0));
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

// Baked small text at a rect offset (matches the HUD label rows).
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

// 2px box border (was DrawBoxBorder; WhitePanel uses it).
public static class Border
{
    public static void Draw(UiGfx gfx, Rectangle r, Color color)
    {
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y, r.Width, 2), color);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), color);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X, r.Y, 2, r.Height), color);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), color);
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

// Talk body lines: the text of the dialogue box (reads the Hud wrap cache).
public sealed class TalkBody : Component, IDrawable
{
    private readonly Func<List<string>> _lines;
    private readonly int _dx, _dy, _step;
    public TalkBody(Func<List<string>> lines, int dx, int dy, int step) { _lines = lines; _dx = dx; _dy = dy; _step = step; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        int y = r.Y + _dy;
        foreach (string line in _lines())
        {
            gfx.Sb.Draw(gfx.Ui.GetSmall(line), new Vector2(r.X + _dx, y), Color.Black);
            y += _step;
        }
    }
}

// The NPC dialogue box (UI/UIWindow2.img/UtilDlgEx): the top piece with
// the name tab, the middle piece that repeats for every line and the
// bottom piece with the blue button bar.
public sealed class TalkFrame : Component, IDrawable
{
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var top = gfx.Tex("ui/talk_dlg_top.png");
        var mid = gfx.Tex("ui/talk_dlg_mid.png");
        var bottom = gfx.Tex("ui/talk_dlg_bottom.png");
        gfx.Sb.Draw(top, new Vector2(r.X, r.Y), Color.White);
        int y = r.Y + top.Height;
        int bottomY = r.Bottom - bottom.Height;
        while (y < bottomY)
        {
            gfx.Sb.Draw(mid, new Vector2(r.X, y), Color.White);
            y += mid.Height;
        }
        gfx.Sb.Draw(bottom, new Vector2(r.X, bottomY), Color.White);
    }
}

// One dialogue button: the official blank button art (UtilDlgEx
// BtOK/BtClose/BtYes/BtNo) with the label written on it.
public sealed class TalkButton : Component, IDrawable
{
    private readonly string _stem, _label, _id;
    private readonly Func<string> _hover;
    private readonly int _dy;
    public TalkButton(string stem, string label, string id, Func<string> hover, int dy) { _stem = stem; _label = label; _id = id; _hover = hover; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var b = Owner.Get<Rect>()!.Box;
        string state = gfx.Pressed ? "_p.png" : _hover() == _id ? "_h.png" : "_n.png";
        gfx.Sb.Draw(gfx.Tex($"ui/{_stem}{state}"), new Vector2(b.X, b.Y), Color.White);
        var t = gfx.Ui.GetSmall(_label);
        gfx.Sb.Draw(t, new Vector2(b.X + (b.Width - t.Width) / 2, b.Y + _dy), Color.Black);
    }
}

// The badge at the right of a MENU row: the official cyan square with the
// letter of the key (UI/UITotalMenu.img/main/key/<code>, 16x16). The code
// is the official key code, not the ASCII value of the key.
public sealed class MenuKeyBadge : Component, IDrawable
{
    private readonly Func<string> _key;
    public MenuKeyBadge(Func<string> key) { _key = key; }
    public void Draw(UiGfx gfx)
    {
        string art = Art(_key());
        if (art == "") return;
        gfx.Sb.Draw(gfx.Tex(art), Owner.Get<Rect>()!.Box, Color.White);
    }

    // The badge art of a key name ("" when the key has no badge).
    public static string Art(string keyName)
    {
        string first = keyName.Split('+', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        int code = Codes.GetValueOrDefault(first, 0);
        if (code == 0 || !Badges.Codes.Contains(code)) return "";
        return $"ui/menu_key_{code}.png";
    }

    // Load every badge (the player can bind any key).
    public static void LoadBadges(MushTownGame game)
    {
        foreach (int code in Badges.Codes) game.LoadTexture($"ui/menu_key_{code}.png");
    }

    // assets/ui/menu_keys.json: the key codes that have a badge. The list
    // comes from the exporter, so a MapleStory version without a badge does
    // not break the game.
    private sealed class BadgeFile
    {
        public List<int> Codes { get; set; } = new();
    }

    private static BadgeFile? _badgeFile;
    private static BadgeFile Badges => _badgeFile ??= MushTownGame.LoadJson<BadgeFile>(
        Path.Combine(Path.GetDirectoryName(MushTownGame.LayoutsDir)!, "assets", "ui", "menu_keys.json"));

    // The official key code of a key name (the badge table of the WZ).
    private static readonly Dictionary<string, int> Codes = BuildCodes();

    private static Dictionary<string, int> BuildCodes()
    {
        var map = new Dictionary<string, int>
        {
            ["Escape"] = 1,
            ["OemMinus"] = 12,
            ["OemPlus"] = 13,
            ["OemOpenBrackets"] = 26,
            ["OemCloseBrackets"] = 27,
            ["LeftControl"] = 29,
            ["RightControl"] = 29,
            ["OemSemicolon"] = 39,
            ["OemQuotes"] = 40,
            ["OemTilde"] = 41,
            ["LeftShift"] = 42,
            ["RightShift"] = 42,
            ["Up"] = 43,
            ["OemComma"] = 51,
            ["OemPeriod"] = 52,
            ["LeftAlt"] = 56,
            ["RightAlt"] = 56,
            ["Enter"] = 57,
            ["Scroll"] = 70,
            ["Home"] = 71,
            ["PageUp"] = 73,
            ["End"] = 79,
            ["PageDown"] = 81,
            ["Insert"] = 82,
            ["Delete"] = 83,
            ["F11"] = 87,
            ["F12"] = 88,
        };
        string digits = "1234567890";
        for (int i = 0; i < 10; i++) map["D" + digits[i]] = 2 + i;
        string top = "QWERTYUIOP";
        for (int i = 0; i < 10; i++) map[top[i].ToString()] = 16 + i;
        string home = "ASDFGHJKL";
        for (int i = 0; i < 9; i++) map[home[i].ToString()] = 30 + i;
        string bottom = "ZXCVBNM";
        for (int i = 0; i < 7; i++) map[bottom[i].ToString()] = 44 + i;
        for (int i = 0; i < 10; i++) map["F" + (i + 1)] = 59 + i;
        return map;
    }
}

// The scroll bar of the chat log (UI/StatusBar3.img/chat/ingame/view/
// scroll): the track (base_t/base_c/base_b) and the thumb. The up and the
// down arrow art of this WZ is empty, so there is nothing to draw there.
// The position is 0 at the top of the track and 1 at the bottom.
// The art names are the chat ones: the component lives with the chat.
public sealed class ChatScrollBar : Component, IDrawable
{
    private readonly Func<float> _position;
    private readonly Func<bool> _enabled; // the disabled bar has no thumb
    public ChatScrollBar(Func<float> position, Func<bool> enabled) { _position = position; _enabled = enabled; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        string stem = _enabled() ? "ui/chat_scroll" : "ui/chat_scroll_off";
        var top = gfx.Tex(stem + "_base_t.png");
        var mid = gfx.Tex(stem + "_base_c.png");
        var bot = gfx.Tex(stem + "_base_b.png");
        gfx.Sb.Draw(top, new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(mid, new Rectangle(r.X, r.Y + top.Height, r.Width, r.Height - top.Height - bot.Height), Color.White);
        gfx.Sb.Draw(bot, new Vector2(r.X, r.Bottom - bot.Height), Color.White);
        if (!_enabled()) return;
        string thumb = gfx.Hovered == Owner ? gfx.Pressed ? "ui/chat_scroll_thumb2.png" : "ui/chat_scroll_thumb1.png" : "ui/chat_scroll_thumb0.png";
        var t = gfx.Tex(thumb);
        int y = r.Y + (int)((r.Height - t.Height) * _position());
        gfx.Sb.Draw(t, new Vector2(r.X, y), Color.White);
    }
}
