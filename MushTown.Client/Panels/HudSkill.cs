using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The skill window: the official MapleStory skill window (UIWindow2.img/Skill/main).
// The frame, the job tabs (the Roman numerals I, II, III), the skill rows and
// the SP box are WZ art; the text is baked from the skill data.
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
        "skill_bg.png", "skill_panel.png", "skill_bar.png", "skill_row.png",
        "skill_row_sel.png", "skill_sp.png", "skill_tab1.png", "skill_tab2.png",
        "skill_tab3.png", "skill_tabsel1.png", "skill_tabsel2.png", "skill_tabsel3.png",
        "skill_spup_n.png", "skill_spup_h.png", "skill_spup_p.png", "skill_spup_d.png",
        "skill_tip_nw.png", "skill_tip_ne.png", "skill_tip_sw.png", "skill_tip_se.png",
        "skill_tip_n.png", "skill_tip_s.png", "skill_tip_w.png", "skill_tip_e.png",
        "skill_tip_c.png",
    };

    // The skill row the player picked (the detail box follows it) and the
    // drag flag (a dragged window ignores the layout spot).
    private int _skillSel;
    private bool _skillDragged;

    // Skill window tabs (one per job) + rows: skill id + fixed cast key
    // (null = passive, no key; rebinds wait for the settings week).
    private static readonly string[] SkillTabNames = { "Warrior", "Fighter", "Crusader" };
    private static readonly (int id, string? key)[][] SkillTabs = new[]
    {
        new (int id, string? key)[]
        {
            (SkillData.SlashBlast, "Q"), (SkillData.IronBody, "R"), (SkillData.WarLeap, "W"),
            (SkillData.WarriorMastery, null), (SkillData.LeapAttack, "E"), (SkillData.GuardianArmor, null),
        },
        new (int id, string? key)[]
        {
            (SkillData.Brandish, "T"), (SkillData.WeaponMastery, null), (SkillData.SpiritBlade, "U"),
            (SkillData.ComboAttack, "F"), (SkillData.FlashBlade, "Y"), (SkillData.AgileArms, null),
            (SkillData.FinalAttack, null), (SkillData.PhysicalTraining, null),
        },
        new (int id, string? key)[]
        {
            (SkillData.IntrepidSlash, "G"), (SkillData.BeamBlade, "H"), (SkillData.Rush, "J"),
            (SkillData.ScarringSword, "L"), (SkillData.ComboSynergy, null), (SkillData.SelfRecovery, null),
            (SkillData.ChanceAttack, null), (SkillData.Endure, null),
        },
    };
    // One-line passive blurbs (per skill level): the window's only help text.
    private static readonly Dictionary<int, string> PassiveBlurb = new()
    {
        [SkillData.WarriorMastery] = "+HP/speed",
        [SkillData.GuardianArmor] = "+attack/DEF",
        [SkillData.WeaponMastery] = "+damage/crit",
        [SkillData.AgileArms] = "+STR",
        [SkillData.FinalAttack] = "bonus hit",
        [SkillData.PhysicalTraining] = "+STR/DEX",
        [SkillData.ComboSynergy] = "+orb gain/dmg",
        [SkillData.SelfRecovery] = "HP/MP regen",
        [SkillData.ChanceAttack] = "+crit",
        [SkillData.Endure] = "+Max HP/DEF",
    };

    private Rectangle SkillRect() => new(_skillPos.X, _skillPos.Y, SkillW, SkillBodyH);
    private Rectangle SkillTitleRect() => new(_skillPos.X, _skillPos.Y, SkillW, _skillLay.Get("titleH", 20));

    private void BuildSkillScene()
    {
        if (!_skillDragged) _skillPos = new Point(_skillLay.Get("x", 326), _skillLay.Get("y", 130));
        foreach (string f in SkillFiles) LoadTexture("ui/" + f);
        _skillScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(SkillRect()));
        body.Add(new Picture("ui/skill_bg.png")); // the window frame (the SKILL title is baked in)
        body.Add(new Picture("ui/skill_panel.png", _skillLay.Get("panelX", 6), _skillLay.Get("panelY", 22))); // the light body panel
        body.Add(new Picture("ui/skill_bar.png", _skillLay.Get("barX", 8), _skillLay.Get("barY", 47))); // the blue bar: the picked skill
        body.Add(new SkillPickBar(() => _skills[SelectedSkillId], () => SkillLiveOf(SelectedSkillId)));
        body.Add(new Picture("ui/skill_sp.png", _skillLay.Get("spX", 212), _skillLay.Get("spY", 29))); // SKILL POINT + the value box
        body.Add(new RightLabel((ui, t) => ui.GetSmall(t), () => SkillSpText(), _skillLay.Get("spValX", 305), _skillLay.Get("spValY", 31), new Color(40, 40, 50)));
        body.Add(new LiveLabel(() => SkillTabNames[_skillTab], _skillLay.Get("jobX", 100), _skillLay.Get("jobY", 33), new Color(90, 90, 100)));
        for (int t = 0; t < SkillTabs.Length; t++)
        {
            int i = t;
            var tb = PanelButton(SkillTabRect(i), "skilltab" + i, () => { _skillTab = i; _skillSel = 0; }, ButtonKind.Click);
            tb.Add(new SkillTabArt(i + 1, () => _skillTab == i));
            body.AddChild(tb);
        }
        var tab = SkillTabs[_skillTab];
        for (int row = 0; row < tab.Length; row++)
        {
            int i = row; // for-loop variables capture shared: copy per row
            int id = tab[i].id;
            var line = new GameObject();
            line.Add(new Rect(SkillRowRect(i)));
            line.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _skillSel = i }); // no LegacyId: the rows stay silent
            line.Add(new SkillListRow(_skills[id], tab[i].key, PassiveBlurb.TryGetValue(id, out string? b) ? b : "", () => _skillSel == i, () => SkillLiveOf(id)));
            body.AddChild(line);
        }
        var detail = new GameObject();
        detail.Add(new Rect(SkillDetailRect()));
        detail.Add(new SkillTipFrame()); // the official skill info frame
        detail.Add(new SkillDetailBox(() => _skills[SelectedSkillId], () => SelectedBlurb, () => SkillLiveOf(SelectedSkillId), () => SelectedKey));
        body.AddChild(detail);
        var plus = PanelButton(SkillPlusRect(), "skillplus", () => { if (LearnSkill(SelectedSkillId)) PushBuild(); }, ButtonKind.Click);
        plus.Add(new SkillPlusArt(() => CanLearn(SelectedSkillId)));
        body.AddChild(plus);
        var max = PanelButton(SkillMaxRect(), "skillmax", () => { if (LearnSkillMax(SelectedSkillId)) PushBuild(); }, ButtonKind.Click);
        max.Add(new LiveTint(() => CanLearn(SelectedSkillId) ? ("MAX", new Color(120, 220, 120)) : ("MAX", new Color(150, 150, 160)), 0, 0));
        body.AddChild(max);
        body.AddChild(PanelButton(SkillTitleRect(), "", () => { _skillDragged = true; _dragSkill = _skillScene.LastClick - _skillPos; })); // silent, like legacy
        var x = PanelButton(SkillXRect(), "x2", () => _showSkill = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png")); // the skill window has no X of its own in the WZ
        body.AddChild(x);
        _skillScene.Add(body);
    }

    // Skill window job tabs (three Roman numerals under the title).
    private Rectangle SkillTabRect(int t)
    {
        var r = SkillRect();
        return new Rectangle(r.X + _skillLay.Get("tabX", 10) + t * _skillLay.Get("tabStep", 26), r.Y + _skillLay.Get("tabY", 27), 25, 20);
    }

    // One skill row of the list (the row art is cut to this height).
    private Rectangle SkillRowRect(int i)
    {
        var r = SkillRect();
        int pitch = _skillLay.Get("rowPitch", 32);
        return new Rectangle(r.X + _skillLay.Get("listX", 8), r.Y + _skillLay.Get("listY", 92) + i * pitch, 140, pitch);
    }

    // The skill info box of the picked skill (right of the list).
    private Rectangle SkillDetailRect()
    {
        var r = SkillRect();
        return new Rectangle(r.X + _skillLay.Get("detailX", 160), r.Y + _skillLay.Get("detailY", 98),
            _skillLay.Get("detailW", 150), _skillLay.Get("detailH", 200));
    }

    // The [+] button (raises the picked skill by one level).
    private Rectangle SkillPlusRect()
    {
        var d = SkillDetailRect();
        return new Rectangle(d.X + _skillLay.Get("plusX", 16), d.Y + _skillLay.Get("plusY", 164), 12, 12);
    }

    // The MAX button (raises the picked skill as far as the SP go).
    private Rectangle SkillMaxRect()
    {
        var d = SkillDetailRect();
        return new Rectangle(d.X + _skillLay.Get("maxX", 34), d.Y + _skillLay.Get("maxY", 163),
            _skillLay.Get("maxW", 34), _skillLay.Get("maxH", 14));
    }

    private Rectangle SkillXRect()
    {
        var r = SkillRect();
        return new Rectangle(r.X + r.Width - _skillLay.Get("closeX", 15) - 11, r.Y + _skillLay.Get("closeY", 2), 11, 11);
    }

    // The picked skill and its numbers.
    private int SelectedSkillId => SkillTabs[_skillTab][_skillSel].id;
    private string SelectedBlurb => PassiveBlurb.TryGetValue(SelectedSkillId, out string? b) ? b : "";
    private string SelectedKey => SkillTabs[_skillTab][_skillSel].key ?? "";

    // The live numbers of one skill (level, SP, cooldown, buff, orbs).
    private SkillLive SkillLiveOf(int id)
    {
        var chara = _save?.Slots[_slot];
        int lv = 0;
        chara?.SkillLv.TryGetValue(id, out lv);
        _fighter.Cooldowns.TryGetValue(id, out float cd);
        _fighter.Buffs.TryGetValue(id, out float buff);
        return new SkillLive(lv, chara?.Sp ?? 0, chara?.Level ?? 1, cd, buff, _fighter.Orbs);
    }

    // The SP number in the official box.
    private string SkillSpText() => (_save?.Slots[_slot]?.Sp ?? 0).ToString();

    // Can the player raise this skill right now?
    private bool CanLearn(int id)
    {
        var chara = _save?.Slots[_slot];
        return chara != null && Progression.CanLearn(chara, _skills[id]);
    }

    // Click a [+]: learn one level. CLI sessions carry no character.
    private bool LearnSkill(int id)
    {
        var chara = _save?.Slots[_slot];
        return chara != null && Progression.LearnSkill(chara, _skills[id]);
    }

    // Click MAX: spend the SP on as many levels as they buy.
    private bool LearnSkillMax(int id)
    {
        var chara = _save?.Slots[_slot];
        return chara != null && Progression.LearnSkillMax(chara, _skills[id]) > 0;
    }
}

// One job tab: the official Roman numeral art (the selected tab is orange).
public sealed class SkillTabArt : Component, IDrawable
{
    private readonly int _index;
    private readonly Func<bool> _selected;
    public SkillTabArt(int index, Func<bool> selected) { _index = index; _selected = selected; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        bool sel = _selected();
        gfx.Sb.Draw(gfx.Tex(sel ? $"ui/skill_tabsel{_index}.png" : $"ui/skill_tab{_index}.png"),
            new Vector2(r.X, r.Y + (sel ? 0 : MushTownGame._skillLay.Get("tabArtY", 2))), Color.White);
    }
}

// One skill row: the official row art, the icon, the name and the live numbers.
// The row art is 140x35 but the rows step 32 (8 skills must fit under the
// blue bar), so the art is cut to the row height and never covers the next row.
public sealed class SkillListRow : Component, IDrawable
{
    private readonly SkillData _skill;
    private readonly string? _key;
    private readonly string _blurb;
    private readonly Func<bool> _selected;
    private readonly Func<SkillLive> _live;
    public SkillListRow(SkillData skill, string? key, string blurb, Func<bool> selected, Func<SkillLive> live)
    {
        _skill = skill; _key = key; _blurb = blurb; _selected = selected; _live = live;
    }
    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._skillLay;
        var r = Owner.Get<Rect>()!.Box;
        var s = _live();
        bool locked = s.Level < _skill.ReqLevel;
        bool cooling = !locked && s.CdMs > 0;
        bool live = s.BuffMs > 0;
        var art = gfx.Tex(_selected() ? "ui/skill_row_sel.png" : "ui/skill_row.png");
        gfx.Sb.Draw(art, r, new Rectangle(0, 0, art.Width, r.Height), Color.White);
        gfx.Sb.Draw(gfx.Tex(locked || cooling ? _skill.Disabled : _skill.Icon), new Vector2(r.X + lay.Get("rowIconX", 1), r.Y + lay.Get("rowIconY", 0)), Color.White);
        var ink = locked ? new Color(140, 140, 150) : new Color(30, 30, 40);
        gfx.Sb.Draw(gfx.Ui.GetSmall(_skill.Name), new Vector2(r.X + lay.Get("rowNameX", 38), r.Y + lay.Get("rowNameY", 3)), ink);
        // The sub line: the level and the cast key (a passive shows its blurb).
        string sub = _skill.Kind == SkillData.KindPassive
            ? _blurb
            : $"Lv.{s.Lv}/{_skill.MaxLevel}  " + (_key ?? "");
        gfx.Sb.Draw(gfx.Ui.GetSmall(sub), new Vector2(r.X + lay.Get("rowSubX", 38), r.Y + lay.Get("rowSubY", 19)), new Color(105, 105, 115));
        // The right of the sub line: what the skill is doing right now.
        // A passive shows its blurb there, so it has no room for this.
        string right = locked ? $"Lv.{_skill.ReqLevel}"
            : live ? $"{(int)Math.Ceiling(s.BuffMs / 1000)}s"
            : cooling ? $"{(int)Math.Ceiling(s.CdMs / 1000)}s" : "";
        if (right == "" || _skill.Kind == SkillData.KindPassive) return;
        var rc = locked ? new Color(200, 60, 60) : new Color(190, 110, 0);
        var rt = gfx.Ui.GetSmall(right);
        gfx.Sb.Draw(rt, new Vector2(r.X + lay.Get("rowRightX", 136) - rt.Width, r.Y + lay.Get("rowSubY", 19)), rc);
    }
}

// The blue bar: the picked skill (icon, name, level).
public sealed class SkillPickBar : Component, IDrawable
{
    private readonly Func<SkillData> _skill;
    private readonly Func<SkillLive> _live;
    public SkillPickBar(Func<SkillData> skill, Func<SkillLive> live) { _skill = skill; _live = live; }
    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._skillLay;
        var skill = _skill();
        var s = _live();
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex(s.Level < skill.ReqLevel ? skill.Disabled : skill.Icon), new Vector2(r.X + lay.Get("pickIconX", 12), r.Y + lay.Get("pickIconY", 51)), Color.White);
        gfx.Sb.Draw(gfx.Ui.GetSmall(skill.Name), new Vector2(r.X + lay.Get("pickNameX", 46), r.Y + lay.Get("pickNameY", 60)), Color.White);
        var t = gfx.Ui.GetSmall($"Lv.{s.Lv}/{skill.MaxLevel}");
        gfx.Sb.Draw(t, new Vector2(r.X + lay.Get("pickRightX", 304) - t.Width, r.Y + lay.Get("pickNameY", 60)), new Color(255, 220, 0));
    }
}

// The official skill info frame (UIToolTip.img/Skill/Frame): nine pieces
// (four corners, four edges and the center) drawn around the owner rect.
public sealed class SkillTipFrame : Component, IDrawable
{
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var nw = gfx.Tex("ui/skill_tip_nw.png"); var ne = gfx.Tex("ui/skill_tip_ne.png");
        var sw = gfx.Tex("ui/skill_tip_sw.png"); var se = gfx.Tex("ui/skill_tip_se.png");
        var n = gfx.Tex("ui/skill_tip_n.png"); var s = gfx.Tex("ui/skill_tip_s.png");
        var w = gfx.Tex("ui/skill_tip_w.png"); var e = gfx.Tex("ui/skill_tip_e.png");
        var c = gfx.Tex("ui/skill_tip_c.png");
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

// The skill info box: the level, the costs and the timers (light text on the
// dark official frame).
public sealed class SkillDetailBox : Component, IDrawable
{
    private readonly Func<SkillData> _skill;
    private readonly Func<string> _blurb;
    private readonly Func<SkillLive> _live;
    private readonly Func<string> _key;
    public SkillDetailBox(Func<SkillData> skill, Func<string> blurb, Func<SkillLive> live, Func<string> key)
    {
        _skill = skill; _blurb = blurb; _live = live; _key = key;
    }
    public void Draw(UiGfx gfx)
    {
        var lay = MushTownGame._skillLay;
        var skill = _skill();
        var s = _live();
        var r = Owner.Get<Rect>()!.Box;
        int y = r.Y + lay.Get("textY", 16);
        int step = lay.Get("detailLine", 16);
        void Line(string text, Color ink)
        {
            gfx.Sb.Draw(gfx.Ui.GetSmall(text), new Vector2(r.X + lay.Get("textX", 16), y), ink);
            y += step;
        }
        Line($"Lv.{s.Lv}/{skill.MaxLevel}", new Color(235, 235, 240));
        string blurb = _blurb();
        if (blurb != "") Line(blurb, new Color(130, 195, 255));
        Line($"Req. Lv.{skill.ReqLevel}", new Color(255, 200, 80));
        Line($"SP {Progression.SkillSpCost} (have {s.Sp})", new Color(235, 235, 240));
        if (skill.Kind != SkillData.KindPassive)
        {
            Line($"MP {skill.Mp}", new Color(235, 235, 240));
            if (s.CdMs > 0) Line($"CD {(int)Math.Ceiling(s.CdMs / 1000)}s", new Color(255, 180, 60));
            else Line($"CD {skill.CooldownMs / 1000}s", new Color(235, 235, 240));
        }
        if (skill.Kind == SkillData.KindBuff)
        {
            if (s.BuffMs > 0) Line($"Buff {(int)Math.Ceiling(s.BuffMs / 1000)}s", new Color(255, 180, 60));
            else Line($"Buff {skill.BuffMs / 1000}s", new Color(235, 235, 240));
        }
        if (skill.SkillId == SkillData.ComboAttack) Line($"{s.Orbs} orbs", new Color(255, 200, 80));
        if (_key() != "") Line($"Key {_key()}", new Color(180, 180, 190));
    }
}

// The [+] button: the official SP-up art in its four states.
public sealed class SkillPlusArt : Component, IDrawable
{
    private readonly Func<bool> _enabled;
    public SkillPlusArt(Func<bool> enabled) => _enabled = enabled;
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        string key = !_enabled() ? "ui/skill_spup_d.png"
            : gfx.Hovered == Owner ? gfx.Pressed ? "ui/skill_spup_p.png" : "ui/skill_spup_h.png"
            : "ui/skill_spup_n.png";
        gfx.Sb.Draw(gfx.Tex(key), new Vector2(r.X, r.Y), Color.White);
    }
}
