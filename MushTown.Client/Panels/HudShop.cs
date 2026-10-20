using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The shop window: the keeper's stock to buy, the bag to sell.
public partial class MushTownGame
{
    private const int ShopW = 340; // shop window: buy rows + sell rows (height follows the bag)
    private const int ShopRowH = 40;

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

    // Shop rects: mesos line 20, Buy header 18 + stock rows, Sell header 18
    // + bag rows (an empty bag keeps one row for the "empty" line).
    private Rectangle ShopRect()
    {
        int sells = Math.Max(1, ShopSellRows().Count);
        int body = 20 + 18 + _shopStock.Count * ShopRowH + 18 + sells * ShopRowH + 10;
        return new Rectangle(_shopPos.X, _shopPos.Y, ShopW, 16 + body);
    }

    private Rectangle ShopTitleRect() => new(_shopPos.X, _shopPos.Y, ShopW, 16);

    private Rectangle ShopXRect()
    {
        var r = ShopRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private int ShopBuyTop() => _shopPos.Y + 16 + 20 + 18;

    private int ShopSellTop() => ShopBuyTop() + _shopStock.Count * ShopRowH + 18;

    private Rectangle ShopBuyRect(int i) =>
        new(_shopPos.X + ShopW - 80, ShopBuyTop() + i * ShopRowH + 7, 68, 26);

    private Rectangle ShopSellRect(int i) =>
        new(_shopPos.X + ShopW - 80, ShopSellTop() + i * ShopRowH + 7, 68, 26);

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

    private void BuildShopScene(List<(int id, int count)> sells)
    {
        _shopScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(ShopRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("SHOP"));
        body.Add(new Fill(new Color(62, 96, 118), 16));
        body.Add(new LiveLabel(() => $"Mesos: {(_save?.Slots[_slot]?.Mesos ?? 0):#,##0}", 12, 16 + 4, new Color(255, 220, 0)));
        body.Add(new Label("Buy", 12, 16 + 22));
        var r = ShopRect();
        for (int i = 0; i < _shopStock.Count; i++)
        {
            int s = i; // for-loop variables capture shared: copy per slot
            int id = _shopStock[s];
            var def = _items[id];
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, ShopBuyTop() + s * ShopRowH, ShopW, ShopRowH)));
            holder.Add(new ShopRow(def.Image, def.Name, $"{def.Price:#,##0}"));
            body.AddChild(holder);
            var buy = PanelButton(ShopBuyRect(s), "buy" + s, () => BuyFromShop(_shopStock[s]));
            buy.Add(new FlatButton("Buy", "buy" + s, () => _hoverId));
            body.AddChild(buy);
        }
        body.Add(new Label("Sell", 12, ShopSellTop() - r.Y - 16));
        if (sells.Count == 0)
            body.Add(new Label("Your bag is empty.", 12, ShopSellTop() - r.Y + 12) { Tint = new Color(130, 140, 150) });
        for (int i = 0; i < sells.Count; i++)
        {
            int s = i; // for-loop variables capture shared: copy per slot
            int id = sells[s].id;
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, ShopSellTop() + s * ShopRowH, ShopW, ShopRowH)));
            holder.Add(new ShopRow(_items[id].Image, $"{_items[id].Name} x{sells[s].count}", $"{Shop.SellPrice(_items[id].Price):#,##0}"));
            body.AddChild(holder);
            var sell = PanelButton(ShopSellRect(s), "sell" + s, () => SellToShop(sells[s].id));
            sell.Add(new FlatButton("Sell", "sell" + s, () => _hoverId));
            body.AddChild(sell);
        }
        body.AddChild(PanelButton(ShopTitleRect(), "", () => _dragShop = _shopScene.LastClick - _shopPos)); // silent, like legacy
        var x = PanelButton(ShopXRect(), "x5", () => _showShop = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        _shopScene.Add(body);
    }
}
