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

    // The art sizes: the skill icon is 32x32, the frame border is 5px.
    public const int BuffIconSize = 32;
    public const int BuffFramePad = 5;
    private const int BuffFrameHeight = BuffFramePad * 2 + BuffIconSize;
    // The live buffs: the skill id and the milliseconds left, in id order.
    private readonly List<(int id, float ms)> _liveBuffs = new();

    // The buff bar object. The HUD chain puts it in the game scene.
    private GameObject BuildBuffBar()
    {
        foreach (string f in BuffFrameFiles) LoadTexture("ui/" + f);
        var bar = new GameObject { NoHit = true }; // art only: the clicks go through
        bar.Add(new Rect(BuffBarRect()));
        bar.Add(new BuffBarArt(() => LiveBuffs(), id => _skills[id].Icon));
        return bar;
    }

    // The live buffs of this frame: the fighter holds the real times.
    private List<(int id, float ms)> LiveBuffs()
    {
        _liveBuffs.Clear();
        foreach (var kv in _fighter.Buffs) _liveBuffs.Add((kv.Key, kv.Value));
        _liveBuffs.Sort((a, b) => a.id.CompareTo(b.id));
        return _liveBuffs;
    }

    // The bar box: the right edge is fixed, the frame grows to the left.
    // The width is the widest the bar gets (the official max count).
    private Rectangle BuffBarRect()
    {
        int right = _buffLay.Get("right", 1362);
        int top = _buffLay.Get("top", 0);
        int w = _buffLay.Get("iconPitch", 39) * _buffLay.Get("maxCount", 10);
        return new Rectangle(right - w, top, w, BuffFrameHeight);
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
        var box = Owner.Get<Rect>()!.Screen;
        int pad = MushTownGame.BuffFramePad; // the border of the frame art
        int size = MushTownGame.BuffIconSize; // the skill icon is 32x32
        int pitch = lay.Get("iconPitch", 39);
        int width = pad * 2 + size + (buffs.Count - 1) * pitch;
        int x = box.Right - width;
        NineSlice.Draw(gfx, "buff_frame", new Rectangle(x, box.Y, width, pad * 2 + size));
        for (int i = 0; i < buffs.Count; i++)
        {
            int ix = x + pad + i * pitch;
            gfx.Sb.Draw(gfx.Tex(_icon(buffs[i].id)), new Vector2(ix, box.Y + pad), Color.White);
            var t = gfx.Ui.GetSmall(BuffTimeText(buffs[i].ms));
            int tx = ix + size - t.Width - lay.Get("timeRight", 1);
            int ty = box.Y + pad + size - t.Height - lay.Get("timeBottom", 1);
            int sh = lay.Get("shadow", 1);
            gfx.Sb.Draw(t, new Vector2(tx - sh, ty), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx + sh, ty), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx, ty - sh), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx, ty + sh), new Color(0, 0, 0, 200));
            gfx.Sb.Draw(t, new Vector2(tx, ty), Color.White);
        }
    }

    // The time left on a buff: minutes and seconds ("2:00").
    private static string BuffTimeText(float ms)
    {
        int s = (int)Math.Ceiling(ms / 1000);
        return $"{s / 60}:{s % 60:00}";
    }
}
