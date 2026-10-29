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

    // The official slot grid (measured on the art): 36px cells, pitch 46,
    // the first cell at (31,101). The small window shows 4 columns, the
    // wide one 16.
    private int InvCols => _invWide ? _invLay.Get("colsWide", 16) : _invLay.Get("cols", 4);
    private Rectangle InvCell(int cell)
    {
        int pitch = _invLay.Get("cellPitch", 46);
        int size = _invLay.Get("cellSize", 36);
        return new Rectangle(_invLay.Get("gridX", 31) + (cell % InvCols) * pitch,
            _invLay.Get("gridY", 101) + (cell / InvCols) * pitch, size, size);
    }

    // The worn gear takes the first row(s); the bag rows start below it.
    private int InvWornRows => (_equips.Count + InvCols - 1) / InvCols;

    private GameObject PanelSlot(Rectangle r, string itemKind, int index, Func<int> count)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        var s = new Slot { ItemKind = itemKind, Index = index };
        s.OnClick = () => ClickSlot(itemKind, index);
        o.Add(s);
        int id = ItemId(itemKind, index);
        o.Add(new ItemTip(() => count() == 0 ? 0 : id)); // the empty cell shows no tip
        o.Add(new InvSlot(_items[id].Image, count, false));
        return o;
    }

    // The item a cell stands for: the USE tab lists potions, the EQUIP tab gear.
    private int ItemId(string kind, int index) => kind == "use" ? _potions[index] : _equips[index];

    private void BuildInvScene()
    {
        LoadInvArt();
        InvPlaceFirst();
        _invPos = ClampPanel(_invPos, InvW); // keep the window on screen (the wide form is wider)
        _invHost.Clear();
        var body = new GameObject();
        body.Add(new Rect(InvRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new Picture(_invWide ? "ui/invw_backgrnd.png" : "ui/inv_backgrnd.png"));
        if (_invTab == 1)
            for (int s = 0; s < _potions.Count; s++)
            {
                int i = s; // for-loop variables capture shared: copy per slot
                int id = _potions[i];
                body.AddChild(PanelSlot(InvCell(i), "use", i, () => _save?.Slots[_slot]?.Use?.GetValueOrDefault(id) ?? 0));
            }
        if (_invTab == 0)
            for (int s = 0; s < _equips.Count; s++)
            {
                int i = s; // for-loop variables capture shared: copy per slot
                int id = _equips[i];
                // The worn gear sits on the first row(s). The small window has
                // 4 columns, so 5 equip types wrap onto a second row: that is
                // the official layout, not a bug.
                body.AddChild(PanelSlot(InvCell(i), "worn", i, () => _save?.Slots[_slot]?.Equips?.Count(w => w == id) ?? 0));
                body.AddChild(PanelSlot(InvCell(InvWornRows * InvCols + i), "bag", i, () => _save?.Slots[_slot]?.EquipBag?.GetValueOrDefault(id) ?? 0));
            }
        for (int t = 0; t < TabCount; t++)
        {
            int i = t; // for-loop variables capture shared: copy per tab
            var tab = new GameObject();
            tab.Add(new Rect(TabRect(i)));
            // Only Equip and Use have content in v1. The other four keep the
            // official art, but they do not react to a click.
            if (i < 2)
                tab.Add(new Button { LegacyId = "invtab" + i, Kind = ButtonKind.Click, OnClick = () => _invTab = i });
            tab.Add(new Picture($"ui/{InvArtPrefix()}_tab_{(i == _invTab ? "s" : "n")}{i}.png", 0, 0));
            body.AddChild(tab);
        }
        body.AddChild(PanelButton(InvTitleRect(), "", () => _dragInv = _uiScene.LastClick - _invPos)); // silent, like legacy
        body.AddChild(InvExpandButton()); // the "+" (small) or the "-" (wide)
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
            for (int t = 0; t < TabCount; t++)
            {
                LoadTexture($"ui/{p}_tab_s{t}.png");
                LoadTexture($"ui/{p}_tab_n{t}.png");
            }
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
            $"ui/{InvArtPrefix()}_close", () => _showInv = false);
        o.Get<Button>()!.LegacyId = "x0"; // the hover tick
        return o;
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
        _invWide = !_invWide;
        BuildInvScene();
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
        if (chara == null) return; // CLI sessions carry no bag
        int id = ItemId(kind, index);
        var act = _hand.Click(chara, kind, id, _items, OwnHpPct(), _timeMs);
        if (act == HandAct.Wore) { PushBuild(); PushName(); PlaySfx("click"); }
        else if (act == HandAct.Moved) PlaySfx("click");
    }
}
