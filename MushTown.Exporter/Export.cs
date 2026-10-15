using System.Text.Json;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Writes assets/ from the local MapleStory client data:
//   assets/maps/{id}.json + tiles/back/obj png files
//   assets/chara/body/{action}.json + part png files
//   assets/ui/*.png (login/server/chars/job/setup art, normal states only)
// Usage: export [DataDir] [OutDir]
public static class Export
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // W6 exports the Maple Island chain. Every map must resolve here: WZ dir
    // rule is not id/1e8, so FindMapImg searches Map0..Map9.
    private static readonly int[] MapIds = new[] { 10000, 20000, 1000000, 1010000, 1020000, 2000000 };

    // Mob table: our life mobs have no own art img, so each borrows its
    // same-named twin (String names verified identical). Stats are v1 feel
    // numbers: the modern client keeps them server-side, no maxHP node exists
    // anywhere in the data (verified in W8 recon). EXP pays for levels 1-10
    // Boss pin: the WZ carries no boss life, so v1 pins Mushmom to map
    // 2000000 (main ground y=527 runs -212..1440; the pin stands east of the
    // NPCs). Its attack1 art ships but never plays: v1 bosses touch like
    // every other mob.
    private const int BossMushmom = 6130101;
    private const int BossMap = 2000000, BossX = 1300, BossY = 527;

    // (W10 curve: 1035 total, the 23 mobs pay 1225).
    // Touch/speed/aggro (W13 feel): touch is flat contact damage, speed is
    // walk px/s (all slower than the player's 140, so everything outruns),
    // aggro is chase range in x px. No attack actions play in v1 (the four
    // twins carry none; Mushmom ships one but contact is the attack).
    private static readonly Dictionary<int, (string art, int hp, double def, int exp, int touch, int speed, int aggro)> Mobs = new()
    {
        [100000] = ("0100100", 40, 0, 35, 4, 40, 180),      // Snail
        [100001] = ("0100101", 55, 0, 50, 6, 45, 220),      // Blue Snail
        [100003] = ("0120100", 70, 0, 65, 8, 35, 200),      // Spore (twin 120100)
        [100004] = ("1210102", 110, 0.05, 95, 12, 55, 280), // Orange Mushroom (twin 1210102)
        [BossMushmom] = ("6130101", 3000, 0.5, 300, 30, 50, 600), // Mushmom: duo Lv10s chew ~17s through the DEF
    };

    // Skill table: v1 feel numbers (SkillData documents the column rules).
    // The modern client keeps skill stats server-side: no level/mpCon/damage
    // nodes exist anywhere in the data (verified in W11 recon). Art cut for
    // v1 (documented, all loud if misread): effect0 second acts, hit variant
    // 1+, Flash charge UI (special/number), Combo counter art (state/special),
    // Beam shootobj, Scarring mob mark. War Leap plays effect variant 0.
    private static readonly Dictionary<int, SkillData> Skills = new()
    {
        // 100 Warrior.
        [SkillData.SlashBlast] = new() { ReqLevel = 1, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 5, CooldownMs = 3000, DmgLo = 150, DmgHi = 250, MobCount = 6, Range = 160, CastLockMs = 600 },
        [SkillData.WarLeap] = new() { ReqLevel = 3, MaxLevel = 1, Kind = SkillData.KindActive, Mp = 3, CooldownMs = 5000 },
        [SkillData.LeapAttack] = new() { ReqLevel = 5, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 8, CooldownMs = 5000, DmgLo = 150, DmgHi = 250, MobCount = 6, Range = 160, CastLockMs = 600 },
        [SkillData.IronBody] = new() { ReqLevel = 2, MaxLevel = 10, Kind = SkillData.KindBuff, Mp = 10, CooldownMs = 10000, BuffMs = 120000, HpPct = 3, DefPct = 2 },
        [SkillData.WarriorMastery] = new() { ReqLevel = 4, MaxLevel = 10, Kind = SkillData.KindPassive, HpPct = 2, MovePct = 1 },
        [SkillData.GuardianArmor] = new() { ReqLevel = 6, MaxLevel = 10, Kind = SkillData.KindPassive, Atk = 2, DefPct = 1 },
        // 110 Fighter.
        [SkillData.Brandish] = new() { ReqLevel = 6, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 8, CooldownMs = 4000, DmgLo = 140, DmgHi = 230, MobCount = 6, Range = 170, CastLockMs = 600 },
        [SkillData.FlashBlade] = new() { ReqLevel = 8, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 10, CooldownMs = 6000, DmgLo = 130, DmgHi = 220, MobCount = 5, Range = 150, CastLockMs = 400 },
        [SkillData.ComboAttack] = new() { ReqLevel = 7, MaxLevel = 5, Kind = SkillData.KindBuff, Mp = 12, CooldownMs = 5000, BuffMs = 180000, OrbChance = 20, OrbMax = 5, OrbDmg = 4 },
        [SkillData.SpiritBlade] = new() { ReqLevel = 7, MaxLevel = 10, Kind = SkillData.KindBuff, Mp = 12, CooldownMs = 5000, BuffMs = 120000, AtkPct = 2 },
        [SkillData.WeaponMastery] = new() { ReqLevel = 6, MaxLevel = 10, Kind = SkillData.KindPassive, DmgPct = 2, CritPct = 1 },
        [SkillData.AgileArms] = new() { ReqLevel = 8, MaxLevel = 10, Kind = SkillData.KindPassive, Str = 2 },
        [SkillData.FinalAttack] = new() { ReqLevel = 8, MaxLevel = 5, Kind = SkillData.KindPassive, ProcPct = 12 },
        [SkillData.PhysicalTraining] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindPassive, Str = 2, Dex = 1 },
        // 111 Crusader.
        [SkillData.IntrepidSlash] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 12, CooldownMs = 5000, DmgLo = 200, DmgHi = 350, MobCount = 6, Range = 180, CastLockMs = 700 },
        [SkillData.BeamBlade] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 10, CooldownMs = 4000, DmgLo = 160, DmgHi = 260, MobCount = 4, Range = 200, CastLockMs = 600 },
        [SkillData.Rush] = new() { ReqLevel = 9, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 10, CooldownMs = 6000, DmgLo = 120, DmgHi = 200, MobCount = 6, Range = 150, CastLockMs = 400 },
        [SkillData.ScarringSword] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindActive, Mp = 12, CooldownMs = 5000, DmgLo = 180, DmgHi = 300, MobCount = 4, Range = 170, CastLockMs = 600 },
        [SkillData.ComboSynergy] = new() { ReqLevel = 9, MaxLevel = 5, Kind = SkillData.KindPassive, SynDmg = 2, SynChance = 6 },
        [SkillData.SelfRecovery] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindPassive, RegenHp = 5, RegenMp = 3 },
        [SkillData.ChanceAttack] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindPassive, CritPct = 2 },
        [SkillData.Endure] = new() { ReqLevel = 10, MaxLevel = 10, Kind = SkillData.KindPassive, HpPct = 3, DefPct = 2 },
    };

    // Saves lobby art: (node path, file name). Char-select buttons carry
    // real 3-state art (_n/_h/_p); job/setup buttons keep one normal PNG
    // with a client hover overlay.
    // Must match Menu.cs UiFiles: every file there must be listed here.
    private static readonly (string node, string file)[] UiFiles = new[]
    {
        ("LoginBack.img/WorldSelect/default/0/0", "worldbg.png"),
        ("Login.img/CharSelect/selectWorld", "selectworld.png"),
        ("Login.img/ClassSelect/list/backgrnd", "listbg.png"),
        ("Login.img/ClassSelect/list/class/button:classEnabled/110/normal/0", "cell_hero.png"),
        ("Login.img/ClassSelect/button:make/normal/0", "btn_make.png"),
        ("LoginBack.img/CustomizeChar/000/0/0", "setupbg.png"),
        ("Login.img/CustomizeChar/000/BtYes/normal/0", "btn_confirm.png"),
        ("Login.img/CustomizeChar/000/BtNo/normal/0", "btn_cancel.png"),
        ("Login.img/CharSelect/BtSelect/normal/0", "cs_select_n.png"),
        ("Login.img/CharSelect/BtSelect/mouseOver/0", "cs_select_h.png"),
        ("Login.img/CharSelect/BtSelect/pressed/0", "cs_select_p.png"),
        ("Login.img/CharSelect/BtNew/normal/0", "cs_new_n.png"),
        ("Login.img/CharSelect/BtNew/mouseOver/0", "cs_new_h.png"),
        ("Login.img/CharSelect/BtNew/pressed/0", "cs_new_p.png"),
        ("Login.img/CharSelect/BtDelete/normal/0", "cs_delete_n.png"),
        ("Login.img/CharSelect/BtDelete/mouseOver/0", "cs_delete_h.png"),
        ("Login.img/CharSelect/BtDelete/pressed/0", "cs_delete_p.png"),
        ("Login.img/CharSelect/BtDelete/sprite/0/0", "cs_delglass.png"),
        ("Login.img/Common/BtPreview/normal/0", "cs_back_n.png"),
        ("Login.img/Common/BtPreview/mouseOver/0", "cs_back_h.png"),
        ("Login.img/Common/BtPreview/pressed/0", "cs_back_p.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/normal/0", "cs_delyes_n.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/mouseOver/0", "cs_delyes_h.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/pressed/0", "cs_delyes_p.png"),
        ("Login.img/CharSelect/BtDeleteCancel/normal/0", "cs_delno_n.png"),
        ("Login.img/CharSelect/BtDeleteCancel/mouseOver/0", "cs_delno_h.png"),
        ("Login.img/CharSelect/BtDeleteCancel/pressed/0", "cs_delno_p.png"),
        ("Login.img/CharSelect/slotJobBoard/closed/canvas:closeBackgrnd", "cs_board.png"),
        ("Login.img/CharSelect/adventure/0", "cs_pillar.png"),
        ("Login.img/CharSelect/page/backgrnd", "cs_page_bg.png"),
        ("Login.img/CharSelect/page/sprite:base/0/0", "cs_page_base.png"),
        ("Login.img/CharSelect/page/button:pageL/normal/0", "cs_page_l_n.png"),
        ("Login.img/CharSelect/page/button:pageL/mouseOver/0", "cs_page_l_h.png"),
        ("Login.img/CharSelect/page/button:pageL/pressed/0", "cs_page_l_p.png"),
        ("Login.img/CharSelect/page/button:pageR/normal/0", "cs_page_r_n.png"),
        ("Login.img/CharSelect/page/button:pageR/mouseOver/0", "cs_page_r_h.png"),
        ("Login.img/CharSelect/page/button:pageR/pressed/0", "cs_page_r_p.png"),
        ("Login.img/CharSelect/page/button:page1/normal/0", "cs_page_1_n.png"),
        ("Login.img/CharSelect/page/button:page1/mouseOver/0", "cs_page_1_h.png"),
        ("Login.img/CharSelect/page/button:page1/pressed/0", "cs_page_1_p.png"),
        ("Login.img/CharSelect/page/button:page2/normal/0", "cs_page_2_n.png"),
        ("Login.img/CharSelect/page/button:page2/mouseOver/0", "cs_page_2_h.png"),
        ("Login.img/CharSelect/page/button:page2/pressed/0", "cs_page_2_p.png"),
        ("Login.img/CharSelect/page/button:page3/normal/0", "cs_page_3_n.png"),
        ("Login.img/CharSelect/page/button:page3/mouseOver/0", "cs_page_3_h.png"),
        ("Login.img/CharSelect/page/button:page3/pressed/0", "cs_page_3_p.png"),
        ("Login.img/CharSelect/page/button:page4/normal/0", "cs_page_4_n.png"),
        ("Login.img/CharSelect/page/button:page4/mouseOver/0", "cs_page_4_h.png"),
        ("Login.img/CharSelect/page/button:page4/pressed/0", "cs_page_4_p.png"),
        ("Login.img/CharSelect/page/button:page5/normal/0", "cs_page_5_n.png"),
        ("Login.img/CharSelect/page/button:page5/mouseOver/0", "cs_page_5_h.png"),
        ("Login.img/CharSelect/page/button:page5/pressed/0", "cs_page_5_p.png"),
    };

    // Animated char-select overlays: (node path, file stem). Each numbered
    // child is one frame; ExportUiFrames writes ui/{stem}_{f}.png plus a
    // ui/{stem}.json of origins, delays, and fades.
    private static readonly (string node, string stem)[] UiFrameSets = new[]
    {
        ("Login.img/CharSelect/character/0", "cs_ghost"),
        ("Login.img/CharSelect/effect/0", "cs_portal"),
        ("Login.img/CharSelect/effect/1", "cs_motes"),
    };

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
        if (!ExportQuests(baseRoot, outDir))
        {
            Environment.ExitCode = 1;
            return;
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
        if (!ExportPotions(item, outDir))
        {
            Environment.ExitCode = 1;
            return;
        }
        if (!ExportEquips(chara, outDir))
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
        Console.WriteLine("export done -> " + Path.GetFullPath(outDir));
    }

    // Every portal with a real target must land on an exported map and a
    // portal name that exists there ( Tn matches the target's Pn ).
    private static bool VerifyLinks(Dictionary<int, MapData> maps)
    {
        foreach (var map in maps.Values)
            foreach (var p in map.Portals)
            {
                if (p.Tm == PortalRules.NoTarget) continue;
                if (!maps.TryGetValue(p.Tm, out var target))
                {
                    Console.WriteLine($"map {map.MapId} portal {p.Pn}: target map {p.Tm} not exported");
                    return false;
                }
                // arrival lands on the ground at/below the target portal (top00/top0
                // platforms sit ~1px above their portal point, so look from y - ArrivalLookUp)
                var land = target.Portals.FirstOrDefault(q => q.Pn == p.Tn);
                if (land == null)
                {
                    Console.WriteLine($"map {map.MapId} portal {p.Pn}: target portal '{p.Tn}' missing on map {p.Tm}");
                    return false;
                }
                if (SceneMath.ArrivalGround(target.Footholds, land.X, land.Y) == null)
                {
                    Console.WriteLine($"map {map.MapId} portal {p.Pn}: no ground below '{p.Tn}' on map {p.Tm}");
                    return false;
                }
            }
        return true;
    }

    // ---------- map ----------

    // img file names are zero-padded (000010000.img); the MapN dir is not
    // derivable from the id, so search all ten.
    private static Wz_Node? FindMapImg(WzRoot root, int mapId)
    {
        for (int n = 0; n <= 9; n++)
        {
            var node = root.Find($@"Map\Map\Map{n}\{mapId:D9}.img");
            if (node != null) return node;
        }
        return null;
    }

    // Portal art, same source as the simulator (MapData.cs:840): game art by
    // type name (7 looks like 2), image 0 means "default". A portalContinue
    // node holds the loop, else the frames sit directly under the node (the
    // simulator's start/exit states are skipped: the loop always shows).
    private static bool ExportPortalArt(WzRoot root, int mapId, Wz_Node p, PortalItem item, string outDir, HashSet<string> saved)
    {
        int pt = Int(p, "pt");
        string? type = pt switch { 2 => "pv", 7 => "pv", 10 => "ph", _ => null };
        if (type == null)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: live type {pt} has no art rule");
            return false;
        }
        if (Int(p, "delay") != 0 || Int(p, "onlyOnce") != 0)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: delay/onlyOnce portals are not supported");
            return false;
        }
        int image = Int(p, "image");
        string img = image == 0 ? "default" : image.ToString();
        var node = root.Find($@"Map\MapHelper.img\portal\game\{type}\{img}");
        if (node == null)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} not found");
            return false;
        }
        var frames = node.Nodes["portalContinue"] ?? node;
        for (int i = 0; ; i++)
        {
            var f = frames.Nodes[i.ToString()];
            if (f == null) break;
            var (png, origin) = WzNodeUtil.ReadCanvas(root, f);
            if (png == null)
            {
                Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} frame {i} has no png");
                return false;
            }
            string path = $"portal/{type}/{img}/{i}.png";
            SaveOnce(png, Path.Combine(outDir, path), saved);
            int delay = Int(f, "delay", 100);
            if (delay <= 0)
            {
                Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} frame {i} bad delay {delay}");
                return false;
            }
            item.Frames.Add(new PortalFrame { Image = path, Dx = -(origin?.X ?? 0), Dy = -(origin?.Y ?? 0), Delay = delay });
        }
        if (item.Frames.Count == 0)
        {
            Console.WriteLine($"map {mapId} portal {item.Pn}: portal art {type}/{img} has no frames");
            return false;
        }
        return true;
    }

    private static MapData? ExportMap(WzRoot root, int mapId, string outDir, HashSet<string> saved)
    {
        var scene = FindMapImg(root, mapId);
        if (scene == null)
        {
            Console.WriteLine($"map {mapId} not found");
            return null;
        }

        var map = new MapData { MapId = mapId };

        // info
        var info = scene.Nodes["info"];
        if (info == null)
        {
            Console.WriteLine($"map {mapId}: info node not found");
            return null;
        }
        map.Info.VrLeft = Int(info, "VRLeft");
        map.Info.VrTop = Int(info, "VRTop");
        map.Info.VrRight = Int(info, "VRRight");
        map.Info.VrBottom = Int(info, "VRBottom");
        map.Info.Bgm = Str(info, "bgm");
        if (map.Info.Bgm == "")
        {
            Console.WriteLine($"map {mapId}: info/bgm missing");
            return null;
        }

        // miniMap: the canvas plus the world rect it covers
        var mm = scene.Nodes["miniMap"];
        if (mm == null)
        {
            Console.WriteLine($"map {mapId}: miniMap node not found");
            return null;
        }
        var (mmpng, _) = WzNodeUtil.ReadCanvas(root, mm.Nodes["canvas"]);
        if (mmpng == null)
        {
            Console.WriteLine($"map {mapId}: miniMap canvas missing");
            return null;
        }
        string mmpath = $"minimap/{mapId}.png";
        SaveOnce(mmpng, Path.Combine(outDir, mmpath), saved);
        map.MiniMap.Image = mmpath;
        map.MiniMap.Width = Int(mm, "width");
        map.MiniMap.Height = Int(mm, "height");
        map.MiniMap.CenterX = Int(mm, "centerX");
        map.MiniMap.CenterY = Int(mm, "centerY");
        if (map.MiniMap.Width <= 0 || map.MiniMap.Height <= 0)
        {
            Console.WriteLine($"map {mapId}: miniMap has a bad rect");
            return null;
        }

        // spawn portal: node "0" ("sp") when it has tm == NoTarget, else the first
        // portal in file order with tm == NoTarget
        var portalNode = scene.Nodes["portal"];
        Wz_Node? spawnPortal = portalNode?.Nodes["0"];
        if (spawnPortal == null || Int(spawnPortal, "tm") != PortalRules.NoTarget)
        {
            spawnPortal = null;
            if (portalNode != null)
                foreach (Wz_Node p in portalNode.Nodes)
                    if (Int(p, "tm") == PortalRules.NoTarget) { spawnPortal = p; break; }
        }
        if (spawnPortal == null)
        {
            Console.WriteLine($"map {mapId}: no spawn portal (tm == {PortalRules.NoTarget})");
            return null;
        }
        int spawnX = Int(spawnPortal, "x"), spawnY = Int(spawnPortal, "y");
        map.Info.SpawnX = spawnX;
        map.Info.SpawnY = spawnY;

        // footholds: flatten to segments; Layer is the top-level group number
        var fh = scene.Nodes["foothold"];
        if (fh != null)
        {
            foreach (Wz_Node group in fh.Nodes)
            {
                if (!int.TryParse(group.Text, out int layer)) continue;
                foreach (Wz_Node sub in WzRoot.Walk(group))
                {
                    if (sub.Nodes["x1"] == null) continue;
                    map.Footholds.Add(new FootholdItem
                    {
                        X1 = Int(sub, "x1"),
                        Y1 = Int(sub, "y1"),
                        X2 = Int(sub, "x2"),
                        Y2 = Int(sub, "y2"),
                        Layer = layer
                    });
                }
            }
        }

        // ground under spawn: foothold covering spawnX, below the portal, closest to its y
        int? groundY = SceneMath.SpawnGroundY(map.Footholds, spawnX, spawnY);
        if (groundY == null)
        {
            Console.WriteLine($"map {mapId}: no foothold under spawn x={spawnX}");
            return null;
        }
        else
        {
            map.Info.SpawnY = groundY.Value;
            Console.WriteLine($"map {mapId}: spawn ground y={groundY.Value}");
        }

        // portals + ladders (cross-map Tn links are verified after all maps load)
        if (portalNode != null)
            foreach (Wz_Node p in portalNode.Nodes)
            {
                var item = new PortalItem
                {
                    Pn = Str(p, "pn"),
                    X = Int(p, "x"),
                    Y = Int(p, "y"),
                    Tm = Int(p, "tm"),
                    Tn = Str(p, "tn")
                };
                if (item.Tm != PortalRules.NoTarget && !ExportPortalArt(root, mapId, p, item, outDir, saved))
                    return null;
                map.Portals.Add(item);
            }
        var ladderNode = scene.Nodes["ladderRope"];
        if (ladderNode != null)
            foreach (Wz_Node l in ladderNode.Nodes)
                map.Ladders.Add(new LadderItem
                {
                    X = Int(l, "x"),
                    Y1 = Int(l, "y1"),
                    Y2 = Int(l, "y2"),
                    Ladder = Int(l, "l") != 0
                });

        // life: mob spawn points and NPC placements (hide=1 skips: quest
        // phantoms). Ids come zero-padded ("0100000"), so parse them; every
        // mob must be exported, NPC art follows after the maps.
        var lifeNode = scene.Nodes["life"];
        if (lifeNode != null)
            foreach (Wz_Node l in lifeNode.Nodes)
            {
                if (Str(l, "type") == "m")
                {
                    if (!int.TryParse(Str(l, "id"), out int mobId) || !Mobs.ContainsKey(mobId))
                    {
                        Console.WriteLine($"map {mapId}: bad or unexported life mob '{Str(l, "id")}'");
                        return null;
                    }
                    map.Life.Add(new LifeItem { MobId = mobId, X = Int(l, "x"), Cy = Int(l, "cy") });
                }
                else if (Str(l, "type") == "n")
                {
                    if (Int(l, "hide") != 0) continue;
                    if (!int.TryParse(Str(l, "id"), out int npcId))
                    {
                        Console.WriteLine($"map {mapId}: bad life npc '{Str(l, "id")}'");
                        return null;
                    }
                    map.Npcs.Add(new NpcSpawn { NpcId = npcId, X = Int(l, "x"), Cy = Int(l, "cy"), FacingRight = Int(l, "f") != 0 });
                }
            }

        int missing = 0;     // an image we should have read but could not: export fails
        int unsupported = 0; // Spine/ani node: has no png by design, only printed

        // back
        var back = scene.Nodes["back"];
        if (back != null)
        {
            foreach (Wz_Node b in back.Nodes)
            {
                string no = Str(b, "no");
                string bS = Str(b, "bS");
                // ani backs keep frames under ani\{no}; like objs, W6 freezes frame 0
                // (the back\{no} node is a 1x1 placeholder, not a frame)
                string path = Int(b, "ani") != 0
                    ? $@"Map\Back\{bS}.img\ani\{no}\0"
                    : $@"Map\Back\{bS}.img\back\{no}";
                var (png, origin) = WzNodeUtil.ReadCanvas(root, root.Find(path));
                if (png == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }
                // png paths mirror the WZ tree (back/{set}/{no}), so one file per
                // source node and no clashes between sets sharing a number
                string img = $"back/{bS}/{no}.png";
                SaveOnce(png, Path.Combine(outDir, img), saved);
                int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                bool flip = Int(b, "f") != 0;
                map.Backs.Add(new BackItem
                {
                    // flipping mirrors the image inside its rectangle, so the anchor
                    // moves to the other edge of it (MeshBatcher.cs:123)
                    X = Int(b, "x") - (flip ? png.Width - ox : ox),
                    Y = Int(b, "y") - oy,
                    Image = img,
                    Front = Int(b, "front") != 0,
                    Flip = flip,
                    Type = Int(b, "type"),
                    Cx = Int(b, "cx"),
                    Cy = Int(b, "cy"),
                    Rx = Int(b, "rx"),
                    Ry = Int(b, "ry")
                });
            }
        }

        // layers: tiles + objs
        foreach (Wz_Node layer in scene.Nodes)
        {
            if (!int.TryParse(layer.Text, out int layerNo)) continue;

            string tS = Str(layer.Nodes["info"], "tS");

            var tileNode = layer.Nodes["tile"];
            if (tileNode != null && tS.Length > 0)
            {
                foreach (Wz_Node t in tileNode.Nodes)
                {
                    int x = Int(t, "x"), y = Int(t, "y");
                    string u = Str(t, "u"), no = Str(t, "no");
                    string path = $@"Map\Tile\{tS}.img\{u}\{no}";
                    var node = root.Find(path);
                    var (png, origin) = WzNodeUtil.ReadCanvas(root, node);
                    if (png == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }
                    string img = $"tiles/{tS}/{u}/{no}.png";
                    SaveOnce(png, Path.Combine(outDir, img), saved);
                    int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                    map.Tiles.Add(new TileItem
                    {
                        X = x - ox,
                        Y = y - oy,
                        Image = img,
                        Layer = layerNo,
                        Z = Int(WzNodeUtil.ResolveUol(node), "z")
                    });
                }
            }
            else if (tileNode != null && tileNode.Nodes.Count > 0)
            {
                missing++;
                Console.WriteLine($"  missing layer {layerNo}: {tileNode.Nodes.Count} tile(s) but no info/tS");
            }

            var objNode = layer.Nodes["obj"];
            if (objNode != null)
            {
                foreach (Wz_Node o in objNode.Nodes)
                {
                    string oS = Str(o, "oS"), l0 = Str(o, "l0"), l1 = Str(o, "l1"), l2 = Str(o, "l2");
                    string path = $@"Map\Obj\{oS}.img\{l0}\{l1}\{l2}";
                    var frame = WzNodeUtil.ResolveUol(root.Find(path));
                    if (frame == null) { missing++; Console.WriteLine($"  missing {path}"); continue; }

                    // animated objs keep frames as children; static ones are the png itself
                    var (png, origin) = WzNodeUtil.ReadCanvas(root, frame);
                    if (png == null)
                    {
                        var first = frame.Nodes["0"];
                        if (first == null)
                        {
                            unsupported++;
                            Console.WriteLine($"  unsupported {path} (spine/ani, no frame 0)");
                            continue;
                        }
                        (png, origin) = WzNodeUtil.ReadCanvas(root, first);
                        if (png == null) { missing++; Console.WriteLine($"  missing {path} (frame 0 has no png)"); continue; }
                    }

                    string img = $"obj/{oS}/{l0}/{l1}/{l2}.png";
                    SaveOnce(png, Path.Combine(outDir, img), saved);
                    int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
                    bool flip = Int(o, "f") != 0;
                    map.Objs.Add(new ObjItem
                    {
                        X = Int(o, "x") - (flip ? png.Width - ox : ox),
                        Y = Int(o, "y") - oy,
                        Image = img,
                        Flip = flip,
                        Layer = layerNo,
                        Z = Int(o, "z")
                    });
                }
            }
        }

        Console.WriteLine($"map {mapId}: {map.Backs.Count} back, {map.Tiles.Count} tile, " +
                          $"{map.Objs.Count} obj, {map.Footholds.Count} foothold, " +
                          $"{map.Portals.Count} portal, {map.Ladders.Count} ladder, " +
                          $"spawn ({spawnX},{map.Info.SpawnY})");
        if (unsupported > 0) Console.WriteLine($"  unsupported {unsupported} map item(s) (spine/ani, no image)");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {missing} map item(s)");
            return null;
        }
        // Boss pin (see BossMushmom): the pin must stand on a foothold.
        if (mapId == BossMap)
        {
            var ground = SceneMath.GroundBelow(map.Footholds, BossX, BossY);
            if (ground == null || SceneMath.FootholdY(ground, BossX) != BossY)
            {
                Console.WriteLine($"map {mapId}: boss pin ({BossX},{BossY}) has no ground");
                return null;
            }
            map.Life.Add(new LifeItem { MobId = BossMushmom, X = BossX, Cy = BossY });
            Console.WriteLine($"map {mapId}: boss {BossMushmom} pinned at ({BossX},{BossY})");
        }
        return map;
    }

    // ---------- character ----------

    private const string BodyImg = @"00002000.img";
    private const string HeadImg = @"00012000.img";
    private const string HairImg = @"Hair\00030000.img";
    private const string FaceImg = @"Face\00020000.img";

    // Beginner gear on top of the naked body. Body, head and hair must carry
    // every action; gear without the art falls back to its stand2 frame (face
    // stays special: one default emotion node shared by every frame).
    private static readonly string[] CharaEquips = new[]
    {
        @"Cap\01000000.img",
        @"Coat\01040036.img",
        @"Pants\01060026.img",
        @"Shoes\01070001.img",
        @"Glove\01080001.img",
        @"Cape\01100000.img",
        @"Weapon\01442000.img",
    };

    // Wearable gear: img path + the beginner img it swaps out of the doll
    // merge (swap position = the beginner img's slot in CharaEquips).
    private static readonly (string gear, string beginner)[] CharaGears = new[]
    {
        (@"Coat\01040039.img", @"Coat\01040036.img"),
        (@"Weapon\01442004.img", @"Weapon\01442000.img"),
    };

    // Def + icon per wearable: art-less rings ride along (no doll pixels).
    private static readonly string[] EquipGearPaths = new[]
    {
        @"Coat\01040039.img",
        @"Weapon\01442004.img",
        @"Ring\01112134.img",
        @"Ring\01112135.img",
        @"Ring\01112162.img",
    };

    // Name tag plates, one per shipped ring (the ring's info/nameTag names
    // its node): 58 bamboo, 59 ink-and-wash, 129 island travel.
    private static readonly int[] NameTagNodes = new[] { 58, 59, 129 };
    // img/action/frame with a uol resolved at both levels (AvatarCanvas.cs:1203-1226)
    private static Wz_Node? FindImgFrame(WzRoot looseRoot, string img, string action, int frameIdx)
    {
        var actionNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{img}\{action}"));
        return WzNodeUtil.ResolveUol(actionNode?.Nodes[frameIdx.ToString()]);
    }

    private static string ImgId(string img) => Path.GetFileNameWithoutExtension(img);

    private static bool ExportCharacter(WzRoot baseRoot, WzRoot looseRoot, string outDir, string action,
        string? gear = null, string? gearBeginner = null)
    {
        // One gear's parts across one action: same frames, same bones, only
        // the target img's parts kept (prefix beginners feed the trash frame
        // for bones; identical bytes may rewrite beginner pngs, harmless).
        // Isolated parts equal the merged bake: bones only flow forward, so
        // the target sees exactly base + preceding beginners + its own parts.
        string tag = gear == null ? $"character {action}" : $"gear {ImgId(gear)} {action}";
        // loose tree (Character.wz) holds origin/map/z; png bytes live in Base _Canvas
        List<string> zNames = new();
        var zmap = baseRoot.Find("zmap.img");
        if (zmap == null || zmap.Nodes.Count == 0)
        {
            Console.WriteLine($@"{tag}: Base\zmap.img missing");
            return false;
        }
        foreach (Wz_Node z in zmap.Nodes) zNames.Add(z.Text);

        var saved = new HashSet<string>();
        var data = new CharaData();
        int missing = 0;

        // the face emotion is one node shared by all frames, not per-frame data
        var faceNode = WzNodeUtil.ResolveUol(looseRoot.Find($@"{FaceImg}\default"));
        if (faceNode == null)
        {
            Console.WriteLine($"{tag}: {FaceImg} node not found");
            return false;
        }

        // frames run 0,1,2... and stop at the first gap (AvatarCanvas.cs:511-522)
        for (int frameIdx = 0; ; frameIdx++)
        {
            var bodyFrame = FindImgFrame(looseRoot, BodyImg, action, frameIdx);
            if (bodyFrame == null)
            {
                // a real gap ends the action; a dangling frame uol is loud
                var bodyAction = WzNodeUtil.ResolveUol(looseRoot.Find($@"{BodyImg}\{action}"));
                if (bodyAction?.Nodes[frameIdx.ToString()] != null)
                {
                    Console.WriteLine($"{tag} frame {frameIdx}: {BodyImg} frame uol is broken");
                    return false;
                }
                break;
            }
            if (!BodyFrameStatic(action, frameIdx, bodyFrame)) return false;

            var headFrame = FindImgFrame(looseRoot, HeadImg, action, frameIdx);
            if (headFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {HeadImg} node not found");
                return false;
            }
            var hairFrame = FindImgFrame(looseRoot, HairImg, action, frameIdx);
            if (hairFrame == null)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {HairImg} node not found");
                return false;
            }

            var outFrame = new CharaFrame { Delay = Int(bodyFrame, "delay", 120) };
            if (outFrame.Delay <= 0)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: {BodyImg} bad delay {outFrame.Delay}");
                return false;
            }
            var bones = new BoneSet(); // y down, feet at (0,0)
            bool face = Int(bodyFrame, "face", 1) != 0;

            // body first (it defines the neck/navel bones), then head, face, hair, gear
            var trash = gear == null ? null : new CharaFrame();
            var baseSink = gear == null ? outFrame : trash!;
            string gearDir = gear == null ? "chara/body" : $"chara/gear/{ImgId(gear)}";
            missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(BodyImg), bodyFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(HeadImg), headFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            if (face) // back-facing frames drop the whole face img (AvatarCanvas.cs:1111-1117)
                missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(FaceImg), faceNode, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            missing += AddFrameParts(baseRoot, baseSink, bones, ImgId(HairImg), hairFrame, outDir, frameIdx, saved, zNames, face, action, "chara/body");
            foreach (string img in CharaEquips)
            {
                bool isTarget = img == gearBeginner;
                string path = isTarget ? gear! : img;
                var equipFrame = FindImgFrame(looseRoot, path, action, frameIdx);
                if (equipFrame == null)
                {
                    // equips without this art (weapon/glove climb frames) reuse
                    // their stand2 frame; body/head/hair stay strict above
                    equipFrame = FindImgFrame(looseRoot, path, "stand2", frameIdx);
                    if (equipFrame == null)
                    {
                        Console.WriteLine($"{tag} frame {frameIdx}: {path} node not found");
                        return false;
                    }
                    Console.WriteLine($"{tag} frame {frameIdx}: {path} falls back to stand2");
                }
                var sink = gear == null || isTarget ? outFrame : trash!;
                missing += AddFrameParts(baseRoot, sink, bones, ImgId(path), equipFrame, outDir, frameIdx, saved, zNames, face, action,
                    isTarget ? gearDir : "chara/body");
                if (isTarget) break; // later beginners add no bones the target could use
            }

            if (outFrame.Parts.Count == 0)
            {
                Console.WriteLine($"{tag} frame {frameIdx}: no parts exported");
                return false;
            }

            // zmap lists front items first; the client draws the list in order,
            // so sort back-to-front (bigger Z first, drawn first). Stable: equal
            // Z keeps file order (the W3 glove pair shares one Z).
            outFrame.Parts = outFrame.Parts.OrderByDescending(p => p.Z).ToList();
            data.Frames.Add(outFrame);
        }

        if (data.Frames.Count == 0)
        {
            Console.WriteLine($"{tag} has no frames");
            return false;
        }

        Console.WriteLine($"{tag}: {data.Frames.Count} frames, " +
                          $"parts [{string.Join(",", data.Frames.Select(f => f.Parts.Count))}]");
        if (missing > 0)
        {
            Console.WriteLine($"  missing {tag} {missing} character part(s)");
            return false;
        }
        string jsonDir = gear == null ? Path.Combine("chara", "body") : Path.Combine("chara", "gear", ImgId(gear));
        WriteJson(Path.Combine(outDir, jsonDir, $"{action}.json"), data);
        return true;
    }

    // One mob's art: every action's frames in numeric order (die1 starts at 1,
    // not 0, hence the sort). Frames are bare pngs with no origin or delay,
    // so each gets delay 100 and the client anchors center-bottom.
    private static bool ExportMob(WzRoot mob, WzRoot str, int mobId, string outDir, HashSet<string> saved)
    {
        var img = mob.Find($@"_Canvas\{Mobs[mobId].art}.img");
        if (img == null)
        {
            Console.WriteLine($"mob {mobId}: art {Mobs[mobId].art}.img not found");
            return false;
        }
        string mobName = str.Find($@"Mob.img\{mobId}\name")?.GetValueEx<string>("") ?? "";
        if (mobName == "")
        {
            Console.WriteLine($"mob {mobId} has no String name");
            return false;
        }
        var data = new MobData
        {
            MobId = mobId,
            Name = mobName,
            MaxHp = Mobs[mobId].hp,
            Def = Mobs[mobId].def,
            Exp = Mobs[mobId].exp,
            Touch = Mobs[mobId].touch,
            Speed = Mobs[mobId].speed,
            Aggro = Mobs[mobId].aggro,
            Boss = mobId == BossMushmom
        };
        foreach (Wz_Node action in img.Nodes)
        {
            if (action.Text == "info") continue;
            var outAction = new MobAction { Name = action.Text };
            var frames = new List<(int key, Wz_Node node)>();
            foreach (Wz_Node f in action.Nodes)
            {
                if (f.Text == "info") continue; // attack metadata (effect), not a frame
                if (!int.TryParse(f.Text, out int key))
                {
                    Console.WriteLine($"mob {mobId} {action.Text}: non-frame child {f.Text}");
                    return false;
                }
                frames.Add((key, f));
            }
            int i = 0;
            foreach ((int _, Wz_Node f) in frames.OrderBy(t => t.key))
            {
                if (f.Value is not Wz_Png png)
                {
                    Console.WriteLine($"mob {mobId} {action.Text} frame {f.Text}: not a png");
                    return false;
                }
                string path = $"mob/{mobId}/{action.Text}/{i}.png";
                SaveOnce(png, Path.Combine(outDir, path), saved);
                outAction.Frames.Add(new MobFrame { Image = path, Delay = 100 });
                i++;
            }
            if (outAction.Frames.Count == 0)
            {
                Console.WriteLine($"mob {mobId} {action.Text}: no frames");
                return false;
            }
            data.Actions.Add(outAction);
        }
        // W8 plays the idle (stand, or move when the mob has no stand, like
        // the blue snail), hit1 (flinch), die1 (death); W12 AI drives move.
        foreach (string need in new[] { "hit1", "die1" })
            if (!data.Actions.Any(a => a.Name == need))
            {
                Console.WriteLine($"mob {mobId}: no {need} action");
                return false;
            }
        if (!data.Actions.Any(a => a.Name is "stand" or "move"))
        {
            Console.WriteLine($"mob {mobId}: no stand or move action");
            return false;
        }
        Console.WriteLine($"mob {mobId}: " +
            string.Join(", ", data.Actions.Select(a => $"{a.Name} x{a.Frames.Count}")));
        WriteJson(Path.Combine(outDir, "mob", $"{mobId}.json"), data);
        return true;
    }

    // One placed NPC: stand frame (a 1x1 stub with _outlink into _Canvas) plus
    // its origin, and the String name + d0 greeting (greeting may be empty).
    private static bool ExportNpc(WzRoot npc, WzRoot str, int npcId, string outDir, HashSet<string> saved)
    {
        if (!UiPng.TryRead(npc, "Npc", $@"{npcId:D7}.img/stand/0", out var png, out var origin))
            return false;
        if (png.Width <= 1 || origin == null)
        {
            Console.WriteLine($"npc {npcId}: stand art is a stub");
            return false;
        }
        string name = str.Find($@"Npc.img\{npcId}\name")?.GetValueEx<string>("") ?? "";
        if (name == "")
        {
            Console.WriteLine($"npc {npcId} has no String name");
            return false;
        }
        string greet = str.Find($@"Npc.img\{npcId}\d0")?.GetValueEx<string>("") ?? "";
        SaveOnce(png, Path.Combine(outDir, "npc", $"{npcId}.png"), saved);
        WriteJson(Path.Combine(outDir, "npc", $"{npcId}.json"),
            new NpcData { NpcId = npcId, Name = name, Greet = greet, OriginX = origin.X, OriginY = origin.Y });
        Console.WriteLine($"npc {npcId}: {name} {png.Width}x{png.Height}");
        return true;
    }

    // Exported quests: 1016 proves full Say pages (its pre is unexported,
    // so it never offers), 1027 is the W15 chain finale (Mai to Lucas talk),
    // 36301 is scripted with empty Say (the client falls back to summaries).
    private static readonly int[] QuestIds = new[] { 1016, 1027, 36301 };

    private static bool ExportQuests(WzRoot baseRoot, string outDir)
    {
        foreach (int questId in QuestIds)
        {
            var img = baseRoot.Find($@"Quest\QuestData\{questId}.img");
            if (img == null)
            {
                Console.WriteLine($"quest {questId} not found");
                return false;
            }
            var info = img.Nodes["QuestInfo"];
            string name = info?.Nodes["name"]?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"quest {questId} has no name");
                return false;
            }
            var check0 = img.Nodes["Check"]?.Nodes["0"];
            var check1 = img.Nodes["Check"]?.Nodes["1"];
            int npc0 = QNum(check0?.Nodes["npc"]), npc1 = QNum(check1?.Nodes["npc"]);
            if (npc0 == 0 || npc1 == 0)
            {
                Console.WriteLine($"quest {questId} has no giver or turn-in (v1 needs both)");
                return false;
            }
            var data = new QuestData
            {
                QuestId = questId,
                Name = name,
                Npc0 = npc0,
                Npc1 = npc1,
                LvMin = QNum(check0?.Nodes["lvmin"]),
                LvMax = QNum(check0?.Nodes["lvmax"]),
                Offer = QStr(info?.Nodes["0"]),
                Progress = QStr(info?.Nodes["1"]),
                Done = QStr(info?.Nodes["2"]),
                NeedItemsToComplete = check1?.Nodes["item"]?.Nodes.Count > 0,
                NeedItemsToAccept = check0?.Nodes["item"]?.Nodes.Count > 0,
                RewardExp = QNum(img.Nodes["Act"]?.Nodes["1"]?.Nodes["exp"]),
                RewardMeso = QNum(img.Nodes["Act"]?.Nodes["1"]?.Nodes["meso"]),
            };
            if (data.RewardExp < 0)
            {
                Console.WriteLine($"quest {questId} has a negative exp reward");
                return false;
            }
            if (data.RewardMeso < 0)
            {
                Console.WriteLine($"quest {questId} has a negative meso reward");
                return false;
            }
            foreach (Wz_Node it in img.Nodes["Act"]?.Nodes["1"]?.Nodes["item"]?.Nodes ?? Enumerable.Empty<Wz_Node>())
            {
                int item = QNum(it.Nodes["id"]), count = QNum(it.Nodes["count"]);
                if (!ItemData.PotionIds.Contains(item) || count <= 0)
                {
                    Console.WriteLine($"quest {questId} has a bad item reward (item {item} x{count})");
                    return false;
                }
                data.RewardItems.Add(new QuestReward { Item = item, Count = count });
            }
            foreach (var side in new[] { check0, check1 })
                foreach (Wz_Node pre in side?.Nodes["quest"]?.Nodes ?? Enumerable.Empty<Wz_Node>())
                    if (QNum(pre.Nodes["state"]) == 2)
                    {
                        int preId = QNum(pre.Nodes["id"]);
                        if (preId == 0)
                        {
                            Console.WriteLine($"quest {questId} has a zero pre id");
                            return false;
                        }
                        if (!data.Pre.Contains(preId)) data.Pre.Add(preId); // both sides may list it
                    }
            var say0 = img.Nodes["Say"]?.Nodes["0"];
            var say1 = img.Nodes["Say"]?.Nodes["1"];
            string first0 = QStr(say0?.Nodes["0"]);
            if (first0 != "") data.Say0.Add(first0);
            data.Say0.AddRange(QPages(say0?.Nodes["yes"]));
            string first1 = QStr(say1?.Nodes["0"]);
            if (first1 != "") data.Say1.Add(first1);
            data.Say1.AddRange(QPages(say1?.Nodes["yes"]));
            data.Stop = QStr(say1?.Nodes["stop"]?.Nodes["0"]);
            if (data.Stop == "") data.Stop = QStr(say1?.Nodes["stop"]?.Nodes["mob"]?.Nodes["0"]);
            // Mob keys ("0","1",...) sort numeric like QPages: #a order-1 is the index.
            var mobs = (check1?.Nodes["mob"]?.Nodes ?? Enumerable.Empty<Wz_Node>())
                .OrderBy(m => int.TryParse(m.Text, out int key) ? key : int.MaxValue);
            foreach (Wz_Node m in mobs)
            {
                int mob = QNum(m.Nodes["id"]), count = QNum(m.Nodes["count"]);
                if (mob == 0 || count == 0)
                {
                    Console.WriteLine($"quest {questId} has a zero kill (mob {mob} x{count})");
                    return false;
                }
                data.Kills.Add(new QuestKill { Mob = mob, Count = count });
            }
            if (data.Kills.Count > 9)
            {
                Console.WriteLine($"quest {questId} has {data.Kills.Count} kills (v1 takes 9)");
                return false;
            }
            Console.WriteLine($"quest {questId}: {name} kills={data.Kills.Count} say0={data.Say0.Count} say1={data.Say1.Count}");
            WriteJson(Path.Combine(outDir, "quest", $"{questId}.json"), data);
        }
        return true;
    }

    // Quest numbers read as int or padded string (missing = 0, e.g. no lvmin).
    private static int QNum(Wz_Node? node) => node?.Value switch
    {
        int i => i,
        string s when int.TryParse(s, out int n) => n,
        _ => 0
    };

    private static string QStr(Wz_Node? node) => node?.Value as string ?? "";

    // Numbered string pages in key order (yes/0, yes/1, ...).
    private static List<string> QPages(Wz_Node? node)
    {
        var pages = new List<(int key, string text)>();
        if (node != null)
            foreach (Wz_Node p in node.Nodes)
                if (int.TryParse(p.Text, out int key) && p.Value is string s && s != "")
                    pages.Add((key, s));
        return pages.OrderBy(t => t.key).Select(t => t.text).ToList();
    }

    // Basic damage skin digits: BasicEff.img NoRed0 0-9 (yellow; the set that
    // also carries Miss/guard words). Miss and the words stay for the week
    // that adds dodge and guard.
    // W9 HUD art: bottom bar (StatusBar3), inventory frame + tab pieces,
    // stat body. Buttons take normal/mouseOver/pressed; disabled never shows.
    // Must match Hud.cs: every file there must be listed here.
    private static bool ExportHud(WzRoot ui, string outDir)
    {
        string sb3 = "StatusBar3.img";
        var files = new List<(string node, string file)>
        {
            ($"{sb3}/main/namePlate/backgrnd", "hud_plate.png"),
            ($"{sb3}/main/namePlate/layer:lv", "hud_lvtag.png"),
            ($"{sb3}/main/status/normal/backgrnd", "hud_hpbg.png"),
            ($"{sb3}/main/status/normal/layer:cover", "hud_hpcover.png"),
            ($"{sb3}/main/status/normal/gauge/hp/layer:0", "hud_hpfill.png"),
            ($"{sb3}/main/status/normal/gauge/mp/layer:0", "hud_mpfill.png"),
            ($"{sb3}/mainBar/EXPBar/1366/layer:back", "hud_expback.png"),
            ($"{sb3}/mainBar/EXPBar/1366/layer:cover", "hud_expcover.png"),
            ($"{sb3}/mainBar/EXPBar/1366/layer:gauge", "hud_expgauge.png"),
            ($"{sb3}/mainBar/quickSlot/backgrnd", "hud_qslot.png"),
            ($"{sb3}/mainBar/quickSlot/layer:cover", "hud_qslotcover.png"),
            ($"{sb3}/KeyConfig/key/29", "hud_key_ctrl.png"),
            ($"{sb3}/KeyConfig/key/56", "hud_key_alt.png"),
            ($"{sb3}/KeyConfig/key/23", "hud_key_i.png"),
            ($"{sb3}/KeyConfig/key/46", "hud_key_c.png"),
            ("UIInventory.img/Inventory/backgrnd", "hud_inv.png"),
            ("UIWindow.img/Item/BtCoin/normal/0", "hud_coin.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:attackBack", "hud_stat.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:highlightLong", "hud_stathilong.png"),
            ("UICharacterInfo.img/common/main/button:close/normal/0", "hud_statx.png"),
        };
        for (int d = 0; d <= 9; d++)
            files.Add(($"{sb3}/main/namePlate/levelNum/{d}", $"hud_lv{d}.png"));
        for (int d = 0; d <= 9; d++)
            files.Add(($"{sb3}/main/status/normal/gauge/number/{d}", $"hud_num{d}.png"));
        for (int tab = 0; tab < 6; tab++)
        {
            files.Add(($"UIInventory.img/Inventory/AutoBuild/tab:category/selected/{tab}", $"hud_itab_s{tab}.png"));
            files.Add(($"UIInventory.img/Inventory/AutoBuild/tab:category/normal/{tab}", $"hud_itab_n{tab}.png"));
        }
        // number's 11th child is "\" (the HP/MP separator): its name breaks
        // path lookup, so the client bakes "/" instead. No other symbols exist.
        (string state, string suffix)[] states = new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") };
        foreach ((string state, string suffix) in states)
        {
            files.Add(($"{sb3}/mainBar/menu/button:Character/{state}/0", $"hud_btn_char_{suffix}.png"));
            files.Add(($"{sb3}/mainBar/menu/button:Menu/{state}/0", $"hud_btn_menu_{suffix}.png"));
            files.Add(($"{sb3}/mainBar/submenu/character/button:character/{state}/0", $"hud_sub_char_{suffix}.png"));
            files.Add(($"{sb3}/mainBar/submenu/character/button:Item/{state}/0", $"hud_sub_item_{suffix}.png"));
        }
        for (int i = 0; i <= 2; i++)
            files.Add(($"{sb3}/mainBar/submenu/backgrnd/{i}", $"hud_sub_bg{i}.png"));
        foreach ((string node, string file) in files)
        {
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        }
        Console.WriteLine($"hud: {files.Count} files");
        return true;
    }

    private static bool ExportUiFrames(WzRoot ui, string outDir)
    {
        foreach ((string node, string stem) in UiFrameSets)
        {
            var set = WzNodeUtil.ResolveUol(ui.Find(node));
            if (set == null)
            {
                Console.WriteLine($"node not found: {node}");
                return false;
            }
            var data = new UiFramesData();
            int slash = node.LastIndexOf('/');
            var off = ui.Find(node[..slash] + "/offset" + node[(slash + 1)..])?.Value as Wz_Vector;
            data.OffX = off?.X ?? 0; // no offset node (ghost): anchor on the feet
            data.OffY = off?.Y ?? 0;
            for (int f = 0; ; f++)
            {
                var frame = WzNodeUtil.ResolveUol(set.Nodes[f.ToString()]);
                if (frame == null) break;
                var origin = frame.Nodes["origin"]?.Value as Wz_Vector;
                if (origin == null)
                {
                    Console.WriteLine($"{node}/{f}: no origin");
                    return false;
                }
                int delay = Int(frame, "delay", -1);
                if (delay < 0)
                {
                    Console.WriteLine($"{node}/{f}: no delay");
                    return false;
                }
                string path = $"ui/{stem}_{f}.png";
                if (!UiPng.Save(ui, "UI", $"{node}/{f}", Path.Combine(outDir, path)))
                    return false;
                int a0 = Int(frame, "a0", 255);
                data.Frames.Add(new UiFrame
                {
                    Image = path,
                    Ox = origin.X,
                    Oy = origin.Y,
                    Delay = delay,
                    A0 = a0,
                    A1 = Int(frame, "a1", a0),
                });
            }
            if (data.Frames.Count == 0)
            {
                Console.WriteLine($"{node}: no frames");
                return false;
            }
            WriteJson(Path.Combine(outDir, "ui", $"{stem}.json"), data);
            Console.WriteLine($"{stem}: {data.Frames.Count} frames off={data.OffX},{data.OffY}");
        }
        return true;
    }

    // Name tag plates: 3 slices + origins + name color per node.
    private static bool ExportNameTag(WzRoot ui, string outDir)
    {
        foreach (int node in NameTagNodes)
        {
            string tag = $"NameTag.img/{node}";
            if (!UiPng.TryRead(ui, "UI", $"{tag}/w", out var w, out var wO)
                || !UiPng.TryRead(ui, "UI", $"{tag}/c", out var c, out var cO)
                || !UiPng.TryRead(ui, "UI", $"{tag}/e", out var e, out var eO))
                return false;
            if (wO == null || cO == null || eO == null)
            {
                Console.WriteLine($"{tag}: a slice has no origin");
                return false;
            }
            int clr = Int(ui.Find(tag), "clr", int.MinValue);
            if (clr == int.MinValue)
            {
                Console.WriteLine($"{tag}: no clr");
                return false;
            }
            WzNodeUtil.SavePng(w, Path.Combine(outDir, "ui", "tag", $"ring{node}_w.png"));
            WzNodeUtil.SavePng(c, Path.Combine(outDir, "ui", "tag", $"ring{node}_c.png"));
            WzNodeUtil.SavePng(e, Path.Combine(outDir, "ui", "tag", $"ring{node}_e.png"));
            WriteJson(Path.Combine(outDir, "ui", "tag", $"ring{node}.json"), new NameTagData
            {
                Clr = clr,
                WOx = wO.X,
                WOy = wO.Y,
                COx = cO.X,
                COy = cO.Y,
                EOx = eO.X,
                EOy = eO.Y,
            });
            Console.WriteLine($"nametag {node}: {w.Width}x{w.Height}/{c.Width}x{c.Height}/{e.Width}x{e.Height} clr={clr}");
        }
        return true;
    }

    private static bool ExportDamageSkin(WzRoot effect, string outDir)
    {
        var data = new DmgData();
        for (int d = 0; d <= 9; d++)
        {
            string node = $"BasicEff.img/NoRed0/{d}";
            var origin = WzNodeUtil.ResolveUol(effect.Find(node))?.Nodes["origin"]?.Value as Wz_Vector;
            if (origin == null)
            {
                Console.WriteLine($"{node}: no origin");
                return false;
            }
            string path = $"effect/dmg/{d}.png";
            if (!UiPng.Save(effect, "Effect", node, Path.Combine(outDir, path)))
                return false;
            data.Digits.Add(new DmgDigit { Image = path, Oy = origin.Y });
        }
        WriteJson(Path.Combine(outDir, "effect", "dmg.json"), data);
        Console.WriteLine("damage skin: NoRed0 0-9");
        return true;
    }

    // W10 potions: red (+50 HP) and blue (+100 MP). Heals come from the WZ
    // spec node; icons are the info/icon canvases (_outlink to _Canvas).
    private static bool ExportPotions(WzRoot item, string outDir)
    {
        foreach (int id in new[] { ItemData.RedPotion, ItemData.BluePotion })
        {
            string key = id.ToString("D8"); // 02000000
            var node = item.Find($"Consume/0200.img/{key}");
            if (node == null)
            {
                Console.WriteLine($"potion {id} not found");
                return false;
            }
            var spec = node.Nodes["spec"];
            if (spec == null)
            {
                Console.WriteLine($"potion {id} has no spec");
                return false;
            }
            var pinfo = node.Nodes["info"];
            if (pinfo == null)
            {
                Console.WriteLine($"potion {id} has no info");
                return false;
            }
            var data = new ItemData
            {
                ItemId = id,
                Image = $"item/{id}.png",
                Hp = Int(spec, "hp"),
                Mp = Int(spec, "mp"),
                Price = Int(pinfo, "price"),
            };
            if (data.Price <= 0)
            {
                Console.WriteLine($"potion {id} has a bad price {data.Price}");
                return false;
            }
            if (!UiPng.Save(item, "Item", $"Consume/0200.img/{key}/info/icon", Path.Combine(outDir, data.Image)))
                return false;
            WriteJson(Path.Combine(outDir, "item", $"{id}.json"), data);
        }
        Console.WriteLine("potions: 2000000 2000003");
        return true;
    }

    // One def + icon per wearable gear: Slot and bonuses come from the img's
    // info node. v1 ships only all-supported stats: any other inc* fails loud
    // (a half-working item must never sneak in); reqs/pose/sfx/speed/tuc/
    // cash sit unused by design (ItemData documents the v1 gear rules).
    private static bool ExportEquips(WzRoot chara, string outDir)
    {
        var shipped = new List<int>();
        foreach (string gear in EquipGearPaths)
        {
            int id = int.Parse(ImgId(gear));
            shipped.Add(id);
            var info = chara.Find($@"{gear}\info");
            if (info == null)
            {
                Console.WriteLine($"equip {id} has no info");
                return false;
            }
            foreach (Wz_Node c in info.Nodes)
            {
                if (c.Text.StartsWith("inc") && c.Text is not ("incPAD" or "incPDD" or "incSTR" or "incDEX"))
                {
                    Console.WriteLine($"equip {id} has unsupported stat {c.Text}");
                    return false;
                }
            }
            var data = new ItemData
            {
                ItemId = id,
                Image = $"item/{id}.png",
                Slot = Str(info, "islot"),
                Atk = Int(info, "incPAD"),
                Def = Int(info, "incPDD"),
                Str = Int(info, "incSTR"),
                Dex = Int(info, "incDEX"),
                Price = Int(info, "price"),
                NameTag = Int(info, "nameTag"),
            };
            if (data.Slot == "")
            {
                Console.WriteLine($"equip {id} has no slot");
                return false;
            }
            if (data.Price <= 0 && Int(info, "cash") == 0) // cash gear carries no meso price
            {
                Console.WriteLine($"equip {id} has a bad price {data.Price}");
                return false;
            }
            if (!UiPng.Save(chara, "Character", $@"{gear}\info/icon", Path.Combine(outDir, data.Image)))
                return false;
            WriteJson(Path.Combine(outDir, "item", $"{id}.json"), data);
        }
        Console.WriteLine($"equips: {string.Join(" ", shipped)}");
        return true;
    }

    // Warrior line (100/110/111): icon + disabled icon + effect frames +
    // hit spark frames (variant 0) + buff loop frames (affected, buffs with
    // art only). Actives without an effect node fail loud (an invisible cast
    // is a data bug); buffs and passives without art are fine (Iron Body and
    // Combo Attack have no effect node; passives are icon-only by design).
    // War Leap carries 4 effect variants: v1 plays variant 0, flipped.
    private static bool ExportSkills(WzRoot skill, WzRoot str, string outDir, HashSet<string> saved)
    {
        foreach (int id in Skills.Keys.OrderBy(x => x))
        {
            var data = Skills[id];
            data.SkillId = id; // the key is the id (initializers don't repeat it)
            data.Effect.Clear(); data.Hit.Clear(); data.Affected.Clear(); // the table is static: a retry must not append twice
            string img = $"{id / 10000}.img";
            var node = skill.Find($@"_Canvas\{img}\skill\{id}");
            if (node == null)
            {
                Console.WriteLine($"skill {id} not found");
                return false;
            }
            string name = str.Find($@"Skill.img\{id}\name")?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"skill {id} has no String name");
                return false;
            }
            data.Name = name;
            data.Icon = $"skill/{id}/icon.png";
            data.Disabled = $"skill/{id}/dis.png";
            if (!UiPng.Save(skill, "Skill", $@"_Canvas/{img}/skill/{id}/icon", Path.Combine(outDir, data.Icon)))
                return false;
            if (!UiPng.Save(skill, "Skill", $@"_Canvas/{img}/skill/{id}/iconDisabled", Path.Combine(outDir, data.Disabled)))
                return false;
            var effect = node.Nodes["effect"];
            if (effect == null && data.Kind == SkillData.KindActive)
            {
                Console.WriteLine($"skill {id} has no effect");
                return false;
            }
            if (effect != null)
            {
                Wz_Node? frames = id == SkillData.WarLeap ? effect.Nodes["0"] : effect;
                if (frames == null)
                {
                    Console.WriteLine($"skill {id} has no effect variant 0");
                    return false;
                }
                if (!SaveSkillFrames(frames, $"skill/{id}/effect", data.Effect, outDir, saved, id)) return false;
            }
            var hit = node.Nodes["hit"]?.Nodes["0"];
            if (node.Nodes["hit"] != null && hit == null)
            {
                Console.WriteLine($"skill {id} has a hit node but no hit 0");
                return false;
            }
            if (hit != null && !SaveSkillFrames(hit, $"skill/{id}/hit", data.Hit, outDir, saved, id)) return false;
            var affected = node.Nodes["affected"];
            if (affected != null && !SaveSkillFrames(affected, $"skill/{id}/affected", data.Affected, outDir, saved, id)) return false;
            Console.WriteLine($"skill {id} {name}: effect x{data.Effect.Count}, hit x{data.Hit.Count}, affected x{data.Affected.Count}");
            WriteJson(Path.Combine(outDir, "skill", $"{id}.json"), data);
        }
        return true;
    }

    // Saves one flat frame list (int-named png children) as dir/{i}.png.
    private static bool SaveSkillFrames(Wz_Node list, string dir, List<string> into, string outDir, HashSet<string> saved, int id)
    {
        var frames = new List<(int key, Wz_Node node)>();
        foreach (Wz_Node f in list.Nodes)
        {
            if (!int.TryParse(f.Text, out int key))
            {
                Console.WriteLine($"skill {id} {dir}: non-frame child {f.Text}");
                return false;
            }
            frames.Add((key, f));
        }
        int i = 0;
        foreach ((int _, Wz_Node f) in frames.OrderBy(t => t.key))
        {
            if (f.Value is not Wz_Png png)
            {
                Console.WriteLine($"skill {id} {dir} frame {f.Text}: not a png");
                return false;
            }
            string path = $"{dir}/{i}.png";
            SaveOnce(png, Path.Combine(outDir, path), saved);
            into.Add(path);
            i++;
        }
        if (into.Count == 0)
        {
            Console.WriteLine($"skill {id} {dir}: no frames");
            return false;
        }
        return true;
    }

    // Adds the png parts of one frame node and returns how many images were missing.
    private static int AddFrameParts(WzRoot baseRoot, CharaFrame outFrame, BoneSet bones, string imgId,
        Wz_Node frame, string outDir, int frameIdx, HashSet<string> saved, List<string> zNames, bool face, string action, string pngDir)
    {
        int missing = 0;
        foreach (Wz_Node raw in frame.Nodes)
        {
            // the frame names the slot (hairOverHead...); the uol target only supplies
            // the image and bones, so naming follows the raw node (AvatarCanvas.cs:749)
            string name = raw.Text;
            Wz_Node? part = WzNodeUtil.ResolveUol(raw);
            // beginner: face flag from the body frame, EarType 0, hair out (this cap
            // leaves hairOverHead showing, verified in logs/w3_stand2.png), first weapon
            // a dangling part has no parent; the frame itself tells multi weapons apart
            bool multiWeapon = (part?.ParentNode ?? frame).Nodes["weapon1"] != null;
            bool visible = SceneMath.PartVisible(name, face, earType: 0, hairCover: false,
                showHairShade: false, weaponIndex: 0, multiWeapon: multiWeapon);
            if (part == null)
            {
                // a hidden part needs no image; a visible one with a dangling uol is loud
                if (visible)
                {
                    missing++;
                    Console.WriteLine($"  character {action} frame {frameIdx}: missing {imgId} {name} image");
                }
                continue;
            }
            if (!visible) continue;

            var (png, origin) = WzNodeUtil.ReadCanvas(baseRoot, part);
            if (png == null)
            {
                // frame props (delay/face/...) carry no image; anything else is a real
                // miss. Deliberately stricter than the reference, which also skips
                // dangling uols and imageless containers without a word.
                if (part.Value is Wz_Png || part.Nodes.Count > 0)
                {
                    missing++;
                    Console.WriteLine($"  character {action} frame {frameIdx}: missing {imgId} {name} image");
                }
                continue;
            }

            string img = $"{pngDir}/{action}/{frameIdx}_{imgId}_{name}.png";
            SaveOnce(png, Path.Combine(outDir, img), saved);

            // a part without a "map" bone hangs on the root (AvatarCanvas.cs:791-794)
            var mapNode = part.Nodes["map"];
            var skin = mapNode == null ? (x: 0, y: 0) : bones.Attach(name, mapNode);

            // zmap lists front items first. A missing z lands in front of everything,
            // a numeric z maps around it (negatives go behind), and a z name that is
            // not in zmap goes behind (AvatarCanvas.cs:980-991).
            var zNode = part.Nodes["z"];
            int? zIndex = zNode?.GetValueEx<int?>(null);
            string zName = zIndex == null ? zNode?.GetValueEx<string>("") ?? "" : "";
            int z;
            if (zNode == null) z = -1;
            else if (zIndex != null) z = zIndex.Value < 0 ? zNames.Count - zIndex.Value : -1 - zIndex.Value;
            else if (zName == "") z = -1; // empty z counts as missing (AvatarCanvas.cs:980)
            else z = zNames.Contains(zName) ? zNames.IndexOf(zName) : zNames.Count;

            int ox = origin?.X ?? 0, oy = origin?.Y ?? 0;
            outFrame.Parts.Add(new CharaPart
            {
                Image = img,
                Img = imgId,
                X = skin.x - ox,
                Y = skin.y - oy,
                Z = z
            });
        }
        return missing;
    }

    // Bone tree of one animation frame. Mirrors AvatarCanvas.CreateBone/AppendBone.
    private sealed class BoneSet
    {
        private readonly Dictionary<string, (string? parent, int x, int y)> _nodes = new();

        private (int x, int y) Abs(string name)
        {
            var (parent, x, y) = _nodes[name];
            while (parent != null)
            {
                var p = _nodes[parent];
                x += p.x;
                y += p.y;
                parent = p.parent;
            }
            return (x, y);
        }

        // Returns where the part's image anchor lands. Bones come from the part's
        // "map" node: the first entry is the bone the image hangs on, the rest are
        // joints that other parts (head, hair, face) attach to.
        public (int x, int y) Attach(string partName, Wz_Node mapNode)
        {
            string? cascadeParent = null;
            (int x, int y) skin = default; // empty map stays at root (reference drops it; ours shows it)
            bool first = true;

            foreach (Wz_Node boneNode in mapNode.Nodes)
            {
                if (boneNode.Text == "muzzle") continue; // skipped by the reference too (AvatarCanvas.cs:776)
                var v = boneNode.GetValue<Wz_Vector>();
                string name = boneNode.Text;

                if (first)
                {
                    first = false;
                    if (_nodes.ContainsKey(name)) // shared bone: hang on a joint at -map
                    {
                        string joint = "@" + name + "_" + partName;
                        _nodes[joint] = (name, -v.X, -v.Y);
                        cascadeParent = joint;
                        skin = Abs(joint);
                    }
                    else // new bone at +map, image offset cancels it out
                    {
                        _nodes[name] = (null, v.X, v.Y);
                        cascadeParent = null;
                        skin = (0, 0);
                    }
                }
                else
                {
                    _nodes[name] = (cascadeParent, v.X, v.Y);
                }
            }
            return skin;
        }
    }

    // ---------- helpers ----------

    private static void SaveOnce(Wz_Png png, string filePath, HashSet<string> saved)
    {
        if (!saved.Add(filePath)) return;
        WzNodeUtil.SavePng(png, filePath);
    }

    private static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    private static int Int(Wz_Node? node, string name, int defaultValue = 0) =>
        node?.Nodes[name]?.GetValueEx<int>(defaultValue) ?? defaultValue;

    // Poses are baked static; a body frame carrying flip/move/rotate fails the
    // export (no W4 action uses them). Only the body frame is checked: the
    // reference plays these from the body action alone (AvatarCanvas.cs:617), ignoring other imgs' values.
    private static bool BodyFrameStatic(string action, int frameIdx, Wz_Node frame)
    {
        var move = frame.Nodes["move"]?.GetValueEx<Wz_Vector>(new Wz_Vector(0, 0));
        int mx = move?.X ?? 0, my = move?.Y ?? 0;
        if (Int(frame, "flip") == 0 && Int(frame, "rotate") == 0 && mx == 0 && my == 0)
            return true;
        Console.WriteLine($"character {action} frame {frameIdx}: {BodyImg} has flip/move/rotate");
        return false;
    }

    private static string Str(Wz_Node? node, string name) =>
        node?.Nodes[name]?.GetValueEx<string>("") ?? "";
}
