using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The quick menu icons (UI/UITotalMenu.img/quickMenu/menu): the 55
// official 30x30 icons of the quick menu bar. The file name uses the
// icon order (0..54), so the client asks for quickmenu_<n>_n.png.
public static partial class Export
{
    // The file names must match the client (MushTown.Client/Panels/HudQuickMenu.cs).
    public static bool ExportQuickMenuUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        var menu = ui.Find("UITotalMenu.img/quickMenu/menu");
        if (menu == null)
        {
            Console.WriteLine("quickMenu: no menu node");
            return false;
        }

        int index = 0;
        var states = new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") };
        foreach (Wz_Node icon in menu.Nodes)
        {
            foreach (var (state, suffix) in states)
                if (!UiPng.Save(ui, $"UITotalMenu.img/quickMenu/menu/{icon.Text}/{state}/0", Path.Combine(dir, $"quickmenu_{index}_{suffix}.png"))) return false;
            // The name proves the order is the official one: the client asks
            // for quickmenu_<official number>, so index 52 must be Key Bindings.
            var tip = ui.Find($"UITotalMenu.img/quickMenu/menu/{icon.Text}/ToolTip")?.Value as string;
            Console.WriteLine($"quick menu {index} = {tip ?? icon.Text}");
            index++;
        }
        Console.WriteLine($"quick menu ui: {index} icons");

        // The hamburger at the right end of the row (tooltip "Menu"): it opens
        // the MENU window, and our MENU has no other mouse entry.
        foreach (var (state, suffix) in states)
            if (!UiPng.Save(ui, $"UITotalMenu.img/quickMenu/button:menu/{state}/0", Path.Combine(dir, $"quickmenu_hamburger_{suffix}.png"))) return false;
        return true;
    }
}
