using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The equipment window art (UIEquip.img): the two 366x444 frames, the
// Equipment / Pet tabs, the Decoration tabs, the five page canvases and the
// three buttons. The file names match MushTown.Client/Panels/HudEquip.cs.
public static partial class Export
{
    public static bool ExportEquipUi(WzRoot ui, string outDir)
    {
        var files = new List<(string node, string file)>
        {
            ("UIEquip.img/Equip/main/backgrnd", "equip_bg.png"),
            ("UIEquip.img/Deco/main/backgrnd", "deco_bg.png"),
            ("UIEquip.img/Equip/EquipTab/canvas:equip", "equip_panel.png"),
            ("UIEquip.img/Equip/PetTab/canvas:pet", "equip_pet.png"),
            ("UIEquip.img/Deco/CoordiTab/canvas:coordi", "deco_panel.png"),
            ("UIEquip.img/Deco/AndroidTab/canvas:and", "deco_android.png"),
            ("UIEquip.img/Deco/DamageSkinTab/canvas:and", "deco_damage.png"),
            ("UIEquip.img/Equip/EquipTab/button:DecoUI/normal/0", "equip_deco.png"),
            ("UIEquip.img/Deco/CoordiTab/button:EquipUI/normal/0", "deco_equip.png"),
            ("UIEquip.img/Equip/main/button:close/normal/0", "equip_close.png"),
        };
        for (int t = 0; t < 2; t++)
        {
            files.Add(($"UIEquip.img/Equip/main/tab:detailTab/normal/{t}", $"equip_tab_n{t}.png"));
            files.Add(($"UIEquip.img/Equip/main/tab:detailTab/selected/{t}", $"equip_tab_s{t}.png"));
        }
        for (int t = 0; t < 3; t++)
        {
            files.Add(($"UIEquip.img/Deco/main/tab:detailTab/normal/{t}", $"deco_tab_n{t}.png"));
            files.Add(($"UIEquip.img/Deco/main/tab:detailTab/selected/{t}", $"deco_tab_s{t}.png"));
        }
        foreach ((string node, string file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        Console.WriteLine($"equipment: {files.Count} files");
        return true;
    }
}
