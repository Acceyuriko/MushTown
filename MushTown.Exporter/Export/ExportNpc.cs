using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// NPC exports: one placed NPC's stand art + String name and greeting.
public static partial class Export
{
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
}
