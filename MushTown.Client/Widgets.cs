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
public sealed class Panel : Component
{
    private readonly string _top, _mid, _bot;
    public Panel(string top, string mid, string bot) { _top = top; _mid = mid; _bot = bot; }
    public override void Draw(UiGfx gfx)
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

public class Button : Component
{
    public string LegacyId = "";
    public ButtonKind Kind; // Silent by default (slots, titles, furniture, gated clicks)
    public bool HoverFill; // white-30 overlay when hovered (text rows)
    public Action? OnClick;
    public void Press() => OnClick?.Invoke();
    public override void Draw(UiGfx gfx)
    {
        if (HoverFill && gfx.HoverButton == this)
            gfx.Sb.Draw(gfx.Pixel, Owner.Get<Rect>()!.Box, new Color(255, 255, 255, 30));
    }
}

// State art: stem + _n/_h/_p by hover/pressed (the HUD buttons moved here from DrawHudButton).
public sealed class Image : Component
{
    private readonly string _stem;
    public Image(string stem) => _stem = stem;
    public override void Draw(UiGfx gfx)
    {
        string key = _stem + (gfx.Hovered == Owner ? (gfx.Pressed ? "_p.png" : "_h.png") : "_n.png");
        gfx.Sb.Draw(gfx.Tex(key), Owner.Get<Rect>()!.Box, Color.White);
    }
}

// Bar visual: full backing, partial fill by live value/max, full cover
// on top. The fill starts at the rect's top-left (matches DrawExpBar).
public sealed class Bar : Component
{
    private readonly string _back, _fill, _cover;
    private readonly Func<(int Value, int Max)> _amount;
    public Bar(string back, string fill, string cover, Func<(int Value, int Max)> amount)
    {
        _back = back; _fill = fill; _cover = cover; _amount = amount;
    }
    public override void Draw(UiGfx gfx)
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
public sealed class Picture : Component
{
    private readonly string _key;
    private readonly int _dx, _dy;
    private readonly bool _stretch;
    public Picture(string key) { _key = key; _stretch = true; }
    public Picture(string key, int dx, int dy) { _key = key; _dx = dx; _dy = dy; }
    public override void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        if (_stretch) gfx.Sb.Draw(gfx.Tex(_key), r, Color.White);
        else gfx.Sb.Draw(gfx.Tex(_key), new Vector2(r.X + _dx, r.Y + _dy), Color.White);
    }
}

// Live baked text at a rect offset (UiText caches by string, so steady
// text costs one lookup; changing text adds entries like the old draws).
public sealed class LiveLabel : Component
{
    private readonly Func<string> _text;
    private readonly int _dx, _dy;
    private readonly Color _color;
    public LiveLabel(Func<string> text, int dx, int dy, Color color) { _text = text; _dx = dx; _dy = dy; _color = color; }
    public override void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_text()), new Vector2(r.X + _dx, r.Y + _dy), _color);
    }
}

// Plate level digits: WZ levelPos stepping (matches DrawPlate).
public sealed class PlateLevel : Component
{
    private readonly Func<int> _level;
    public PlateLevel(Func<int> level) => _level = level;
    public override void Draw(UiGfx gfx)
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
public sealed class BarNumber : Component
{
    private readonly Func<string> _text;
    private readonly int _centerDx, _dy;
    public BarNumber(Func<string> text, int centerDx, int dy) { _text = text; _centerDx = centerDx; _dy = dy; }
    public override void Draw(UiGfx gfx)
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
public sealed class Tracker : Component
{
    private readonly Func<List<string>> _lines;
    public Tracker(Func<List<string>> lines) => _lines = lines;
    public override void Draw(UiGfx gfx)
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
public sealed class BossBar : Component
{
    private readonly int _w;
    private readonly Func<(string Name, int Hp, int MaxHp)?> _boss;
    public BossBar(int width, Func<(string Name, int Hp, int MaxHp)?> boss) { _w = width; _boss = boss; }
    public override void Draw(UiGfx gfx)
    {
        var b = _boss();
        if (b == null) return;
        int x = (1366 - _w) / 2;
        var head = gfx.Ui.GetSmall($"{b.Value.Name}  {b.Value.Hp} / {b.Value.MaxHp}");
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x - 4, 8, _w + 8, 32), new Color(0, 0, 0, 120));
        gfx.Sb.Draw(head, new Vector2(x + (_w - head.Width) / 2, 10), new Color(255, 220, 0));
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x, 28, _w, 8), Color.Black);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(x + 1, 29, Math.Max(1, SceneMath.BarWidth(_w - 2, b.Value.Hp, b.Value.MaxHp)), 6), new Color(220, 60, 60));
    }
}

// Minimap (matches DrawMinimap).
public sealed class Minimap : Component
{
    private readonly Func<MapData> _map;
    private readonly Func<float> _px, _py;
    public Minimap(Func<MapData> map, Func<float> px, Func<float> py) { _map = map; _px = px; _py = py; }
    public override void Draw(UiGfx gfx)
    {
        var map = _map();
        var tex = gfx.Tex(map.MiniMap.Image);
        float s = Math.Min(220f / tex.Width, 160f / tex.Height);
        int w = (int)(tex.Width * s), h = (int)(tex.Height * s);
        int x = 1366 - 8 - w, y = 8;
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
public sealed class Label : Component
{
    private readonly string _text;
    private readonly int _dx, _dy;
    public Color Tint = Color.White;
    public Label(string text, int dx, int dy) { _text = text; _dx = dx; _dy = dy; }
    public override void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_text), new Vector2(r.X + _dx, r.Y + _dy), Tint);
    }
}
