using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The shop window art (UI/UINpcShop.img): the keeper's Shop frame on the
// left and the player's Inventory frame on the right. The file names must
// match the ones the client loads (MushTown.Client/Panels/HudShop.cs).
//
// Both frames have the ten item rows baked in. The left bar has two tabs
// (Sell / Buy Back); the right bar has the four inventory tabs, drawn with
// the left tab art and our own labels.
public static partial class Export
{
    public static bool ExportShopUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            ("UINpcShop.img/Shop/backgrnd", "shop_backgrnd.png"),
            ("UINpcShop.img/Inventory/Normal/backgrnd", "shop_inv_backgrnd.png"),
            ("UINpcShop.img/Shop/itemList/back/selected", "shop_row_s.png"),
            ("UINpcShop.img/Inventory/Normal/itemList/back/selected", "shop_inv_row_s.png"),
            ("UINpcShop.img/Inventory/Normal/layer:meso", "shop_inv_meso.png"),
            ("UINpcShop.img/PointInfo/Meso/iconShop", "shop_meso.png"),
        };
        // The left tabs: one blank cyan tab, in its normal and selected states.
        files.Add(("UINpcShop.img/Shop/tab:npc/normal/0", "shop_tab_n.png"));
        files.Add(("UINpcShop.img/Shop/tab:npc/selected/0", "shop_tab_s.png"));
        // The buy-quantity popup (the same frame the official shop uses).
        files.Add(("_Canvas/UINpcShop.img/popup/Buy/top/canvas:backgrnd", "shop_buy_top.png"));
        files.Add(("_Canvas/UINpcShop.img/popup/Buy/mid_buyForCoin1/canvas:backgrnd", "shop_buy_mid.png"));
        files.Add(("_Canvas/UINpcShop.img/popup/Buy/bottom/canvas:bottom", "shop_buy_bottom.png"));
        // The close button: the same X the other windows use.
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            files.Add(($"UINpcShop.img/Inventory/Normal/button:close/{state}/0", $"shop_x_{suffix}.png"));
        foreach ((string node, string file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        if (!ExportSysPop(ui, outDir)) return false; // the notice box and the buy popup share this art
        Console.WriteLine($"shop: {files.Count} files");
        return true;
    }
}
