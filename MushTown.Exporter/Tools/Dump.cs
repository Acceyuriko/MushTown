using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Ad-hoc structure dumper used while learning the WZ layout.
// Usage: dump <path-under-Base> [depth]
public static class Dump
{
    public static bool Run(WzRoot root, string path, int depth)
    {
        if (string.IsNullOrEmpty(path))
        {
            Print(root.Structure.WzNode, depth, 0);
            return true;
        }

        var node = root.Find(path);
        if (node == null)
        {
            Console.WriteLine($"NOT FOUND: {path}");
            return false;
        }
        Print(node, depth, 0);
        return true;
    }

    // Prints every node under Base whose name matches, with its full path.
    public static bool Find(WzRoot root, string name)
    {
        bool found = false;
        foreach (Wz_Node hit in root.FindAll(name))
        {
            Console.WriteLine($"{hit.FullPath} = {Describe(hit.Value)}");
            found = true;
        }
        if (!found) Console.WriteLine($"NOT FOUND: {name}");
        return found;
    }

    private static void Print(Wz_Node node, int maxDepth, int level)
    {
        string indent = new string(' ', level * 2);

        // Image nodes carry their content in img.Node; extracting it fills the children in.
        Wz_Node content = node;
        if (node.Value is Wz_Image img)
        {
            img.TryExtract();
            content = img.Node;
        }

        Console.WriteLine($"{indent}{node.Text} = {Describe(node.Value)} ({content.Nodes.Count} children)");
        if (level >= maxDepth) return;

        foreach (Wz_Node child in content.Nodes)
        {
            Print(child, maxDepth, level + 1);
        }
    }

    // Raw blobs (Spine skeletons) start with plain text: show the head so the
    // format and version are readable from dump output.
    private static string Peek(Wz_RawData raw)
    {
        var buf = new byte[raw.Length]; // CopyTo fills the whole blob, no partial reads
        raw.CopyTo(buf);
        return new string(buf.Take(28).Select(b => b is >= 32 and < 127 ? (char)b : '.').ToArray());
    }

    private static string Describe(object? value) => value switch
    {
        null => "-",
        Wz_Image => "img",
        Wz_Png png => $"png {png.Width}x{png.Height} {png.Format}",
        Wz_Vector v => $"vec {v.X},{v.Y}",
        Wz_Uol u => $"uol {u.Uol}",
        Wz_Sound s => $"sound {s.SoundType} {s.Ms}ms {s.DataLength}b",
        Wz_Video => "video",
        Wz_RawData r => $"raw {r.Length}b [{Peek(r)}]",
        string s => $"[str {s.Length}] {s[..Math.Min(50, s.Length)].Replace('\n', ' ')}",
        _ => value.ToString() ?? "?"
    };
}
