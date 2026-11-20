using MushTown.Exporter;

// Subcommands:
//   smoke    [DataDir]                        WzLib open test (default)
//   dump     <path-under-Base> [depth]        print WZ node structure
//   catdump  <Category> <path> [depth]        same, inside a category wz
//   find     <node name>                      locate nodes by name under Base
//   cat      <Category> <path>               print a string node's full value
//   raw      <Category> <path> <out>         save a raw-data node's bytes
//   spine    <skel-file> [bone]              print a .skel rig's bones, slots, and timelines
//   spinebake [id|all]                       bake ClassSelect rigs to assets/spine/
//   uipng    [Category] <node-path> <out.png>   save one canvas as png
//   export   [DataDir] [OutDir]               export map + character assets
//   sound    [OutDir]                        export only the sounds
//   panel    <name> [OutDir]                  export one panel's art (inventory|stat|skill|passive|quest|map|keys|menu|quickmenu|chat|shop|buff|guide|party|cashshop)
//   questscan <npcid> [<npcid>...]       list quests starting/ending at these NPCs
//   pick item <folder> <prefix>          list Character part ids with names
//   pick hair <out.png> <id,id,...>       contact sheet of the doll per hair
// Only smoke and export take a DataDir; the others always read the default one,
// so their first argument is their own.
//
// Rerun after you pull a change (the client stops at start when a file is missing or old):
//   export  - the start gear changed (the start gear is the six Finsaliel pieces).
//           - the equipment tooltip was reworked (the set table assets/set/448.json; the required stats, the category and the
//             set number in the equip json; the ui/etip_*.png art; `panel stat` exports the etip art too).
//           - the swing actions changed (body, hair, face and all exported gear got swingO2 and swingO3).
//           - the fly action was added (body, hair, face and all exported gear got fly: the floating pose of Mana Wave's slow fall).
//           - Mana Wave's slow fall art was added (assets/skill/wz/2001011.json child 2001012: Art special0..2 and specialAffected0..2
//             with their frames under assets/skill/wz/2001012/).
//           - the Fire/Poison 1st-job skills changed (new assets/skill/wz/<id>.json and icons; the numbers come from the WZ
//             `common` formulas). The MP Boost weapon crit change is part of this: 2000006.json got WeaponBonus.
//           - the Fire/Poison 2nd-job passives were added (assets/skill/wz/2100000, 2100006, 2100007, 2100009, 2100011 .json and their
//             icons; Agile Magic's attack speed is Common boosterActionSpeed). The skill window's page II shows them; an old export
//             makes the client stop at start (no exported wz def for the page II ids).
//           - the Fire/Poison 2nd-job actives were added (assets/skill/wz/2101001.json Meditation and 2101004.json Flame Orb, their icons
//             and their art under assets/skill/wz/<id>/: effect, hit for Flame Orb, effect and affected for Meditation; the json has
//             AffectedFrames and Art["affected"]). An old export makes the client stop at start.
//           - Meditation became a passive (assets/skill/wz/2101001.json: Kind passive, Effect "Magic ATT: +30", no effect or affected
//             art; the old json is a buff and the client stops at start). The buff bar frame art (ui/buff_frame_*.png) is no longer drawn.
//           - Poison Breath was added (assets/skill/wz/2101005.json, its icons and its art under assets/skill/wz/2101005/: effect and hit; the
//             json has the dot, dotInterval and dotTime formulas in Common). The skill window's page II lists it; an old export makes the
//             client stop at start (no exported wz def for the page II ids).
//           - the dodge changed (the mob json got Acc; an old one reads 0 and the dodge rate comes out wrong. effect/hurtdmg.json
//             got the Miss word and effect/hurtdmg/Miss.png; an old hurtdmg.json has no Miss word and the client fails on a dodge).
//           - Empirical Knowledge was added (assets/skill/wz/80002762.json: the common formulas x/time/prop/y plus the mark art
//             Art["mob0"], ["mob1"], ["mob2"] under assets/skill/wz/80002762/mob0..2/). The client fails on a missing mark art.
//           - the monster MP was added (the mob json got MaxMp from WZ info/maxMP; MP Eater absorbs it). Rerun export after the change;
//             an old mob json has MaxMp 0.
//           - the WZ number icons of Elemental Drain and Elemental Adaptation are no longer exported (the client draws the layer count in
//             gold digits itself): the folders assets/skill/wz/2100009/num and assets/skill/wz/2111011/num are not used any more, delete them
//             by hand (the export does not remove old files); the json no longer has NumberFrames/NumberOrigins.
//           - the Fire/Poison 3rd-job passives were added (assets/skill/wz/2110000, 2110001, 2110009, 2110015, 2110016 .json and their
//             icons). The skill window's page III shows them; an old export makes the client stop at start (no exported wz def for the
//             page III ids).
//           - the Fire/Poison 3rd-job actives were added (assets/skill/wz/2111002.json Explosion with its effect and hit art, and
//             2111011.json Elemental Adaptation, a buff: Kind buff, the cast art effect plus Art["effect0"] with its png paths under
//             assets/skill/wz/2111011/effect0/, its special is exported as Art["block"], png under
//             assets/skill/wz/2111011/block/: the block art, played once per blocked status (special0 is not used); an old json has none and Menu.cs refuses it: run `export`).
//             The skill window's page III lists them. An old export makes the client stop at start (no exported wz def for the page III ids;
//             the passive json of Elemental Adaptation, with no art, is refused by Menu.cs: run `export`).
//           - Poison Breath got its body action script (assets/skill/wz/2101005.json has Script: the steps of Character 00002000.img/
//             poisonBreath, an action name, a frame and a time each). An old json has no Script and Menu.cs refuses it: run `export`.
//           - Poison Mist was added (assets/skill/wz/2111003.json, its icons and its art under assets/skill/wz/2111003/: effect, hit, effect1 (the second
//             cast layer) and tile0..tile8 (the looping puffs of the ground zone), all in Art with origins and delays). The skill window's page III lists it;
//             an old export makes the client stop at start (no wz def for the page III id, or no effect1 / tile art: Menu.cs refuses it): run `export`.
//   sound   - once after the decoder changed (every wav is encoded again). It exports only the sounds: the effects, the bgm of
//             each map and the login theme.
//   panel   - exports one panel's art only.
//           - panel passive exports the default passive skills of the skill window's beginner page (name, text, max-level
//             numbers, icon) into assets/skill/passive/. Rerun `panel passive` and `panel skill` after DefaultPassiveSkills
//             changed (the client fails on a mismatch). Rerun `panel passive` after the Monster Park Potion change (new green
//             potion icon assets/skill/passive/item2023126.png).
//           - panel skill also exports the macro list window and the five macro_*.png icons: rerun it after the macro change.
//           - panel map also exports the minimap shop NPC triangle icons mini_dot_shop.png and npclist_shop.png: rerun it after
//             the minimap shop icon change.
string cmd = args.Length > 0 ? args[0] : "smoke";

switch (cmd)
{
    case "smoke":
        Smoke.Run(Arg(1, ExporterPaths.DataDir));
        return;

    case "export":
        Export.Run(Arg(1, ExporterPaths.DataDir), Arg(2, ExporterPaths.OutDir));
        return;

    case "sound":
        Environment.ExitCode = Export.ExportSounds(WzRoot.LoadBase(ExporterPaths.DataDir), Arg(1, ExporterPaths.OutDir)) ? 0 : 1;
        return;

    case "dump":
        if (!TryOptionalInt(2, 3, "depth", out int dumpDepth)) return;
        Environment.ExitCode = Dump.Run(WzRoot.LoadBase(ExporterPaths.DataDir, packs: true), Arg(1, ""), dumpDepth) ? 0 : 1;
        return;

    case "catdump":
        if (args.Length < 3)
        {
            Console.WriteLine("usage: catdump <Category> <path> [depth]");
            Environment.ExitCode = 1;
            return;
        }
        if (!TryOptionalInt(3, 3, "depth", out int catDepth)) return;
        Environment.ExitCode = Dump.Run(WzRoot.LoadCategory(ExporterPaths.DataDir, args[1]), args[2], catDepth) ? 0 : 1;
        return;

    case "find":
        if (args.Length < 2)
        {
            Console.WriteLine("usage: find <name> [category] [partial]");
            Environment.ExitCode = 1;
            return;
        }
        // the category is optional: without it the whole Base tree is searched
        var findRoot = args.Length > 2
            ? WzRoot.LoadCategory(ExporterPaths.DataDir, args[2])
            : WzRoot.LoadBase(ExporterPaths.DataDir);
        Environment.ExitCode = Dump.Find(findRoot, args[1], args.Length > 3 && args[3] == "partial") ? 0 : 1;
        return;

    case "cat":
        if (args.Length < 3)
        {
            Console.WriteLine("usage: cat <Category> <path>");
            Environment.ExitCode = 1;
            return;
        }
        Environment.ExitCode = Dump.Cat(WzRoot.LoadCategory(ExporterPaths.DataDir, args[1]), args[2]) ? 0 : 1;
        return;

    case "raw":
        if (args.Length < 4)
        {
            Console.WriteLine("usage: raw <Category> <path> <out>");
            Environment.ExitCode = 1;
            return;
        }
        Environment.ExitCode = Dump.Raw(WzRoot.LoadCategory(ExporterPaths.DataDir, args[1]), args[2], args[3]) ? 0 : 1;
        return;

    case "spine":
        if (args.Length < 2)
        {
            Console.WriteLine("usage: spine <skel-file> [bone]");
            Environment.ExitCode = 1;
            return;
        }
        if (!TryOptionalInt(2, -1, "bone", out int bone)) return;
        Environment.ExitCode = Dump.Spine(args[1], bone) ? 0 : 1;
        return;

    case "spinebake":
        Environment.ExitCode = SpineBake.Run(args.Length > 1 ? args[1] : "all") ? 0 : 1;
        return;

    case "uipng":
        if (args.Length == 3)
        {
            Environment.ExitCode = UiPng.Run(args[1], args[2]) ? 0 : 1;
            return;
        }
        if (args.Length == 4)
        {
            Environment.ExitCode = UiPng.Run(args[1], args[2], args[3]) ? 0 : 1;
            return;
        }
        Console.WriteLine("usage: uipng <node-path> <out.png> | uipng <Category> <node-path> <out.png>");
        Environment.ExitCode = 1;
        return;

    case "panel":
        if (args.Length < 2)
        {
            Console.WriteLine("usage: panel <inventory|equip|stat|skill|passive|quest|map|keys|menu|quickmenu|chat|shop|buff|guide|party|cashshop> [OutDir]");
            Environment.ExitCode = 1;
            return;
        }
        Environment.ExitCode = Export.RunPanel(args[1], Arg(2, ExporterPaths.OutDir)) ? 0 : 1;
        return;

    case "questscan":
        if (args.Length < 2)
        {
            Console.WriteLine("usage: questscan <npcid> [<npcid>...]");
            Environment.ExitCode = 1;
            return;
        }
        Environment.ExitCode = QuestScan.Run(WzRoot.LoadBase(ExporterPaths.DataDir), args[1..]) ? 0 : 1;
        return;

    case "pick":
        if (args.Length >= 4 && args[1] == "item")
        {
            Environment.ExitCode = Pick.Items(args[2], args[3]) ? 0 : 1;
            return;
        }
        if (args.Length >= 4 && args[1] == "hair")
        {
            Environment.ExitCode = Pick.Hair(args[2], args[3]) ? 0 : 1;
            return;
        }
        Console.WriteLine("usage: pick item <folder> <prefix> | pick hair <out.png> <id,id,...>");
        Environment.ExitCode = 1;
        return;

    default:
        Console.WriteLine($"unknown command: {cmd}");
        Console.WriteLine("usage: smoke [DataDir] | dump <path> [depth] | catdump <Category> <path> [depth] | find <name> | cat <Category> <path> | raw <Category> <path> <out> | spine <skel-file> [bone] | spinebake [id|all] | uipng [Category] <node-path> <out.png> | export [DataDir] [OutDir] | sound [OutDir] | questscan <npcid> [<npcid>...] | pick item <folder> <prefix> | pick hair <out.png> <id,id,...>");
        Environment.ExitCode = 1;
        return;
}

string Arg(int i, string fallback) => args.Length > i ? args[i] : fallback;

// An unparsable optional number is a bad argument, not something to silently ignore.
bool TryOptionalInt(int i, int fallback, string label, out int v)
{
    v = fallback;
    if (args.Length <= i) return true;
    if (int.TryParse(args[i], out v)) return true;
    Console.WriteLine($"bad {label}: {args[i]}");
    Environment.ExitCode = 1;
    return false;
}
