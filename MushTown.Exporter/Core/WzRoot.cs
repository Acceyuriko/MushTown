using WzComparerR2.WzLib;

namespace MushTown.Exporter;

public static class ExporterPaths
{
    public const string DataDir = @"E:\game\maplestory\appdata\Data";
    public const string OutDir = "assets";
}

// Loads the merged KMST data tree. Base.wz is the container holding all category
// trees (Character/Map/Mob/...). Category .wz files are loaded separately later
// when we need their own content (e.g. equipment images).
public sealed class WzRoot
{
    public Wz_Structure Structure { get; }

    private WzRoot(Wz_Structure structure) => Structure = structure;

    public static WzRoot LoadBase(string dataDir)
    {
        var structure = new Wz_Structure();
        structure.LoadKMST1125DataWz(Path.Combine(dataDir, "Base", "Base.wz"));
        return new WzRoot(structure);
    }

    // Loads a category file directly (e.g. Map.wz) - some content lives only there.
    public static WzRoot LoadCategory(string dataDir, string category)
    {
        var structure = new Wz_Structure();
        structure.LoadKMST1125DataWz(Path.Combine(dataDir, category, category + ".wz"));
        return new WzRoot(structure);
    }

    // Paths may use '/' or '\' as separator.
    public Wz_Node? Find(string path) =>
        Structure.WzNode.FindNodeByPath(true, path.Split('/', '\\'));

    public IEnumerable<Wz_Node> FindAll(string name, bool partial = false) => FindAll(Structure.WzNode, name, partial);

    // Pre-order search by node name, for trees not wrapped in a WzRoot.
    public static IEnumerable<Wz_Node> FindAll(Wz_Node node, string name, bool partial = false) =>
        Walk(node).Where(n => partial
            ? n.Text.Contains(name, StringComparison.OrdinalIgnoreCase)
            : n.Text == name);

    // Every descendant of node, top-down. Used by FindAll.
    public static IEnumerable<Wz_Node> Walk(Wz_Node node)
    {
        foreach (Wz_Node child in node.Nodes)
        {
            yield return child;
            // the names inside a .img only exist after the image is parsed
            if (child.Value is Wz_Image img)
            {
                img.TryExtract();
                foreach (Wz_Node sub in Walk(img.Node)) yield return sub;
            }
            else
            {
                foreach (Wz_Node sub in Walk(child)) yield return sub;
            }
        }
    }
}
