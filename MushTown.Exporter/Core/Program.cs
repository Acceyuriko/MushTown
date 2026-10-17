using MushTown.Exporter;

// Subcommands:
//   smoke    [DataDir]                        WzLib open test (default)
//   dump     <path-under-Base> [depth]        print WZ node structure
//   catdump  <Category> <path> [depth]        same, inside a category wz
//   find     <node name>                      locate nodes by name under Base
//   uipng    [Category] <node-path> <out.png>   save one canvas as png
//   export   [DataDir] [OutDir]               export map + character assets
//   questscan <npcid> [<npcid>...]       list quests starting/ending at these NPCs
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
        if (!TryDepth(2, out int dumpDepth)) return;
        Environment.ExitCode = Dump.Run(WzRoot.LoadBase(ExporterPaths.DataDir), Arg(1, ""), dumpDepth) ? 0 : 1;
        return;

    case "catdump":
        if (args.Length < 3)
        {
            Console.WriteLine("usage: catdump <Category> <path> [depth]");
            Environment.ExitCode = 1;
            return;
        }
        if (!TryDepth(3, out int catDepth)) return;
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

    case "questscan":
        if (args.Length < 2)
        {
            Console.WriteLine("usage: questscan <npcid> [<npcid>...]");
            Environment.ExitCode = 1;
            return;
        }
        Environment.ExitCode = QuestScan.Run(WzRoot.LoadBase(ExporterPaths.DataDir), args[1..]) ? 0 : 1;
        return;

    default:
        Console.WriteLine($"unknown command: {cmd}");
        Console.WriteLine("usage: smoke [DataDir] | dump <path> [depth] | catdump <Category> <path> [depth] | find <name> | uipng [Category] <node-path> <out.png> | export [DataDir] [OutDir] | questscan <npcid> [<npcid>...]");
        Environment.ExitCode = 1;
        return;
}

string Arg(int i, string fallback) => args.Length > i ? args[i] : fallback;

// An unparsable depth is a bad argument, not something to silently ignore.
bool TryDepth(int i, out int depth)
{
    depth = 3;
    if (args.Length <= i) return true;
    if (int.TryParse(args[i], out depth)) return true;
    Console.WriteLine($"bad depth: {args[i]}");
    Environment.ExitCode = 1;
    return false;
}
