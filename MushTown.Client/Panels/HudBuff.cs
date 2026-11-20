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

    // The buff bar object. The HUD chain puts it in the game scene. The art is one object that the clicks go through; over it lie
    // maxCount hover cells (one for each place an icon can stand, counted from the right edge), each with the skill window's tooltip of
    // the buff there. A cell is on only while an icon stands in its place, so an empty place takes no click.
    private GameObject BuildBuffBar()
    {
        var root = new GameObject(); // no Rect of its own: only the cells below can be hit
        var art = new GameObject { NoHit = true };
        art.Add(new Rect(BuffBarRect()));
        art.Add(new BuffBarArt(() => LiveBuffs(), id => _skills[id].Icon, () => CountedIcons()));
        root.AddChild(art);
        var box = BuffBarRect();
        int pitch = _buffLay.Get("iconPitch", 32);
        var cells = new List<GameObject>();
        for (int place = 0; place < _buffLay.Get("maxCount", 10); place++)
        {
            int p = place; // its own copy for the closure
            var cell = new GameObject { Enabled = false };
            cell.Add(new Rect(new Rectangle(box.Right - BuffIconSize - p * pitch, box.Y, BuffIconSize, BuffIconSize)));
            cell.Add(new SkillTip(() => BuffIdAt(p) is { } id ? SkillViewOf(id) : null));
            root.AddChild(cell);
            cells.Add(cell);
        }
        _buffCells = root.Add(new BuffCells(cells, () => BuffIds().Count));
        return root;
    }

    private BuffCells? _buffCells;

    // The skill ids of the icons on the bar, left to right: the layered buffs first, then the timed buffs in id order (the order BuffBarArt draws).
    private readonly List<int> _buffIds = new();

    private List<int> BuffIds()
    {
        _buffIds.Clear();
        foreach (var c in CountedIcons()) _buffIds.Add(c.Id);
        foreach (var b in LiveBuffs()) _buffIds.Add(b.id);
        return _buffIds;
    }

    // The buff whose icon stands the given place from the right edge (0 = the rightmost), null when no icon stands there.
    private int? BuffIdAt(int place)
    {
        var ids = BuffIds();
        return place < ids.Count ? ids[ids.Count - 1 - place] : null;
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
        if (layers > 0) _countedIcons.Add(new BuffCount(_wzSkills[id].Icon, layers, top, id));
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
public readonly record struct BuffCount(string Icon, int Layers, bool Top, int Id);

// Turns the hover cells of the buff bar on for the places that hold an icon, and off for the rest.
public sealed class BuffCells : Component
{
    private readonly List<GameObject> _cells;
    private readonly Func<int> _count;
    public BuffCells(List<GameObject> cells, Func<int> count)
    {
        _cells = cells; _count = count;
    }

    public override void Update(float dt) => Sync();

    // Also called by the shots, which never run Update.
    public void Sync()
    {
        int count = _count();
        for (int place = 0; place < _cells.Count; place++) _cells[place].Enabled = place < count;
    }
}

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
