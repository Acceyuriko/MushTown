using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The character info window art (UICharacterInfo.img): the window with its
// STAT/EQUIP tab bar, the stats page (the label art with the names baked in)
// and the equip page (the slot frames with the slot names baked in).
// The item tooltip frame (UIToolTip.img) is the detail box a slot opens.
// The file names must match the client (MushTown.Client/Panels/HudStat.cs).
public static partial class Export
{
    public static bool ExportStatUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            // The window: the dark frame with the tab strip on top.
            ("UICharacterInfo.img/remote/detail/backgrnd", "stat_win.png"),
            ("UICharacterInfo.img/remote/detail/tab:detailTab/selected/0", "stat_tab_stat_s.png"),
            ("UICharacterInfo.img/remote/detail/tab:detailTab/normal/0", "stat_tab_stat_n.png"),
            ("UICharacterInfo.img/remote/detail/tab:detailTab/selected/1", "stat_tab_equip_s.png"),
            ("UICharacterInfo.img/remote/detail/tab:detailTab/normal/1", "stat_tab_equip_n.png"),
            ("UICharacterInfo.img/common/main/button:close/normal/0", "stat_x.png"),
            // The stats page: the panel (with both light panels in it) and the label art.
            ("UICharacterInfo.img/common/detailStat/canvas:attackBack", "stat_page.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:mainStatFont", "stat_label_main.png"),
            ("UICharacterInfo.img/common/detailStat/Stat/1/titleImage", "stat_label_mp.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:attackFont", "stat_label_attack.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:utilityFont", "stat_label_utility.png"),
            // The row highlight the official page puts under the hovered row.
            ("UICharacterInfo.img/common/detailStat/canvas:highlightLong", "stat_hl_long.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:highlightShort", "stat_hl_short.png"),
            // The equip page: every slot frame with its name.
            ("UICharacterInfo.img/remote/detailEquip/canvas:equip", "stat_equip.png"),
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
