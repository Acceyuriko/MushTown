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
        foreach (int id in CountedSkills) // the layer icons are drawn live: load them now
            if (_wzSkills.TryGetValue(id, out var counted))
                foreach (string f in counted.NumberFrames) LoadTexture(f);
        var bar = new GameObject { NoHit = true }; // art only: the clicks go through
        bar.Add(new Rect(BuffBarRect()));
        bar.Add(new BuffBarArt(() => LiveBuffs(), id => _skills[id].Icon, () => CountedIcons()));
        return bar;
    }

    // The buffs the bar shows by layers (no time): Elemental Drain and Elemental Adaptation's shield.
    private static readonly int[] CountedSkills = { WzSkillData.ElementalDrain, WzSkillData.ElementalAdaptation };
    private readonly List<BuffCount> _countedIcons = new();

    // The layered buffs that hold layers now, in CountedSkills order: the skill icon and the WZ number icon of the layer count over it.
    // A buff with no exported number art is skipped.
    private List<BuffCount> CountedIcons()
    {
        _countedIcons.Clear();
        AddCounted(WzSkillData.ElementalDrain, CurrentDrainLayers());
        AddCounted(WzSkillData.ElementalAdaptation, _fighter.StatusShield);
        return _countedIcons;
    }

    private void AddCounted(int id, int layers)
    {
        if (layers > 0 && _wzSkills.TryGetValue(id, out var wz) && wz.NumberFrames.Count >= layers)
            _countedIcons.Add(new BuffCount(wz.Icon, wz.NumberFrames[layers - 1], wz.NumberOrigins[layers - 1][0], wz.NumberOrigins[layers - 1][1]));
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

// A layered buff on the bar: the skill icon, the number icon of the layer count, and the WZ origin of the number icon (the point of it that sits
// on the skill icon's bottom left corner; Elemental Drain's number icon is the whole icon, origin 0,32: it is drawn over the skill icon and
// covers it, on purpose).
public readonly record struct BuffCount(string Icon, string Number, int OriginX, int OriginY);

// The buff bar art: the live buff icons (the skill icon of each buff) with the time left on the icon. The layered buffs (Elemental
// Drain, Elemental Adaptation) ride in front as their WZ number icons (the layer count is drawn on the icon; no time).
public sealed class BuffBarArt : Component, IDrawable
{
    private readonly Func<List<(int id, float ms)>> _buffs;
    private readonly Func<int, string> _icon;
    private readonly Func<List<BuffCount>> _counted;
    public BuffBarArt(Func<List<(int id, float ms)>> buffs, Func<int, string> icon, Func<List<BuffCount>> counted)
    {
        _buffs = buffs; _icon = icon; _counted = counted;
    }

    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._buffLay;
        var entries = new List<(string image, string? time, BuffCount? count)>();
        foreach (var c in _counted()) entries.Add((c.Icon, null, c)); // the layered buffs first: no time left, the count is drawn over the icon
        foreach (var b in _buffs()) entries.Add((_icon(b.id), BuffTimeText(b.ms), null));
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
            if (entries[i].count is { } count) // the origin lands on the icon's bottom left corner
                gfx.Sb.Draw(gfx.Tex(count.Number), new Vector2(ix - count.OriginX, box.Y + size - count.OriginY), Color.White);
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
