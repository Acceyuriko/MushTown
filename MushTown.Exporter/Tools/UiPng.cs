using System.Diagnostics.CodeAnalysis;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Saves one canvas node as a .png file, for art review before a rework week.
// Usage: uipng <node-path> <out.png>            (UI tree)
//        uipng <Category> <node-path> <out.png>  (another category wz)
public static class UiPng
{
    public static bool Run(string nodePath, string outPath) =>
        Save(WzRoot.LoadCategory(ExporterPaths.DataDir, "UI"), "UI", nodePath, outPath);

    public static bool Run(string category, string nodePath, string outPath) =>
        Save(WzRoot.LoadCategory(ExporterPaths.DataDir, category), category, nodePath, outPath);

    // Same save against an already-loaded root, so export loads once.
    public static bool Save(WzRoot root, string nodePath, string outPath) =>
        Save(root, "UI", nodePath, outPath);

    public static bool Save(WzRoot root, string category, string nodePath, string outPath)
    {
        if (!TryRead(root, category, nodePath, out Wz_Png? png, out Wz_Vector? origin))
            return false;
        WzNodeUtil.SavePng(png, outPath);
        Console.WriteLine($"{outPath}: {png.Width}x{png.Height} origin={origin?.X},{origin?.Y}");
        return true;
    }

    // Same link walk without the save: exporters needing the origin read here.
    public static bool TryRead(WzRoot root, string category, string nodePath,
        [NotNullWhen(true)] out Wz_Png? png, out Wz_Vector? origin)
    {
        png = null;
        origin = null;
        Wz_Node? node = WzNodeUtil.ResolveUol(root.Find(nodePath));
        if (node == null)
        {
            Console.WriteLine($"node not found: {nodePath}");
            return false;
        }

        // Canvases are 1x1 placeholders; the real bytes live in _Canvas
        // behind an _outlink like "<Category>/_Canvas/...". Links are absolute
        // from the Data dir, but this root IS the category tree, so strip the
        // "<Category>/" prefix.
        origin = node.Nodes["origin"]?.Value as Wz_Vector;
        Wz_Node? target = node;
        while (target != null)
        {
            Wz_Node? inlink = target.Nodes["_inlink"];
            string? link = (target.Nodes["_outlink"]?.Value
                ?? target.Nodes["source"]?.Value
                ?? inlink?.Value) as string;
            if (link == null) break;
            target = inlink != null
                ? target.GetNodeWzImage()?.Node.FindNodeByPath(true, link.Split('/'))
                : root.Find(link.StartsWith(category + "/") ? link.Substring(category.Length + 1) : link);
            target = WzNodeUtil.ResolveUol(target);
        }
        png = target?.Value as Wz_Png;
        if (png == null)
        {
            Console.WriteLine($"no image at: {nodePath}");
            return false;
        }
        return true;
    }
}
