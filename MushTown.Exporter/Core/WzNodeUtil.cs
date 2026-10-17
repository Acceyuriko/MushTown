using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Reads real data out of WZ nodes with the KMST link tricks handled:
// a node can be a Wz_Uol (often relative, like ../../front/head), or carry a
// "source"/"_inlink"/"_outlink" string child that points at the real node.
public static class WzNodeUtil
{
    // Follow Wz_Uol values.
    public static Wz_Node? ResolveUol(Wz_Node? node)
    {
        while (node?.Value is Wz_Uol uol)
            node = uol.HandleUol(node);
        return node;
    }

    // Follow Wz_Uol and source/_inlink/_outlink links to the node holding the png.
    // Links are resolved against the Base tree (or the current image for _inlink).
    public static Wz_Node? ResolveLink(WzRoot baseRoot, Wz_Node? node)
    {
        while (true)
        {
            node = ResolveUol(node);
            if (node == null) return null;

            string? link = LinkPath(node);
            if (link == null) return node;

            Wz_Node? target = node.Nodes["_inlink"] != null
                ? node.GetNodeWzImage()?.Node.FindNodeByPath(true, link.Split('/'))
                : baseRoot.Find(link);

            if (target == null) return null;
            node = target;
        }
    }

    private static readonly string[] LinkKeys = new[] { "source", "_inlink", "_outlink" };

    private static string? LinkPath(Wz_Node node)
    {
        foreach (string key in LinkKeys)
        {
            var child = node.Nodes[key];
            if (child?.Value is string s && s.Length > 0)
                return s;
        }
        return null;
    }

    // A canvas node's draw metadata: origin (vec), optional.
    // origin lives on the loose node; the png bytes may be behind an _outlink.
    public static (Wz_Png? png, Wz_Vector? origin) ReadCanvas(WzRoot baseRoot, Wz_Node? node)
    {
        node = ResolveUol(node);
        if (node == null) return (null, null);

        Wz_Vector? origin = node.Nodes["origin"]?.Value as Wz_Vector;
        return (ResolveLink(baseRoot, node)?.Value as Wz_Png, origin);
    }

    // Saves a WZ png as a normal .png file with alpha.
    public static void SavePng(Wz_Png png, string filePath)
    {
        using var bitmap = png.ExtractPng();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }
}
