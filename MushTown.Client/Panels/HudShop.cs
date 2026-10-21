using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The shop window: the official MapleStory art (UINpcShop.img/Shop). The
// frame has ten item rows baked in. The keeper's stock to buy comes first,
// the bag to sell under it.
public partial class MushTownGame
{
    // Every number of this window lives in layouts/panel-shop.json.
    private static readonly PanelLayout _shopLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-shop.json"));

    private bool _shopPlaced; // the window took the spot from the layout file
    private int _shopTab;      // 0 = buy from the keeper, 1 = sell to the keeper

    // The official list: ten rows in the frame art.
    private const int ShopRowCount = 10;

    // The window width: the drag clamp in the HUD chain uses this.
    private int ShopW => _shopLay.Get("w", 380);
    private int ShopH => _shopLay.Get("h", 681);
    private Rectangle ShopRect() => new(_shopPos.X, _shopPos.Y, ShopW, ShopH);

    // The official ink: the title is pale, the item text is black on the
    // light rows.
    private static readonly Color ShopTitleInk = new(190, 203, 217);
    private static readonly Color ShopNameInk = new(20, 20, 20);
    private static readonly Color ShopPriceInk = new(70, 70, 70);

    // Sell rows: owned potions first, then owned equips (both in tab order).
    // Draw and clicks share this list, so row i is always row i.
    private List<(int id, int count)> ShopSellRows()
    {
        var rows = new List<(int, int)>();
        var chara = _save?.Slots[_slot];
        if (chara == null) return rows;
        foreach (int id in _potions)
            if (chara.Use?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        foreach (int id in _equips)
            if (chara.EquipBag?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        return rows;
    }

    // One row of the official list: the item list starts at listX/listY.
    private Rectangle ShopRowRect(int i) => new(
            _shopPos.X + _shopLay.Get("listX", 20),
            _shopPos.Y + _shopLay.Get("listY", 141) + i * _shopLay.Get("rowPitch", 50),
            _shopLay.Get("rowW", 336), _shopLay.Get("rowH", 45));

    // The keeper: the stock list belongs to one shop entry.
    private int ShopNpc()
    {
        foreach (var (npc, shop) in _shops)
            if (ReferenceEquals(shop.Stock, _shopStock)) return npc;
        return 0;
    }

    // The window opens at the spot in the layout file; the drag moves it.
    private void ShopPlaceFirst()
    {
        if (_shopPlaced) return;
        _shopPlaced = true;
        _shopPos = new Point(_shopLay.Get("x", 493), _shopLay.Get("y", 43));
    }

    // Click a stock row: buy one at WZ price (a broke wallet logs and skips).
    private void BuyFromShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no wallet
        var def = _items[id];
        int price = def.Price;
        if (Shop.Buy(chara, id, price))
        {
            GameLog($"bought {def.Name} for {price:#,##0} mesos");
            PlaySfx("shopbuy");
        }
        else GameLog($"not enough mesos for {def.Name} ({price:#,##0})");
    }

    // Click a bag row: sell one at half price (rows show owned only, so it lands).
    private void SellToShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no bag
        var def = _items[id];
        if (Shop.Sell(chara, id, def))
        {
            GameLog($"sold {def.Name} for {Shop.SellPrice(def.Price):#,##0} mesos");
            PlaySfx("click");
        }
    }

    // The shop art: loaded once, then the texture cache answers.
    private void LoadShopArt()
    {
        LoadTexture("ui/shop_backgrnd.png");
        for (int i = 0; i < 2; i++) LoadTexture($"ui/shop_tab{i}.png");
        foreach (string s in new[] { "n", "h", "p" }) LoadTexture($"ui/shop_x_{s}.png");
    }

    // One item row: the icon, the name, the price. The click buys or sells.
    private GameObject ShopRow(int row, string image, string name, string price, string id, Action onClick)
    {
        var o = PanelButton(ShopRowRect(row), id, onClick);
        o.Add(new Picture(image, _shopLay.Get("iconX", 7), _shopLay.Get("iconY", 7)));
        o.Add(new Label(name, _shopLay.Get("nameX", 56), _shopLay.Get("nameY", 6)) { Tint = ShopNameInk });
        o.Add(new Label(price, _shopLay.Get("priceX", 72), _shopLay.Get("priceY", 24)) { Tint = ShopPriceInk });
        return o;
    }

    // An empty list says so (the official art has no text for it).
    private GameObject ShopEmptyRow(int row)
    {
        var o = new GameObject();
        o.Add(new Rect(ShopRowRect(row)));
        o.Add(new Label("No item", _shopLay.Get("nameX", 56), _shopLay.Get("nameY", 6)) { Tint = ShopNameInk });
        return o;
    }

    // One tab of the strip: the official tab piece, the label and the click
    // that picks the list.
    private GameObject ShopTab(int index, string label)
    {
        int x = _shopLay.Get("tabX", 11) + index * _shopLay.Get("tabStep", 72);
        int y = _shopLay.Get("tabY", 103);
        var o = PanelButton(new Rectangle(_shopPos.X + x, _shopPos.Y + y,
                _shopLay.Get("tabW", 72), _shopLay.Get("tabH", 30)),
            "shoptab" + index, () => _shopTab = index);
        o.Add(new Label(label, _shopLay.Get("tabNameX", 46) - _ui.GetSmall(label).Width / 2, 4) { Tint = Color.White });
        return o;
    }

    private void BuildShopScene(List<(int id, int count)> sells)
    {
        LoadShopArt();
        ShopPlaceFirst();
        _shopScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(ShopRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new Picture("ui/shop_backgrnd.png")); // the official frame, the rows are baked in
        string title = NpcName(ShopNpc());
        body.Add(new LiveLabel(() => title, _shopLay.Get("titleX", 190) - _ui.GetMid(title).Width / 2, _shopLay.Get("titleY", 52), ShopTitleInk, LabelFont.Mid));
        // the tab bar: the official tab pieces, one per kind of list
        for (int i = 0; i < 2; i++)
            body.Add(new Picture($"ui/shop_tab{i}.png", _shopLay.Get("tabX", 11) + i * _shopLay.Get("tabStep", 72), _shopLay.Get("tabY", 103)));
        body.AddChild(ShopTab(0, "Buy"));
        body.AddChild(ShopTab(1, "Sell"));
        // the mesos line in the bottom bar
        body.Add(new RightLabel((ui, s) => ui.GetMeso(s),
            () => $"Mesos: {(_save?.Slots[_slot]?.Mesos ?? 0):#,##0}",
            _shopLay.Get("mesoX", 356), _shopLay.Get("mesoY", 650), ShopTitleInk));
        // the rows: the keeper's stock on the buy tab, the bag on the sell tab
        int row = 0;
        if (_shopTab == 0)
        {
            if (_shopStock.Count == 0) body.AddChild(ShopEmptyRow(row++));
            foreach (int id in _shopStock)
            {
                if (row >= ShopRowCount) break;
                int s = row; // for-loop variables capture shared: copy per slot
                var def = _items[id];
                body.AddChild(ShopRow(row++, def.Image, def.Name, $"{def.Price:#,##0}", "buy" + s, () => BuyFromShop(id)));
            }
        }
        else
        {
            if (sells.Count == 0) body.AddChild(ShopEmptyRow(row++));
            foreach (var (id, count) in sells)
            {
                if (row >= ShopRowCount) break;
                int s = row; // for-loop variables capture shared: copy per slot
                var def = _items[id];
                body.AddChild(ShopRow(row++, def.Image, $"{def.Name} x{count}", $"Sell {Shop.SellPrice(def.Price):#,##0}", "sell" + s, () => SellToShop(id)));
            }
        }
        // the drag strip at the top and the X
        body.AddChild(PanelButton(new Rectangle(_shopPos.X, _shopPos.Y, ShopW, _shopLay.Get("dragH", 30)),
            "", () => _dragShop = _shopScene.LastClick - _shopPos)); // silent, like legacy
        body.AddChild(ArtButton(new Rectangle(_shopPos.X + _shopLay.Get("closeX", 349), _shopPos.Y + _shopLay.Get("closeY", 2),
                _shopLay.Get("closeW", 27), _shopLay.Get("closeH", 27)),
            "ui/shop_x", () => _showShop = false));
        _shopScene.Add(body);
    }
}
