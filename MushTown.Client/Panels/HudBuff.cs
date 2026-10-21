using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The buff bar at the top right of the HUD: the official favoriteBuff frame
// (UI/StatusBar3.img/BuffSetting) around the live buff icons. Each icon is
// the icon of the buffed skill, with the time left on it.
public partial class MushTownGame
{
    // Every number of the bar lives in layouts/panel-buff.json.
    internal static readonly PanelLayout _buffLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-buff.json"));

    // The frame art (MushTown.Exporter ExportBuffUi). Missing files fail loud.
    private static readonly string[] BuffFrameFiles = new[]
    {
        "buff_frame_nw.png", "buff_frame_n.png", "buff_frame_ne.png",
        "buff_frame_sw.png", "buff_frame_s.png", "buff_frame_se.png",
        "buff_frame_w.png", "buff_frame_c.png", "buff_frame_e.png",
    };

    // The live buffs: the skill id and the milliseconds left, in id order.
    private List<(int id, float ms)> LiveBuffs()
    {
        var list = new List<(int id, float ms)>();
        foreach (var kv in _fighter.Buffs) list.Add((kv.Key, kv.Value));
        list.Sort((a, b) => a.id.CompareTo(b.id));
        return list;
    }

    // The buff bar object. The HUD chain puts it in the game scene.
    private GameObject BuildBuffBar()
    {
        foreach (string f in BuffFrameFiles) LoadTexture("ui/" + f);
        var bar = new GameObject { NoHit = true }; // art only: the clicks go through
        bar.Add(new Rect(BuffBarRect()));
        bar.Add(new BuffBarArt(() => LiveBuffs(), id => _skills[id].Icon));
        return bar;
    }

    // The bar box: the right edge is fixed, the frame grows to the left.
    private Rectangle BuffBarRect()
    {
        int right = _buffLay.Get("right", 1362);
        int top = _buffLay.Get("top", 0);
        int maxW = _buffLay.Get("maxWidth", 240);
        int h = _buffLay.Get("pad", 5) * 2 + _buffLay.Get("iconSize", 32);
        return new Rectangle(right - maxW, top, maxW, h);
    }
}

// The buff bar art: the official frame around the live buff icons (the skill
// icon of each buff) with the time left on the icon.
public sealed class BuffBarArt : Component, IDrawable
{
    private readonly Func<List<(int id, float ms)>> _buffs;
    private readonly Func<int, string> _icon;
    public BuffBarArt(Func<List<(int id, float ms)>> buffs, Func<int, string> icon) { _buffs = buffs; _icon = icon; }

    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._buffLay;
        var buffs = _buffs();
        if (buffs.Count == 0) return; // no buff, no bar
        var box = Owner.Get<Rect>()!.Box;
        int pad = lay.Get("pad", 5);
        int size = lay.Get("iconSize", 32);
        int pitch = lay.Get("iconPitch", 39);
        int width = pad * 2 + size + (buffs.Count - 1) * pitch;
        int x = box.Right - width;
        DrawFrame(gfx, new Rectangle(x, box.Y, width, pad * 2 + size));
        for (int i = 0; i < buffs.Count; i++)
        {
            int ix = x + pad + i * pitch;
            gfx.Sb.Draw(gfx.Tex(_icon(buffs[i].id)), new Vector2(ix, box.Y + pad), Color.White);
            var t = gfx.Ui.GetSmall(BuffTimeText(buffs[i].ms));
            int tx = ix + size - t.Width - lay.Get("timeRight", 1);
            int ty = box.Y + pad + size - t.Height - lay.Get("timeBottom", 1);
            gfx.Sb.Draw(t, new Vector2(tx - 1, ty), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx + 1, ty), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx, ty - 1), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx, ty + 1), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx, ty), Color.White);
        }
    }

    // The time left on a buff: minutes and seconds ("2:00").
    private static string BuffTimeText(float ms)
    {
        int s = (int)Math.Ceiling(ms / 1000);
        return $"{s / 60}:{s % 60:00}";
    }

    // The official nine piece frame: the corners, the edges and the center.
    private static void DrawFrame(UiGfx gfx, Rectangle r)
    {
        var nw = gfx.Tex("ui/buff_frame_nw.png"); var ne = gfx.Tex("ui/buff_frame_ne.png");
        var sw = gfx.Tex("ui/buff_frame_sw.png"); var se = gfx.Tex("ui/buff_frame_se.png");
        var n = gfx.Tex("ui/buff_frame_n.png"); var s = gfx.Tex("ui/buff_frame_s.png");
        var w = gfx.Tex("ui/buff_frame_w.png"); var e = gfx.Tex("ui/buff_frame_e.png");
        var c = gfx.Tex("ui/buff_frame_c.png");
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
