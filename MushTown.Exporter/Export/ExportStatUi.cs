using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The character info window art (UICharacterInfo.img): the top block with the
// portrait, the bottom block with the STAT strip, the stats page (the label
// art with the names baked in). The item tooltip frame (UIToolTip.img) is the
// box a hovered item opens. The file names must match the client
// (MushTown.Client/Panels/HudStat.cs).
public static partial class Export
{
    public static bool ExportStatUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            // The top block: the Character Info frame and its close button.
            ("UICharacterInfo.img/common/main/backgrnd", "charinfo_main.png"),
            ("UICharacterInfo.img/common/main/button:close/normal/0", "stat_x.png"),
            // The bottom block: the window frame and the STAT strip.
            ("UICharacterInfo.img/local/detail/backgrnd", "stat_win.png"),
            ("UICharacterInfo.img/local/detail/layer:stat", "stat_statbar.png"),
            // The name plate under the portrait (the official character name tag).
            ("UIWindow2.img/UserInfo/character/name", "stat_name.png"),
            // The stats page: the panel (with both light panels in it) and the label art.
            ("UICharacterInfo.img/common/detailStat/canvas:attackBack", "stat_page.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:mainStatFont", "stat_label_main.png"),
            ("UICharacterInfo.img/common/detailStat/Stat/1/titleImage", "stat_label_mp.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:attackFont", "stat_label_attack.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:utilityFont", "stat_label_utility.png"),
            // The row highlight the official page puts under the hovered row.
            ("UICharacterInfo.img/common/detailStat/canvas:highlightLong", "stat_hl_long.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:highlightShort", "stat_hl_short.png"),
        };
        // The item tooltip frame: four corners, four edges, one center.
        foreach (string piece in new[] { "nw", "ne", "sw", "se", "n", "s", "w", "e", "c" })
            files.Add(($"UIToolTip.img/Default/frame/flexible/{piece}", $"tip_{piece}.png"));
        foreach ((string node, string file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        Console.WriteLine($"stat: {files.Count} files");
        return true;
    }
}
