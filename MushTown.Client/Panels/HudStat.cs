using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The character info window (UICharacterInfo.img): the official Character
// Info block with the portrait on top and the STAT block with the live
// numbers under it. Every number of the window lives in
// layouts/panel-stat.json.
public partial class MushTownGame
{
    private static readonly PanelLayout _statLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-stat.json"));

    private const int UtilityFirstRow = 22; // the bottom block's rows: page 2 shows the next six values in the same boxes
    private int _statPage; // 0 = mesos / exp page, 1 = defense / knockback page
    private bool _statPlaced; // the window took the spot from the layout file

    private int StatW => _statLay.Get("w", 472);
    private int StatH => _statLay.Get("h", 709);
    private int StatBodyX => _statLay.Get("bodyX", 12);
    private int StatBodyY => _statLay.Get("bodyY", 268);
    private Rectangle StatRect() => new(0, 0, StatW, StatH);
    private Rectangle StatXRect() => new(_statLay.Get("closeX", 449), _statLay.Get("closeY", 12), 11, 11);
    private Rectangle StatDragRect() => new(0, 0, StatXRect().X, _statLay.Get("titleH", 32));

    // The window takes the spot from the layout file the first time it opens.
    private void StatPlaceFirst()
    {
        if (_statPlaced) return;
        _statPlaced = true;
        _statPos = new Point(_statLay.Get("x", 447), _statLay.Get("y", 29));
    }

    private void LoadStatArt()
    {
        foreach (string f in new[]
        {
            "charinfo_main.png", "stat_win.png", "stat_statbar.png", "stat_name.png", "stat_x.png", "stat_page.png",
            "stat_label_main.png", "stat_label_mp.png", "stat_label_attack.png", "stat_label_utility.png", "stat_label_defense.png",
            "tip_nw.png", "tip_ne.png", "tip_sw.png", "tip_se.png", "tip_n.png", "tip_s.png",
            "tip_w.png", "tip_e.png", "tip_c.png",
            "etip_top.png", "etip_mid.png", "etip_btm.png", "etip_line.png", "etip_cat_w.png", "etip_cat_c.png", "etip_cat_e.png", "etip_icon_base.png", "etip_icon_shade.png", "etip_star_empty.png",
            "etip_set_top.png", "etip_set_mid.png", "etip_set_btm.png", "etip_set_line.png",
        })
            LoadTexture("ui/" + f);
    }

    private readonly StatDescData _statDesc; // the hover text of the rows (assets/ui/stat_desc.json), loaded by the constructor

    private void BuildStatScene()
    {
        LoadStatArt();
        StatPlaceFirst();
        _statPos = ClampPanel(_statPos, StatW, StatH); // the window is taller than the old one: keep it on screen
        _statHost.Clear();
        var body = new GameObject();
        body.Add(new Rect(StatRect())); // the body swallows the clicks, like the legacy fallthrough
        body.Add(new Picture("ui/charinfo_main.png", _statLay.Get("topX", 0), _statLay.Get("topY", 0)));
        body.Add(new Picture("ui/stat_win.png", _statLay.Get("blockX", 0), _statLay.Get("blockY", 230)));
        body.Add(new Picture("ui/stat_statbar.png", _statLay.Get("stripX", 11), _statLay.Get("stripY", 241)));
        BuildStatPage(body);
        // the portrait stands on the white block: real gear and the current look
        var feet = new GameObject { NoHit = true };
        feet.Add(new Rect(new Rectangle(_statLay.Get("feetX", 236), _statLay.Get("feetY", 159), 1, 1)));
        feet.Add(new AvatarPreview(this, "stand1", false, chara: () => Hero));
        body.AddChild(feet);
        var job = new GameObject();
        job.Add(new Rect(new Rectangle(
            _statLay.Get("jobX", 76) - _statLay.Get("jobW", 108) / 2,
            _statLay.Get("jobY", 42), _statLay.Get("jobW", 108), 10)));
        job.Add(new Text { Value = JobLabel, Ink = Color.White, Align = TextAlign.Center, Line = TextLine.Top });
        body.AddChild(job);
        var lv = new GameObject();
        lv.Add(new Rect(new Rectangle(
            _statLay.Get("lvX", 244) - _statLay.Get("lvW", 80) / 2,
            _statLay.Get("lvY", 33), _statLay.Get("lvW", 80), 10)));
        lv.Add(new Text { Value = () => $"Lv. {Hero?.Level ?? 1}", Ink = new Color(90, 95, 105), Align = TextAlign.Center, Line = TextLine.Top });
        body.AddChild(lv);
        body.Add(new NamePlate(() => Hero?.Name ?? "",
            _statLay.Get("nameX", 236), _statLay.Get("nameY", 162)));
        body.Add(new Picture("ui/stat_x.png", StatXRect().X, StatXRect().Y));
        body.AddChild(PanelButton(StatDragRect(), "", () => _dragStat = _uiScene.LastClick - _statPos));
        body.AddChild(PanelButton(StatXRect(), "x1", () => _showStat = false, ButtonKind.Click));
        _statHost.AddChild(body);
    }

    // The job name in the top-left pill, as plain text: the class the create
    // page picked (Hero stays Hero, not the Warrior branch).
    private string JobLabel() => JobNames.Of(Hero?.Job ?? 110);

    // The stats page: the official panel art with the live numbers.
    private void BuildStatPage(GameObject body)
    {
        int bx = StatBodyX, by = StatBodyY;
        body.Add(new Picture("ui/stat_page.png", bx, by)); // the 448x406 stat panel (the two light panels are in it)
        body.Add(new Picture("ui/stat_label_main.png", bx + 12, by + 52)); // HP, STR, INT
        body.Add(new Picture("ui/stat_label_mp.png", bx + 231, by + 52)); // MP
        body.Add(new Picture("ui/stat_label_attack.png", bx + 11, by + 136)); // the attack stats
        // Page 1 is mesos / drop / exp, page 2 is defense / speed / knockback resistance; the art carries the dots.
        body.Add(new Picture(_statPage == 0 ? "ui/stat_label_utility.png" : "ui/stat_label_defense.png", bx + 12, by + 329));
        body.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => CurrentCombatPower().ToString("#,##0"), bx + _statLay.Get("cpX", 437), by + _statLay.Get("cpY", 9), () => Color.White)); // combat power, at the right end of the bar
        var rows = StatRows();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int index = i; // the label reads the value later, so the index must be its own copy
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(bx + row.X, by + row.Y, row.W, 16)));
            o.Add(new Button { Kind = ButtonKind.Silent }); // the row only takes the hover
            o.Add(new StatTip(() => StatTipFor(PanelRow(index))));
            o.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => StatValue(PanelRow(index)), row.Vx - row.X, row.Vy - row.Y, () => StatRowColor(PanelRow(index))));
            body.AddChild(o);
        }
        // The page dots flip the bottom block between its two pages.
        body.AddChild(PanelButton(new Rectangle(bx + _statLay.Get("pageX", 204), by + _statLay.Get("pageY", 388),
            _statLay.Get("pageW", 37), _statLay.Get("pageH", 11)), "", () => { _statPage ^= 1; _statBuilt = false; }, ButtonKind.Click));
    }

    // One stat column: the hover box (x, w) and the number spot (vx).
    private readonly record struct StatColumn(int X, int W, int Vx);

    // One stat area: the two columns, the first row and the row pitch.
    private readonly record struct StatArea(StatColumn Left, StatColumn Right, int Y, int Pitch, int Rows);

    // One stat row: the label art carries the name, this is the hover box
    // and the number spot (the stats page space).
    private readonly record struct StatRowBox(int X, int Y, int W, int Vx, int Vy);

    // The official row grid: three areas, two columns each. The row counts
    // are fixed by the official label art (3, 8 and 3 rows).
    private List<StatRowBox> StatRows()
    {
        var areas = new[]
        {
            new StatArea(
                new StatColumn(_statLay.Get("mainLeftX", 7), _statLay.Get("mainLeftW", 215), _statLay.Get("mainLeftVx", 217)),
                new StatColumn(_statLay.Get("mainRightX", 227), _statLay.Get("mainRightW", 201), _statLay.Get("mainRightVx", 425)),
                _statLay.Get("mainY", 48), _statLay.Get("mainPitch", 22), 3),
            new StatArea(
                new StatColumn(_statLay.Get("atkLeftX", 5), _statLay.Get("atkLeftW", 215), _statLay.Get("atkLeftVx", 217)),
                new StatColumn(_statLay.Get("atkRightX", 227), _statLay.Get("atkRightW", 215), _statLay.Get("atkRightVx", 437)),
                _statLay.Get("atkY", 131), _statLay.Get("atkPitch", 22), 8),
            new StatArea(
                new StatColumn(_statLay.Get("utilLeftX", 5), _statLay.Get("utilLeftW", 215), _statLay.Get("utilLeftVx", 217)),
                new StatColumn(_statLay.Get("utilRightX", 227), _statLay.Get("utilRightW", 215), _statLay.Get("utilRightVx", 437)),
                _statLay.Get("utilY", 324), _statLay.Get("utilPitch", 22), 3),
        };
        var rows = new List<StatRowBox>();
        foreach (var a in areas)
            for (int i = 0; i < a.Rows; i++)
            {
                int y = a.Y + i * a.Pitch;
                rows.Add(new StatRowBox(a.Left.X, y, a.Left.W, a.Left.Vx, y + 1));
                rows.Add(new StatRowBox(a.Right.X, y, a.Right.W, a.Right.Vx, y + 1));
            }
        return rows;
    }

    // The live value of one row, read when the row draws so the numbers
    // follow the game. The index follows the row order of StatRows: the main
    // area (HP, MP, STR, DEX, INT, LUK), the attack area (damage range,
    // damage, final damage, boss damage, ignore defense, normal enemy damage,
    // attack power, critical rate, magic att, critical damage, cooldown
    // reduction, buff duration, cooldown not applied, ignore elemental
    // resistance, additional status damage, summons duration), the utility
    // area (mesos, star force, drop rate, arcane power, exp, sacred power),
    // and 28..33 = page 2 of the bottom block. Every value comes from the
    // one snapshot; a row the snapshot has no key for shows 0 (0% for a
    // percentage row).
    private string StatValue(int index)
    {
        var c = _save?.Slots[_slot];
        switch (index)
        {
            case 0:
                {
                    long maxHp = _stats.IntValue(StatKey.MAX_HP);
                    return $"{(c?.Hp ?? maxHp):#,##0} / {maxHp:#,##0}";
                }
            case 1:
                {
                    long maxMp = _stats.IntValue(StatKey.MAX_MP);
                    return $"{(c?.Mp ?? maxMp):#,##0} / {maxMp:#,##0}";
                }
            case 6: return DamageRange();
            case 8:
                {
                    // Final damage with the Elemental Drain layers on top: the build's final multiplier times the Drain multiplier.
                    double mult = DamageChain.FinalDamageMultiplier(_stats) * (1.0 + CurrentDrainLayers() * DrainPctNow() / 100.0);
                    return StatDisplay.Format(StatFormat.Pct2, (mult - 1.0) * 100.0);
                }
            default: return StatDisplay.Text(_stats, index); // the format and the rounding of every other row live in Shared
        }
    }

    // The panel row (0..33) of a box: the bottom block's boxes show the next
    // six rows on page 2.
    private int PanelRow(int index) => index >= UtilityFirstRow ? index + _statPage * 6 : index;

    // What the hover of one row shows: the official text and the source groups. The Status Resistance text carries a %d for the
    // share of the status duration the number cuts.
    private StatTipView StatTipFor(int row)
    {
        var desc = _statDesc.Rows[row];
        var lines = desc.Lines.Select(l => row == StatDisplay.StatusResistanceRow
            ? l.Replace("%d%%", $"{StatDisplay.StatusReductionPct(_stats.Additive(StatKey.STATUS_RESISTANCE))}%")
            : l).ToList();
        var sources = row == 6 // the damage range has no source of its own
            ? new List<(string, string)>()
            : StatDisplay.Sources(_stats, row).Select(l => (l.Label, StatDisplay.LineText(row, l))).ToList();
        if (row == StatDisplay.FinalDamageRow && CurrentDrainLayers() > 0) // the final damage row also carries the Drain multiplier
            sources.Add(("Elemental Drain", StatDisplay.Format(StatFormat.Pct2, CurrentDrainLayers() * DrainPctNow())));
        return new StatTipView(desc.Title, lines, sources);
    }

    // The damage range the panel shows: the current class and weapon profile
    // read from the same snapshot as the rest of the panel, raised by the Elemental Drain layers.
    private string DamageRange()
    {
        var (lo, hi) = DamageChain.PanelRange(_stats, CurrentJob());
        double factor = 1.0 + CurrentDrainLayers() * DrainPctNow() / 100.0;
        return $"{(long)Math.Floor(lo * factor):#,##0}~{(long)Math.Floor(hi * factor):#,##0}";
    }

    // Elemental Drain raises the damage range (row 6) and the final damage (row 8): those two rows turn magenta while it has layers.
    private Color StatRowColor(int row) =>
        (row == 6 || row == StatDisplay.FinalDamageRow) && CurrentDrainLayers() > 0 ? DrainMagenta : Color.White;
    private static readonly Color DrainMagenta = new(255, 90, 255);

    // The combat power: the wiki formula of CombatPower, read from the full snapshot (every source, as the panel).
    private long CurrentCombatPower() =>
        CombatPower.Calculate(_stats, CurrentJob(), StatTables.MaxWeaponCoefficient(_save?.Slots[_slot]?.Job ?? 0));
}

// The character name on the official blue plate: the plate grows with the
// name. Only the middle column stretches, so the rounded ends keep their
// shape and the 1px edge stays 1px instead of turning into a white band.
public sealed class NamePlate : Component, IDrawable
{
    private const int Cap = 7; // the rounded end of the 89x19 plate
    private readonly Func<string> _text;
    private readonly int _cx, _y;
    public NamePlate(Func<string> text, int cx, int y) { _text = text; _cx = cx; _y = y; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var t = gfx.Ui.GetSmall(_text());
        var plate = gfx.Tex("ui/stat_name.png");
        int w = Math.Max(plate.Width, t.Width + 14); // the official plate width; a longer name stretches it
        int x = r.X + _cx - w / 2, y = r.Y + _y;
        int h = plate.Height;
        gfx.Sb.Draw(plate, new Rectangle(x, y, Cap, h), new Rectangle(0, 0, Cap, h), Color.White);
        gfx.Sb.Draw(plate, new Rectangle(x + Cap, y, w - Cap * 2, h), new Rectangle(plate.Width / 2, 0, 1, h), Color.White);
        gfx.Sb.Draw(plate, new Rectangle(x + w - Cap, y, Cap, h), new Rectangle(plate.Width - Cap, 0, Cap, h), Color.White);
        Text.Draw(gfx.Sb, gfx.Ui, t, new Rectangle(x, y, w, h), Color.White, TextLine.Middle, TextAlign.Center);
    }
}

// What a stat row hover shows: the row name, the official text and the source
// groups (label, number).
public sealed record StatTipView(string Title, List<string> Lines, List<(string Label, string Value)> Sources);

// The hover box of a stat row (the official Default tooltip frame): the row
// name, the text of UICharacterInfo.img detailStat, then the "in use" list of
// source groups. The tip layer draws it next to the cursor.
public sealed class StatTip : Component
{
    private readonly Func<StatTipView> _view;
    public StatTip(Func<StatTipView> view) => _view = view;

    public void DrawAt(UiGfx gfx, TipAnchor anchor, PanelLayout lay)
    {
        var view = _view();
        int pad = lay.Get("statTipPad", 8), gap = lay.Get("statTipLineGap", 16), valueGap = lay.Get("statTipValueGap", 16);
        int textW = lay.Get("statTipW", 260) - pad * 2;
        var title = gfx.Ui.GetSmall(view.Title);
        var lines = new List<Texture2D?>(); // null = the blank line between two paragraphs
        foreach (string line in view.Lines)
            if (line == "") lines.Add(null);
            else foreach (string wrapped in gfx.Ui.Wrap(line, textW)) lines.Add(gfx.Ui.GetSmall(wrapped));
        var inUse = gfx.Ui.GetSmall("[In Use]");
        var labels = view.Sources.Select(s => gfx.Ui.GetSmall(s.Label)).ToList();
        var values = view.Sources.Select(s => gfx.Ui.GetSmall(s.Value)).ToList();
        int rowW = 0;
        for (int i = 0; i < labels.Count; i++) rowW = Math.Max(rowW, labels[i].Width + valueGap + values[i].Width);
        int inner = Math.Max(Math.Max(title.Width, lines.Max(l => l?.Width ?? 0)), rowW);
        int w = inner + pad * 2;
        int rows = 2 + lines.Count + (labels.Count > 0 ? 2 + labels.Count : 0); // title + blank, text, blank + header + sources
        var box = anchor.At(gfx, w, pad * 2 + rows * gap);
        NineSlice.Draw(gfx, "tip", box);
        gfx.Sb.Draw(title, new Vector2(box.X + (w - title.Width) / 2, box.Y + pad), Color.White);
        int y = box.Y + pad + gap * 2;
        foreach (var line in lines)
        {
            if (line != null) gfx.Sb.Draw(line, new Vector2(box.X + pad, y), new Color(214, 216, 222));
            y += gap;
        }
        if (labels.Count == 0) return;
        y += gap;
        gfx.Sb.Draw(inUse, new Vector2(box.X + pad, y), Color.White);
        y += gap;
        for (int i = 0; i < labels.Count; i++)
        {
            gfx.Sb.Draw(labels[i], new Vector2(box.X + pad, y), new Color(214, 216, 222));
            gfx.Sb.Draw(values[i], new Vector2(box.Right - pad - values[i].Width, y), Color.White);
            y += gap;
        }
    }
}
