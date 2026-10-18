using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Ad-hoc structure dumper used while learning the WZ layout.
// Serves the dump/catdump/find/cat/raw/spine debug verbs (see Program.cs).
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

    // Prints a string node's full value (atlas text runs thousands of chars).
    public static bool Cat(WzRoot root, string path)
    {
        var node = root.Find(path);
        if (node?.Value is not string s)
        {
            Console.WriteLine($"NOT A STRING: {path}");
            return false;
        }
        Console.WriteLine(s);
        return true;
    }

    // Saves a Wz_RawData node's bytes (Spine skeletons) to a file.
    public static bool Raw(WzRoot root, string path, string outPath)
    {
        var node = root.Find(path);
        if (node?.Value is not Wz_RawData raw)
        {
            Console.WriteLine($"NOT RAW DATA: {path}");
            return false;
        }
        var buf = new byte[raw.Length];
        raw.CopyTo(buf);
        File.WriteAllBytes(outPath, buf);
        Console.WriteLine($"wrote {raw.Length} bytes to {outPath}");
        return true;
    }

    // Reads a .skel file off disk and prints its header, string table,
    // bones, slots, skins, and full anim timelines. Fails loud on any
    // divergence so the 4.1 layout gets pinned.
    public static bool Spine(string file, int boneDetail = -1)
    {
        var r = new SkelReader(File.ReadAllBytes(file));
        Console.WriteLine($"hash {r.ReadInt32():X8}{r.ReadInt32():X8}");
        Console.WriteLine($"version {r.ReadString()}");
        Console.WriteLine($"frame x={r.ReadFloat()} y={r.ReadFloat()} w={r.ReadFloat()} h={r.ReadFloat()}");
        bool nonessential = r.ReadByte() != 0;
        Console.WriteLine($"nonessential {nonessential} fps {r.ReadFloat()} images {r.ReadString()} audio {r.ReadString()}");
        int sn = r.ReadVarint(true);
        var table = new List<string>(sn);
        for (int i = 0; i < sn; i++) table.Add(r.ReadString() ?? "");
        Console.WriteLine($"strings {sn}: {string.Join(" ", table)}");
        var bones = r.ReadBones();
        Console.WriteLine($"bones {bones.Count}, next offset {r.Position}");
        var slots = r.ReadSlots(table);
        Console.WriteLine($"slots {slots.Count}, next offset {r.Position}");
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            Console.WriteLine($"  slot{i} {s.Name} bone={s.Bone} att={s.Attachment} color={s.Color:X8} dark={s.Dark:X8} blend={s.Blend}");
        }
        for (int i = 0; i < bones.Count; i++)
            if (bones[i].Shx != 0 || bones[i].Shy != 0)
                Console.WriteLine($"  shear-setup b{i}: {bones[i].Shx},{bones[i].Shy}");
        r.ReadConstraints();
        Console.WriteLine($"no constraints, skins at {r.Position}");
        var skins = r.ReadSkins(slots, table);
        int total = skins.Values.Sum(s => s.Values.Sum(a => a.Count));
        Console.WriteLine($"skins {skins.Count}, attachments {total}, next offset {r.Position}");
        foreach (var (slot, atts) in skins["default"])
            foreach (var (name, att) in atts)
            {
                string weight = "";
                if (att.Verts.Length != att.Uvs.Length)
                {
                    float min = 99, max = -99;
                    int p = 0, vCount = att.Uvs.Length / 2;
                    for (int v = 0; v < vCount; v++)
                    {
                        float s = 0;
                        int n = (int)att.Verts[p++];
                        for (int k = 0; k < n; k++) { p++; p++; p++; s += att.Verts[p++]; }
                        min = Math.Min(min, s); max = Math.Max(max, s);
                    }
                    weight = $" wsum=[{min:F4},{max:F4}]";
                    int q = 0, mx = 0;
                    p = 0;
                    var first = new List<int>();
                    for (int v = 0; v < vCount; v++)
                    {
                        int n = (int)att.Verts[p++];
                        if (v < 4) first.Add(n);
                        mx = Math.Max(mx, n);
                        q += n; p += n * 4;
                    }
                    weight += $" quads={q} maxn={mx} first=[{string.Join(",", first)}]";
                }
                Console.WriteLine($"  {slot}/{name} path={att.Path} verts={att.Verts.Length} uvs={att.Uvs.Length} tris={att.Tris.Length} uv0={att.Uvs[0]:F4},{att.Uvs[1]:F4} uv1={att.Uvs[2]:F4},{att.Uvs[3]:F4} color={att.Color:X8}{weight}");
            }
        r.ReadEvents();
        Console.WriteLine($"no events, anims at {r.Position}");
        var anims = r.ReadAnims(table);
        foreach (var a in anims)
        {
            int tls = a.Slots.Values.Sum(s => (s.Attach.Count > 0 ? 1 : 0) + (s.Rgba.Count > 0 ? 1 : 0) + (s.Seq.Count > 0 ? 1 : 0))
                + a.Bones.Values.Sum(b => b.One.Count + b.Two.Count)
                + a.Deforms.Count + a.DrawOrderCount + a.EventCount;
            int rot = a.Bones.Values.Sum(b => b.One.ContainsKey(0) ? 1 : 0);
            int tra = a.Bones.Values.Sum(b => b.Two.ContainsKey(1) ? 1 : 0);
            int scl = a.Bones.Values.Sum(b => b.Two.ContainsKey(4) ? 1 : 0);
            int shr = a.Bones.Values.Sum(b => b.Two.ContainsKey(7) ? 1 : 0);
            int rgba = a.Slots.Values.Sum(s => s.Rgba.Count > 0 ? 1 : 0);
            int seq = a.Slots.Values.Sum(s => s.Seq.Count > 0 ? 1 : 0);
            int rbez = a.Slots.Values.Sum(s => s.Rgba.Count(f => f.Curve != null && f.Curve.Length > 0));
            int dbez = a.Deforms.Sum(d => d.Frames.Count(f => f.Curve != null && f.Curve.Length > 0));
            Console.WriteLine($"anim {a.Name}: {tls} timelines, slots {a.Slots.Count}, bones {a.Bones.Count}, deform {a.Deforms.Count}, draw {a.DrawOrderCount}, events {a.EventCount}");
            Console.WriteLine($"  rotate {rot}, translate {tra}, scale {scl}, shear {shr}, rgba {rgba}, seq {seq}, rgbaBez {rbez}, deformBez {dbez}");
            foreach (var (slot, st) in a.Slots)
            {
                foreach (var f in st.Seq)
                    Console.WriteLine($"  seq slot{slot}: t={f.Time} index={f.Index} delay={f.Delay}");
                if (st.Rgba.Count > 0)
                    Console.WriteLine($"  rgba slot{slot}: n={st.Rgba.Count} first=({st.Rgba[0].Time:F3},{st.Rgba[0].R:F3},{st.Rgba[0].G:F3},{st.Rgba[0].B:F3},{st.Rgba[0].A:F3}) last=({st.Rgba[^1].Time:F3},{st.Rgba[^1].R:F3},{st.Rgba[^1].G:F3},{st.Rgba[^1].B:F3},{st.Rgba[^1].A:F3})");
            }
            foreach (var d in a.Deforms)
                Console.WriteLine($"  deform slot{d.Slot} {d.Attach}: n={d.Frames.Count} " + string.Join(" ", d.Frames.Select(f => $"({f.Time:F3},s{f.Start},l{f.Verts.Length},{FmtCurve(f.Curve)})")));
            foreach (var (bone, bt) in a.Bones.OrderBy(b => b.Key))
            {
                foreach (var (type, fr) in bt.One.OrderBy(t => t.Key))
                    Console.WriteLine($"  bone{type} b{bone}: n={fr.Count} first=({fr[0].Time:F3},{fr[0].Value:F3}) last=({fr[^1].Time:F3},{fr[^1].Value:F3})");
                foreach (var (type, fr) in bt.Two.OrderBy(t => t.Key))
                    Console.WriteLine($"  bone{type} b{bone}: n={fr.Count} first=({fr[0].Time:F3},{fr[0].X:F3},{fr[0].Y:F3}) last=({fr[^1].Time:F3},{fr[^1].X:F3},{fr[^1].Y:F3})");
                if (bone == boneDetail)
                {
                    foreach (var (type, fr) in bt.One.OrderBy(t => t.Key))
                        foreach (var f in fr)
                            Console.WriteLine($"    t1.{type} t={f.Time:F4} v={f.Value:F4} c={FmtCurve(f.Curve)}");
                    foreach (var (type, fr) in bt.Two.OrderBy(t => t.Key))
                        foreach (var f in fr)
                            Console.WriteLine($"    t2.{type} t={f.Time:F4} x={f.X:F4} y={f.Y:F4} cx={FmtCurve(f.CurveX)} cy={FmtCurve(f.CurveY)}");
                }
            }
        }
        Console.WriteLine($"end offset {r.Position}");
        return true;
    }

    private static string FmtCurve(float[]? c) =>
        c == null ? "lin" : c.Length == 0 ? "step" : string.Join(",", c.Select(v => v.ToString("F4")));

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
