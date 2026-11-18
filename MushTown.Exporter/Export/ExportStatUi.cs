using MushTown.Shared;
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
            ("UICharacterInfo.img/common/detailStat/canvas:defenseFont", "stat_label_defense.png"), // page 2: defense, speed, knockback resistance
        };
        // The item tooltip frame: four corners, four edges, one center.
        foreach (string piece in new[] { "nw", "ne", "sw", "se", "n", "s", "w", "e", "c" })
            files.Add(($"UIToolTip.img/Default/frame/flexible/{piece}", $"tip_{piece}.png"));
        // The equipment tooltip (UIToolTip.img/Item/Equip): the left frame, the set frame on the right,
        // the category bar (the job row), the icon box and the empty star.
        foreach (string part in new[] { "top", "mid", "btm", "line" })
            files.Add(($"UIToolTip.img/Item/Equip/frame/common/{part}", $"etip_{part}.png"));
        foreach (string part in new[] { "w", "c", "e" })
            files.Add(($"UIToolTip.img/Item/Equip/frame/common/category/{part}", $"etip_cat_{part}.png"));
        foreach (string part in new[] { "top", "mid", "btm", "line" })
            files.Add(($"UIToolTip.img/Item/Equip/frame/set/{part}", $"etip_set_{part}.png"));
        files.Add(("UIToolTip.img/Item/Equip/itemIcon/base", "etip_icon_base.png"));
        files.Add(("UIToolTip.img/Item/Equip/itemIcon/shade", "etip_icon_shade.png"));
        files.Add(("UIToolTip.img/Item/Equip/textIcon/starForce/empty", "etip_star_empty.png"));
        foreach ((string node, string file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        if (!ExportStatDesc(ui, outDir)) return false;
        Console.WriteLine($"stat: {files.Count} files");
        return true;
    }

    // The WZ node (Stat/<n>) of each panel row, in the row order of
    // HudStat.StatRows: left then right, area by area. Checked against the
    // clickRangeLT of each node (research/character-stats.md 8.4).
    private static readonly int[] StatDescNodes =
    {
        0, 1, 2, 3, 4, 5, // HP MP / STR DEX / INT LUK
        7, 6, 9, 8, 12, 11, 10, 18, 13, 21, 14, 16, 17, 15, 20, 19, // the attack area
        22, 23, 24, 25, 26, 27, // mesos, star force, drop, arcane, exp, sacred
        28, 29, 30, 31, 32, 33, // page 2 of the bottom block: defense, status resistance, speed, jump, knockback resistance, attack speed
    };

    // Title and Desc of every row. The WZ text carries the two characters
    // backslash-n (and backslash-r backslash-n) as line breaks and #c...# colour
    // marks: lines are split and the marks are dropped.
    private static bool ExportStatDesc(WzRoot ui, string outDir)
    {
        var data = new StatDescData();
        foreach (int n in StatDescNodes)
        {
            var node = ui.Find($"UICharacterInfo.img/common/detailStat/Stat/{n}");
            if (node?.Nodes["Title"]?.Value is not string title || node.Nodes["Desc"]?.Value is not string desc)
            {
                Console.WriteLine($"node not found: detailStat/Stat/{n} Title/Desc");
                return false;
            }
            var lines = desc.Replace(@"\r\n", "\n").Replace(@"\n", "\n").Replace("\r\n", "\n")
                .Replace("#c", "").Replace("#", "").Split('\n').Select(l => l.Trim()).ToList();
            data.Rows.Add(new StatDescRow { Title = title, Lines = lines });
        }
        WriteJson(Path.Combine(outDir, "ui", "stat_desc.json"), data);
        return true;
    }
}
