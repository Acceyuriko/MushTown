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
    public Label(string text, int dx, int dy) { _text = text; _dx = dx; _dy = dy; }
    public override void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Ui.GetSmall(_text), new Vector2(r.X + _dx, r.Y + _dy), Color.White);
    }
}
