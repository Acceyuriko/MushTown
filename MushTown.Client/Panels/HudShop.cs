using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The shop window: the keeper's official Shop frame on the left and the
// player's Inventory frame on the right (UI/UINpcShop.img). The left list is
// the stock to buy or the buy-back list; the right list is the bag, split by
// the four official inventory tabs. A single click selects a row, a second
// click on the same row within the double-click window acts.
public partial class MushTownGame
{
    private static readonly PanelLayout _shopLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-shop.json"));

    private bool _shopPlaced; // the window took the spot from the layout file
    private int _shopTab;      // 0 = keeper's stock, 1 = buy back
    private int _shopInvTab;   // 0 = Equip, 1 = Use, 2 = Etc, 3 = Set-up
    private int _shopSelId;    // selected row on the left
    private int _shopInvSelId; // selected row on the right
    private int _shopLastId;
    private bool _shopLastInv;
    private double _shopLastAt;
    private string _shopNotice = "";
    private string _shopBuiltKey = "";
    private int _shopBuyId; // the stock row whose quantity popup is open (0 = none)
    private readonly TextField _shopBuyField = new(_shopLay) { DigitsOnly = true, Limit = 5 };
    private readonly Dictionary<int, List<(int id, int count)>> _shopBuybacks = new();

    private const int ShopRowCount = 10;

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

    private List<(int id, int count)> ShopBuyback()
    {
        int npc = ShopNpc();
        if (!_shopBuybacks.TryGetValue(npc, out var list))
            _shopBuybacks[npc] = list = new List<(int, int)>();
        return list;
    }

    private List<(int id, int count)> ShopBagRows(int tab)
    {
        var rows = new List<(int, int)>();
        var chara = _save?.Slots[_slot];
        if (chara == null) return rows;
        if (tab == 0)
        {
            foreach (int id in _equips)
                if (chara.EquipBag?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        }
        else if (tab == 1)
        {
            foreach (int id in _potions)
                if (chara.Use?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        }
        return rows;
    }

    private void ShopPlaceFirst()
    {
        if (_shopPlaced) return;
        _shopPlaced = true;
        _shopPos = new Point(_shopLay.Get("x", 493), _shopLay.Get("y", 43));
    }

    // One click selects; the second click on the same row acts.
    private void ShopRowClicked(int id, bool inv, Action act)
    {
        if (_shopLastId == id && _shopLastInv == inv && _timeMs - _shopLastAt <= _shopLay.Get("doubleClickMs", 350))
        {
            _shopLastId = 0;
            _shopLastInv = false;
            _shopLastAt = 0;
            act();
            return;
        }
        _shopLastId = id;
        _shopLastInv = inv;
        _shopLastAt = _timeMs;
        if (inv) _shopInvSelId = id;
        else _shopSelId = id;
    }

    private void BuyFromShop(int id, int count = 1)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        var def = _items[id];
        long total = (long)def.Price * count;
        if (Shop.Buy(chara, id, def.Price, def, count))
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

    // Double-click on a stock row opens the official quantity popup. The
    // field starts at 1 and is already focused; Enter buys, Escape closes.
    private void OpenBuyPopup(int id)
    {
        _shopBuyId = id;
        _shopBuyField.SetText("1");
        _shopBuyField.ReplaceOnType = true;
        _shopBuyField.Ink = Color.Black;
        _shopBuyField.Focus(Keyboard.GetState());
    }

    private void CloseBuyPopup()
    {
        _shopBuyField.Unfocus();
        _shopBuyId = 0;
    }

    private int ShopBuyCount()
    {
        return int.TryParse(_shopBuyField.Text, out int n) ? n : 0;
    }

    private void ConfirmBuy()
    {
        int count = ShopBuyCount();
        if (count <= 0) return;
        int id = _shopBuyId;
        CloseBuyPopup();
        BuyFromShop(id, count);
    }

    // The quantity field owns the keys while its popup is open.
    private void UpdateShopBuyKeys(KeyboardState kb, float dtMs)
    {
        var key = _shopBuyField.Read(kb, dtMs);
        if (key == FieldKey.Leave) CloseBuyPopup();
        else if (key == FieldKey.Submit) ConfirmBuy();
    }

    private void SellToShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        var def = _items[id];
        if (!Shop.Sell(chara, id, def)) return;
        var list = ShopBuyback();
        int at = list.FindIndex(r => r.id == id);
        if (at >= 0) list[at] = (id, list[at].count + 1);
        else list.Add((id, 1));
        LogEvent($"sold {def.Name} for {Shop.SellPrice(def.Price):#,##0} mesos");
    }

    private void BuyBackFromShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        var def = _items[id];
        if (!Shop.Buy(chara, id, def.Price, def))
        {
            _shopNotice = $"Not enough mesos.\nYou need {def.Price:#,##0} mesos.";
            return;
        }
        var list = ShopBuyback();
        int at = list.FindIndex(r => r.id == id);
        if (at >= 0)
        {
            if (list[at].count <= 1) list.RemoveAt(at);
            else list[at] = (id, list[at].count - 1);
        }
        _shopNotice = "";
        LogEvent($"bought back {def.Name} for {def.Price:#,##0} mesos");
        PlaySfx("shopbuy");
    }

    // The tree rebuild key: tabs, the notice and both live lists. Row selection
    // is drawn live, so picking a row does not rebuild the shop.
    private string ShopSceneKey()
    {
        var chara = _save?.Slots[_slot];
        string bag = chara == null ? "" :
            string.Join("|", ShopBagRows(0).Select(r => $"{r.id}:{r.count}")) + "/" +
            string.Join("|", ShopBagRows(1).Select(r => $"{r.id}:{r.count}"));
        string buyback = string.Join("|", ShopBuyback().Select(r => $"{r.id}:{r.count}"));
        return $"{_shopTab}:{_shopInvTab}:{ShopNpc()}:{_shopNotice}:{_shopBuyId}:{_shopStock?.Count}|{bag}|{buyback}";
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
        foreach (string f in new[] { "wm_pop_top.png", "wm_pop_mid.png", "wm_pop_bottom.png" })
            LoadTexture("ui/" + f);
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/shop_x_{s}.png");
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/wm_pop_yes_{s}.png");
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/wm_pop_no_{s}.png");
        foreach (string f in new[] { "shop_buy_top.png", "shop_buy_mid.png", "shop_buy_bottom.png" })
            LoadTexture("ui/" + f);
    }

    private GameObject ShopRow(Rectangle rect, bool inv, int id, string name, int count, string price, string legacyId, Action act)
    {
        var o = PanelButton(rect, legacyId, () => ShopRowClicked(id, inv, act), ButtonKind.Click);
        o.Add(new ShopRowArt(() => inv ? _shopInvSelId == id : _shopSelId == id, inv ? "ui/shop_inv_row_s.png" : "ui/shop_row_s.png"));
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
            "shoptab" + index, () => { _shopTab = index; _shopSelId = 0; _shopLastId = 0; _shopNotice = ""; }, ButtonKind.Click);
        o.Add(new Picture(index == _shopTab ? "ui/shop_tab_s.png" : "ui/shop_tab_n.png", 0, 0));
        o.Add(new Text { Value = () => label, Ink = index == _shopTab ? Color.White : new Color(190, 220, 235), Align = TextAlign.Center });
        return o;
    }

    private GameObject ShopInvTab(int index, string label)
    {
        int x = ShopLeftW + _shopLay.Get("invTabX", 11) + index * _shopLay.Get("invTabStep", 76);
        int y = _shopLay.Get("invTabY", 103);
        var o = PanelButton(new Rectangle(x, y, _shopLay.Get("invTabW", 74), _shopLay.Get("invTabH", 22)),
            "shopinvt" + index, () => { _shopInvTab = index; _shopInvSelId = 0; _shopLastId = 0; _shopNotice = ""; }, ButtonKind.Click);
        o.Add(new Picture(index == _shopInvTab ? "ui/shop_tab_s.png" : "ui/shop_tab_n.png",
            (_shopLay.Get("invTabW", 74) - 70) / 2, 0));
        o.Add(new Text { Value = () => label, Ink = index == _shopInvTab ? Color.White : new Color(190, 220, 235), Align = TextAlign.Center });
        return o;
    }

    // The official buy-quantity popup: top (icon/name), middle (input and
    // prices) and bottom (confirmation buttons). It sits over the shop pair.
    private GameObject ShopBuyPopup()
    {
        int topH = _shopLay.Get("buyTopH", 159);
        int midH = _shopLay.Get("buyMidH", 156);
        int bottomH = _shopLay.Get("buyBottomH", 112);
        int w = _shopLay.Get("buyW", 392);
        int h = topH + midH + bottomH;
        int x = _shopLay.Get("buyX", (ShopW - w) / 2);
        int y = _shopLay.Get("buyY", (ShopH - h) / 2);
        var popup = new GameObject();
        popup.Add(new Rect(new Rectangle(x, y, w, h)));
        popup.AddChild(PictureAt("ui/shop_buy_top.png", 0, 0, w, topH));
        popup.AddChild(PictureAt("ui/shop_buy_mid.png", 0, topH, w, midH));
        popup.AddChild(PictureAt("ui/shop_buy_bottom.png", 0, topH + midH, w, bottomH));

        var item = _items[_shopBuyId];
        var icon = new GameObject();
        icon.Add(new Rect(new Rectangle(_shopLay.Get("buyIconX", 171), _shopLay.Get("buyIconY", 75),
            _shopLay.Get("buyIconW", 50), _shopLay.Get("buyIconH", 50))));
        icon.Add(new ShopIcon(item.Image));
        popup.AddChild(icon);
        popup.AddChild(ShopPopupText(() => item.Name, 0, _shopLay.Get("buyNameY", 132), w, 18,
            Color.White, TextAlign.Center));

        var input = new GameObject();
        input.Add(new Rect(new Rectangle(_shopLay.Get("buyInputX", 124), _shopLay.Get("buyInputY", 199),
            _shopLay.Get("buyInputW", 57), _shopLay.Get("buyInputH", 20))));
        input.Add(_shopBuyField);
        popup.AddChild(input);

        popup.AddChild(ShopPopupText(() => "Price", _shopLay.Get("buyPriceX", 56),
            _shopLay.Get("buyPriceY", 260), 90, 16, new Color(188, 254, 252)));
        popup.AddChild(ShopPopupText(() => $"{item.Price:#,##0}", _shopLay.Get("buyPriceX", 56),
            _shopLay.Get("buyPriceY", 260), 300, 16, Color.White, TextAlign.Right));
        popup.AddChild(ShopPopupText(() => "Total", _shopLay.Get("buyPriceX", 56),
            _shopLay.Get("buyTotalY", 283), 90, 16, new Color(188, 254, 252)));
        popup.AddChild(ShopPopupText(() => $"{(long)item.Price * ShopBuyCount():#,##0}",
            _shopLay.Get("buyPriceX", 56), _shopLay.Get("buyTotalY", 283), 300, 16,
            Color.White, TextAlign.Right));

        popup.AddChild(ArtButton(new Rectangle(_shopLay.Get("buyYesX", 109), _shopLay.Get("buyYesY", 377), 83, 26),
            "ui/wm_pop_yes", ConfirmBuy));
        popup.AddChild(ArtButton(new Rectangle(_shopLay.Get("buyNoX", 200), _shopLay.Get("buyNoY", 377), 83, 26),
            "ui/wm_pop_no", CloseBuyPopup));

        var root = new GameObject { Modal = true };
        root.AddChild(PanelButton(ShopRect(), "shopbuyblock", () => { }, ButtonKind.Silent));
        root.AddChild(popup);
        return root;
    }

    private static GameObject PictureAt(string key, int x, int y, int w, int h)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Picture(key));
        return o;
    }

    private static GameObject ShopPopupText(Func<string> value, int x, int y, int w, int h,
        Color ink, TextAlign align = TextAlign.Left)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Text { Value = value, X = 0, W = w, Ink = ink, Line = TextLine.Middle, Align = align });
        return o;
    }

    private GameObject ShopNoticeBox()
    {
        int w = _shopLay.Get("noticeW", 318), h = _shopLay.Get("noticeH", 160);
        var box = new Rectangle((ShopW - w) / 2, (ShopH - h) / 2, w, h);
        var blocker = PanelButton(ShopRect(), "", () => { }, ButtonKind.Silent); // the shop waits behind the notice
        var popup = new GameObject();
        popup.Add(new Rect(box));
        popup.Add(new Panel("ui/wm_pop_top.png", "ui/wm_pop_mid.png", "ui/wm_pop_bottom.png"));
        popup.Add(new SysPopText
        {
            Lines = _ui.Wrap(_shopNotice, _shopLay.Get("noticeTextW", 286)),
            Y = _shopLay.Get("noticeTextY", 38),
            Step = _shopLay.Get("noticeLineStep", 17),
            MaxWidth = _shopLay.Get("noticeTextW", 286),
        });
        popup.AddChild(ArtButton(new Rectangle(_shopLay.Get("noticeOkX", 117), _shopLay.Get("noticeOkY", 115), 83, 26),
            "ui/wm_pop_yes", () => _shopNotice = ""));
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
            _shopLay.Get("npcPortraitX", 70), _shopLay.Get("npcPortraitY", 88)));

        for (int i = 0; i < 2; i++) body.AddChild(ShopTab(i, i == 0 ? "Sell" : "Buy Back"));
        for (int i = 0; i < 4; i++) body.AddChild(ShopInvTab(i, i == 0 ? "Equip" : i == 1 ? "Use" : i == 2 ? "Etc" : "Set-up"));

        // The player side: avatar feet at the official preview spot, meso only.
        var avatar = new GameObject();
        avatar.Add(new Rect(new Rectangle(ShopLeftW + _shopLay.Get("invAvatarX", 58),
            _shopLay.Get("invAvatarY", 88), 1, 1)));
        avatar.Add(new AvatarPreview(this, "stand1", () => 0, true));
        body.AddChild(avatar);
        body.Add(new Picture("ui/shop_inv_meso.png", ShopLeftW + _shopLay.Get("invMesoBgX", 106),
            _shopLay.Get("invMesoBgY", 66)));
        body.Add(new ShopMesoLine(() => (_save?.Slots[_slot]?.Mesos ?? 0).ToString("#,##0"),
            ShopLeftW + _shopLay.Get("invMesoX", 293), _shopLay.Get("invMesoY", 70),
            _shopLay.Get("priceH", 14)));
        body.Add(new Label("Double-click an item to buy or sell.", _shopLay.Get("tipX", 18), _shopLay.Get("tipY", 657))
        { Tint = ShopTitleInk });

        // The left list.
        int row = 0;
        if (_shopTab == 0)
        {
            if (_shopStock.Count == 0) body.AddChild(ShopEmptyRow(ShopRowRect(row++)));
            foreach (int id in _shopStock)
            {
                if (row >= ShopRowCount)
                {
                    GameLog($"the shop has {_shopStock.Count} items, the list shows {ShopRowCount}");
                    break;
                }
                var def = _items[id];
                body.AddChild(ShopRow(ShopRowRect(row), false, id, def.Name, 1, $"{def.Price:#,##0}", "shopbuy" + row, () => OpenBuyPopup(id)));
                row++;
            }
        }
        else
        {
            var list = ShopBuyback();
            if (list.Count == 0) body.AddChild(ShopEmptyRow(ShopRowRect(row++)));
            foreach (var (id, count) in list)
            {
                if (row >= ShopRowCount)
                {
                    GameLog($"the buy-back list has {list.Count} items, the list shows {ShopRowCount}");
                    break;
                }
                var def = _items[id];
                body.AddChild(ShopRow(ShopRowRect(row), false, id, def.Name, count, $"{def.Price:#,##0}", "shopbuyback" + row, () => BuyBackFromShop(id)));
                row++;
            }
        }

        // The right bag list, one of the four inventory tabs.
        var bag = ShopBagRows(_shopInvTab);
        if (bag.Count == 0) body.AddChild(ShopEmptyRow(ShopInvRowRect(0)));
        for (int i = 0; i < bag.Count; i++)
        {
            if (i >= ShopRowCount)
            {
                GameLog($"the bag has {bag.Count} items, the list shows {ShopRowCount}");
                break;
            }
            var (id, count) = bag[i];
            var def = _items[id];
            body.AddChild(ShopRow(ShopInvRowRect(i), true, id, def.Name, count, $"{Shop.SellPrice(def.Price):#,##0}",
                "shopinvsell" + i, () => SellToShop(id)));
        }

        // Drag strips: both headers. The X is the right inventory's official one.
        body.AddChild(PanelButton(new Rectangle(0, 0, ShopW, _shopLay.Get("dragH", 30)), "",
            () => _dragShop = _uiScene.LastClick - _shopPos));
        body.AddChild(ArtButton(new Rectangle(ShopLeftW + _shopLay.Get("invCloseX", 295),
                _shopLay.Get("invCloseY", 2), _shopLay.Get("invCloseW", 27), _shopLay.Get("invCloseH", 27)),
            "ui/shop_x", () => { _showShop = false; _shopBuyId = 0; }));

        if (_shopNotice != "") body.AddChild(ShopNoticeBox());
        if (_shopBuyId != 0) body.AddChild(ShopBuyPopup());
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
