using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The shop window art (UI/UINpcShop.img/Shop). The file names must match
// the ones the client loads (MushTown.Client/Panels/HudShop.cs).
//
// The frame (backgrnd) has the ten item rows baked in. The tab bar has two
// pieces of the same tab (the buy tab and the sell tab).
public static partial class Export
{
    public static bool ExportShopUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            ("UINpcShop.img/Shop/backgrnd", "shop_backgrnd.png"),
        };
        // The tabs: the buy tab and the sell tab.
        for (int i = 0; i < 2; i++)
            files.Add(($"UINpcShop.img/Shop/tab:npc/selected/{i}", $"shop_tab{i}.png"));
        // The close button: the same X the other windows use.
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            files.Add(($"UINpcShop.img/Inventory/Normal/button:close/{state}/0", $"shop_x_{suffix}.png"));
        foreach ((string node, string file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        Console.WriteLine($"shop: {files.Count} files");
        return true;
    }
}
