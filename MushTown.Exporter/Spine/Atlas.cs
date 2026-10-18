namespace MushTown.Exporter;

// KMST atlas text: page blocks (name, size, filter, pma, scale) then region
// blocks (name, bounds, optional offsets, optional rotate in degrees).
// Written from the WZ bytes; only size/bounds/offsets/rotate are needed.
public sealed class Atlas
{
    public sealed record Page(string Name, int W, int H);
    public sealed record Region(string Name, int Page, int X, int Y, int W, int H,
        int Ox, int Oy, int OrigW, int OrigH, int Rot);

    public List<Page> Pages { get; } = new();
    public Dictionary<string, Region> Regions { get; } = new();

    public static Atlas Parse(string text)
    {
        var atlas = new Atlas();
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        int i = 0;
        while (i < lines.Count)
        {
            string name = lines[i++];
            if (name.Contains(':'))
                throw new InvalidDataException($"atlas: want a name, got '{name}'");
            var keys = new Dictionary<string, string>();
            while (i < lines.Count && lines[i].Contains(':'))
            {
                int c = lines[i].IndexOf(':');
                keys[lines[i][..c]] = lines[i][(c + 1)..].Trim();
                i++;
            }
            if (keys.TryGetValue("size", out string? size))
            {
                int[] wh = Nums(size, 2);
                atlas.Pages.Add(new Page(name, wh[0], wh[1]));
            }
            else
            {
                if (!keys.TryGetValue("bounds", out string? bstr))
                    throw new InvalidDataException($"atlas: region '{name}' has no bounds");
                int[] b = Nums(bstr, 4);
                int ox = 0, oy = 0, ow = b[2], oh = b[3], rot = 0;
                if (keys.TryGetValue("offsets", out string? off))
                {
                    int[] o = Nums(off, 4);
                    ox = o[0]; oy = o[1]; ow = o[2]; oh = o[3];
                }
                if (keys.TryGetValue("rotate", out string? r)) rot = int.Parse(r);
                if (rot is not (0 or 90 or 180 or 270))
                    throw new InvalidDataException($"atlas: region '{name}' rotates {rot}");
                atlas.Regions[name] = new Region(name, atlas.Pages.Count - 1,
                    b[0], b[1], b[2], b[3], ox, oy, ow, oh, rot);
            }
        }
        return atlas;
    }

    private static int[] Nums(string s, int n)
    {
        int[] v = s.Split(',').Select(int.Parse).ToArray();
        if (v.Length != n) throw new InvalidDataException($"atlas: want {n} numbers, got '{s}'");
        return v;
    }
}
