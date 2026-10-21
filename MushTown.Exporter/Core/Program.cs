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
//   panel    <name> [OutDir]                  export one panel's art (inventory|stat|skill|quest|map|keys|menu)
//   questscan <npcid> [<npcid>...]       list quests starting/ending at these NPCs
//   pick item <folder> <prefix>          list Character part ids with names
//   pick hair <out.png> <id,id,...>       contact sheet of the doll per hair
// Only smoke and export take a DataDir; the others always read the default one,
// so their first argument is their own.
string cmd = args.Length > 0 ? args[0] : "smoke";

switch (cmd)
{
    case "smoke":
        Smoke.Run(Arg(1, ExporterPaths.DataDir));
        return;

    case "export":
        Export.Run(Arg(1, ExporterPaths.DataDir), Arg(2, ExporterPaths.OutDir));
        return;

    case "dump":
        if (!TryOptionalInt(2, 3, "depth", out int dumpDepth)) return;
        Environment.ExitCode = Dump.Run(WzRoot.LoadBase(ExporterPaths.DataDir), Arg(1, ""), dumpDepth) ? 0 : 1;
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
            Console.WriteLine("usage: find <name>");
            Environment.ExitCode = 1;
            return;
        }
        Environment.ExitCode = Dump.Find(WzRoot.LoadBase(ExporterPaths.DataDir), args[1]) ? 0 : 1;
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
            Console.WriteLine("usage: panel <inventory|stat|skill|quest|map|keys|menu> [OutDir]");
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
        Console.WriteLine("usage: smoke [DataDir] | dump <path> [depth] | catdump <Category> <path> [depth] | find <name> | cat <Category> <path> | raw <Category> <path> <out> | spine <skel-file> [bone] | spinebake [id|all] | uipng [Category] <node-path> <out.png> | export [DataDir] [OutDir] | questscan <npcid> [<npcid>...] | pick item <folder> <prefix> | pick hair <out.png> <id,id,...>");
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
