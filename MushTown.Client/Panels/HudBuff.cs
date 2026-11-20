using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The buff bar at the top right of the HUD: the live buff icons side by side, no frame and no padding.
// Each icon is the icon of the buffed skill, with the time left on it.
public partial class MushTownGame
{
    // Every number of the bar lives in layouts/panel-buff.json.
    internal static readonly PanelLayout _buffLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-buff.json"));

    // The art size: the skill icon is 32x32.
    public const int BuffIconSize = 32;
    // The live buffs: the skill id and the milliseconds left, in id order.
    private readonly List<(int id, float ms)> _liveBuffs = new();

    // The buff bar object. The HUD chain puts it in the game scene.
    private GameObject BuildBuffBar()
    {
        if (_wzSkills.TryGetValue(WzSkillData.ElementalDrain, out var drain)) // the layer icons are drawn live: load them now
            foreach (string f in drain.NumberFrames) LoadTexture(f);
        var bar = new GameObject { NoHit = true }; // art only: the clicks go through
        bar.Add(new Rect(BuffBarRect()));
        bar.Add(new BuffBarArt(() => LiveBuffs(), id => _skills[id].Icon,
            () => CurrentDrainLayers(), DrainIcon));
        return bar;
    }

    // The Elemental Drain buff icon at this many layers: the WZ number icon (its top-right digit is the count). Empty if the
    // art was not exported; the callers skip an empty icon.
    private string DrainIcon(int layers)
    {
        if (!_wzSkills.TryGetValue(WzSkillData.ElementalDrain, out var wz) || wz.NumberFrames.Count == 0) return "";
        return wz.NumberFrames[layers - 1];
    }

    // The live buffs of this frame: the fighter holds the real times.
    private List<(int id, float ms)> LiveBuffs()
    {
        _liveBuffs.Clear();
        foreach (var kv in _fighter.Buffs) _liveBuffs.Add((kv.Key, kv.Value));
        _liveBuffs.Sort((a, b) => a.id.CompareTo(b.id));
        return _liveBuffs;
    }

    // The bar box: the right edge is fixed, the icons grow to the left.
    // The width is the widest the bar gets (the official max count).
    private Rectangle BuffBarRect()
    {
        int right = _buffLay.Get("right", 1357);
        int top = _buffLay.Get("top", 5);
        int w = _buffLay.Get("iconPitch", 32) * _buffLay.Get("maxCount", 10);
        return new Rectangle(right - w, top, w, BuffIconSize);
    }
}

// The buff bar art: the live buff icons (the skill icon of each buff) with the time left on the icon. Elemental Drain rides in
// front as its WZ number icon (the layer count is drawn on the icon; no time).
public sealed class BuffBarArt : Component, IDrawable
{
    private readonly Func<List<(int id, float ms)>> _buffs;
    private readonly Func<int, string> _icon;
    private readonly Func<int> _drainLayers;
    private readonly Func<int, string> _drainIcon;
    public BuffBarArt(Func<List<(int id, float ms)>> buffs, Func<int, string> icon, Func<int> drainLayers, Func<int, string> drainIcon)
    {
        _buffs = buffs; _icon = icon; _drainLayers = drainLayers; _drainIcon = drainIcon;
    }

    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._buffLay;
        var entries = new List<(string image, string? time)>();
        int layers = _drainLayers();
        if (layers > 0) { var img = _drainIcon(layers); if (img.Length > 0) entries.Add((img, null)); } // Elemental Drain first: no time left, the count is on the icon
        foreach (var b in _buffs()) entries.Add((_icon(b.id), BuffTimeText(b.ms)));
        if (entries.Count == 0) return; // no buff, no bar
        var box = Owner.Get<Rect>()!.Screen;
        int size = MushTownGame.BuffIconSize; // the skill icon is 32x32
        int pitch = lay.Get("iconPitch", 32);
        int x = box.Right - size - (entries.Count - 1) * pitch;
        for (int i = 0; i < entries.Count; i++)
        {
            int ix = x + i * pitch;
            if (entries[i].image != "")
                gfx.Sb.Draw(gfx.Tex(entries[i].image), new Vector2(ix, box.Y), Color.White);
            if (entries[i].time is not { } time) continue;
            var t = gfx.Ui.GetSmall(time);
            int tx = ix + size - t.Width - lay.Get("timeRight", 1);
            int ty = box.Y + size - t.Height - lay.Get("timeBottom", 1);
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
