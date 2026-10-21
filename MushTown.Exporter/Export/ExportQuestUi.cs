using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The quest window art (UIWindow.img/Quest): the list panel (left), the
// detail panel (right), the tab labels, the list icons, the detail section
// titles, the progress gauge and the X button. The file names must match
// the client (MushTown.Client/Panels/HudQuest.cs).
public static partial class Export
{
    public static bool ExportQuestUi(WzRoot ui, string outDir)
    {
        var files = new[]
        {
            ("UIWindow.img/Quest/backgrnd", "quest_list.png"),
            ("UIWindow.img/Quest/backgrnd2", "quest_detail.png"),
            ("UIWindow.img/Quest/Tab/enabled/0", "quest_tab0.png"),
            ("UIWindow.img/Quest/Tab/enabled/1", "quest_tab1.png"),
            ("UIWindow.img/Quest/Tab/enabled/2", "quest_tab2.png"),
            ("UIWindow.img/Quest/icon0", "quest_icon0.png"),
            ("UIWindow.img/Quest/icon1", "quest_icon1.png"),
            ("UIWindow.img/Quest/basic", "quest_basic.png"),
            ("UIWindow.img/Quest/prob", "quest_prob.png"),
            ("UIWindow.img/Quest/reward", "quest_reward.png"),
            ("UIWindow.img/Quest/Gauge2/frame", "quest_gauge.png"),
            ("UIWindow.img/Quest/Gauge2/gauge", "quest_gauge_fill.png"),
            // The X in the corner: the quest window has no close button of
            // its own, so it takes the official one (the stat window's).
            ("UICharacterInfo.img/common/main/button:close/normal/0", "quest_x.png"),
        };
        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        if (!ExportQuestHelperUi(ui, outDir)) return false;
        Console.WriteLine($"quest ui: {files.Length} files");
        return true;
    }

    // The quest helper bar (UI/Quest.img/Helper): the bar on the screen,
    // the count digits and the two buttons. The file names must match the
    // client (MushTown.Client/Panels/HudQuestHelper.cs).
    public static bool ExportQuestHelperUi(WzRoot ui, string outDir)
    {
        var files = new[]
        {
            ("Quest.img/Helper/canvas:topClosed", "qhelper_top.png"),
            ("Quest.img/Helper/canvas:topOpened", "qhelper_topopen.png"),
            ("Quest.img/Helper/canvas:mid", "qhelper_mid.png"),
            ("Quest.img/Helper/canvas:bottom", "qhelper_bottom.png"),
        };
        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;

        // the count of the quests in the list
        for (int i = 0; i < 10; i++)
            if (!UiPng.Save(ui, $"Quest.img/Helper/qNum/{i}", Path.Combine(outDir, "ui", $"qhelper_num_{i}.png")))
                return false;

        // the chevron that opens the list and the X that closes the bar
        foreach (var (node, name) in new[]
        {
            ("button:openList", "qhelper_open"),
            ("button:closeList", "qhelper_closelist"),
            ("button:close", "qhelper_close"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"Quest.img/Helper/{node}/{state}/0", Path.Combine(outDir, "ui", $"{name}_{suffix}.png")))
                    return false;

        Console.WriteLine("quest helper ui: bar, 10 digits, 3 buttons");
        return true;
    }
}
