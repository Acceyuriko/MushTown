using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The skill window art (UIWindow2.img/Skill/main). The file names must match
// the client (MushTown.Client/Panels/HudSkill.cs).
public static partial class Export
{
    public static bool ExportSkillUi(WzRoot ui, WzRoot skill, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            ("UIWindow2.img/Skill/main/backgrnd", "skill_bg.png"),      // the window frame (SKILL title)
            ("UIWindow2.img/Skill/main/backgrnd2", "skill_panel.png"),  // the light body panel
            ("UIWindow2.img/Skill/main/backgrnd3", "skill_bar.png"),    // the blue bar: the name of the page
            ("UIWindow2.img/Skill/main/skill0", "skill_row.png"),       // a skill cell
            ("UIWindow2.img/Skill/main/skillBlank", "skill_blank.png"), // an empty cell
            ("UIWindow2.img/Skill/main/BtMacro/normal/0", "skill_bt_macro_n.png"),
            ("UIWindow2.img/Skill/main/BtMacro/mouseOver/0", "skill_bt_macro_h.png"),
            ("UIWindow2.img/Skill/main/BtMacro/pressed/0", "skill_bt_macro_p.png"),
            // The bottom buttons that do not work yet: the gray art only.
            ("UIWindow2.img/Skill/main/BtHyper/disabled/0", "skill_bt_hyper_d.png"),
            ("UIWindow2.img/Skill/main/BtGuildSkill/disabled/0", "skill_bt_guild_d.png"),
            ("UIWindow2.img/Skill/main/BtCooltimeEndAlarm/disabled/0", "skill_bt_alarm_d.png"),
            ("UIWindow2.img/Skill/main/BtSequence/disabled/0", "skill_bt_sequence_d.png"),
            // The skill tooltip: the dotted line and the icon frame.
            ("UIToolTip.img/Skill/Frame/dotline_w", "skill_tip_dotw.png"),
            ("UIToolTip.img/Skill/Frame/dotline_c", "skill_tip_dotc.png"),
            ("UIToolTip.img/Skill/Frame/dotline_e", "skill_tip_dote.png"),
            ("UIToolTip.img/Skill/Frame/iconBackgrnd", "skill_tip_iconbg.png"),
        };
        // The tabs: the beginner tab (0), then the Roman numerals I to V. The enabled art
        // is the gray tab, the selected art the orange one. VI (6) is not lit yet: only its
        // disabled art (the lock) is used.
        for (int i = 0; i <= 5; i++)
        {
            files.Add(($"UIWindow2.img/Skill/main/Tab/enabled/{i}", $"skill_tab{i}.png"));
            files.Add(($"UIWindow2.img/Skill/main/Tab/selected/{i}", $"skill_tabsel{i}.png"));
        }
        files.Add(("UIWindow2.img/Skill/main/Tab/disabled/6", "skill_tabdis6.png"));
        // The skill info box: the official skill tooltip frame (nine pieces:
        // four corners, four edges and the center).
        foreach (string part in new[] { "nw", "ne", "sw", "se", "n", "s", "w", "e", "c" })
            files.Add(($"UIToolTip.img/Skill/Frame/{part}", $"skill_tip_{part}.png"));
        foreach (var (node, file) in files)
        {
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        }
        // The book of each page, drawn in the blue bar: the beginner page (000) and the three jobs.
        foreach (string book in new[] { "000", "100", "110", "111" })
            if (!UiPng.Save(skill, "Skill", $"_Canvas/{book}.img/info/icon", Path.Combine(outDir, "ui", $"skill_book{book}.png")))
                return false;
        Console.WriteLine($"skill ui: {files.Count + 4} files");
        return true;
    }
}
