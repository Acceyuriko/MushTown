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

    // Prints every node whose name matches, with its full path.
    public static bool Find(WzRoot root, string name, bool partial = false)
    {
        bool found = false;
        foreach (Wz_Node hit in root.FindAll(name, partial))
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

    // Reads a .skel file off disk and prints what the shared reader makes of
    // it: bones, slots, skins and every anim timeline. The bake reads rigs the
    // same way, so this is exactly what gets baked.
    public static bool Spine(string file, int boneDetail = -1)
    {
        var rig = SkelReader.Read(File.ReadAllBytes(file));
        Console.WriteLine($"fps {rig.Fps}, bones {rig.Bones.Count}, slots {rig.Slots.Count}, anims {rig.Anims.Count}");
        for (int i = 0; i < rig.Bones.Count; i++)
        {
            var b = rig.Bones[i];
            Console.WriteLine($"  bone{i} {b.Name} parent={b.Parent} x={b.X:F1} y={b.Y:F1} rot={b.Rot:F1} sx={b.Sx:F3} sy={b.Sy:F3} shx={b.Shx} shy={b.Shy} len={b.Len:F1}");
        }
        for (int i = 0; i < rig.Slots.Count; i++)
        {
            var s = rig.Slots[i];
            Console.WriteLine($"  slot{i} {s.Name} bone={rig.Bones[s.Bone].Name} att={s.Attachment} color={s.Color:X8} dark={s.Dark:X8} blend={s.Blend}");
        }
        foreach (var (skin, bySlot) in rig.Skins)
        {
            Console.WriteLine($"skin {skin}: {bySlot.Count} slots, {bySlot.Values.Sum(a => a.Count)} attachments");
            foreach (var (slot, atts) in bySlot)
                foreach (var (name, att) in atts)
                    Console.WriteLine($"  {slot}/{name} {DescribeAtt(att)}");
        }
        foreach (var a in rig.Anims)
        {
            int tls = a.Slots.Values.Sum(s => (s.Attach.Count > 0 ? 1 : 0) + (s.Rgba.Count > 0 ? 1 : 0) + (s.Seq.Count > 0 ? 1 : 0))
                + a.Bones.Values.Sum(b => b.One.Count + b.Two.Count)
                + a.Deforms.Count + a.DrawOrderCount + a.EventCount;
            Console.WriteLine($"anim {a.Name}: {tls} timelines, slots {a.Slots.Count}, bones {a.Bones.Count}, deform {a.Deforms.Count}, draw {a.DrawOrderCount}, events {a.EventCount}, constraint end {a.ConstraintEnd}");
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
        return true;
    }

    private static string DescribeAtt(SkelReader.SkinAtt att)
    {
        if (att is SkelReader.RegionAtt reg)
            return $"region path={reg.Path} x={reg.X:F1} y={reg.Y:F1} rot={reg.Rot:F1} sx={reg.Sx:F3} sy={reg.Sy:F3} w={reg.W:F0} h={reg.H:F0} color={reg.Color:X8} seq={FmtSeq(reg.Seq)}";
        if (att is SkelReader.NoDrawAtt)
            return "no-draw (bounding box, path, point or clipping)";
        var mesh = (SkelReader.MeshAtt)att;
        string weight = "";
        if (mesh.Verts.Length != mesh.Uvs.Length)
        {
            float min = 99, max = -99;
            int p = 0, vCount = mesh.Uvs.Length / 2;
            for (int v = 0; v < vCount; v++)
            {
                float s = 0;
                int n = (int)mesh.Verts[p++];
                for (int k = 0; k < n; k++) { p++; p++; p++; s += mesh.Verts[p++]; }
                min = Math.Min(min, s); max = Math.Max(max, s);
            }
            weight = $" wsum=[{min:F4},{max:F4}]";
            int q = 0, mx = 0;
            p = 0;
            var first = new List<int>();
            for (int v = 0; v < vCount; v++)
            {
                int n = (int)mesh.Verts[p++];
                if (v < 4) first.Add(n);
                mx = Math.Max(mx, n);
                q += n; p += n * 4;
            }
            weight += $" quads={q} maxn={mx} first=[{string.Join(",", first)}]";
        }
        return $"mesh path={mesh.Path} verts={mesh.Verts.Length} uvs={mesh.Uvs.Length} tris={mesh.Tris.Length} uv0={mesh.Uvs[0]:F4},{mesh.Uvs[1]:F4} uv1={mesh.Uvs[2]:F4},{mesh.Uvs[3]:F4} color={mesh.Color:X8} seq={FmtSeq(mesh.Seq)}{weight}";
    }

    private static string FmtSeq(SkelReader.SeqAtt? seq) =>
        seq == null ? "-" : $"{seq.Count}@{seq.Start}+{seq.Setup}/{seq.Digits}";
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
