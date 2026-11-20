using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The buff bar at the top right of the HUD: the live buff icons side by side, no frame and no padding.
// Each icon is the icon of the buffed skill, with the countdown (TimerOverlay) on it.
public partial class MushTownGame
{
    // Every number of the bar lives in layouts/panel-buff.json.
    internal static readonly PanelLayout _buffLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-buff.json"));

    // The art size: the skill icon is 32x32.
    public const int BuffIconSize = 32;
    // The live buffs: the skill id and its time, in id order.
    private readonly List<(int id, SkillTimer timer)> _liveBuffs = new();

    // The buff bar object. The HUD chain puts it in the game scene.
    private GameObject BuildBuffBar()
    {
        var bar = new GameObject { NoHit = true }; // art only: the clicks go through
        bar.Add(new Rect(BuffBarRect()));
        bar.Add(new BuffBarArt(() => LiveBuffs(), id => _skills[id].Icon, () => CountedIcons()));
        return bar;
    }

    // The buffs the bar shows by layers (no time): Elemental Drain's layers (gold digits top right) and Elemental Adaptation's shield (bottom right).
    private readonly List<BuffCount> _countedIcons = new();

    // The layered buffs that hold layers now: the skill icon and the layer count over it.
    private List<BuffCount> CountedIcons()
    {
        _countedIcons.Clear();
        AddCounted(WzSkillData.ElementalDrain, CurrentDrainLayers(), top: true);
        AddCounted(WzSkillData.ElementalAdaptation, _fighter.StatusShield, top: false);
        return _countedIcons;
    }

    private void AddCounted(int id, int layers, bool top)
    {
        if (layers > 0) _countedIcons.Add(new BuffCount(_wzSkills[id].Icon, layers, top));
    }

    // The live buffs of this frame: the fighter holds the time left, the data the whole time.
    private List<(int id, SkillTimer timer)> LiveBuffs()
    {
        _liveBuffs.Clear();
        foreach (var kv in _fighter.Buffs) _liveBuffs.Add((kv.Key, new SkillTimer(kv.Value, _skills[kv.Key].BuffMs)));
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

// A layered buff on the bar: the skill icon and the layer count drawn on it in gold (TimerOverlay.DrawCount), at the top right or the bottom right.
public readonly record struct BuffCount(string Icon, int Layers, bool Top);

// The buff bar art: the live buff icons (the skill icon of each buff) with the countdown over the icon (TimerOverlay). The layered buffs (Elemental
// Drain, Elemental Adaptation) come first with their layer count on the icon (no time).
public sealed class BuffBarArt : Component, IDrawable
{
    private readonly Func<List<(int id, SkillTimer timer)>> _buffs;
    private readonly Func<int, string> _icon;
    private readonly Func<List<BuffCount>> _counted;
    public BuffBarArt(Func<List<(int id, SkillTimer timer)>> buffs, Func<int, string> icon, Func<List<BuffCount>> counted)
    {
        _buffs = buffs; _icon = icon; _counted = counted;
    }

    public void Draw(UiGfx gfx)
    {
        var entries = new List<(string image, SkillTimer timer, BuffCount? count)>();
        foreach (var c in _counted()) entries.Add((c.Icon, default, c)); // the layered buffs first: no time left, the count is drawn over the icon
        foreach (var b in _buffs()) entries.Add((_icon(b.id), b.timer, null));
        if (entries.Count == 0) return; // no buff, no bar
        var box = Owner.Get<Rect>()!.Screen;
        int size = MushTownGame.BuffIconSize; // the skill icon is 32x32
        int pitch = MushTownGame._buffLay.Get("iconPitch", 32);
        int x = box.Right - size - (entries.Count - 1) * pitch;
        for (int i = 0; i < entries.Count; i++)
        {
            int ix = x + i * pitch;
            if (entries[i].image != "")
                gfx.Sb.Draw(gfx.Tex(entries[i].image), new Vector2(ix, box.Y), Color.White);
            var icon = new Rectangle(ix, box.Y, size, size);
            if (entries[i].count is { } count) TimerOverlay.DrawCount(gfx, icon, count.Layers, count.Top);
            TimerOverlay.Draw(gfx, icon, entries[i].timer);
        }
    }
}
