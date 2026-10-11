using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Ad-hoc structure dumper used while learning the WZ layout.
// Usage: dump <path-under-Base> [depth]
public static class Dump
{
    public static void Run(WzRoot root, string path, int depth)
    {
        if (string.IsNullOrEmpty(path))
        {
            Print(root.Structure.WzNode, depth, 0);
            return;
        }

        if (path.StartsWith("find "))
        {
            string name = path["find ".Length..];
            foreach (Wz_Node hit in root.FindAll(name))
            {
                Console.WriteLine($"{hit.FullPath} = {Describe(hit.Value)}");
            }
            return;
        }

        var node = root.Find(path);
        if (node == null)
        {
            Console.WriteLine($"NOT FOUND: {path}");
            return;
        }
        Print(node, depth, 0);
    }

    private static void Print(Wz_Node node, int maxDepth, int level)
    {
        string indent = new string(' ', level * 2);

        // Image nodes carry their content in img.Node after extraction.
        Wz_Node content = node.Value is Wz_Image img ? img.Node : node;

        Console.WriteLine($"{indent}{node.Text} = {Describe(node.Value)} ({content.Nodes.Count} children)");
        if (level >= maxDepth) return;

        foreach (Wz_Node child in content.Nodes)
        {
            Print(child, maxDepth, level + 1);
        }
    }

    private static string Describe(object? value) => value switch
    {
        null => "-",
        Wz_Image => "img",
        Wz_Png png => $"png {png.Width}x{png.Height} {png.Format}",
        Wz_Vector v => $"vec {v.X},{v.Y}",
        Wz_Uol u => $"uol {u}",
        Wz_Sound => "sound",
        Wz_Video => "video",
        _ => value.ToString() ?? "?"
    };
}
