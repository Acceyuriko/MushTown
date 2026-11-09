using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The character info window (UICharacterInfo.img): the official Character
// Info block with the portrait on top and the STAT block with the live
// numbers under it. Every number of the window lives in
// layouts/panel-stat.json.
public partial class MushTownGame
{
    private static readonly PanelLayout _statLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-stat.json"));

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
            "stat_label_main.png", "stat_label_mp.png", "stat_label_attack.png", "stat_label_utility.png",
            "stat_hl_long.png", "stat_hl_short.png",
            "tip_nw.png", "tip_ne.png", "tip_sw.png", "tip_se.png", "tip_n.png", "tip_s.png",
            "tip_w.png", "tip_e.png", "tip_c.png",
        })
            LoadTexture("ui/" + f);
    }

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
        feet.Add(new AvatarPreview(this, "stand1", StandFrame, false, () => Hero));
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

    // The idle frame of the portrait: the same clock every other idle in the
    // game reads, so the doll breathes instead of holding one frame.
    private int StandFrame() => Idle.At(Delays("stand1", Hero?.Skin ?? 0), (float)_timeMs);

    // The stats page: the official panel art with the live numbers.
    private void BuildStatPage(GameObject body)
    {
        int bx = StatBodyX, by = StatBodyY;
        body.Add(new Picture("ui/stat_page.png", bx, by)); // the 448x406 stat panel (the two light panels are in it)
        body.Add(new Picture("ui/stat_label_main.png", bx + 12, by + 52)); // HP, STR, INT
        body.Add(new Picture("ui/stat_label_mp.png", bx + 231, by + 52)); // MP
        body.Add(new Picture("ui/stat_label_attack.png", bx + 11, by + 136)); // the attack stats
        body.Add(new Picture("ui/stat_label_utility.png", bx + 12, by + 329)); // mesos and exp
        body.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => CombatPower().ToString("#,##0"), bx + _statLay.Get("cpX", 437), by + _statLay.Get("cpY", 9), Color.White)); // combat power, at the right end of the bar
        var rows = StatRows();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int index = i; // the label reads the value later, so the index must be its own copy
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(bx + row.X, by + row.Y, row.W, 16)));
            o.Add(new Button { Kind = ButtonKind.Silent }); // the row only takes the hover
            o.Add(new StatHighlight(row.Long ? "ui/stat_hl_long.png" : "ui/stat_hl_short.png"));
            o.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => StatValue(index), row.Vx - row.X, row.Vy - row.Y, Color.White));
            body.AddChild(o);
        }
    }

    // One stat column: the hover box (x, w), the number spot (vx) and the
    // bar art the official page uses for that column.
    private readonly record struct StatColumn(int X, int W, int Vx, bool Long);

    // One stat area: the two columns, the first row and the row pitch.
    private readonly record struct StatArea(StatColumn Left, StatColumn Right, int Y, int Pitch, int Rows);

    // One stat row: the label art carries the name, this is the hover box
    // and the number spot (the stats page space).
    private readonly record struct StatRowBox(int X, int Y, int W, int Vx, int Vy, bool Long);

    // The official row grid: three areas, two columns each. The row counts
    // are fixed by the official label art (3, 8 and 3 rows).
    private List<StatRowBox> StatRows()
    {
        var areas = new[]
        {
            new StatArea(
                new StatColumn(_statLay.Get("mainLeftX", 7), _statLay.Get("mainLeftW", 215), _statLay.Get("mainLeftVx", 217), true),
                new StatColumn(_statLay.Get("mainRightX", 227), _statLay.Get("mainRightW", 201), _statLay.Get("mainRightVx", 425), false),
                _statLay.Get("mainY", 48), _statLay.Get("mainPitch", 22), 3),
            new StatArea(
                new StatColumn(_statLay.Get("atkLeftX", 5), _statLay.Get("atkLeftW", 215), _statLay.Get("atkLeftVx", 217), true),
                new StatColumn(_statLay.Get("atkRightX", 227), _statLay.Get("atkRightW", 215), _statLay.Get("atkRightVx", 437), true),
                _statLay.Get("atkY", 131), _statLay.Get("atkPitch", 22), 8),
            new StatArea(
                new StatColumn(_statLay.Get("utilLeftX", 5), _statLay.Get("utilLeftW", 215), _statLay.Get("utilLeftVx", 217), true),
                new StatColumn(_statLay.Get("utilRightX", 227), _statLay.Get("utilRightW", 215), _statLay.Get("utilRightVx", 437), true),
                _statLay.Get("utilY", 324), _statLay.Get("utilPitch", 22), 3),
        };
        var rows = new List<StatRowBox>();
        foreach (var a in areas)
            for (int i = 0; i < a.Rows; i++)
            {
                int y = a.Y + i * a.Pitch;
                rows.Add(new StatRowBox(a.Left.X, y, a.Left.W, a.Left.Vx, y + 1, a.Left.Long));
                rows.Add(new StatRowBox(a.Right.X, y, a.Right.W, a.Right.Vx, y + 1, a.Right.Long));
            }
        return rows;
    }

    // The live value of one row, read when the row draws so the numbers
    // follow the game. The index follows the row order of StatRows: the main
    // area (HP, MP, STR, DEX, INT, LUK), the attack area (damage range, final
    // damage, attack power, critical rate), the utility area (mesos, exp).
    // The stats the game has no value for show 0.
    private string StatValue(int index)
    {
        var c = _save?.Slots[_slot];
        switch (index)
        {
            case 0:
                {
                    int maxHp = Progression.BuffedMaxHp(c?.Level ?? 1, OwnHpPct());
                    return $"{c?.Hp ?? maxHp} / {maxHp}";
                }
            case 1:
                {
                    int maxMp = Progression.MaxMp(c?.Level ?? 1);
                    return $"{c?.Mp ?? maxMp} / {maxMp}";
                }
            case 2: return $"{OwnBuild().Str}";
            case 3: return $"{OwnBuild().Dex}";
            case 4: return $"{c?.Int ?? 4}";
            case 5: return $"{c?.Luk ?? 4}";
            case 6: return DamageRange();
            case 8: return $"{OwnBuild().FinalPct}%"; // final damage
            case 12: return $"{OwnBuild().WeaponAtk}";
            case 13: return $"{OwnBuild().CritPct}%"; // critical rate
            case 22: return $"{(c?.Mesos ?? 0).ToString("#,##0")}";
            case 26: return $"{c?.Exp ?? 0} / {Progression.ExpNext(c?.Level ?? 1)}";
            default: return "0";
        }
    }

    // The damage range the panel shows: the melee range of the own build.
    private string DamageRange()
    {
        var b = OwnBuild();
        var (lo, hi) = Progression.PanelRange(b.Str, b.Dex, b.WeaponAtk);
        return $"{lo}~{hi}";
    }

    // The combat power: our own number, not the official one (the official
    // formula is not in the WZ). It grows with the level, the attack and the
    // main stats, so the gear and the skills move it.
    private int CombatPower()
    {
        var b = OwnBuild();
        int level = _save?.Slots[_slot]?.Level ?? 1;
        return level * 100 + b.WeaponAtk * 4 + b.Str + b.Dex;
    }
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

// The official row highlight: the bar under the hovered stat row. It covers
// the row box, so the bar is always as wide as the column it belongs to.
public sealed class StatHighlight : Component, IDrawable
{
    private readonly string _art;
    public StatHighlight(string art) => _art = art;
    public void Draw(UiGfx gfx)
    {
        if (gfx.Hovered != Owner) return;
        var r = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex(_art), new Rectangle(r.X, r.Y, r.Width, r.Height), Color.White);
    }
}
