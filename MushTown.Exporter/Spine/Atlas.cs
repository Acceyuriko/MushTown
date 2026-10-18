namespace MushTown.Exporter;

// KMST atlas text: page blocks (name, size, filter, pma, scale) then region
// blocks. Regions use bounds/offsets/rotate-degrees (KMST) or
// xy/size/orig/offset/rotate-bool (official Spine); 18200 uses official.
// Written from the WZ bytes; only the rect/trim/rotate are needed.
public sealed class Atlas
{
    public sealed record Page(string Name, int W, int H, bool Pma);
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
            if (keys.ContainsKey("bounds") || keys.ContainsKey("xy"))
            {
                int x, y, w, h, ox = 0, oy = 0, ow, oh;
                if (keys.TryGetValue("bounds", out string? bstr))
                {
                    int[] b = Nums(bstr, 4);
                    x = b[0]; y = b[1]; w = b[2]; h = b[3]; ow = w; oh = h;
                    if (keys.TryGetValue("offsets", out string? off))
                    {
                        int[] o = Nums(off, 4);
                        ox = o[0]; oy = o[1]; ow = o[2]; oh = o[3];
                    }
                }
                else
                {
                    if (!keys.TryGetValue("xy", out string? xys) || !keys.TryGetValue("size", out string? sjs)
                        || !keys.TryGetValue("orig", out string? ogs) || !keys.TryGetValue("offset", out string? ofs))
                        throw new InvalidDataException($"atlas: region '{name}' lacks xy/size/orig/offset");
                    int[] p = Nums(xys, 2), s = Nums(sjs, 2);
                    int[] og = Nums(ogs, 2), of = Nums(ofs, 2);
                    x = p[0]; y = p[1]; w = s[0]; h = s[1]; ow = og[0]; oh = og[1];
                    ox = of[0]; oy = of[1];
                }
                int rot = 0;
                if (keys.TryGetValue("rotate", out string? r))
                    rot = r switch { "true" => 90, "false" => 0, _ => int.Parse(r) };
                if (rot is not (0 or 90 or 180 or 270))
                    throw new InvalidDataException($"atlas: region '{name}' rotates {rot}");
                atlas.Regions[name] = new Region(name, atlas.Pages.Count - 1,
                    x, y, w, h, ox, oy, ow, oh, rot);
            }
            else if (keys.TryGetValue("size", out string? size))
            {
                int[] wh = Nums(size, 2);
                // pma marks premultiplied pages; absent means straight alpha.
                atlas.Pages.Add(new Page(name, wh[0], wh[1], keys.TryGetValue("pma", out var pma) && pma == "true"));
            }
            else throw new InvalidDataException($"atlas: block '{name}' is neither page nor region");
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
