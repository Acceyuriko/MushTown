using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Reads real data out of WZ nodes with the KMST link tricks handled:
// a node can be a Wz_Uol, or carry a "source"/"_inlink"/"_outlink" string child
// that points at the real node somewhere else in the tree.
public static class WzNodeUtil
{
    // Follow Wz_Uol values and source/_inlink/_outlink string links.
    public static Wz_Node ResolveLink(Wz_Node node)
    {
        while (true)
        {
            if (node == null) return null;

            if (node.Value is Wz_Uol uol)
            {
                node = uol.HandleUol(node);
                continue;
            }

            string link = LinkPath(node);
            if (link == null) return node;

            Wz_Node target = null;
            if (node.Nodes["_inlink"] != null)
                target = node.GetNodeWzImage()?.Node.FindNodeByPath(true, link.Split('/'));
            else
                target = WzRoot.LastBase.Find(link);
            if (target == null) return null;
            node = target;
        }
    }

    private static string LinkPath(Wz_Node node)
    {
        foreach (string key in new[] { "source", "_inlink", "_outlink" })
        {
            var child = node.Nodes[key];
            if (child?.Value is string s && s.Length > 0)
                return s;
        }
        return null;
    }

    // A png node's draw metadata: origin (vec) and z, both optional.
    // origin/z live on the loose node; the png bytes may be behind an _outlink.
    public static (Wz_Png png, Wz_Vector? origin, int? z) ReadCanvas(Wz_Node node)
    {
        Wz_Vector? origin = node.Nodes["origin"]?.Value as Wz_Vector;
        int? z = node.Nodes["z"]?.GetValueEx<int?>(null);

        Wz_Node resolved = ResolveLink(node);
        if (resolved?.Value is not Wz_Png png) return (null, origin, z);
        return (png, origin, z);
    }

    // Saves a WZ png as a normal .png file with alpha.
    public static void SavePng(Wz_Png png, string filePath)
    {
        using var bitmap = png.ExtractPng();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }
}
