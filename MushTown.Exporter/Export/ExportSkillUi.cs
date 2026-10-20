using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The skill window art (UIWindow2.img/Skill/main). The file names must match
// the client (MushTown.Client/Panels/HudSkill.cs).
public static partial class Export
{
    public static bool ExportSkillUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            ("UIWindow2.img/Skill/main/backgrnd", "skill_bg.png"),      // the window frame (SKILL title)
            ("UIWindow2.img/Skill/main/backgrnd2", "skill_panel.png"),  // the light body panel
            ("UIWindow2.img/Skill/main/backgrnd3", "skill_bar.png"),    // the blue bar: the picked skill
            ("UIWindow2.img/Skill/main/skill0", "skill_row.png"),       // a skill row
            ("UIWindow2.img/Skill/main/skill1", "skill_row_sel.png"),   // the picked skill row
            ("UIWindow2.img/Skill/main/skillPoint", "skill_sp.png"),    // SKILL POINT + the value box
            ("UIWindow2.img/Skill/main/BtSpUp/normal/0", "skill_spup_n.png"),
            ("UIWindow2.img/Skill/main/BtSpUp/mouseOver/0", "skill_spup_h.png"),
            ("UIWindow2.img/Skill/main/BtSpUp/pressed/0", "skill_spup_p.png"),
            ("UIWindow2.img/Skill/main/BtSpUp/disabled/0", "skill_spup_d.png"),
        };
        // The job tabs: Roman numerals I, II, III (Warrior, Fighter, Crusader).
        // The enabled art is the gray tab, the selected art the orange one.
        for (int i = 1; i <= 3; i++)
        {
            files.Add(($"UIWindow2.img/Skill/main/Tab/enabled/{i}", $"skill_tab{i}.png"));
            files.Add(($"UIWindow2.img/Skill/main/Tab/selected/{i}", $"skill_tabsel{i}.png"));
        }
        // The skill info box: the official skill tooltip frame (nine pieces:
        // four corners, four edges and the center).
        foreach (string part in new[] { "nw", "ne", "sw", "se", "n", "s", "w", "e", "c" })
            files.Add(($"UIToolTip.img/Skill/Frame/{part}", $"skill_tip_{part}.png"));
        foreach (var (node, file) in files)
        {
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        }
        Console.WriteLine($"skill ui: {files.Count} files");
        return true;
    }
}
