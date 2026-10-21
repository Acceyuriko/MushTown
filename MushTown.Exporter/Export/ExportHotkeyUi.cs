using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The hotkey bar art (UI/UIQuickSlot.img/quickSlot): the official cell
// frame strip, 16 cells in two rows. The client draws one cell per hotkey
// action out of it (MushTown.Client/Panels/HudHotkeyBar.cs). The key caps
// are the key settings window art (ExportKeyUi), the client reuses them.
public static partial class Export
{
    // The file names must match the client (MushTown.Client/Panels/HudHotkeyBar.cs).
    public static bool ExportHotkeyUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        // the cell frames: the official quick slot strip
        if (!UiPng.Save(ui, "UIQuickSlot.img/quickSlot/backgrnd", Path.Combine(dir, "hotkey_cells.png"))) return false;

        Console.WriteLine("hotkey ui: 1 cell strip");
        return true;
    }
}
