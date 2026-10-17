using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Finds official quests that start or end at one of our NPCs:
// usage: questscan <npcid> [<npcid>...]
// Prints one line per match: quest id, name, start npc, end npc.
public static class QuestScan
{
    public static bool Run(WzRoot root, string[] npcIds)
    {
        if (!npcIds.All(id => id.Length > 0 && id.All(char.IsDigit)))
        {
            Console.WriteLine("usage: questscan <npcid> [<npcid>...] (digits only)");
            return false;
        }
        var want = new HashSet<string>(npcIds.Select(id => id.TrimStart('0')).Where(s => s != ""));
        var questData = root.Find("Quest/QuestData");
        if (questData == null)
        {
            Console.WriteLine("NOT FOUND: Quest/QuestData");
            return false;
        }
        int scanned = 0, matched = 0;
        foreach (Wz_Node child in questData.Nodes)
        {
            if (child.Value is not Wz_Image img) continue;
            if (!img.TryExtract())
            {
                Console.WriteLine($"BAD IMG: {child.Text}");
                return false;
            }
            scanned++;
            string npc0 = Num(img.Node.Nodes["Check"]?.Nodes["0"]?.Nodes["npc"]);
            string npc1 = Num(img.Node.Nodes["Check"]?.Nodes["1"]?.Nodes["npc"]);
            if (!want.Contains(npc0) && !want.Contains(npc1)) continue;
            matched++;
            string name = img.Node.Nodes["QuestInfo"]?.Nodes["name"]?.Value as string ?? "?";
            Console.WriteLine($"{Path.GetFileNameWithoutExtension(child.Text)} name={name} npc0={npc0} npc1={npc1}");
        }
        Console.WriteLine($"scanned {scanned}, matched {matched}");
        return true;
    }

    // Npc ids read as int or string; normalized without zero padding.
    private static string Num(Wz_Node? node) => node?.Value switch
    {
        int i => i.ToString().TrimStart('0'),
        string s => s.TrimStart('0'),
        _ => ""
    };
}
