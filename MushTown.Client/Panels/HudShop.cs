using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The shop window: the keeper's official Shop frame on the left and the
// player's Inventory frame on the right (UI/UINpcShop.img). The left list is
// the stock to buy or the buy-back list; the right list is the bag, split by
// the six official inventory tabs. A single click selects a row, a second
// click on the same row within the double-click window acts.
public partial class MushTownGame
{
    private static readonly PanelLayout _shopLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-shop.json"));

    private bool _shopPlaced; // the window took the spot from the layout file
    private int _shopTab;      // 0 = keeper's stock, 1 = buy back
    private int _shopInvTab;   // 0 Equip, 1 Use, 2 Etc, 3 Set-up, 4 Cash, 5 Dec
    private readonly ShopScroll _shopScroll = new();
    private ScrollBar? _shopBar, _shopInvBar;
    private int _shopSelId;    // selected row on the left
    private string _shopSelTab = ""; // and the bag it came from
    private int _shopInvSelId; // selected row on the right
    private int _shopLastId;
    private string _shopLastTab = "";
    private bool _shopLastInv;
    private double _shopLastAt;
    private string _shopNotice = "";
    private string _shopBuiltKey = "";
    private int _shopQtyId; // the row whose quantity popup is open (0 = none)
    private bool _shopQtySelling;
    private bool _shopQtyBack; // the popup buys back a sold row (the row's own count is the cap)
    private string _shopQtyTab = ""; // the bag the sold row came from
    private readonly TextField _shopQtyField = new(_shopLay) { DigitsOnly = true, Limit = 10 };
    private int _shopWheel; // the last wheel value the shop saw
    private readonly Dictionary<int, ShopBuybackList> _shopBuybacks = new();

    private const int ShopRowCount = ShopScroll.VisibleRows;
    private const int MaxBuyQuantity = 999;

    private int ShopLeftW => _shopLay.Get("w", 380);
    private int ShopW => ShopLeftW + _shopLay.Get("invW", 324);
    private int ShopH => _shopLay.Get("h", 681);
    private Rectangle ShopRect() => new(0, 0, ShopW, ShopH);

    private static readonly Color ShopTitleInk = new(190, 203, 217);
    private static readonly Color ShopNameInk = new(20, 20, 20);

    private Rectangle ShopRowRect(int i) => new(
        _shopLay.Get("listX", 20),
        _shopLay.Get("listY", 141) + i * _shopLay.Get("rowPitch", 50),
        _shopLay.Get("rowW", 336), _shopLay.Get("rowH", 45));

    private Rectangle ShopInvRowRect(int i) => new(
        ShopLeftW + _shopLay.Get("invListX", 20),
        _shopLay.Get("invListY", 141) + i * _shopLay.Get("invRowPitch", 50),
        _shopLay.Get("invRowW", 280), _shopLay.Get("invRowH", 45));

    private int ShopNpc()
    {
        foreach (var (npc, shop) in _shops)
            if (ReferenceEquals(shop.Stock, _shopStock)) return npc;
        return 0;
    }

    private ShopBuybackList ShopBuyback()
    {
        int npc = ShopNpc();
        if (!_shopBuybacks.TryGetValue(npc, out var list))
            _shopBuybacks[npc] = list = new ShopBuybackList();
        return list;
    }

    private List<(int id, int count)> ShopBagRows(int tab)
    {
        var rows = new List<(int, int)>();
        var chara = _save?.Slots[_slot];
        if (chara == null) return rows;
        string kind = TabKind(tab);
        if (kind == "") return rows;
        if (kind is "cash" or "dec")
        {
            var bag = kind == "cash" ? chara.CashBag : chara.DecBag;
            if (bag == null) return rows;
            foreach (var (id, count) in bag.OrderBy(kv => kv.Key))
                if (count > 0 && _items.ContainsKey(id)) rows.Add((id, count));
            return rows;
        }
        var ids = kind == "equip" ? _equips : _potions;
        foreach (int id in ids)
        {
            int n = Inventory.Count(chara, kind, id);
            if (n > 0) rows.Add((id, n));
        }
        return rows;
    }

    private int ShopLeftRows() => _shopTab == 0 ? _shopStock.Count : ShopBuyback().Count;

    private int ShopInvRows() => ShopBagRows(_shopInvTab).Count;

    private void ShopClampScroll()
    {
        _shopScroll.ClampLeft(_shopTab, ShopLeftRows());
        _shopScroll.ClampInv(_shopInvTab, ShopInvRows());
    }

    // One official scroll bar: the track rectangle is the layout spot; the
    // thumb moves through it. A list of ten rows or fewer shows the disabled
    // track and no thumb.
    private GameObject ShopScrollBar(bool inv)
    {
        int tab = inv ? _shopInvTab : _shopTab;
        int max = inv ? _shopScroll.InvMax(ShopInvRows()) : _shopScroll.LeftMax(ShopLeftRows());
        var bar = new ScrollBar(
            () => (float)(inv ? _shopScroll.Inv(tab) : _shopScroll.Left(tab)) / max,
            value =>
            {
                int row = (int)MathF.Round(value * max);
                if (inv) _shopScroll.SetInv(tab, row);
                else _shopScroll.SetLeft(tab, row);
            },
            () => max > 0,
            $"ui/{(inv ? "shop_inv" : "shop")}_scroll_base.png",
            $"ui/{(inv ? "shop_inv" : "shop")}_scroll_thumb",
            _shopLay.Get(inv ? "invScrollThumbH" : "scrollThumbH", inv ? 26 : 28));
        if (inv) _shopInvBar = bar; else _shopBar = bar;
        int x = inv ? ShopLeftW + _shopLay.Get("invScrollX", 304) : _shopLay.Get("scrollX", 360);
        int y = inv ? _shopLay.Get("invScrollY", 141) : _shopLay.Get("scrollY", 141);
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, _shopLay.Get("scrollW", 5), _shopLay.Get("scrollH", 495))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => bar.Grab(_uiScene.LastClick.Y) });
        o.Add(bar);
        return o;
    }

    // A row follows its list's saved scroll row. Rows outside the ten visible
    // slots move far off screen and stop taking hits; the list itself is not
    // clipped, so no extra clip layer is needed.
    private GameObject ShopScrollRow(GameObject row, int index, bool inv)
    {
        int tab = inv ? _shopInvTab : _shopTab;
        var rect = row.Get<Rect>()!;
        int listY = inv ? _shopLay.Get("invListY", 141) : _shopLay.Get("listY", 141);
        int pitch = inv ? _shopLay.Get("invRowPitch", 50) : _shopLay.Get("rowPitch", 50);
        var rowScroll = new ShopRowScroll(index, rect.Offset.X, listY, pitch, ShopRowCount,
            () => inv ? _shopScroll.Inv(tab) : _shopScroll.Left(tab));
        row.Add(rowScroll);
        rowScroll.Apply();
        return row;
    }

    private Rectangle ShopListScreenRect(bool inv)
    {
        var r = inv ? ShopInvRowRect(0) : ShopRowRect(0);
        int pitch = inv ? _shopLay.Get("invRowPitch", 50) : _shopLay.Get("rowPitch", 50);
        return new Rectangle(_shopPos.X + r.X, _shopPos.Y + r.Y, r.Width, ShopRowCount * pitch);
    }

    // The wheel scrolls the list under the mouse, three rows per notch. The
    // quantity popup owns the wheel while it is open.
    private void UpdateShopWheel(int gx, int gy)
    {
        int wheel = _input.Wheel;
        if (!_showShop || RootOf(_uiScene.Hovered) != _shopHost || _shopQtyId != 0 || _shopNotice != "")
        {
            _shopWheel = wheel;
            return;
        }
        bool left = ShopListScreenRect(false).Contains(gx, gy);
        bool right = ShopListScreenRect(true).Contains(gx, gy);
        if (!left && !right)
        {
            _shopWheel = wheel;
            return;
        }
        int notches = (wheel - _shopWheel) / 120;
        _shopWheel = wheel;
        if (notches == 0) return;
        if (left) _shopScroll.WheelLeft(_shopTab, notches, ShopLeftRows());
        if (right) _shopScroll.WheelInv(_shopInvTab, notches, ShopInvRows());
    }

    private void ShopPlaceFirst()
    {
        if (_shopPlaced) return;
        _shopPlaced = true;
        _shopPos = new Point(_shopLay.Get("x", 493), _shopLay.Get("y", 43));
    }

    // One click selects; the second click on the same row acts.
    private void ShopRowClicked(int id, bool inv, string tab, Action act)
    {
        if (_shopLastId == id && _shopLastInv == inv && _shopLastTab == tab
            && _timeMs - _shopLastAt <= _shopLay.Get("doubleClickMs", 350))
        {
            _shopLastId = 0;
            _shopLastInv = false;
            _shopLastTab = "";
            _shopLastAt = 0;
            act();
            return;
        }
        _shopLastId = id;
        _shopLastInv = inv;
        _shopLastTab = tab;
        _shopLastAt = _timeMs;
        if (inv) _shopInvSelId = id;
        else
        {
            _shopSelId = id;
            _shopSelTab = tab;
        }
    }

    private void BuyFromShop(int id, int count = 1)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        var def = _items[id];
        string tab = Shop.BagFor(def);
        long total = (long)def.Price * count;
        if (!Inventory.CanAdd(chara, tab, id, count))
        {
            _shopNotice = "Your bag is full.";
            return;
        }
        if (Shop.Buy(chara, id, def.Price, tab, count))
        {
            _shopNotice = "";
            LogEvent($"bought {def.Name} x{count} for {total:#,##0} mesos");
            PlaySfx("shopbuy");
        }
        else
        {
            _shopNotice = $"Not enough mesos.\nYou need {total:#,##0} mesos.";
        }
    }

    // Double-click on a stock or bag row opens the shared quantity popup. The
    // field starts at 1 and is already focused; Enter confirms, Escape closes.
    private void OpenQuantityPopup(bool selling, int id, string tab = "", bool back = false)
    {
        _shopQtyId = id;
        _shopQtySelling = selling;
        _shopQtyBack = back;
        _shopQtyTab = tab;
        _shopWheel = _input.Wheel;
        _shopQtyField.SetText("1");
        _shopQtyField.ReplaceOnType = true;
        _shopQtyField.Ink = Color.Black;
        _shopQtyField.Focus(Keyboard.GetState());
    }

    private void CloseQuantityPopup()
    {
        _shopQtyField.Unfocus();
        _shopQtyId = 0;
        _shopQtyTab = "";
    }

    private int ShopQuantityCount()
    {
        return int.TryParse(_shopQtyField.Text, out int n) ? n : 0;
    }

    private void ConfirmBuy()
    {
        int count = ShopQuantityCount();
        int id = _shopQtyId;
        CloseQuantityPopup();
        BuyFromShop(id, count);
    }

    private void ConfirmBuyBack()
    {
        int count = ShopQuantityCount();
        int id = _shopQtyId;
        string tab = _shopQtyTab;
        CloseQuantityPopup();
        BuyBackFromShop(id, tab, count);
    }

    // Enter and the OK button: the popup's three jobs.
    private void ConfirmQuantity()
    {
        if (_shopQtySelling) ConfirmSell();
        else if (_shopQtyBack) ConfirmBuyBack();
        else ConfirmBuy();
    }

    // The quantity field owns the keys while its popup is open.
    private void UpdateShopPopupKeys(KeyboardState kb, float dtMs)
    {
        var key = _shopQtyField.Read(kb, dtMs);
        ClampQuantity();
        if (key == FieldKey.Leave) CloseQuantityPopup();
        else if (key == FieldKey.Submit) ConfirmQuantity();
    }

    // The field always shows the number that will be bought or sold: at least
    // 1, at most the cap.
    private int ShopQuantityMax() => _shopQtySelling ? ShopBagCount(_shopQtyTab, _shopQtyId)
        : _shopQtyBack ? ShopBuyback().Rows.First(r => r.id == _shopQtyId && r.tab == _shopQtyTab).count
        : MaxBuyQuantity;

    private void ClampQuantity()
    {
        int max = ShopQuantityMax();
        string text = Math.Clamp(int.TryParse(_shopQtyField.Text, out int n) ? n : 0, 1, max).ToString();
        if (_shopQtyField.Text == text) return;
        _shopQtyField.SetText(text);
        _shopQtyField.ReplaceOnType = true;
    }

    // The wheel over the input steps the number by one. A wheel notch is 120.
    private void UpdateShopPopupWheel()
    {
        int delta = _input.Wheel - _shopWheel;
        _shopWheel = _input.Wheel;
        if (delta == 0 || _shopQtyId == 0) return;
        if (!ShopQuantityFieldRect().Contains(_input.Mouse)) return;
        int step = delta / 120;
        if (step == 0) step = Math.Sign(delta);
        AdjustQuantity(step);
    }

    private Rectangle ShopPopupRect()
    {
        int w = _shopLay.Get("qtyW", 318);
        int h = _shopLay.Get("qtyH", 150);
        return new Rectangle(_shopLay.Get("qtyX", (ShopW - w) / 2), _shopLay.Get("qtyY", (ShopH - h) / 2), w, h);
    }

    private Rectangle ShopQuantityFieldRect()
    {
        var popup = ShopPopupRect();
        return new Rectangle(_shopPos.X + popup.X + _shopLay.Get("qtyFieldX", 48),
            _shopPos.Y + popup.Y + _shopLay.Get("qtyFieldY", 62),
            _shopLay.Get("qtyFieldW", 200), _shopLay.Get("qtyFieldH", 20));
    }

    private void AdjustQuantity(int step)
    {
        int max = ShopQuantityMax();
        _shopQtyField.SetText(Math.Clamp((long)ShopQuantityCount() + step, 1, max).ToString());
        _shopQtyField.ReplaceOnType = true;
    }

    private int ShopBagCount(string tab, int id)
    {
        var chara = _save?.Slots[_slot];
        return chara == null ? 0 : Inventory.Count(chara, tab, id);
    }

    private void ConfirmSell()
    {
        if (!SellToShop(_shopQtyId, ShopQuantityCount(), _shopQtyTab)) return;
        CloseQuantityPopup();
    }

    private bool SellToShop(int id, int count, string tab)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false;
        var def = _items[id];
        if (!Shop.Sell(chara, id, def.Price, tab, count)) return false;
        ShopBuyback().Add(id, count, tab);
        ShopClampScroll();
        LogEvent($"sold {def.Name} x{count} for {(long)Shop.SellPrice(def.Price) * count:#,##0} mesos");
        return true;
    }

    private void BuyBackFromShop(int id, string tab, int count)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        var def = _items[id];
        long total = (long)def.Price * count;
        if (!Shop.Buy(chara, id, def.Price, tab, count))
        {
            _shopNotice = $"Not enough mesos.\nYou need {total:#,##0} mesos.";
            return;
        }
        ShopBuyback().Take(id, tab, count);
        _shopNotice = "";
        LogEvent($"bought back {def.Name} x{count} for {total:#,##0} mesos");
        PlaySfx("shopbuy");
    }

    // The tree rebuild key: tabs, the notice and both live lists. Row selection
    // is drawn live, so picking a row does not rebuild the shop.
    private string ShopSceneKey()
    {
        string bag = string.Join("/", Enumerable.Range(0, 6).Select(tab =>
            string.Join("|", ShopBagRows(tab).Select(r => $"{r.id}:{r.count}"))));
        string buyback = string.Join("|", ShopBuyback().Rows.Select(r => $"{r.id}:{r.count}:{r.tab}"));
        return $"{_shopTab}:{_shopInvTab}:{ShopNpc()}:{_shopNotice}:{_shopQtyId}:{_shopQtySelling}:{_shopQtyBack}:{_shopStock?.Count}|{bag}|{buyback}";
    }

    private void LoadShopArt()
    {
        LoadTexture("ui/shop_backgrnd.png");
        LoadTexture("ui/shop_inv_backgrnd.png");
        LoadTexture("ui/shop_row_s.png");
        LoadTexture("ui/shop_inv_row_s.png");
        LoadTexture("ui/shop_tab_n.png");
        LoadTexture("ui/shop_tab_s.png");
        LoadTexture("ui/shop_inv_meso.png");
        LoadTexture("ui/shop_meso.png");
        for (int t = 0; t < 6; t++)
        {
            LoadTexture($"ui/inv_tab_n{t}.png");
            LoadTexture($"ui/inv_tab_s{t}.png");
        }
        LoadTexture("ui/shop_scroll_base.png");
        LoadTexture("ui/shop_inv_scroll_base.png");
        for (int d = 0; d < 3; d++)
        {
            LoadTexture($"ui/shop_scroll_thumb{d}.png");
            LoadTexture($"ui/shop_inv_scroll_thumb{d}.png");
        }
        foreach (string f in new[] { "wm_pop_top.png", "wm_pop_mid.png", "wm_pop_bottom.png", "wm_pop_textbox.png" })
            LoadTexture("ui/" + f);
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/shop_x_{s}.png");
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/wm_pop_yes_{s}.png");
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/wm_pop_no_{s}.png");
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/shop_max_{s}.png");
    }

    private GameObject ShopRow(Rectangle rect, bool inv, int id, string name, int count, string price, string legacyId, string tab, Action act)
    {
        var o = PanelButton(rect, legacyId, () => ShopRowClicked(id, inv, tab, act), ButtonKind.Click);
        o.Add(new ShopRowArt(() => inv ? _shopInvSelId == id : _shopSelId == id && _shopSelTab == tab,
            inv ? "ui/shop_inv_row_s.png" : "ui/shop_row_s.png"));
        o.Add(new ShopIcon(_items[id].Image));
        o.Add(new Label(name, _shopLay.Get("nameX", 56), _shopLay.Get("nameY", 6)) { Tint = ShopNameInk });
        if (count > 1)
        {
            int w = _shopLay.Get("countW", 52);
            var qty = new GameObject { NoHit = true };
            qty.Add(new Rect(new Rectangle(rect.Width - _shopLay.Get("countRight", 8) - w,
                _shopLay.Get("nameY", 6), w, _shopLay.Get("nameH", 12))));
            qty.Add(new Text
            {
                Value = () => $"x{count}",
                X = 0,
                W = w,
                Line = TextLine.Middle,
                Align = TextAlign.Right,
                Ink = ShopNameInk,
            });
            o.AddChild(qty);
        }
        o.Add(new ShopPriceLine(inv ? "Sell" : "", price, _shopLay.Get("priceX", 56),
            _shopLay.Get("priceY", 24), _shopLay.Get("priceH", 14)));
        o.Add(new ItemTip(() => id));
        return o;
    }

    private GameObject ShopEmptyRow(Rectangle rect)
    {
        var o = new GameObject();
        o.Add(new Rect(rect));
        return o;
    }

    private GameObject ShopTab(int index, string label)
    {
        int x = _shopLay.Get("tabX", 11) + index * _shopLay.Get("tabStep", 72);
        int y = _shopLay.Get("tabY", 103);
        var o = PanelButton(new Rectangle(x, y, _shopLay.Get("tabW", 70), _shopLay.Get("tabH", 22)),
            "shoptab" + index, () => { _shopTab = index; _shopSelId = 0; _shopSelTab = ""; _shopLastId = 0; _shopLastTab = ""; _shopNotice = ""; }, ButtonKind.Click);
        o.Add(new Picture(index == _shopTab ? "ui/shop_tab_s.png" : "ui/shop_tab_n.png", 0, 0));
        o.Add(new Text { Value = () => label, Ink = index == _shopTab ? Color.White : new Color(190, 220, 235), Align = TextAlign.Center });
        return o;
    }

    private GameObject ShopInvTab(int index)
    {
        int x = ShopLeftW + _shopLay.Get("invTabX", 11) + index * _shopLay.Get("invTabStep", 37);
        int y = _shopLay.Get("invTabY", 103);
        var o = PanelButton(new Rectangle(x, y, _shopLay.Get("invTabW", 35), _shopLay.Get("invTabH", 22)),
            "shopinvt" + index, () => { _shopInvTab = index; _shopInvSelId = 0; _shopLastId = 0; _shopLastTab = ""; _shopNotice = ""; }, ButtonKind.Click);
        o.Add(new Picture($"ui/inv_tab_{(index == _shopInvTab ? "s" : "n")}{index}.png", 0, 0));
        return o;
    }

    // The shared number-input popup: buying and selling both use it; only the
    // prompt, max and confirm differ.
    private GameObject ShopQuantityPopup()
    {
        var popup = SysPopUi.BuildQuantity(ShopPopupRect(),
            () => _shopQtySelling ? "How many do you want to sell?" : _shopQtyBack ? "How many do you want to buy back?" : "How many do you want to buy?",
            _shopQtyField, () => ShopQuantityMax(), ConfirmQuantity,
            CloseQuantityPopup, _shopLay);

        var root = new GameObject { Modal = true };
        root.AddChild(PanelButton(ShopRect(), "shopquantityblock", () => { }, ButtonKind.Silent));
        root.AddChild(popup);
        return root;
    }

    private GameObject ShopNoticeBox()
    {
        int w = _shopLay.Get("noticeW", 318), h = _shopLay.Get("noticeH", 160);
        var box = new Rectangle((ShopW - w) / 2, (ShopH - h) / 2, w, h);
        var blocker = PanelButton(ShopRect(), "", () => { }, ButtonKind.Silent); // the shop waits behind the notice
        var popup = SysPopUi.BuildOk(box, _ui.Wrap(_shopNotice, _shopLay.Get("noticeTextW", 286)), () => _shopNotice = "",
            _shopLay.Get("noticeTextY", 38), _shopLay.Get("noticeLineStep", 17), w - _shopLay.Get("noticeTextW", 286),
            _shopLay.Get("noticeOkY", 115), 0, _shopLay);
        // The blocker must be under the popup: add it first, the popup last.
        var root = new GameObject { Modal = true };
        root.AddChild(blocker);
        root.AddChild(popup);
        return root;
    }

    private void BuildShopScene()
    {
        LoadShopArt();
        ShopPlaceFirst();
        _shopHost.Clear();
        var body = new GameObject();
        body.Add(new Rect(ShopRect()));
        body.Add(new Picture("ui/shop_backgrnd.png", 0, 0));
        body.Add(new Picture("ui/shop_inv_backgrnd.png", ShopLeftW, 0));

        string title = NpcName(ShopNpc());
        body.Add(new LiveLabel(() => title, _shopLay.Get("titleX", 139),
            _shopLay.Get("titleY", 55), ShopTitleInk, LabelFont.Mid));
        body.Add(new QuestNpcPortrait(new LifeStand { Game = this, Kind = 0, Id = ShopNpc() },
            _shopLay.Get("npcPortraitX", 70), _shopLay.Get("npcPortraitY", 88), faceRight: true));

        for (int i = 0; i < 2; i++) body.AddChild(ShopTab(i, i == 0 ? "Sell" : "Buy Back"));
        for (int i = 0; i < 6; i++) body.AddChild(ShopInvTab(i));

        // The player side: avatar feet at the official preview spot, meso only.
        var avatar = new GameObject();
        avatar.Add(new Rect(new Rectangle(ShopLeftW + _shopLay.Get("invAvatarX", 58),
            _shopLay.Get("invAvatarY", 88), 1, 1)));
        avatar.Add(new AvatarPreview(this, "stand1", false, chara: () => _save?.Slots[_slot]));
        body.AddChild(avatar);
        body.Add(new Picture("ui/shop_inv_meso.png", ShopLeftW + _shopLay.Get("invMesoBgX", 106),
            _shopLay.Get("invMesoBgY", 66)));
        body.Add(new ShopMesoLine(() => (_save?.Slots[_slot]?.Mesos ?? 0).ToString("#,##0"),
            ShopLeftW + _shopLay.Get("invMesoX", 293), _shopLay.Get("invMesoY", 70),
            _shopLay.Get("priceH", 14)));
        body.Add(new Label("Double-click an item to buy or sell.", _shopLay.Get("tipX", 18), _shopLay.Get("tipY", 657))
        { Tint = ShopTitleInk });

        // The left list: every row is built; the scroll row moves it in or out.
        if (ShopLeftRows() == 0) body.AddChild(ShopEmptyRow(ShopRowRect(0)));
        else if (_shopTab == 0)
        {
            for (int i = 0; i < _shopStock.Count; i++)
            {
                int id = _shopStock[i];
                var def = _items[id];
                body.AddChild(ShopScrollRow(ShopRow(ShopRowRect(0), false, id, def.Name, 1,
                    $"{def.Price:#,##0}", "shopbuy" + i, "", () => OpenQuantityPopup(false, id)), i, false));
            }
        }
        else
        {
            var list = ShopBuyback().Rows;
            for (int i = 0; i < list.Count; i++)
            {
                var (id, count, tab) = list[i];
                var def = _items[id];
                body.AddChild(ShopScrollRow(ShopRow(ShopRowRect(0), false, id, def.Name, count,
                    $"{def.Price:#,##0}", "shopbuyback" + i, tab,
                    () => { if (count > 1) OpenQuantityPopup(false, id, tab, back: true); else BuyBackFromShop(id, tab, 1); }), i, false));
            }
        }
        body.AddChild(ShopScrollBar(false));

        // The right bag list, one of the six inventory tabs.
        var bag = ShopBagRows(_shopInvTab);
        string kind = TabKind(_shopInvTab);
        if (bag.Count == 0) body.AddChild(ShopEmptyRow(ShopInvRowRect(0)));
        for (int i = 0; i < bag.Count; i++)
        {
            var (id, count) = bag[i];
            var def = _items[id];
            body.AddChild(ShopScrollRow(ShopRow(ShopInvRowRect(0), true, id, def.Name, count,
                $"{Shop.SellPrice(def.Price):#,##0}", "shopinvsell" + i, "",
                () => OpenQuantityPopup(true, id, kind)), i, true));
        }
        body.AddChild(ShopScrollBar(true));

        // Drag strips: both headers. The X is the right inventory's official one.
        body.AddChild(PanelButton(new Rectangle(0, 0, ShopW, _shopLay.Get("dragH", 30)), "",
            () => _dragShop = _uiScene.LastClick - _shopPos));
        body.AddChild(ArtButton(new Rectangle(ShopLeftW + _shopLay.Get("invCloseX", 295),
                _shopLay.Get("invCloseY", 2), _shopLay.Get("invCloseW", 27), _shopLay.Get("invCloseH", 27)),
            "ui/shop_x", () => { _showShop = false; _shopQtyId = 0; _shopQtyField.Unfocus(); }));

        if (_shopNotice != "") body.AddChild(ShopNoticeBox());
        if (_shopQtyId != 0) body.AddChild(ShopQuantityPopup());
        _shopHost.AddChild(body);
    }
}

// The official selected row. Only a picked row turns blue; hover does not.
// The official selected picture keeps its 48px item well white, so fill that
// well with the same blue to make the whole row read as selected.
public sealed class ShopRowArt : Component, IDrawable
{
    private static readonly Color SelectedFill = new(129, 211, 235);
    private readonly Func<bool> _selected;
    private readonly string _art;
    public ShopRowArt(Func<bool> selected, string art) { _selected = selected; _art = art; }
    public void Draw(UiGfx gfx)
    {
        if (!_selected()) return;
        var r = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex(_art), new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(r.X + 1, r.Y + 1, 46, r.Height - 2), SelectedFill);
    }
}

// The item icon centered in the 45px icon well at the left of a shop row.
public sealed class ShopIcon : Component, IDrawable
{
    private readonly string _image;
    public ShopIcon(string image) => _image = image;
    public void Draw(UiGfx gfx)
    {
        var icon = gfx.Tex(_image);
        var r = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(icon, new Vector2(r.X + (r.Height - icon.Width) / 2,
            r.Y + (r.Height - icon.Height + 1) / 2), Color.White);
    }
}

// The player's meso total: the number right-aligned at the official spot and
// the coin right before it, so the two always read as one line.
public sealed class ShopMesoLine : Component, IDrawable
{
    private static readonly Color Ink = new(190, 203, 217);
    private readonly Func<string> _amount;
    private readonly int _right, _y, _boxH;
    public ShopMesoLine(Func<string> amount, int right, int y, int boxH)
    { _amount = amount; _right = right; _y = y; _boxH = boxH; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var box = new Rectangle(r.X, r.Y + _y, _right, _boxH);
        var amount = gfx.Ui.GetMeso(_amount());
        int x = box.Right - amount.Width;
        Text.Draw(gfx.Sb, gfx.Ui, amount, new Rectangle(x, box.Y, amount.Width, box.Height),
            Ink, TextLine.Middle, TextAlign.Left, gfx.Ui.MesoInk);
        var coin = gfx.Tex("ui/shop_meso.png");
        gfx.Sb.Draw(coin, new Vector2(x - coin.Width - 4,
            box.Y + (box.Height - coin.Height) / 2), Color.White);
    }
}

// One shop row follows a list's saved scroll row. A row outside the ten
// visible slots moves off screen and stops taking hits, so no clip layer is
// needed over the fixed row art.
public sealed class ShopRowScroll : Component
{
    private readonly int _row, _x, _y, _pitch, _visible;
    private readonly Func<int> _scroll;

    public ShopRowScroll(int row, int x, int y, int pitch, int visible, Func<int> scroll)
    {
        _row = row;
        _x = x;
        _y = y;
        _pitch = pitch;
        _visible = visible;
        _scroll = scroll;
    }

    // Build time and every frame both use this: a shot never runs Update.
    public void Apply()
    {
        int slot = _row - _scroll();
        var rect = Owner.Get<Rect>()!;
        if (slot < 0 || slot >= _visible)
        {
            rect.Offset = new Point(_x, slot < 0 ? -10000 : 10000);
            Owner.NoHit = true;
            return;
        }
        rect.Offset = new Point(_x, _y + slot * _pitch);
        Owner.NoHit = false;
    }

    public override void Update(float dt) => Apply();
}

// A shop price line: [Sell] [coin] number. The coin sits before the number.
public sealed class ShopPriceLine : Component, IDrawable
{
    private readonly string _prefix, _amount;
    private readonly int _x, _y, _boxH;
    public ShopPriceLine(string prefix, string amount, int x, int y, int boxH)
    { _prefix = prefix; _amount = amount; _x = x; _y = y; _boxH = boxH; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var box = new Rectangle(r.X + _x, r.Y + _y, r.Width - _x, _boxH);
        int x = box.X;
        if (_prefix != "")
        {
            var p = gfx.Ui.GetSmall(_prefix);
            Text.Draw(gfx.Sb, gfx.Ui, p, new Rectangle(x, box.Y, p.Width, box.Height),
                new Color(70, 70, 70), TextLine.Middle, TextAlign.Left);
            x += p.Width + 3;
        }
        var amount = gfx.Ui.GetMeso(_amount);
        var coin = gfx.Tex("ui/shop_meso.png");
        gfx.Sb.Draw(coin, new Vector2(x, box.Y + (box.Height - coin.Height) / 2), Color.White);
        x += coin.Width + 2;
        Text.Draw(gfx.Sb, gfx.Ui, amount, new Rectangle(x, box.Y, box.Right - x, box.Height),
            new Color(70, 70, 70), TextLine.Middle, TextAlign.Left, gfx.Ui.MesoInk);
    }
}

// One keeper's buy-back rows. The key is id + tab, so the same item sold
// from two bags stays two rows.
public sealed class ShopBuybackList
{
    private readonly List<(int id, int count, string tab)> _rows = new();

    public int Count => _rows.Count;
    public IReadOnlyList<(int id, int count, string tab)> Rows => _rows;

    public void Add(int id, int count, string tab)
    {
        int at = _rows.FindIndex(r => r.id == id && r.tab == tab);
        if (at >= 0) _rows[at] = (id, _rows[at].count + count, tab);
        else _rows.Add((id, count, tab));
    }

    public void Take(int id, string tab, int count = 1)
    {
        int at = _rows.FindIndex(r => r.id == id && r.tab == tab);
        if (_rows[at].count <= count) _rows.RemoveAt(at);
        else _rows[at] = (id, _rows[at].count - count, tab);
    }
}
