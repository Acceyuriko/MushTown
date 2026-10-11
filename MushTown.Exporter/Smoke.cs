using System.Diagnostics;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// W1 smoke test: can WzLib open this KMST 1205+ client at all?
// Usage: smoke [DataDir]
public static class Smoke
{
    // Sound is skipped: 69 chunk files, 8.2 GB, not needed until the audio milestone.
    private static readonly string[] Folders =
    {
        "Base", "Character", "Map", "Mob", "Npc", "Skill", "Item",
        "Quest", "String", "UI", "Effect", "Etc", "Reactor", "Morph", "TamingMob",
    };

    public static void Run(string dataDir)
    {
        Console.WriteLine($"dataDir = {dataDir}");
        Console.WriteLine();

        var loaded = new Dictionary<string, Wz_Structure>();

        foreach (string name in Folders)
        {
            string entry = Path.Combine(dataDir, name, name + ".wz");
            if (!File.Exists(entry))
            {
                Console.WriteLine($"{name,-12} MISSING");
                continue;
            }

            var sw = Stopwatch.StartNew();
            int dirs = 0, imgs = 0;
            void Count(Wz_Node node)
            {
                foreach (Wz_Node child in node.Nodes)
                {
                    if (child.Value is Wz_Image) imgs++;
                    else dirs++;
                    Count(child);
                }
            }

            try
            {
                bool kmst = new Wz_Structure().IsKMST1125WzFormat(entry);
                var structure = new Wz_Structure();
                structure.LoadKMST1125DataWz(entry);

                Count(structure.WzNode);
                loaded[name] = structure;

                Console.WriteLine($"{name,-12} OK   kmst={kmst,-5} top={structure.WzNode.Nodes.Count,4} dirs={dirs,6} imgs={imgs,6} {sw.ElapsedMilliseconds,6} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{name,-12} FAIL {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine();
        foreach (string target in new[] { "00002000.img", "100000000.img", "0100100.img" })
        {
            foreach (var (folder, st) in loaded)
            {
                var hit = FindPath(st.WzNode, target);
                if (hit != null)
                {
                    Console.WriteLine($"[locate] {target,-16} -> {folder}:{hit}");
                    break;
                }
            }
        }

        Console.WriteLine();
        if (loaded.TryGetValue("Base", out var baseWz))
        {
            ProbeCharacter(baseWz);
            ProbeMap(baseWz);
        }
    }

    private static void ProbeCharacter(Wz_Structure baseWz)
    {
        var body = FindNode(baseWz.WzNode, "00002000.img");
        if (body?.Value is Wz_Image bodyImg && bodyImg.TryExtract())
        {
            string[] actions = bodyImg.Node.Nodes.Where(n => n.Text != "info").Select(n => n.Text).ToArray();
            Console.WriteLine($"[Base] 00002000.img actions = {actions.Length}: {string.Join(", ", actions.Take(15))} ...");
        }
        else Console.WriteLine("[Base] 00002000.img extract FAILED");
    }

    private static void ProbeMap(Wz_Structure baseWz)
    {
        // Several nodes share a map id: the ones under _Canvas are minimaps.
        foreach (var cand in FindAll(baseWz.WzNode, "100000000.img"))
        {
            if (cand.Value is not Wz_Image img || !img.TryExtract()) continue;
            string[] keys = img.Node.Nodes.Select(n => n.Text).ToArray();
            if (keys.Contains("foothold"))
            {
                Console.WriteLine($"[Base] real map 100000000 sections = {keys.Length}: {string.Join(", ", keys)}");
                return;
            }
        }
        Console.WriteLine("[Base] real map 100000000 NOT FOUND");
    }

    private static string? FindPath(Wz_Node node, string name)
    {
        foreach (Wz_Node child in node.Nodes)
        {
            if (child.Text == name) return child.Text;
            var sub = FindPath(child, name);
            if (sub != null) return child.Text + "\\" + sub;
        }
        return null;
    }

    private static Wz_Node? FindNode(Wz_Node node, string name)
    {
        foreach (Wz_Node child in node.Nodes)
        {
            if (child.Text == name) return child;
            var sub = FindNode(child, name);
            if (sub != null) return sub;
        }
        return null;
    }

    private static IEnumerable<Wz_Node> FindAll(Wz_Node node, string name)
    {
        foreach (Wz_Node child in node.Nodes)
        {
            if (child.Text == name) yield return child;
            foreach (Wz_Node sub in FindAll(child, name)) yield return sub;
        }
    }
}
