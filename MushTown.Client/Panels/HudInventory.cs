using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

using CashShopData = MushTown.Shared.CashShop;

namespace MushTown.Client;

// The inventory window: the official MapleStory art (UIInventory.img).
// Two official forms: the small window (242x593) and the wide one
// (800x546) that the "+" button opens; the "-" button goes back.
public partial class MushTownGame
{
    private const int TabCount = 6;

    // Every number of this window lives in layouts/panel-inventory.json.
    private static readonly PanelLayout _invLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-inventory.json"));
    // The Utility popup is a cash shop feature; its numbers live with the shop.
    private static readonly PanelLayout _cashLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-cashshop.json"));
    private readonly TextField _cashUseField = new(_cashLay) { Limit = 12, Ink = new Color(48, 48, 48) };
    private int _cashUseId;
    private string _cashUseNotice = "";

    // Player drop quantity: the hand's bag kind/id, or mesos, while the popup is open.
    private readonly TextField _dropQtyField = new(_shopLay) { DigitsOnly = true, Limit = 11, Ink = Color.Black };
    private int _dropQtyId;
    private bool _dropQtyMeso;
    private bool DropQtyOpen => _dropQtyMeso || _dropQtyId != 0;
    private bool CursorFree => (DropQtyOpen || _hand.Empty) && !Holding && !WornHeld;
    private string _dropQtyKind = "";
    private int _dropWheel;
    private bool _showHeldDropCell; // staging only: keep the held stack visible in the bag

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

    // How many rows the tab actually uses: the last filled cell decides, so
    // the small window never scrolls past its last item into empty rows.
    private int InvUsedRows(string kind)
    {
        var chara = _save?.Slots[_slot];
        if (kind == "" || chara == null) return 0;
        return InventoryScroll.UsedRows(InvCols, i => Inventory.SlotId(chara, kind, i));
    }

    // The last row the small window may show first. The wide form shows all
    // 128 cells, so it never scrolls.
    private int InvScrollMax(string kind) =>
        _invWide ? 0 : InventoryScroll.MaxRow(InvUsedRows(kind), InvRows);

    // Keep the remembered row legal after the contents or the tab changed.
    private void InvClampScroll()
    {
        if (!_showInv || _invWide) return;
        int max = InvScrollMax(TabKind(_invTab));
        _invScroll[_invTab] = Math.Clamp(_invScroll[_invTab], 0, max);
    }

    // The official scrollbar: the small window's track at (214,99), the wide
    // one at (769,99). A disabled track draws no thumb.
    private GameObject InvScrollBar()
    {
        int tab = _invTab;
        string kind = TabKind(tab);
        string prefix = InvArtPrefix();
        int thumbH = _invLay.Get("scrollThumbH", 28);
        var bar = new ScrollBar(
            () => (float)_invScroll[tab] / InvScrollMax(kind),
            value => _invScroll[tab] = (int)MathF.Round(value * InvScrollMax(kind)),
            () => InvScrollMax(kind) > 0,
            $"ui/{prefix}_scroll_base.png", $"ui/{prefix}_scroll_thumb", thumbH);
        _invBar = bar;
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(
            _invWide ? _invLay.Get("scrollWideX", 769) : _invLay.Get("scrollX", 214),
            _invLay.Get("scrollY", 99),
            _invLay.Get("scrollW", 5),
            _invLay.Get("scrollH", 368))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => bar.Grab(_uiScene.LastClick.Y) });
        o.Add(bar);
        return o;
    }

    // The wheel scrolls the open inventory while the mouse is over its grid:
    // one notch is three rows.
    private void UpdateInvWheel(int gx, int gy)
    {
        int wheel = _input.Wheel;
        bool over = _showInv && RootOf(_uiScene.Hovered) == _invHost
            && InvGridScreenRect().Contains(gx, gy);
        if (!over) { _invWheel = wheel; return; }
        int notches = (wheel - _invWheel) / 120;
        _invWheel = wheel;
        if (notches == 0) return;
        int max = InvScrollMax(TabKind(_invTab));
        if (max == 0) return; // the wide form and short tabs keep their saved row
        _invScroll[_invTab] = InventoryScroll.WheelRow(_invScroll[_invTab], notches, max);
    }

    private Rectangle InvGridScreenRect()
    {
        var r = InvGridRect();
        return new Rectangle(_invPos.X + r.X, _invPos.Y + r.Y, r.Width, r.Height);
    }

    // A bag equip dropped on its matching equipment box goes on. The box must
    // be the front window: a covered slot does not take the drop.
    private bool DropBagOnEquip(int gx, int gy)
    {
        if (_hand.Empty || _hand.Kind != "equip") return false;
        if (WindowFor(HotkeyAction.Equip) is not EquipWindow window
            || !window.Show || !window.EquipmentPage) return false;
        if (RootOf(_uiScene.TopAt(gx, gy)) != window.Host) return false;
        if (window.SlotAt(gx, gy) is not { } slot
            || !EquipSlots.Accepts(slot, _hand.Id, _items)) return false;
        if (Hero is not { } chara || !Progression.WearEquip(chara, _hand.Id, _items)) return false;
        FinishWornDrop(window);
        return true;
    }

    // A DEC coupon or fashion dropped on its deco box goes on. The box must
    // be the front window and the item must fill that box.
    private bool DropBagOnCashSlot(int gx, int gy)
    {
        if (_hand.Empty || _hand.Kind != "dec") return false;
        if (WindowFor(HotkeyAction.Equip) is not EquipWindow window
            || !window.Show || !window.CashPage) return false;
        if (RootOf(_uiScene.TopAt(gx, gy)) != window.Host) return false;
        if (window.SlotAt(gx, gy) is not { } slot
            || EquipSlots.CashBox(_hand.Id, _items) != slot) return false;
        if (Hero is not { } chara || !Progression.WearCash(chara, _hand.Id, _items)) return false;
        FinishWornDrop(window);
        return true;
    }

    // The five steps both bag drops end with: empty the hand, repaint the equip
    // window, save, and refresh the world and the name tag.
    private void FinishWornDrop(EquipWindow window)
    {
        _hand.Return();
        window.Dirty = true;
        SaveChara();
        PushBuild();
        PushName();
        PushAppearance();
    }

    // A bag item clicked into the world leaves the bag. One copy drops at
    // once; a stack first asks how many through the shared NPC-shop box.
    private bool DropBagOnWorld()
    {
        if (_hand.Empty) return false;
        var chara = _save?.Slots[_slot];
        if (chara == null) return false;
        int count = Inventory.Count(chara, _hand.Kind!, _hand.Id);
        if (count == 0) return false;
        if (count == 1)
        {
            DropFromBag(chara, _hand.Kind!, _hand.Id, 1);
            return true;
        }
        _dropQtyMeso = false;
        _dropQtyKind = _hand.Kind!;
        _dropQtyId = _hand.Id;
        _dropWheel = _input.Wheel;
        _dropQtyField.SetText("1");
        _dropQtyField.ReplaceOnType = true;
        _dropQtyField.Focus(Keyboard.GetState());
        BuildInvScene();
        _invBuilt = true;
        return true;
    }

    // The bag's coin button: the same popup as a held stack, with the meso
    // balance as its max.
    private void OpenMesoDrop()
    {
        if (Meso <= 0) return;
        if (!_hand.Empty) _hand.Return();
        _dropQtyMeso = true;
        _dropQtyId = 0;
        _dropQtyKind = "";
        _dropWheel = _input.Wheel;
        _dropQtyField.SetText("1");
        _dropQtyField.ReplaceOnType = true;
        _dropQtyField.Focus(Keyboard.GetState());
        BuildInvScene();
        _invBuilt = true;
    }

    private void DropFromBag(CharaSlot chara, string kind, int id, int count)
    {
        bool online = _net != null && _net.Connected;
        if (!online && _localWorld == null) return;
        if (online && !_net!.SendDropItem(id, count, kind)) return; // send failed: bag stays
        if (!Inventory.Remove(chara, kind, id, count)) return;
        _hand.Return();
        if (!online)
        {
            _localWorld!.DropItem(_mapId, _mover.X, _mover.Y, kind, DropKind.Item, id, count);
            DrainDrops(_localWorld);
        }
        SaveChara();
        LogEvent($"dropped {_items[id].Name} x{count}");
    }

    // Mesos leave the purse only after the drop goes out; the account itself
    // is client-owned, so the ground pile is the copy everyone can see.
    private void DropMesos(CharaSlot chara, long amount)
    {
        bool online = _net != null && _net.Connected;
        if (!online && _localWorld == null) return;
        if (chara.Mesos < amount) return;
        if (online && !_net!.SendDropMesos(amount)) return; // send failed: purse stays
        chara.Mesos -= amount;
        if (!online)
        {
            _localWorld!.DropItem(_mapId, _mover.X, _mover.Y, "", DropKind.Mesos, 0, 0, amount);
            DrainDrops(_localWorld);
        }
        SaveChara();
        LogEvent($"dropped {amount} mesos");
    }

    private long DropQuantityMax()
    {
        var chara = _save?.Slots[_slot];
        if (_dropQtyMeso) return chara == null ? 1 : Math.Max(1, chara.Mesos);
        return chara == null ? 1 : Math.Max(1, Inventory.Count(chara, _dropQtyKind, _dropQtyId));
    }

    private long DropQuantityCount() =>
        Math.Clamp(long.TryParse(_dropQtyField.Text, out long n) ? n : 1, 1, DropQuantityMax());

    private void ClampDropQuantity()
    {
        string text = DropQuantityCount().ToString();
        if (_dropQtyField.Text == text) return;
        _dropQtyField.SetText(text);
        _dropQtyField.ReplaceOnType = true;
    }

    private void ConfirmDropQuantity()
    {
        string kind = _dropQtyKind;
        int id = _dropQtyId;
        long count = DropQuantityCount();
        bool mesos = _dropQtyMeso;
        CloseDropQuantity();
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        if (mesos) DropMesos(chara, count);
        else if (kind != "") DropFromBag(chara, kind, id, (int)count);
    }

    private void CloseDropQuantity()
    {
        _dropQtyMeso = false;
        _dropQtyId = 0;
        _dropQtyKind = "";
        _dropQtyField.Unfocus();
        BuildInvScene();
        _invBuilt = true;
    }

    private void UpdateDropPopupKeys(KeyboardState kb, float dtMs)
    {
        var key = _dropQtyField.Read(kb, dtMs);
        ClampDropQuantity();
        if (key == FieldKey.Leave) CloseDropQuantity();
        else if (key == FieldKey.Submit) ConfirmDropQuantity();
    }

    private Rectangle DropPopupRect()
    {
        int w = _shopLay.Get("qtyW", 318), h = _shopLay.Get("qtyH", 150);
        return new Rectangle((GameWidth - w) / 2 - _invPos.X, (GameHeight - h) / 2 - _invPos.Y, w, h);
    }

    private Rectangle DropQuantityFieldRect()
    {
        var popup = DropPopupRect();
        return new Rectangle(_invPos.X + popup.X + _shopLay.Get("qtyFieldX", 48),
            _invPos.Y + popup.Y + _shopLay.Get("qtyFieldY", 62),
            _shopLay.Get("qtyFieldW", 200), _shopLay.Get("qtyFieldH", 20));
    }

    private void UpdateDropPopupWheel()
    {
        int delta = _input.Wheel - _dropWheel;
        _dropWheel = _input.Wheel;
        if (delta == 0 || !DropQtyOpen) return;
        if (!DropQuantityFieldRect().Contains(_input.Mouse)) return;
        int step = delta / 120;
        if (step == 0) step = Math.Sign(delta);
        _dropQtyField.SetText(Math.Clamp(DropQuantityCount() + step, 1, DropQuantityMax()).ToString());
        _dropQtyField.ReplaceOnType = true;
    }

    private GameObject DropQuantityPopup()
    {
        var root = new GameObject { Modal = true };
        var block = new GameObject();
        block.Add(new Rect(new Rectangle(-_invPos.X, -_invPos.Y, GameWidth, GameHeight)));
        block.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => { } });
        root.AddChild(block);
        root.AddChild(SysPopUi.BuildQuantity(DropPopupRect(), () => "How many do you want to drop?",
            _dropQtyField, DropQuantityMax, ConfirmDropQuantity, CloseDropQuantity, _shopLay));
        return root;
    }

    private static GameObject PictureAt(string key, int x, int y, int w, int h)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Picture(key));
        return o;
    }

    // A worn item dropped on the bag grid comes off. The bag decides where it
    // lands: a full bag refuses the drop and the item stays in hand.
    private bool DropWornHeld(int gx, int gy)
    {
        if (!WornHeld) return false;
        if (!_showInv || !InvGridScreenRect().Contains(gx, gy)
            || RootOf(_uiScene.TopAt(gx, gy)) != _invHost) return false;
        if (!TakeOffWorn(_wornHeldId, _wornHeldCash)) return true;
        ClearWornHand();
        if (WindowFor(HotkeyAction.Equip) is { } window) window.Dirty = true;
        return true;
    }

    private static string TabKind(int tab) => tab switch
    {
        0 => "equip",
        1 => "use",
        4 => "cash",
        5 => "dec",
        _ => "", // Etc / Set-up are empty for now
    };

    private GameObject PanelSlot(Rectangle r, string itemKind, Func<int> index, Func<int> count)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Slot { OnClick = () => ClickSlot(itemKind, index()) });
        o.Add(new ItemTip(() => count() == 0 ? 0 : ItemId(itemKind, index()))); // the empty cell shows no tip
        Func<int> liveId = () => count() == 0 ? 0 : ItemId(itemKind, index());
        if (itemKind == "dec")
        {
            o.Add(new LookBust(() => CouponBust(liveId()), (gfx, rect, cosmetic) => DrawLookBust(gfx, rect, cosmetic)));
            o.Add(new InvItem(() => CouponBust(liveId()) != 0 ? "" : ItemImage(liveId())));
        }
        else
            o.Add(new InvItem(() => count() == 0 ? "" : ItemImage(ItemId(itemKind, index()))));
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
        InvClampScroll();
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
            int cell = s; // the screen row; the bag index is read live from the scroll row
            Func<int> index = () => InventoryScroll.CellIndex(
                _invWide ? 0 : _invScroll[_invTab], InvCols, cell);
            grid.AddChild(PanelSlot(InvCell(s), kind, index, () =>
            {
                int i = index();
                var chara = _save?.Slots[_slot];
                return kind == "" || chara == null || (!_showHeldDropCell && _hand.Kind == kind && _hand.Cell == i)
                    ? 0
                    : Inventory.Count(chara, kind, ItemId(kind, i));
            }));
        }
        body.AddChild(InvScrollBar());
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
        var coin = new GameObject(); // purse draws last (bag rows can reach it when the roster grows)
        var coinButton = new GameObject();
        coinButton.Add(new Rect(new Rectangle(_invLay.Get("coinX", 25), _invLay.Get("coinY", 485), 12, 13)));
        coinButton.Add(new Button { Kind = ButtonKind.Click, OnClick = OpenMesoDrop });
        coinButton.Add(new Image($"ui/{InvArtPrefix()}_coin"));
        coin.AddChild(coinButton);
        var meso = new GameObject { NoHit = true };
        meso.Add(new Rect(InvRect()));
        meso.Add(new RightLabel((ui, s) => ui.GetMeso(s),
            () => (_save?.Slots[_slot]?.Mesos ?? 0).ToString("#,##0"),
            _invWide ? _invLay.Get("mesoWideX", 185) : _invLay.Get("mesoX", 213),
            _invWide ? _invLay.Get("mesoWideY", 484) : _invLay.Get("mesoY", 484),
            new Color(96, 104, 113)));
        coin.AddChild(meso);
        body.AddChild(coin);
        _invHost.AddChild(body);
        if (_cashUseId != 0) _invHost.AddChild(CashUsePopup());
        if (DropQtyOpen) _invHost.AddChild(DropQuantityPopup());
        _invBuiltTab = _invTab;
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
            foreach (string s in new[] { "n", "h", "p" })
                LoadTexture($"ui/{p}_coin_{s}.png");
            // The X, and the expand button: the "+" in the small window, the "-" in the wide one.
            foreach (string b in new[] { "close", p == "inv" ? "plus" : "minus" })
                foreach (string s in new[] { "n", "h", "p" })
                    LoadTexture($"ui/{p}_{b}_{s}.png");
            foreach (string s in new[] { "n", "h", "p" })
                LoadTexture($"ui/{p}_sort_{s}.png");
            LoadTexture($"ui/{p}_scroll_base.png");
            for (int d = 0; d < 3; d++)
                LoadTexture($"ui/{p}_scroll_thumb{d}.png");
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
        foreach (string pop in new[] { "wm_pop_top.png", "wm_pop_mid.png", "wm_pop_bottom.png", "wm_pop_textbox.png" })
            LoadTexture("ui/" + pop);
        foreach (string b in new[] { "wm_pop_yes", "wm_pop_no", "shop_max" })
            foreach (string s in new[] { "n", "h", "p" })
                LoadTexture($"ui/{b}_{s}.png");
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
        _invScroll[_invTab] = 0; // the new order starts at the top
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
        var act = _hand.Click(chara, kind, id, _items, MaxHpNow(), MaxMpNow(), _timeMs, index);
        if (act == HandAct.Wore)
        {
            if (WindowFor(HotkeyAction.Equip) is { } equipWindow) equipWindow.Dirty = true;
            PushBuild();
            PushName();
            PushAppearance();
            PlaySfx("click");
        }
        else if (act == HandAct.Moved)
        {
            PlaySfx("click");
        }
        else if (act == HandAct.Used)
        {
            PlaySfx("click");
            OpenCashUse(id);
        }
    }

    // CASH tab double-click: Character Name Change opens the input popup;
    // SP Reset Scroll opens its confirmation. Every other cash item has no
    // use in this week.
    private void OpenCashUse(int id)
    {
        if (id is not (5400000 or 5051001)) return;
        _cashUseId = id;
        _cashUseNotice = "";
        var chara = _save?.Slots[_slot];
        if (id == 5400000)
        {
            _cashUseField.SetText(chara?.Name ?? "");
            _cashUseField.Focus(Keyboard.GetState());
        }
        BuildInvScene();
        _invBuilt = true;
    }

    private void CloseCashUse()
    {
        _cashUseId = 0;
        _cashUseNotice = "";
        _cashUseField.Unfocus();
        BuildInvScene();
        _invBuilt = true;
    }

    private GameObject CashUsePopup()
    {
        int w = _cashLay.Get("utilityPopW", 318), h = _cashLay.Get("utilityPopH", 160);
        var box = new Rectangle((GameWidth - w) / 2 - _invPos.X, (GameHeight - h) / 2 - _invPos.Y, w, h);
        var lines = _cashUseNotice != ""
            ? new[] { _cashUseNotice }
            : _cashUseId == 5400000
                ? new[] { "Enter a new character name." }
                : new[] { "Reset all skills and refund their SP?" };
        var popup = SysPopUi.Build(box, lines,
            _cashUseId == 5400000 ? ConfirmCashName : ConfirmCashReset, CloseCashUse, _cashLay,
            _cashLay.Get("utilityPopTextY", 34));
        if (_cashUseId == 5400000)
        {
            popup.AddChild(PictureAt("ui/wm_pop_textbox.png", 0, _cashLay.Get("utilityTextBoxY", 47), w,
                _cashLay.Get("utilityTextBoxH", 44)));
            var input = new GameObject();
            input.Add(new Rect(new Rectangle(
                _cashLay.Get("utilityNameFieldX", 48), _cashLay.Get("utilityNameFieldY", 62),
                _cashLay.Get("utilityNameFieldW", 200), _cashLay.Get("utilityNameFieldH", 20))));
            input.Add(_cashUseField);
            popup.AddChild(input);
        }
        var root = new GameObject { Modal = true };
        root.AddChild(PanelButton(new Rectangle(-_invPos.X, -_invPos.Y, GameWidth, GameHeight), "cashuseblock", () => { }, ButtonKind.Silent));
        root.AddChild(popup);
        return root;
    }

    // --screen cashuserename/cashusereset: open the popup over the CASH tab.
    internal void PoseCashUse(int id)
    {
        _invTab = 4; // 0 Equip, 1 Use, 2 Etc, 3 Set-up, 4 Cash, 5 Dec
        OpenCashUse(id);
    }

    // --screen dropqty/dropqtymax: show five Red Potions in the bag and one held,
    // then open the real drop quantity popup. dropqtymax also fills MAX.
    internal void PoseDropQuantity(bool fillMax = false)
    {
        var chara = _save!.Slots[_slot]!;
        const int id = ItemData.RedPotion;
        chara.Use = new Dictionary<int, int> { [id] = 5 };
        chara.UseOrder = new List<int>(new int[Inventory.Capacity]);
        chara.UseOrder[1] = id; // the held stack sits at cell 1; cell 0 stays empty
        _showHeldDropCell = true;
        _invTab = 1;
        _hand.Return();
        _hand.Click(chara, "use", id, _items, MaxHpNow(), MaxMpNow(), _timeMs, 1);
        if (!DropBagOnWorld()) throw new InvalidDataException("dropqty needs a held potion");
        if (fillMax)
        {
            _dropQtyField.SetText(DropQuantityMax().ToString());
            _dropQtyField.ReplaceOnType = true;
        }
    }

    private void ConfirmCashName()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        string name = _cashUseField.Text;
        if (name == chara.Name)
        {
            _cashUseNotice = "The name is unchanged.";
            BuildInvScene();
            _invBuilt = true;
            return;
        }
        if (Inventory.Count(chara, "cash", _cashUseId) <= 0 || !CashShopData.Rename(_store, _saveSlot, _slot, name))
        {
            _cashUseNotice = "That name is not valid.";
            BuildInvScene();
            _invBuilt = true;
            return;
        }
        Inventory.Remove(chara, "cash", _cashUseId, 1);
        SaveChara();
        PushName();
        SyncHeroPlate();
        CloseCashUse();
    }

    private void ConfirmCashReset()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        if (!CashShopData.ResetSkills(chara))
        {
            _cashUseNotice = "You have no skills to reset.";
            BuildInvScene();
            _invBuilt = true;
            return;
        }
        Inventory.Remove(chara, "cash", _cashUseId, 1);
        SaveChara();
        PushBuild();
        _skillBuilt = false;
        CloseCashUse();
    }

    private void UpdateCashUseKeys(KeyboardState kb, float dtMs)
    {
        if (_cashUseId != 5400000) return;
        if (_cashUseField.Read(kb, dtMs) == FieldKey.Submit) ConfirmCashName();
    }

    private void EscCashUse()
    {
        if (_cashUseId != 0) CloseCashUse();
    }
}
