using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The inventory window art (UIInventory.img). The file names must match
// the ones the client loads (MushTown.Client/Panels/HudInventory.cs).
//
// Two official forms: the small window (backgrnd 242x593) and the wide
// one the "+" button opens (FullBackgrnd 800x546). Each form has its own
// AutoBuild folder with the X button, the meso coin and the six tab
// icons. Only the expand button the form shows is exported: the "+" in
// the small window, the "-" in the wide one.
public static partial class Export
{
    public static bool ExportInventoryUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            ("UIInventory.img/Inventory/backgrnd", "inv_backgrnd.png"),
            ("UIInventory.img/Inventory/FullBackgrnd", "invw_backgrnd.png"),
        };
        // (file prefix, AutoBuild folder, expand button, expand stem):
        // the small window and the wide one.
        foreach ((string prefix, string build, string expandButton, string expandStem) in new[]
        {
            ("inv", "AutoBuild", "button:full", "plus"),
            ("invw", "FullAutoBuild", "button:min", "minus"),
        })
        {
            // The close button (X) and the expand button ("+" small, "-" wide).
            foreach ((string button, string stem) in new[] { ("button:close", "close"), (expandButton, expandStem) })
            {
                files.Add(($"UIInventory.img/Inventory/{build}/{button}/normal/0", $"{prefix}_{stem}_n.png"));
                files.Add(($"UIInventory.img/Inventory/{build}/{button}/mouseOver/0", $"{prefix}_{stem}_h.png"));
                files.Add(($"UIInventory.img/Inventory/{build}/{button}/pressed/0", $"{prefix}_{stem}_p.png"));
            }
            files.Add(($"UIInventory.img/Inventory/{build}/button:meso/normal/0", $"{prefix}_coin.png"));
            // The six category tabs (Equip, Use, Etc, Set-up, Cash, Dec).
            for (int t = 0; t < 6; t++)
            {
                files.Add(($"UIInventory.img/Inventory/{build}/tab:category/selected/{t}", $"{prefix}_tab_s{t}.png"));
                files.Add(($"UIInventory.img/Inventory/{build}/tab:category/normal/{t}", $"{prefix}_tab_n{t}.png"));
            }
        }
        foreach ((string node, string file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        Console.WriteLine($"inventory: {files.Count} files");
        return true;
    }
}
