using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The skill window: the official MapleStory skill window (UIWindow2.img/Skill/main).
// The frame, the page tabs, the two columns of skill cells and the button row are WZ
// art; the text is baked from the skill data. No skill points: every skill stands at
// its max level.
public partial class MushTownGame
{
    // Every number of this window lives in layouts/panel-skill.json.
    internal static readonly PanelLayout _skillLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-skill.json"));

    // The window box (the official art is 318x361).
    private static int SkillW => _skillLay.Get("w", 318);
    private static int SkillBodyH => _skillLay.Get("h", 361);

    // The window art (MushTown.Exporter ExportSkillUi). Missing files fail loud.
    private static readonly string[] SkillFiles = new[]
    {
        "skill_bg.png", "skill_panel.png", "skill_bar.png", "skill_row.png", "skill_blank.png",
        "skill_tab0.png", "skill_tab1.png", "skill_tab2.png", "skill_tab3.png", "skill_tab4.png", "skill_tab5.png",
        "skill_tabsel0.png", "skill_tabsel1.png", "skill_tabsel2.png", "skill_tabsel3.png", "skill_tabsel4.png", "skill_tabsel5.png",
        "skill_tabdis6.png",
        "skill_bt_macro_n.png", "skill_bt_macro_h.png", "skill_bt_macro_p.png",
        "skill_bt_hyper_d.png", "skill_bt_guild_d.png", "skill_bt_alarm_d.png", "skill_bt_sequence_d.png",
        "skill_tip_nw.png", "skill_tip_ne.png", "skill_tip_sw.png", "skill_tip_se.png",
        "skill_tip_n.png", "skill_tip_s.png", "skill_tip_w.png", "skill_tip_e.png",
        "skill_tip_c.png", "skill_tip_dotw.png", "skill_tip_dotc.png", "skill_tip_dote.png", "skill_tip_iconbg.png",
        "skill_book000.png", "skill_book100.png", "skill_book110.png", "skill_book111.png",
    };

    // The skill window pages: the beginner page (the default passives), then the Roman
    // numerals I to V. VI (6) stays gray until the character changes jobs (not made yet).
    private const int SkillTabBeginner = 0;
    private const int SkillTabLast = 5;
    private const int SkillTabVI = 6;
    private const int SkillRows = 7; // the rows of the two columns, the pages scroll by row
    private int _skillScroll;  // the first row shown
    private int _skillWheel;   // the last wheel value the list saw
    private bool _skillDragged;
    private ScrollBar? _skillBar;
    private DefaultPassiveSkillData _passives = new();
    private List<SkillView> _skillPage = new();
    private int SkillScrollMax => Math.Max(0, (_skillPage.Count + 1) / 2 - SkillRows);

    // The name and the book of each page (the blue bar), the skill ids of each job page.
    private static readonly string[] SkillTabNames = { "Legendary Skill", "Warrior", "Fighter", "Crusader", "4th Job", "5th Job" };
    private static readonly string?[] SkillTabBooks = { "ui/skill_book000.png", "ui/skill_book100.png", "ui/skill_book110.png", "ui/skill_book111.png", null, null };
    private static readonly int[][] SkillTabs = new[]
    {
        Array.Empty<int>(), // the beginner page lists the default passives, not skill ids
        new[] { SkillData.SlashBlast, SkillData.IronBody, SkillData.WarLeap, SkillData.WarriorMastery, SkillData.LeapAttack, SkillData.GuardianArmor },
        new[] { SkillData.Brandish, SkillData.WeaponMastery, SkillData.SpiritBlade, SkillData.ComboAttack, SkillData.FlashBlade, SkillData.AgileArms, SkillData.FinalAttack, SkillData.PhysicalTraining },
        new[] { SkillData.IntrepidSlash, SkillData.BeamBlade, SkillData.Rush, SkillData.ScarringSword, SkillData.ComboSynergy, SkillData.SelfRecovery, SkillData.ChanceAttack, SkillData.Endure },
        Array.Empty<int>(), // IV: no skills yet
        Array.Empty<int>(), // V: no skills yet
    };

    private Rectangle SkillRect() => new(0, 0, SkillW, SkillBodyH);
    private Rectangle SkillTitleRect() => new(0, 0, SkillW, _skillLay.Get("titleH", 20));

    // The cells of one page: the default passives or the skills of the job.
    private List<SkillView> SkillPageOf(int tab) => tab == SkillTabBeginner
        ? _passives.Rows.Select(r => new SkillView(r.Name, r.Icon, r.Icon, r.MaxLevel, r.Desc, r.Effect, 0, true)).ToList()
        : SkillTabs[tab].Select(id =>
        {
            var s = _skills[id];
            return new SkillView(s.Name, s.Icon, s.Disabled, s.MaxLevel, s.Desc, new[] { SkillText.Effect(s, s.MaxLevel) }, id, s.Kind == SkillData.KindPassive);
        }).ToList();

    private void BuildSkillScene()
    {
        if (!_skillDragged) _skillPos = new Point(_skillLay.Get("x", 326), _skillLay.Get("y", 130));
        foreach (string f in SkillFiles) LoadTexture("ui/" + f);
        _skillPage = SkillPageOf(_skillTab);
        foreach (var view in _skillPage) LoadTexture(view.Icon);
        _skillScroll = Math.Min(_skillScroll, SkillScrollMax);
        _skillHost.Clear();
        var body = new GameObject();
        body.Add(new Rect(SkillRect()));
        body.Add(new Picture("ui/skill_bg.png")); // the window frame (the SKILL title is baked in)
        body.Add(new Picture("ui/skill_panel.png", _skillLay.Get("panelX", 6), _skillLay.Get("panelY", 22))); // the light body panel
        var bar = new GameObject();
        bar.Add(new Rect(new Rectangle(_skillLay.Get("barX", 8), _skillLay.Get("barY", 47), _skillLay.Get("barW", 304), _skillLay.Get("barH", 45))));
        bar.Add(new Picture("ui/skill_bar.png", 0, 0)); // the blue bar: the name of the page
        bar.Add(new SkillPageBar(SkillTabNames[_skillTab], SkillTabBooks[_skillTab]));
        body.AddChild(bar);
        for (int t = 0; t <= SkillTabLast; t++)
        {
            int i = t;
            var tb = PanelButton(SkillTabRect(i), "skilltab" + i, () => { _skillTab = i; _skillScroll = 0; }, ButtonKind.Click);
            tb.Add(new SkillTabArt(i, () => _skillTab == i));
            body.AddChild(tb);
        }
        var locked = new GameObject(); // VI: gray, nothing to click
        locked.Add(new Rect(SkillTabRect(SkillTabVI)));
        locked.Add(new Picture("ui/skill_tabdis6.png", 0, _skillLay.Get("tabArtY", 2)));
        body.AddChild(locked);
        BuildSkillCells(body);
        BuildSkillButtons(body);
        body.AddChild(PanelButton(SkillTitleRect(), "", () => { _skillDragged = true; _dragSkill = _uiScene.LastClick - _skillPos; })); // silent, like legacy
        var x = PanelButton(SkillXRect(), "x2", CloseSkillWindow, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png")); // the skill window has no X of its own in the WZ
        body.AddChild(x);
        _skillHost.AddChild(body);
    }

    // Two columns of cells, SkillRows rows. A cell reads its skill from the scroll row (so a
    // scroll needs no rebuild); a cell with no skill is the empty cell art. A job page lets a
    // click pick an active skill up (the strip and the keys take the drop); a passive does not.
    private void BuildSkillCells(GameObject body)
    {
        LoadTexture("ui/inv_scroll_base.png");
        for (int t = 0; t < 3; t++) LoadTexture($"ui/inv_scroll_thumb{t}.png");
        for (int slot = 0; slot < SkillRows * 2; slot++)
        {
            int i = slot;
            SkillView? View() => _skillScroll * 2 + i < _skillPage.Count ? _skillPage[_skillScroll * 2 + i] : null;
            var cell = new GameObject();
            cell.Add(new Rect(SkillCellRect(i)));
            if (_skillTab != SkillTabBeginner)
            {
                cell.Add(new Button { Kind = ButtonKind.Click, OnClick = () => PickUpSkill(View()) });
                cell.Add(new Grab(() => CanPickUp(View())));
            }
            cell.Add(new SkillCell(View, StateOf));
            cell.Add(new SkillTip(View));
            body.AddChild(cell);
        }
        var scroll = new ScrollBar(
            () => (float)_skillScroll / Math.Max(1, SkillScrollMax),
            value => _skillScroll = (int)MathF.Round(value * SkillScrollMax),
            () => SkillScrollMax > 0,
            "ui/inv_scroll_base.png", "ui/inv_scroll_thumb", _skillLay.Get("scrollThumbH", 28));
        _skillBar = scroll;
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(_skillLay.Get("scrollX", 297), _skillLay.Get("scrollY", 92),
            _skillLay.Get("scrollW", 5), SkillRows * _skillLay.Get("rowPitch", 34))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => scroll.Grab(_uiScene.LastClick.Y) });
        o.Add(scroll);
        body.AddChild(o);
    }

    // The button row under the cells: only MACRO NOTIFIER is lit, and it opens nothing yet
    // (the macro window comes with its own ticket). The others show their gray art.
    private void BuildSkillButtons(GameObject body)
    {
        int y = _skillLay.Get("btnY", 336);
        foreach (var (art, key) in new[] { ("hyper", "hyperX"), ("guild", "guildX"), ("alarm", "alarmX"), ("sequence", "sequenceX") })
        {
            var gray = new GameObject();
            gray.Add(new Rect(new Rectangle(_skillLay.Get(key, 8), y, _skillLay.Get("grayW", 50), _skillLay.Get("btnH", 16))));
            gray.Add(new Picture($"ui/skill_bt_{art}_d.png", 0, 0));
            body.AddChild(gray);
        }
        var macro = new GameObject();
        macro.Add(new Rect(new Rectangle(_skillLay.Get("macroX", 220), y, _skillLay.Get("macroW", 89), _skillLay.Get("btnH", 16))));
        macro.Add(new Button { LegacyId = "skillmacro", Kind = ButtonKind.Click });
        macro.Add(new Image("ui/skill_bt_macro"));
        body.AddChild(macro);
    }

    private static bool CanPickUp(SkillView? view) => view is { Passive: false };

    private void PickUpSkill(SkillView? view)
    {
        if (CanPickUp(view)) Hold(new BindTarget(BindKind.Skill, view!.SkillId));
    }

    // The live part of a cell: the timers and the skill in hand.
    private SkillCellState StateOf(SkillView view)
    {
        if (view.SkillId == 0) return default;
        _fighter.Cooldowns.TryGetValue(view.SkillId, out float cd);
        _fighter.Buffs.TryGetValue(view.SkillId, out float buff);
        return new SkillCellState(cd, buff, Held == new BindTarget(BindKind.Skill, view.SkillId));
    }

    // The wheel scrolls the cells while the mouse is over the window: one notch is one row.
    private void UpdateSkillWheel()
    {
        int wheel = _input.Wheel;
        bool over = _showSkill && RootOf(_uiScene.Hovered) == _skillHost;
        if (!over) { _skillWheel = wheel; return; }
        int notches = (wheel - _skillWheel) / 120;
        _skillWheel = wheel;
        _skillScroll = Math.Clamp(_skillScroll - notches, 0, SkillScrollMax);
    }

    // The skill window closes. A skill a click picked up stays in hand: the
    // quick slot strip and the keys are drop areas with or without this window.
    private void CloseSkillWindow()
    {
        _showSkill = false;
        _dragSkill = null;
    }

    // Skill window page tabs (the beginner tab and the Roman numerals under the title).
    private Rectangle SkillTabRect(int t) => new(_skillLay.Get("tabX", 10) + t * _skillLay.Get("tabStep", 26),
        _skillLay.Get("tabY", 27), _skillLay.Get("tabW", 25), _skillLay.Get("tabH", 20));

    // One cell of the two columns (the cell art is cut to this height).
    private Rectangle SkillCellRect(int slot)
    {
        int pitch = _skillLay.Get("rowPitch", 34);
        return new Rectangle(_skillLay.Get("listX", 8) + slot % 2 * _skillLay.Get("colStep", 142),
            _skillLay.Get("listY", 92) + slot / 2 * pitch, _skillLay.Get("cellW", 140), pitch);
    }

    private Rectangle SkillXRect() => new(SkillRect().Width - _skillLay.Get("closeX", 15) - 11,
        _skillLay.Get("closeY", 2), 11, 11);
}

// One skill of a page, as the cell and the tooltip show it: a job skill or a default
// passive (SkillId 0). The numbers are the max level, read once when the page is built.
public sealed record SkillView(string Name, string Icon, string IconOff, int MaxLevel,
    IReadOnlyList<string> Desc, IReadOnlyList<string> Effect, int SkillId, bool Passive);

// What changes from frame to frame in a cell.
public readonly record struct SkillCellState(float CdMs, float BuffMs, bool Held);

// One page tab: the official Roman numeral art (the selected tab is orange).
public sealed class SkillTabArt : Component, IDrawable
{
    private readonly int _index;
    private readonly Func<bool> _selected;
    public SkillTabArt(int index, Func<bool> selected) { _index = index; _selected = selected; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        bool sel = _selected();
        gfx.Sb.Draw(gfx.Tex(sel ? $"ui/skill_tabsel{_index}.png" : $"ui/skill_tab{_index}.png"),
            new Vector2(r.X, r.Y + (sel ? 0 : MushTownGame._skillLay.Get("tabArtY", 2))), Color.White);
    }
}

// The blue bar: the book of the page and its name, centered.
public sealed class SkillPageBar : Component, IDrawable
{
    private readonly string _name;
    private readonly string? _book;
    public SkillPageBar(string name, string? book) { _name = name; _book = book; }
    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._skillLay;
        var r = Owner.Get<Rect>()!.Screen;
        if (_book != null)
            gfx.Sb.Draw(gfx.Tex(_book), new Vector2(r.X + lay.Get("bookX", 6), r.Y + lay.Get("bookY", 7)), Color.White);
        var name = gfx.Ui.GetSmall(_name);
        gfx.Sb.Draw(name, new Vector2(r.X + lay.Get("barNameX", 171) - name.Width / 2, r.Y + lay.Get("barNameY", 14)), Color.White);
    }
}

// One cell of the list: the official cell art (or the empty cell), the icon, the name
// and the max level. The right of the second line shows what the skill is doing now.
public sealed class SkillCell : Component, IDrawable
{
    private readonly Func<SkillView?> _view;
    private readonly Func<SkillView, SkillCellState> _state;
    public SkillCell(Func<SkillView?> view, Func<SkillView, SkillCellState> state) { _view = view; _state = state; }
    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._skillLay;
        var r = Owner.Get<Rect>()!.Screen;
        var view = _view();
        var art = gfx.Tex(view == null ? "ui/skill_blank.png" : "ui/skill_row.png");
        gfx.Sb.Draw(art, r, new Rectangle(0, 0, art.Width, r.Height), Color.White);
        if (view == null) return;
        var s = _state(view);
        bool cooling = s.CdMs > 0;
        if (!s.Held) // in hand: the icon rides the mouse, the cell keeps the name and the numbers
            gfx.Sb.Draw(gfx.Tex(cooling ? view.IconOff : view.Icon), new Vector2(r.X + lay.Get("rowIconX", 1), r.Y + lay.Get("rowIconY", 0)), Color.White);
        int textW = lay.Get("rowRightX", 136) - lay.Get("rowNameX", 38);
        gfx.Sb.Draw(Text.Fit(gfx, view.Name, textW), new Vector2(r.X + lay.Get("rowNameX", 38), r.Y + lay.Get("rowNameY", 3)), new Color(30, 30, 40));
        gfx.Sb.Draw(gfx.Ui.GetSmall(view.MaxLevel.ToString()), new Vector2(r.X + lay.Get("rowSubX", 38), r.Y + lay.Get("rowSubY", 19)), new Color(105, 105, 115));
        string right = s.BuffMs > 0 ? $"{(int)Math.Ceiling(s.BuffMs / 1000)}s"
            : cooling ? $"{(int)Math.Ceiling(s.CdMs / 1000)}s" : "";
        if (right == "") return;
        var rt = gfx.Ui.GetSmall(right);
        gfx.Sb.Draw(rt, new Vector2(r.X + lay.Get("rowRightX", 136) - rt.Width, r.Y + lay.Get("rowSubY", 19)), new Color(190, 110, 0));
    }
}

// The hover box of a cell (the official skill tooltip frame): the name, the icon in its frame,
// the master level and the description, then a dotted line, the current level and the effect.
// The tip layer draws it next to the cursor.
public sealed class SkillTip : Component
{
    private readonly Func<SkillView?> _view;
    public SkillTip(Func<SkillView?> view) => _view = view;

    public void DrawAt(UiGfx gfx, TipAnchor anchor)
    {
        if (_view() is not { } view) return;
        var lay = MushTownGame._skillLay;
        int pad = lay.Get("tipPad", 12), gap = lay.Get("tipLine", 16), w = lay.Get("tipW", 270);
        var frame = gfx.Tex("ui/skill_tip_iconbg.png");
        int textX = pad + frame.Width + lay.Get("tipIconGap", 8);
        var title = gfx.Ui.GetSmall(view.Name);
        var side = new List<(Texture2D Tex, Color Ink)> { (gfx.Ui.GetSmall($"[Master Level: {view.MaxLevel}]"), Color.White) };
        foreach (string text in view.Desc)
            foreach (string wrapped in gfx.Ui.Wrap(text, w - textX - pad))
                side.Add((gfx.Ui.GetSmall(wrapped), new Color(221, 221, 226)));
        var effect = new List<Texture2D>();
        foreach (string text in view.Effect)
            foreach (string wrapped in gfx.Ui.Wrap(text, w - pad * 2))
                effect.Add(gfx.Ui.GetSmall(wrapped));
        // The wrap measures a little narrower than the drawn text: the box grows to the widest line.
        w = Math.Max(w, Math.Max(textX + side.Max(l => l.Tex.Width) + pad, effect.Count == 0 ? 0 : effect.Max(t => t.Width) + pad * 2));
        int topH = Math.Max(frame.Height, side.Count * gap);
        int titleY = pad, topY = titleY + gap + lay.Get("tipTitleGap", 6), lineY = topY + topH + lay.Get("tipSectionGap", 8);
        int levelY = lineY + lay.Get("tipLineGap", 3) + lay.Get("tipSectionGap", 8);
        var box = anchor.At(gfx, w, levelY + (1 + effect.Count) * gap + pad);
        NineSlice.Draw(gfx, "skill_tip", box);
        int dot = lay.Get("tipDot", 5);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(box.X + pad, box.Y + titleY + lay.Get("tipDotY", 5), dot, dot), new Color(90, 200, 240)); // the dot before the name
        gfx.Sb.Draw(title, new Vector2(box.X + pad + lay.Get("tipTitleX", 10), box.Y + titleY), Color.White);
        gfx.Sb.Draw(frame, new Vector2(box.X + pad, box.Y + topY), Color.White);
        var icon = gfx.Tex(view.Icon);
        int iconW = icon.Width * lay.Get("tipIconScale", 2), iconH = icon.Height * lay.Get("tipIconScale", 2);
        gfx.Sb.Draw(icon, new Rectangle(box.X + pad + (frame.Width - iconW) / 2, box.Y + topY + (frame.Height - iconH) / 2, iconW, iconH), Color.White);
        int y = box.Y + topY;
        foreach (var (tex, ink) in side)
        {
            gfx.Sb.Draw(tex, new Vector2(box.X + textX, y), ink);
            y += gap;
        }
        var west = gfx.Tex("ui/skill_tip_dotw.png"); var centre = gfx.Tex("ui/skill_tip_dotc.png"); var east = gfx.Tex("ui/skill_tip_dote.png");
        int x0 = box.X + pad, x1 = box.Right - pad;
        gfx.Sb.Draw(west, new Vector2(x0, box.Y + lineY), Color.White);
        gfx.Sb.Draw(centre, new Rectangle(x0 + west.Width, box.Y + lineY, x1 - x0 - west.Width - east.Width, centre.Height), Color.White);
        gfx.Sb.Draw(east, new Vector2(x1 - east.Width, box.Y + lineY), Color.White);
        y = box.Y + levelY;
        gfx.Sb.Draw(gfx.Ui.GetSmall($"[Current Level: {view.MaxLevel}]"), new Vector2(box.X + pad, y), Color.White);
        y += gap;
        foreach (var tex in effect)
        {
            gfx.Sb.Draw(tex, new Vector2(box.X + pad, y), Color.White);
            y += gap;
        }
    }
}
