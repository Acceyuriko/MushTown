using System.Drawing;
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

        // the window body: the white panel, the seven tabs, the quick menu strip
        if (!SaveMenuBg(ui, "UITotalMenu.img/main/backgrnd", Path.Combine(dir, "menu_bg.png"))) return false;

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

        // the close button: the official X is white and our panel is solid
        // white, so the X is baked dark grey (the hover art has its own plate)
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("pressed", "p"), ("disabled", "d") })
            if (!SaveCloseX(ui, $"UITotalMenu.img/main/button:close/{state}/0", Path.Combine(dir, $"menu_close_{suffix}.png"))) return false;
        if (!UiPng.Save(ui, "UITotalMenu.img/main/button:close/mouseOver/0", Path.Combine(dir, "menu_close_h.png"))) return false;

        Console.WriteLine($"menu ui: {colCount} columns");
        return true;
    }

    // The window body with the white panel solid. The official body is a
    // translucent white (the game draws a blurred screen behind it) and we
    // have no blur, so the white keeps its shape but stops being see-through.
    private static bool SaveMenuBg(WzRoot ui, string node, string path)
    {
        if (!UiPng.TryRead(ui, "UI", node, out var png, out _)) return false;
        using var bitmap = png.ExtractPng();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.A >= 60 && p.R >= 250 && p.G >= 250 && p.B >= 250)
                    bitmap.SetPixel(x, y, Color.FromArgb(255, p.R, p.G, p.B));
            }
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return true;
    }

    // The close button with a dark X. The official X is white and our panel
    // is solid white, so a white X would be invisible.
    private static bool SaveCloseX(WzRoot ui, string node, string path)
    {
        if (!UiPng.TryRead(ui, "UI", node, out var png, out _)) return false;
        using var bitmap = png.ExtractPng();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.A > 0 && p.R >= 250 && p.G >= 250 && p.B >= 250)
                    bitmap.SetPixel(x, y, Color.FromArgb(p.A, 105, 112, 130));
            }
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return true;
    }
}
