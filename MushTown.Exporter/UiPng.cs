using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Saves one UI canvas node as a .png file, for art review before a rework week.
// Usage: uipng <node-path> <out.png>   (path like Login.img/Title_new/backgrd)
public static class UiPng
{
    public static bool Run(string nodePath, string outPath)
    {
        WzRoot root = WzRoot.LoadCategory(ExporterPaths.DataDir, "UI");
        Wz_Node? node = WzNodeUtil.ResolveUol(root.Find(nodePath));
        if (node == null)
        {
            Console.WriteLine($"node not found: {nodePath}");
            return false;
        }

        // UI canvases are 1x1 placeholders; the real bytes live in _Canvas
        // behind an _outlink like "UI/_Canvas/...". Links are absolute from the
        // Data dir, but this root IS the UI tree, so strip the "UI/" prefix.
        Wz_Vector? origin = node.Nodes["origin"]?.Value as Wz_Vector;
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
                : root.Find(link.StartsWith("UI/") ? link.Substring(3) : link);
            target = WzNodeUtil.ResolveUol(target);
        }
        Wz_Png? png = target?.Value as Wz_Png;
        if (png == null)
        {
            Console.WriteLine($"no image at: {nodePath}");
            return false;
        }
        WzNodeUtil.SavePng(png, outPath);
        Console.WriteLine($"{outPath}: {png.Width}x{png.Height} origin={origin?.X},{origin?.Y}");
        return true;
    }
}
