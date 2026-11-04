using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// NPC exports: one placed NPC's stand art + String name and greeting lines.
public static partial class Export
{
    // One placed NPC: every stand frame (a 1x1 stub with _outlink into _Canvas)
    // plus the origin of the first, the String name, and every greeting line
    // (String d0, d1, ...; may be empty). The world map tip plays the frames, so
    // an NPC that walks in place uses all of them.
    private static bool ExportNpc(WzRoot npc, WzRoot str, int npcId, string outDir, HashSet<string> saved)
    {
        string name = str.Find($@"Npc.img\{npcId}\name")?.GetValueEx<string>("") ?? "";
        if (name == "")
        {
            Console.WriteLine($"npc {npcId} has no String name");
            return false;
        }
        var says = new List<string>();
        for (int i = 0; ; i++)
        {
            string line = str.Find($@"Npc.img\{npcId}\d{i}")?.GetValueEx<string>("") ?? "";
            if (line == "") break;
            says.Add(line);
        }
        string stem = $@"{npcId:D7}.img/stand";
        // Frame numbers may have holes. Walk the nodes that are there, in
        // number order, and write the frames out 0, 1, 2... The client then
        // finds `{id}.png`, `{id}_1.png` by count.
        var keys = new List<int>();
        var stand = npc.Find(stem);
        if (stand != null)
            foreach (Wz_Node n in stand.Nodes)
            {
                if (!int.TryParse(n.Text, out int key))
                {
                    Console.WriteLine($"npc {npcId} stand: non-frame child {n.Text}");
                    return false;
                }
                keys.Add(key);
            }
        keys.Sort();
        var delays = new List<int>();
        Wz_Vector? origin = null;
        int written = 0;
        foreach (int key in keys)
        {
            string path = $"{stem}/{key}";
            if (!UiPng.TryRead(npc, "Npc", path, out var png, out var at) || png.Width <= 1) break; // a stub ends the run
            origin ??= at;
            delays.Add(Int(UiPng.Node(npc, path), "delay", 100));
            SaveOnce(png, Path.Combine(outDir, "npc", written == 0 ? $"{npcId}.png" : $"{npcId}_{written}.png"), saved);
            written++;
        }
        if (delays.Count == 0 || origin == null)
        {
            Console.WriteLine($"npc {npcId}: stand art is a stub");
            return false;
        }
        WriteJson(Path.Combine(outDir, "npc", $"{npcId}.json"),
            new NpcData { NpcId = npcId, Name = name, Says = says, OriginX = origin.X, OriginY = origin.Y, StandDelays = delays });
        Console.WriteLine($"npc {npcId}: {name} stand frames {delays.Count}");
        return true;
    }
}
