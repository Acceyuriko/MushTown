using MushTown.Exporter;

// Subcommands:
//   smoke    [DataDir]                        WzLib open test (default)
//   dump     <path-under-Base> [depth]       print WZ node structure
//   catdump  <Category> <path> [depth]       same, inside a category wz
//   export   [DataDir] [OutDir]              export map + character assets
string cmd = args.Length > 0 ? args[0] : "smoke";

if (cmd == "dump")
{
    string path = args.Length > 1 ? args[1] : "";
    int depth = args.Length > 2 && int.TryParse(args[2], out int d) ? d : 3;
    Dump.Run(WzRoot.LoadBase(ExporterPaths.DataDir), path, depth);
    return;
}

if (cmd == "catdump")
{
    // catdump <Category> <path-under-that-category> [depth]
    string category = args.Length > 1 ? args[1] : "";
    string path = args.Length > 2 ? args[2] : "";
    int depth = args.Length > 3 && int.TryParse(args[3], out int d2) ? d2 : 3;
    Dump.Run(WzRoot.LoadCategory(ExporterPaths.DataDir, category), path, depth);
    return;
}

string dataDir = args.Length > 1 ? args[1] : ExporterPaths.DataDir;

if (cmd == "export")
{
    string outDir = args.Length > 2 ? args[2] : ExporterPaths.OutDir;
    Export.Run(dataDir, outDir);
    return;
}

Smoke.Run(dataDir);
