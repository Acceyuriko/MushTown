using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The MENU window art (UI/UITotalMenu.img/main): the window body with the
// seven blue tabs, the menu item buttons and the bottom row of seven
// buttons. Every button PNG carries its own icon and label baked in, so
// the client only draws the PNGs.
public static partial class Export
{
    // The file names must match the client (MushTown.Client/Panels/HudMenu.cs).
    public static bool ExportMenuUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        // the window body: the white panel, the seven tabs, the quick menu
        // strip. The white is 40% opaque on purpose: the client draws a
        // blurred copy of the screen behind it, so the body reads as glass.
        if (!UiPng.Save(ui, "UITotalMenu.img/main/backgrnd", Path.Combine(dir, "menu_bg.png"))) return false;

        // the menu items: one art set per row (the row count comes from the tree)
        var columns = ui.Find("UITotalMenu.img/main/menu/buttonInfo");
        if (columns == null)
        {
            Console.WriteLine("UITotalMenu: no buttonInfo");
            return false;
        }
        int colCount = 0;
        foreach (Wz_Node col in columns.Nodes)
        {
            if (!int.TryParse(col.Text, out int c)) continue;
            int rowCount = 0;
            foreach (Wz_Node row in col.Nodes)
            {
                if (!int.TryParse(row.Text, out int r)) continue; // the column label ("info")
                // The file name uses the display row (the client counts rows
                // from 0); the WZ path uses the node name.
                string stem = $"menu_item_c{c}r{rowCount}";
                foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p"), ("disabled", "d") })
                    if (!UiPng.Save(ui, $"UITotalMenu.img/main/menu/buttonInfo/{c}/{r}/{state}/0", Path.Combine(dir, $"{stem}_{suffix}.png"))) return false;
                rowCount++;
            }
            Console.WriteLine($"menu column {c}: {rowCount} rows");
            colCount++;
        }

        // the bottom row: seven buttons, each with its own word
        foreach (var (node, name) in new[]
        {
            ("button:cashShop", "menu_cashshop"),
            ("button:mapleAuction", "menu_auction"),
            ("button:channelChange", "menu_channel"),
            ("button:characterSwitching", "menu_switchchar"),
            ("button:keySetting", "menu_keys"),
            ("button:option", "menu_option"),
            ("button:gameQuit", "menu_quit"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p"), ("disabled", "d") })
                if (!UiPng.Save(ui, $"UITotalMenu.img/main/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the close button: the official white X
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p"), ("disabled", "d") })
            if (!UiPng.Save(ui, $"UITotalMenu.img/main/button:close/{state}/0", Path.Combine(dir, $"menu_close_{suffix}.png"))) return false;

        Console.WriteLine($"menu ui: {colCount} columns");
        return true;
    }
}
