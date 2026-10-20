using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The character info window: the official MapleStory art (UICharacterInfo).
// One window with two tabs: STAT (the stats page) and EQUIP (the equip
// page). The official label art carries the stat names, the numbers are
// live, the equip slots show the worn items and open the item detail box.
// Every number of the window lives in layouts/panel-stat.json.
public partial class MushTownGame
{
    private static readonly PanelLayout _statLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-stat.json"));

    private int _statTab; // 0 = STAT, 1 = EQUIP
    private bool _statPlaced; // the window took the spot from the layout file
    private int _statDetail; // the worn item the user clicked (0 = none)

    // The window: the official detail window (472x479), the tab bar on top.
    private int StatW => _statLay.Get("w", 472);
    private int StatH => _statLay.Get("h", 479);
    private int StatBodyX => _statLay.Get("bodyX", 12);
    private int StatBodyY => _statLay.Get("bodyY", 38);
    private Rectangle StatRect() => new(_statPos.X, _statPos.Y, StatW, StatH);
    private Rectangle StatTabRect(int tab) => new(
        _statPos.X + _statLay.Get("tabX", 11) + tab * _statLay.Get("tabGap", 113),
        _statPos.Y + _statLay.Get("tabY", 12), 111, 20);
    private Rectangle StatXRect() => new(_statPos.X + StatW - 23, _statPos.Y + _statLay.Get("tabY", 12), 11, 11);
    private Rectangle StatDragRect() => new(StatTabRect(1).Right + 4, _statPos.Y, StatXRect().X - StatTabRect(1).Right - 8, 32);

    // The window takes the spot from the layout file the first time it opens.
    private void StatPlaceFirst()
    {
        if (_statPlaced) return;
        _statPlaced = true;
        _statPos = new Point(_statLay.Get("x", 656), _statLay.Get("y", 130));
    }

    private void LoadStatArt()
    {
        foreach (string f in new[]
        {
            "stat_win.png", "stat_tab_stat_s.png", "stat_tab_stat_n.png", "stat_tab_equip_s.png",
            "stat_tab_equip_n.png", "stat_x.png", "stat_page.png", "stat_label_main.png",
            "stat_label_mp.png", "stat_label_attack.png", "stat_label_utility.png", "stat_hl_long.png",
            "stat_hl_short.png", "stat_equip.png", "tip_nw.png", "tip_ne.png", "tip_sw.png", "tip_se.png",
            "tip_n.png", "tip_s.png", "tip_w.png", "tip_e.png", "tip_c.png",
        })
            LoadTexture("ui/" + f);
    }

    private void BuildStatScene()
    {
        LoadStatArt();
        StatPlaceFirst();
        _statPos = ClampPanel(_statPos, StatW); // the window is wider than the old one: keep it on screen
        _statScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(StatRect())); // the body swallows the clicks, like the legacy fallthrough
        body.Add(new Picture("ui/stat_win.png")); // the official detail window
        if (_statTab == 0) BuildStatPage(body);
        else BuildEquipPage(body);
        // the tab bar: the official STAT and EQUIP buttons
        int tabX = _statLay.Get("tabX", 11), tabY = _statLay.Get("tabY", 12);
        body.Add(new Picture(_statTab == 0 ? "ui/stat_tab_stat_s.png" : "ui/stat_tab_stat_n.png", tabX, tabY));
        body.Add(new Picture(_statTab == 1 ? "ui/stat_tab_equip_s.png" : "ui/stat_tab_equip_n.png", tabX + _statLay.Get("tabGap", 113), tabY));
        body.Add(new Picture("ui/stat_x.png", StatXRect().X - _statPos.X, StatXRect().Y - _statPos.Y));
        // the name and level sit on the free strip right of the tabs
        body.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => $"{_save?.Slots[_slot]?.Name ?? ""}  Lv.{_save?.Slots[_slot]?.Level ?? 1}",
            _statLay.Get("nameX", 440), _statLay.Get("nameY", 8), new Color(200, 220, 0)));
        body.AddChild(PanelButton(StatTabRect(0), "", () => SetStatTab(0), ButtonKind.Click));
        body.AddChild(PanelButton(StatTabRect(1), "", () => SetStatTab(1), ButtonKind.Click));
        body.AddChild(PanelButton(StatXRect(), "x1", () => _showStat = false, ButtonKind.Click));
        body.AddChild(PanelButton(StatDragRect(), "", () => _dragStat = _statScene.LastClick - _statPos));
        _statScene.Add(body);
    }

    // The tab changed: the page changed with it, so the scene is rebuilt.
    private void SetStatTab(int tab)
    {
        _statTab = tab;
        _statDetail = 0;
        BuildStatScene();
        _statBuiltPos = _statPos;
    }

    // The stats page: the official panel art with the live numbers.
    private void BuildStatPage(GameObject body)
    {
        int bx = StatBodyX, by = StatBodyY;
        body.Add(new Picture("ui/stat_page.png", bx, by)); // the 448x406 stat panel (the two light panels are in it)
        body.Add(new Picture("ui/stat_label_main.png", bx + 12, by + 52)); // HP, STR, INT
        body.Add(new Picture("ui/stat_label_mp.png", bx + 231, by + 52)); // MP
        body.Add(new Picture("ui/stat_label_attack.png", bx + 11, by + 136)); // the attack stats
        body.Add(new Picture("ui/stat_label_utility.png", bx + 12, by + 329)); // mesos and exp
        body.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => "0", bx + _statLay.Get("cpX", 437), by + _statLay.Get("cpY", 9), Color.White)); // combat power, at the right end of the bar
        var rows = StatRows();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int index = i; // the label reads the value later, so the index must be its own copy
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(_statPos.X + bx + row.X, _statPos.Y + by + row.Y, row.W, 16)));
            o.Add(new Button { Kind = ButtonKind.Silent }); // the row only takes the hover
            o.Add(new StatHighlight(row.Long ? "ui/stat_hl_long.png" : "ui/stat_hl_short.png"));
            o.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => StatValue(index), row.Vx - row.X, row.Vy - row.Y, Color.White));
            body.AddChild(o);
        }
    }

    // The equip page: the official slot frames, the worn items in them.
    private void BuildEquipPage(GameObject body)
    {
        int bx = StatBodyX, by = StatBodyY;
        body.Add(new Picture("ui/stat_equip.png", bx, by));
        foreach (int id in _save?.Slots[_slot]?.Equips ?? new List<int>())
        {
            if (!_items.TryGetValue(id, out var data)) continue;
            var box = EquipSlotBox(data.Slot);
            if (box.X < 0) continue; // a kind the official page has no slot for
            var slot = new GameObject();
            slot.Add(new Rect(new Rectangle(_statPos.X + bx + box.X, _statPos.Y + by + box.Y, 42, 42)));
            slot.Add(new Button { Kind = ButtonKind.Click, OnClick = () => ShowStatDetail(id) });
            slot.Add(new EquipIcon(data.Image));
            body.AddChild(slot);
        }
        if (_statDetail != 0) BuildStatDetail(body, _statDetail);
    }

    // Clicking a slot opens the item detail; clicking it again closes it.
    private void ShowStatDetail(int id)
    {
        _statDetail = _statDetail == id ? 0 : id;
        BuildStatScene();
        _statBuiltPos = _statPos;
    }

    // The item detail box: the official tooltip frame around the item.
    private void BuildStatDetail(GameObject body, int id)
    {
        var data = _items[id];
        int x = _statLay.Get("detailX", 252), y = _statLay.Get("detailY", 68);
        int w = _statLay.Get("detailW", 200), h = _statLay.Get("detailH", 160);
        int iconSize = _statLay.Get("detailIconSize", 32);
        var box = new GameObject();
        box.Add(new Rect(new Rectangle(_statPos.X + x, _statPos.Y + y, w, h)));
        box.Add(new ToolTipFrame());
        box.Add(new LiveLabel(() => data.Name, _statLay.Get("detailNameX", 48), _statLay.Get("detailNameY", 6), new Color(255, 220, 0)));
        int ly = _statLay.Get("detailLineY", 28);
        foreach (string line in ItemDetailLines(data))
        {
            box.Add(new LiveLabel(() => line, _statLay.Get("detailLineX", 48), ly, Color.White));
            ly += _statLay.Get("detailLineGap", 16);
        }
        body.AddChild(box);
        var icon = new GameObject();
        icon.Add(new Rect(new Rectangle(_statPos.X + x + _statLay.Get("detailIconX", 8), _statPos.Y + y + _statLay.Get("detailIconY", 8), iconSize, iconSize)));
        icon.Add(new EquipIcon(data.Image));
        body.AddChild(icon);
    }

    // The detail lines of one item: the bonuses it gives.
    private static List<string> ItemDetailLines(ItemData data)
    {
        var lines = new List<string>();
        if (data.Atk != 0) lines.Add($"Attack +{data.Atk}");
        if (data.Def != 0) lines.Add($"Defense +{data.Def}");
        if (data.Str != 0) lines.Add($"STR +{data.Str}");
        if (data.Dex != 0) lines.Add($"DEX +{data.Dex}");
        if (data.Hp != 0) lines.Add($"HP +{data.Hp}");
        if (data.Mp != 0) lines.Add($"MP +{data.Mp}");
        if (data.Price > 0) lines.Add($"Sells for {Shop.SellPrice(data.Price).ToString("#,##0")} mesos");
        return lines;
    }

    // The equip slots the game has data for: the WZ slot kind gives the slot
    // box on the official equip page (a kind with no slot stays empty).
    private (int X, int Y) EquipSlotBox(string? kind) => kind switch
    {
        "Wp" => (_statLay.Get("weaponX", 105), _statLay.Get("weaponY", 83)),
        "Ma" => (_statLay.Get("topX", 150), _statLay.Get("topY", 83)),
        "Ri" => (_statLay.Get("ringX", 15), _statLay.Get("ringY", 38)),
        _ => (-1, -1),
    };

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
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex(_art), new Rectangle(r.X, r.Y, r.Width, r.Height), Color.White);
    }
}

// The item icon in the middle of an equip slot.
public sealed class EquipIcon : Component, IDrawable
{
    private readonly string _image;
    public EquipIcon(string image) => _image = image;
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var icon = gfx.Tex(_image);
        gfx.Sb.Draw(icon, new Vector2(r.X + (r.Width - icon.Width) / 2, r.Y + (r.Height - icon.Height) / 2), Color.White);
    }
}

// The official item tooltip frame: nine pieces (four corners, four edges and
// the center) drawn around the owner rect.
public sealed class ToolTipFrame : Component, IDrawable
{
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var nw = gfx.Tex("ui/tip_nw.png"); var ne = gfx.Tex("ui/tip_ne.png");
        var sw = gfx.Tex("ui/tip_sw.png"); var se = gfx.Tex("ui/tip_se.png");
        var n = gfx.Tex("ui/tip_n.png"); var s = gfx.Tex("ui/tip_s.png");
        var w = gfx.Tex("ui/tip_w.png"); var e = gfx.Tex("ui/tip_e.png");
        var c = gfx.Tex("ui/tip_c.png");
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
