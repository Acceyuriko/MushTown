using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The contents guide window (UIContentsGuide.img): the modern client
// guide window with its tab, its three buttons and the three content
// areas (field, boss, special). The file names must match the client
// (MushTown.Client/Panels/HudGuide.cs).
public static partial class Export
{
    public static bool ExportGuideUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        var files = new[]
        {
            ("UIContentsGuide.img/Maximum/backgrnd", "guide_bg.png"),
            ("UIContentsGuide.img/Maximum/field_wnd/field_wnd_level/list1/canvas:backgrnd", "guide_field.png"),
            ("UIContentsGuide.img/Maximum/boss_wnd/layer:backgrnd", "guide_boss.png"),
            ("UIContentsGuide.img/Maximum/special_wnd/layer:backgrnd", "guide_special.png"),
        };
        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(dir, file)))
                return false;

        // the content icons of the field list (the first rows of the grid)
        for (int i = 0; i < 12; i++)
            if (!UiPng.Save(ui, $"UIContentsGuide.img/BtField/{i}/normal/0", Path.Combine(dir, $"guide_item_{i}.png")))
                return false;

        // the tab of the window: three pieces (left, middle, right)
        foreach (var (state, name) in new[] { ("normal", "guide_tab"), ("selected", "guide_tabsel") })
            for (int piece = 0; piece < 3; piece++)
                if (!UiPng.Save(ui, $"UIContentsGuide.img/Maximum/tab:mainTab/{state}/{piece}", Path.Combine(dir, $"{name}_{piece}.png")))
                    return false;

        // the three buttons: minimize, close, help
        foreach (var (node, name) in new[]
        {
            ("UIContentsGuide.img/Maximum/button:BtMin", "guide_min"),
            ("UIContentsGuide.img/Maximum/button:BtClose", "guide_close"),
            ("UIContentsGuide.img/Maximum/button:BtHelp", "guide_help"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p"), ("disabled", "d") })
                if (!UiPng.Save(ui, $"{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png")))
                    return false;

        if (!ExportGuideSmallWindow(ui, dir)) return false;

        Console.WriteLine("guide ui: done");
        return true;
    }

    // The small guide window (UIContentsGuide.img/Minimum): the window the
    // game shows on the screen. It holds the two cards (main quest and job
    // advance), four buttons and the two dots under the cards.
    private static bool ExportGuideSmallWindow(WzRoot ui, string dir)
    {
        var files = new[]
        {
            ("UIContentsGuide.img/Minimum/backgrnd", "guide_small_bg.png"),
            ("UIContentsGuide.img/Minimum/BtMainQuest/normal/0", "guide_small_mainquest.png"),
            ("UIContentsGuide.img/Minimum/BtMainQuest/pressed/0", "guide_small_mainquest_p.png"),
            ("UIContentsGuide.img/Minimum/BtJob/normal/0", "guide_small_job.png"),
            ("UIContentsGuide.img/Minimum/Dot/0", "guide_small_dot0.png"),
            ("UIContentsGuide.img/Minimum/Dot/1", "guide_small_dot1.png"),
        };
        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(dir, file)))
                return false;

        // the four buttons of the small window
        foreach (var (node, name) in new[]
        {
            ("UIContentsGuide.img/Minimum/button:Max", "guide_small_max"),
            ("UIContentsGuide.img/Minimum/button:Close", "guide_small_close"),
            ("UIContentsGuide.img/Minimum/button:Left", "guide_small_left"),
            ("UIContentsGuide.img/Minimum/button:Right", "guide_small_right"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p"), ("disabled", "d") })
                if (!UiPng.Save(ui, $"{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png")))
                    return false;

        Console.WriteLine("guide small window: done");
        return true;
    }
}
