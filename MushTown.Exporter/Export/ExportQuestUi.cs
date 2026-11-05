using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The quest window art (UI/Quest.img/Main): the list panel (left), the
// detail panel (right), the group bars, the empty/selected layers, the
// close buttons, the detail section bars, the reward icons and the three
// wide buttons. The file names must match the client
// (MushTown.Client/Panels/HudQuest.cs, HudQuestParts.cs).
public static partial class Export
{
    public static bool ExportQuestUi(WzRoot ui, string outDir)
    {
        var files = new List<(string Node, string File)>
        {
            ("Quest.img/Main/questList/backgrnd", "quest_list.png"),
            ("Quest.img/Main/questInfo/backgrnd", "quest_info.png"),
            ("Quest.img/Main/questInfo/layer:empty", "quest_empty.png"),
            ("Quest.img/Main/questInfo/layer:selected", "quest_selected.png"),
            ("Quest.img/Main/questList/bottom/selected", "quest_row_sel.png"),
            ("Quest.img/Main/questInfo/summary/canvas:detail", "quest_sec_info.png"),
            ("Quest.img/Main/questInfo/summary/canvas:target", "quest_sec_obj.png"),
            ("Quest.img/Main/questInfo/summary/box/layer:boxS", "quest_box.png"),
            ("Quest.img/Main/questInfo/summary/reward/layer:reward", "quest_reward.png"),
            ("Quest.img/Main/questInfo/summary/reward/icon/exp", "quest_exp.png"),
            ("Quest.img/Main/questInfo/summary/reward/icon/meso", "quest_meso.png"),
            ("Quest.img/Main/questInfo/title/layer:npcShadow", "quest_npc_shadow.png"),
            ("Quest.img/Main/questInfo/helper/layer:helper", "qhelper_add.png"),
            ("Quest.img/Main/questInfo/helper/check", "qhelper_check.png"),
        };

        // The three group bars: 0 = Available, 1 = In Progress, 2 = Completed.
        // The title and the chevron are baked; opened/closed picks the arrow.
        for (int i = 0; i < 3; i++)
            foreach (var (node, state) in new[] { ("opened", "open"), ("closed", "close") })
                foreach (var (art, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                    files.Add(($"Quest.img/Main/questList/bottom/state/{i}/{node}/{art}/0", $"quest_grp{i}_{state}_{suffix}.png"));

        foreach (var (art, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
        {
            files.Add(($"Quest.img/Main/questList/button:close/{art}/0", $"quest_close_{suffix}.png"));
            files.Add(($"Quest.img/Main/questInfo/button:close/{art}/0", $"quest_x_{suffix}.png"));
        }

        // The wide blue buttons of the detail panel (Start / Complete / Forfeit).
        foreach (var (button, stem) in new[] { ("startQuest", "quest_start"), ("endQuest", "quest_end"), ("giveUp", "quest_giveup") })
            foreach (var (art, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                files.Add(($"Quest.img/Main/questInfo/button:{button}/{art}/0", $"{stem}_{suffix}.png"));

        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        if (!ExportSysPop(ui, outDir)) return false; // the teleport/forfeit confirmation box
        if (!ExportQuestHelperUi(ui, outDir)) return false;
        Console.WriteLine($"quest ui: {files.Count} files");
        return true;
    }

    // The quest helper bar (UI/Quest.img/Helper): the bar on the screen,
    // the count digits and the buttons. The file names must match the
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

        // the X of one quest row
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, $"Quest.img/Helper/questTitle/button:close/{state}/0", Path.Combine(outDir, "ui", $"qhelper_row_close_{suffix}.png")))
                return false;

        Console.WriteLine("quest helper ui: bar, 10 digits, 4 buttons");
        return true;
    }
}
