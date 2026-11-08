using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The inventory window: the official MapleStory art (UIInventory.img).
// Two official forms: the small window (242x593) and the wide one
// (800x546) that the "+" button opens; the "-" button goes back.
public partial class MushTownGame
{
    private const int TabCount = 6;

    // Every number of this window lives in layouts/panel-inventory.json.
    private static readonly PanelLayout _invLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-inventory.json"));

    private bool _invWide; // the wide window is open
    private bool _invPlaced; // the window took the spot from the layout file

    private int InvW => _invWide ? _invLay.Get("wExpanded", 800) : _invLay.Get("w", 242);
    private int InvH => _invWide ? _invLay.Get("hExpanded", 546) : _invLay.Get("h", 593);
    private Rectangle InvRect() => new(0, 0, InvW, InvH);
    private Rectangle InvTitleRect() => new(0, 0, InvW, _invLay.Get("titleH", 28));

    // The official slot grid (UIInventory.img pos): 42px cells, pitch 46,
    // the first cell at (28,101). The small window shows 4 columns, the
    // wide one 16.
    private int InvCols => _invWide ? _invLay.Get("colsWide", 16) : _invLay.Get("cols", 4);
    private int InvPageCols => _invLay.Get("pageCols", 4);
    private int InvPageGapX => _invLay.Get("pageGapX", 2);
    private int InvRows => _invLay.Get("rows", 8);
    private Rectangle InvGridRect()
    {
        int pitch = _invLay.Get("cellPitch", 46);
        int size = _invLay.Get("cellSize", 42);
        var last = InvCell(InvCols - 1);
        return new Rectangle(_invLay.Get("gridX", 28), _invLay.Get("gridY", 101),
            last.Right, (InvRows - 1) * pitch + size);
    }
    private Rectangle InvCell(int cell)
    {
        int pitch = _invLay.Get("cellPitch", 46);
        int size = _invLay.Get("cellSize", 42);
        int col = cell % InvCols;
        int page = col / InvPageCols;
        int x = page * (InvPageCols * pitch + InvPageGapX) + (col % InvPageCols) * pitch;
        return new Rectangle(x, (cell / InvCols) * pitch, size, size);
    }

    // The visible cells: small 4x8, wide 16x8.
    private int InvVisible => InvCols * InvRows;

    private static string TabKind(int tab) => tab switch
    {
        0 => "equip",
        1 => "use",
        4 => "cash",
        5 => "dec",
        _ => "", // Etc / Set-up are empty for now
    };

    private GameObject PanelSlot(Rectangle r, string itemKind, int index, Func<int> count)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Slot { OnClick = () => ClickSlot(itemKind, index) });
        o.Add(new ItemTip(() => count() == 0 ? 0 : ItemId(itemKind, index))); // the empty cell shows no tip
        o.Add(new InvItem(() => count() == 0 ? "" : ItemImage(ItemId(itemKind, index))));
        if (itemKind != "equip")
        {
            var number = new GameObject { NoHit = true };
            number.Add(new Rect(new Rectangle(
                _invLay.Get("countItemX", 0), _invLay.Get("countItemY", 28),
                _invLay.Get("countItemW", 42), _invLay.Get("countItemH", 11))));
            number.Add(new InvNumber(count, false, "ui/item_num"));
            o.AddChild(number);
        }
        return o;
    }

    // The icon of an item id; "" for an id this build has no data for
    // (a save may carry an item that was removed from the export).
    private string ItemImage(int id) => _items.TryGetValue(id, out var data) ? data.Image : "";

    // The official gray pill in the small/wide header: used cells / 128. The
    // used number is the white slotNum0 set; the total is the dark slotNum1
    // set, right-aligned to its vector on the right of the baked slash.
    private GameObject InvCount()
    {
        var r = _invWide
            ? new Rectangle(_invLay.Get("countWideX", 710), _invLay.Get("countWideY", 70),
                _invLay.Get("countWideW", 70), _invLay.Get("countWideH", 15))
            : new Rectangle(_invLay.Get("countX", 152), _invLay.Get("countY", 70),
                _invLay.Get("countW", 70), _invLay.Get("countH", 15));
        int usedRight = (_invWide ? _invLay.Get("countUsedWideX", 741) : _invLay.Get("countUsedX", 184)) - r.X;
        int totalRight = (_invWide ? _invLay.Get("countTotalWideX", 768) : _invLay.Get("countTotalX", 210)) - r.X;
        int numH = _invLay.Get("countNumH", 8);
        int gap = _invLay.Get("countGap", 2);
        int top = _invLay.Get("countNumY", 81) - r.Y - numH;
        string kind = TabKind(_invTab);
        var root = new GameObject();
        root.Add(new Rect(r));
        var used = new GameObject { NoHit = true };
        used.Add(new Rect(new Rectangle(0, top, usedRight, numH)));
        used.Add(new InvNumber(() =>
        {
            var chara = _save?.Slots[_slot];
            return chara == null || kind == "" ? 0 : Inventory.Used(chara, kind);
        }, true, zero: true));
        root.AddChild(used);
        var total = new GameObject { NoHit = true };
        total.Add(new Rect(new Rectangle(usedRight + gap, top, totalRight - usedRight - gap, numH)));
        total.Add(new InvNumber(() => Inventory.Capacity, true, "ui/inv_num_total"));
        root.AddChild(total);
        return root;
    }

    // The item a cell stands for: the positional order list of its tab.
    // 0 when the build has no data for that id.
    private int ItemId(string kind, int index)
    {
        var chara = _save?.Slots[_slot];
        if (kind == "" || chara == null) return 0;
        int id = Inventory.SlotId(chara, kind, index);
        return _items.ContainsKey(id) ? id : 0;
    }

    private void BuildInvScene()
    {
        LoadInvArt();
        InvPlaceFirst();
        _invPos = ClampPanel(_invPos, InvW); // keep the window on screen (the wide form is wider)
        _invHost.Clear();
        var body = new GameObject();
        body.Add(new Rect(InvRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new Picture(_invWide ? "ui/invw_backgrnd.png" : "ui/inv_backgrnd.png"));
        string kind = TabKind(_invTab);
        var grid = new GameObject(); // the official slot grid: cell rects are local to it
        grid.Add(new Rect(InvGridRect()));
        body.AddChild(grid);
        for (int s = 0; s < InvVisible; s++)
        {
            int i = s; // for-loop variables capture shared: copy per slot
            grid.AddChild(PanelSlot(InvCell(i), kind, i, () =>
            {
                var chara = _save?.Slots[_slot];
                return kind == "" || chara == null || (_hand.Kind == kind && _hand.Cell == i)
                    ? 0
                    : Inventory.Count(chara, kind, ItemId(kind, i));
            }));
        }
        for (int t = 0; t < TabCount; t++)
        {
            int i = t; // for-loop variables capture shared: copy per tab
            var tab = new GameObject();
            tab.Add(new Rect(TabRect(i)));
            tab.Add(new Button
            {
                LegacyId = "invtab" + i,
                Kind = ButtonKind.Click,
                OnClick = () =>
                {
                    if (!_hand.Empty) _hand.Return(); // the held preview points at the old tab's cells
                    _invTab = i;
                },
            });
            tab.Add(new Picture($"ui/{InvArtPrefix()}_tab_{(i == _invTab ? "s" : "n")}{i}.png", 0, 0));
            body.AddChild(tab);
        }
        body.AddChild(InvCount());
        body.AddChild(PanelButton(InvTitleRect(), "", () => _dragInv = _uiScene.LastClick - _invPos)); // silent, like legacy
        body.AddChild(InvExpandButton()); // the "+" (small) or the "-" (wide)
        body.AddChild(InvSortButton());
        body.AddChild(InvCloseButton());
        var coin = new GameObject { NoHit = true }; // purse draws last (bag rows can reach it when the roster grows)
        coin.Add(new Rect(InvRect()));
        coin.Add(new Picture($"ui/{InvArtPrefix()}_coin.png", _invLay.Get("coinX", 25), _invLay.Get("coinY", 485)));
        coin.Add(new RightLabel((ui, s) => ui.GetMeso(s),
            () => (_save?.Slots[_slot]?.Mesos ?? 0).ToString("#,##0"),
            _invWide ? _invLay.Get("mesoWideX", 185) : _invLay.Get("mesoX", 213),
            _invWide ? _invLay.Get("mesoWideY", 484) : _invLay.Get("mesoY", 484),
            new Color(96, 104, 113)));
        body.AddChild(coin);
        _invHost.AddChild(body);
    }

    // The art file prefix: the small window or the wide one.
    private string InvArtPrefix() => _invWide ? "invw" : "inv";

    // The inventory art: loaded once, then the texture cache answers.
    private void LoadInvArt()
    {
        LoadTexture("ui/inv_backgrnd.png");
        LoadTexture("ui/invw_backgrnd.png");
        foreach (string p in new[] { "inv", "invw" })
        {
            LoadTexture($"ui/{p}_coin.png");
            // The X, and the expand button: the "+" in the small window, the "-" in the wide one.
            foreach (string b in new[] { "close", p == "inv" ? "plus" : "minus" })
                foreach (string s in new[] { "n", "h", "p" })
                    LoadTexture($"ui/{p}_{b}_{s}.png");
            foreach (string s in new[] { "n", "h", "p" })
                LoadTexture($"ui/{p}_sort_{s}.png");
            for (int t = 0; t < TabCount; t++)
            {
                LoadTexture($"ui/{p}_tab_s{t}.png");
                LoadTexture($"ui/{p}_tab_n{t}.png");
            }
        }
        for (int d = 0; d < 10; d++)
        {
            LoadTexture($"ui/inv_num{d}.png"); // SLOT used: white slotNum0
            LoadTexture($"ui/inv_num_total{d}.png"); // SLOT total: dark slotNum1
            LoadTexture($"ui/item_num{d}.png"); // item count: black-outlined ItemNo
        }
        foreach (string side in new[] { "nw", "ne", "sw", "se", "n", "s", "w", "e", "c" })
            LoadTexture($"ui/tip_{side}.png"); // the tooltip frame: the stat window loads it too, this one must not depend on that
    }

    // The official expand button: "+" opens the wide window, "-" goes back.
    private GameObject InvExpandButton()
    {
        int dx = _invWide ? _invLay.Get("minusX", 729) : _invLay.Get("plusX", 193);
        int dy = _invWide ? _invLay.Get("minusY", 8) : _invLay.Get("plusY", 8);
        int w = _invWide ? _invLay.Get("minusW", 19) : _invLay.Get("plusW", 19);
        int h = _invWide ? _invLay.Get("minusH", 19) : _invLay.Get("plusH", 19);
        var o = ArtButton(new Rectangle(dx, dy, w, h),
            $"ui/{InvArtPrefix()}_{(_invWide ? "minus" : "plus")}", ToggleInvWide);
        o.Get<Button>()!.LegacyId = _invWide ? "invmin" : "invplus"; // the hover tick
        return o;
    }

    // The official X (close) button.
    private GameObject InvCloseButton()
    {
        int dx = _invWide ? _invLay.Get("closeWideX", 769) : _invLay.Get("closeX", 211);
        var o = ArtButton(new Rectangle(dx, _invLay.Get("closeY", 4),
                _invLay.Get("closeW", 27), _invLay.Get("closeH", 27)),
            $"ui/{InvArtPrefix()}_close", () =>
            {
                if (!_hand.Empty) _hand.Return();
                _showInv = false;
            });
        o.Get<Button>()!.LegacyId = "x0"; // the hover tick
        return o;
    }

    // The official sort button: sorts only the open tab, from the first cell.
    private GameObject InvSortButton()
    {
        var r = new Rectangle(
            _invWide ? _invLay.Get("sortWideX", 21) : _invLay.Get("sortX", 19),
            _invWide ? _invLay.Get("sortWideY", 67) : _invLay.Get("sortY", 66),
            _invWide ? _invLay.Get("sortWideW", 68) : _invLay.Get("sortW", 22),
            _invWide ? _invLay.Get("sortWideH", 22) : _invLay.Get("sortH", 23));
        var o = ArtButton(r, $"ui/{InvArtPrefix()}_sort", SortInv);
        o.Get<Button>()!.LegacyId = "invsort";
        return o;
    }

    private void SortInv()
    {
        var chara = _save?.Slots[_slot];
        string kind = TabKind(_invTab);
        if (chara == null || kind == "") return;
        if (!_hand.Empty) _hand.Return(); // sorting moves the cells: the held preview must not keep an old index
        Inventory.Sort(chara, kind, _items);
    }

    // The window opens at the spot in the layout file; the drag moves it.
    // "wide" in the layout file starts the wide window (the + button opens it too).
    private void InvPlaceFirst()
    {
        if (_invPlaced) return;
        _invPlaced = true;
        _invWide = _invLay.Get("wide", 0) == 1;
        _invPos = new Point(
            _invWide ? _invLay.Get("xExpanded", 283) : _invLay.Get("x", 1114),
            _invWide ? _invLay.Get("yExpanded", 111) : _invLay.Get("y", 45));
    }

    // The "+" opens the wide window, the "-" puts the small one back.
    // The window keeps the spot the player dragged it to.
    private void ToggleInvWide()
    {
        if (!_hand.Empty) _hand.Return(); // the small form hides cells above 31
        _invWide = !_invWide;
        BuildInvScene();
        _invBuilt = true;
        _invBuiltTab = _invTab;
    }

    // Official tabs: 35x22 art at pitch 37 (small), 128x22 at pitch 130 (wide).
    private Rectangle TabRect(int t)
    {
        int step = _invWide ? _invLay.Get("tabWideStep", 130) : _invLay.Get("tabStep", 37);
        int w = _invWide ? _invLay.Get("tabWideW", 128) : _invLay.Get("tabW", 35);
        return new Rectangle(_invLay.Get("tabX", 11) + step * t, _invLay.Get("tabY", 31),
            w, _invLay.Get("tabH", 22));
    }

    // One inventory slot click, through the hand (pick, drop, wear,
    // swap, double-click drink). Conditional sounds stay explicit here.
    private void ClickSlot(string kind, int index)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || kind == "") return; // CLI sessions carry no bag
        int id = ItemId(kind, index);
        var act = _hand.Click(chara, kind, id, _items, OwnHpPct(), _timeMs, index);
        if (act == HandAct.Wore)
        {
            PushBuild();
            PushName();
            PlaySfx("click");
        }
        else if (act == HandAct.Moved)
        {
            PlaySfx("click");
        }
    }
}
