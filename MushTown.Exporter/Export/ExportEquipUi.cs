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
            ("UIEquip.img/Equip/EquipTab/canvas:disabled", "equip_disabled.png"), // the red lock over a blocked Bottom box
            // The part-name art on every empty slot (42x42, same cell art).
            ("UIEquip.img/Equip/EquipTab/SlotName/1", "equip_slot_hat.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/2", "equip_slot_face_acc.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/3", "equip_slot_eye_acc.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/4", "equip_slot_earrings.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/5", "equip_slot_top.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/6", "equip_slot_bottom.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/7", "equip_slot_shoes.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/8", "equip_slot_gloves.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/9", "equip_slot_cape.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/10", "equip_slot_secondary.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/11", "equip_slot_weapon.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/12", "equip_slot_ring.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/17", "equip_slot_pendant.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/21", "equip_slot_medal.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/22", "equip_slot_belt.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/23", "equip_slot_shoulder.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/26", "equip_slot_pocket.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/27", "equip_slot_android.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/28", "equip_slot_heart.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/29", "equip_slot_badge.png"),
            ("UIEquip.img/Equip/EquipTab/SlotName/30", "equip_slot_emblem.png"),
            ("UIEquip.img/Deco/CoordiTab/SlotName/hair", "equip_slot_hair.png"),
            ("UIEquip.img/Deco/CoordiTab/SlotName/face", "equip_slot_face.png"),
            ("UIEquip.img/Deco/CoordiTab/SlotName/skin", "equip_slot_skin.png"),
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
