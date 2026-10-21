using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Writes assets/ from the local MapleStory client data:
//   assets/maps/{id}.json + tiles/back/obj png files
//   assets/chara/body/{action}.json + part png files
//   assets/ui/*.png (login/server/chars/job/setup art, normal states only)
// Usage: export [DataDir] [OutDir]
public static partial class Export
{
    // Dev helper: bake one action with a chosen hair into outDir (hair picker).
    internal static bool BakeCharacter(WzRoot baseRoot, WzRoot chara, string outDir, string action, string hairImg) =>
        ExportCharacter(baseRoot, chara, outDir, action, hair: hairImg);

    // One panel's art export: the workers use it to get their art fast,
    // without the full export.
    public static bool RunPanel(string name, string outDir)
    {
        var ui = WzRoot.LoadCategory(ExporterPaths.DataDir, "UI");
        return name switch
        {
            "inventory" => ExportInventoryUi(ui, outDir),
            "stat" => ExportStatUi(ui, outDir),
            "skill" => ExportSkillUi(ui, outDir),
            "quest" => ExportQuestUi(ui, outDir),
            "map" => ExportMapUi(ui, WzRoot.LoadCategory(ExporterPaths.DataDir, "Map"), WzRoot.LoadCategory(ExporterPaths.DataDir, "String"), outDir),
            "keys" => ExportKeyUi(ui, outDir),
            "menu" => ExportMenuUi(ui, outDir),
            "quickmenu" => ExportQuickMenuUi(ui, outDir),
            "chat" => ExportChatUi(ui, outDir),
            "shop" => ExportShopUi(ui, outDir),
            "buff" => ExportBuffUi(ui, outDir),
            "hotkey" => ExportHotkeyUi(ui, outDir),
            "guide" => ExportGuideUi(ui, outDir),
            _ => false,
        };
    }

    public static void Run(string dataDir, string outDir)
    {
        var baseRoot = WzRoot.LoadBase(dataDir);
        var saved = new HashSet<string>(); // png names are global: one set for all maps
        var maps = new Dictionary<int, MapData>();
        foreach (int mapId in MapIds)
        {
            var map = ExportMap(baseRoot, mapId, outDir, saved);
            if (map == null)
            {
                Environment.ExitCode = 1;
                return;
            }
            maps[mapId] = map;
        }
        if (!VerifyLinks(maps))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!SoundExport.Run(baseRoot, maps, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        foreach (var map in maps.Values)
            WriteJson(Path.Combine(outDir, "maps", $"{map.MapId}.json"), map);
        var str = WzRoot.LoadCategory(dataDir, "String");
        var npc = WzRoot.LoadCategory(dataDir, "Npc");
        var npcIds = maps.Values.SelectMany(m => m.Npcs).Select(n => n.NpcId).Distinct().OrderBy(id => id);
        foreach (int npcId in npcIds)
        {
            if (!ExportNpc(npc, str, npcId, outDir, saved))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        var chara = WzRoot.LoadCategory(dataDir, "Character");
        foreach (string action in Net.Actions)
        {
            if (!ExportCharacter(baseRoot, chara, outDir, action))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        foreach ((string gear, string beginner) in CharaGears)
        {
            foreach (string action in Net.Actions)
            {
                if (!ExportCharacter(baseRoot, chara, outDir, action, gear, beginner))
                {
                    Environment.ExitCode = 1;
                    return;
                }
            }
        }
        var mob = WzRoot.LoadCategory(dataDir, "Mob");
        foreach (int mobId in Mobs.Keys.OrderBy(id => id))
        {
            if (!ExportMob(mob, str, mobId, outDir, saved))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        var effect = WzRoot.LoadCategory(dataDir, "Effect");
        if (!ExportDamageSkin(effect, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        var item = WzRoot.LoadCategory(dataDir, "Item");
        if (!ExportPotions(item, str, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!ExportEquips(chara, str, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        // Quests run after items: reward validation reads the exported defs.
        if (!ExportQuests(baseRoot, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        var skill = WzRoot.LoadCategory(dataDir, "Skill");
        if (!ExportSkills(skill, str, outDir, saved))
        {
            Environment.ExitCode = 1;
            return;
        }
        var ui = WzRoot.LoadCategory(dataDir, "UI");
        foreach ((string node, string file) in UiFiles)
        {
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
            {
                Environment.ExitCode = 1;
                return;
            }
        }
        if (!ExportJobs(ui, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!SpineBake.BakeAll(ui, outDir)) // missing ClassSelect aborts, like the other steps
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!ExportUiFrames(ui, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!ExportNameTag(ui, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!ExportHud(ui, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        // The game panels: one export step per panel.
        if (!ExportInventoryUi(ui, outDir) || !ExportStatUi(ui, outDir) || !ExportSkillUi(ui, outDir)
            || !ExportQuestUi(ui, outDir) || !ExportMapUi(ui, baseRoot, str, outDir) || !ExportKeyUi(ui, outDir)
            || !ExportMenuUi(ui, outDir) || !ExportQuickMenuUi(ui, outDir) || !ExportChatUi(ui, outDir)
            || !ExportShopUi(ui, outDir) || !ExportBuffUi(ui, outDir) || !ExportHotkeyUi(ui, outDir)
            || !ExportGuideUi(ui, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!ExportCursor(ui, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        WriteJson(Path.Combine(outDir, "manifest.json"), new ManifestData
        {
            Potions = PotionIds.OrderBy(x => x).ToList(),
            Equips = EquipGearPaths.Select(g => int.Parse(ImgId(g))).OrderBy(x => x).ToList(),
            Skills = Skills.Keys.OrderBy(x => x).ToList(),
        });
        // Tracked art/fonts (official free Nexon fonts, see art/fonts/README.md)
        // ride into every export: the game reads only assets/fonts/.
        Directory.CreateDirectory(Path.Combine(outDir, "fonts"));
        foreach (string f in Directory.GetFiles(Path.Combine("art", "fonts"), "*.ttf"))
            File.Copy(f, Path.Combine(outDir, "fonts", Path.GetFileName(f)), true);
        Console.WriteLine("export done -> " + Path.GetFullPath(outDir));
    }
}
