using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Exporter;

// Bakes a parsed 4.1 rig to plain per-frame meshes: bone poses sampled
// with curves, world matrices down the tree, skinned world verts, deform
// offsets, slot colors, sequence regions. All math here is standard 2D
// (compose, weighted blend, cubic Bezier); every stage was checked number
// by number against a posing black-box probe before shipping.
public sealed class RigBaker
{
    public sealed record FrameData(float[] Verts, float[] Color, int Page, float[]? Uvs);
    public sealed record SlotData(int Blend, int[] Tris, float[] Uvs, List<FrameData> Frames);
    public sealed record BakedAnim(float Fps, List<string> Pages, List<SlotData> Slots);

    public static BakedAnim Bake(byte[] skel, Atlas atlas)
    {
        var r = new SkelReader(skel);
        float fps = r.ReadHeader();
        var table = r.ReadStrings();
        var bones = r.ReadBones();
        var slots = r.ReadSlots(table);
        if (!float.IsFinite(fps) || fps <= 0)
            throw new InvalidDataException($"fps {fps} cannot bake");
        if (slots.Count == 0)
            throw new InvalidDataException("rig has no slots");
        r.ReadConstraints();
        var skins = r.ReadSkins(slots, table);
        r.ReadEvents();
        var anims = r.ReadAnims(table);
        if (anims.Count != 1)
            throw new InvalidDataException($"want 1 animation, got {anims.Count}");
        if (r.Position != r.Length)
            throw new InvalidDataException($"bake stopped at {r.Position}, file is {r.Length}");
        var anim = anims[0];
        if (anim.DrawOrderCount > 0)
            throw new InvalidDataException("draw order timelines are not baked");

        float duration = 0;
        foreach (var tls in anim.Slots.Values)
        {
            if (tls.Attach.Count > 0)
                throw new InvalidDataException("attachment timelines are not baked");
            foreach (var f in tls.Rgba) duration = Math.Max(duration, f.Time);
            foreach (var f in tls.Seq) duration = Math.Max(duration, f.Time);
        }
        foreach (var tls in anim.Bones.Values)
        {
            // Bone timeline ids: 0 rotate, 1 translate, 4 scale, 7 shear.
            foreach (int type in tls.One.Keys)
                if (type != 0)
                    throw new InvalidDataException($"single-component timeline {type} is not baked");
            foreach (int type in tls.Two.Keys)
                if (type is not (1 or 4 or 7))
                    throw new InvalidDataException($"two-component timeline {type} is not baked");
            foreach (var l in tls.One.Values) foreach (var f in l) duration = Math.Max(duration, f.Time);
            foreach (var l in tls.Two.Values) foreach (var f in l) duration = Math.Max(duration, f.Time);
        }
        foreach (var d in anim.Deforms) foreach (var f in d.Frames) duration = Math.Max(duration, f.Time);

        int frames = Math.Max(1, (int)Math.Round(duration * fps));
        var baked = new BakedAnim(fps,
            atlas.Pages.Select(p => p.Name).ToList(), new List<SlotData>(slots.Count));
        var skin = skins["default"];
        for (int s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            if (slot.Blend is not (0 or 1))
                throw new InvalidDataException($"slot {slot.Name} blends {slot.Blend}: want 0 or 1");
            if (slot.Dark != -1) // -1 is white: a real dark tint is two-color, not baked
                throw new InvalidDataException($"slot {slot.Name} has a dark tint");
            var mesh = slot.Attachment != null && skin.TryGetValue(slot.Name, out var atts)
                && atts.TryGetValue(slot.Attachment, out var o) ? o : null;
            var seq = mesh?.Seq;
            Atlas.Region? setupRegion = mesh == null ? null : RegionFor(atlas, mesh.Path, -1, seq);
            int pairs = mesh == null ? 0 : PairCount(mesh);
            var outFrames = new List<FrameData>(frames);
            for (int f = 0; f < frames; f++)
                outFrames.Add(BakeFrame(bones, slots, anim, s, mesh, pairs, seq, setupRegion,
                    atlas, duration * f / frames));
            float[] uvs = mesh == null ? Array.Empty<float>()
                : seq != null ? Array.Empty<float>() : PageUvs(mesh.Uvs, atlas, setupRegion!);
            baked.Slots.Add(new SlotData(slot.Blend,
                mesh?.Tris ?? Array.Empty<int>(), uvs, outFrames));
        }
        // The client fits normal-blend verts; without any there is nothing to show.
        if (!baked.Slots.Any(s => s.Blend == 0 && s.Frames.Any(f => f.Verts.Length > 0)))
            throw new InvalidDataException("rig has no normal-blend geometry");
        return baked;
    }

    private static FrameData BakeFrame(List<SkelReader.Bone> bones, List<SkelReader.Slot> slots,
        SkelReader.Anim anim, int slotIndex, SkelReader.MeshAtt? mesh, int pairs, int[]? seq,
        Atlas.Region? setupRegion, Atlas atlas, float t)
    {
        float[] verts = Array.Empty<float>();
        int page = setupRegion?.Page ?? 0;
        float[]? uvs = null;
        if (mesh != null)
        {
            float[] m = WorldMatrices(bones, anim, t);
            float[] deform = DeformAt(anim, slotIndex, mesh.Name, pairs * 2, t);
            verts = Skin(mesh, slots[slotIndex].Bone, m, deform);
            if (seq != null)
            {
                var region = RegionFor(atlas, mesh.Path, SeqIndexAt(anim, slotIndex, seq, t), seq);
                page = region.Page;
                uvs = PageUvs(mesh.Uvs, atlas, region);
            }
        }
        float[] color = ColorAt(slots[slotIndex], mesh, anim, slotIndex, t);
        return new FrameData(verts, color, page, uvs);
    }

    // Bone world matrices, 6 floats each (a, b, c, d, tx, ty). Bones come
    // parent-first, so one pass down the list composes the whole tree.
    // Timeline keys are offsets: translate/rotate/shear add to setup,
    // scale multiplies. Verified against probe poses number by number.
    private static float[] WorldMatrices(List<SkelReader.Bone> bones, SkelReader.Anim anim, float t)
    {
        float[] m = new float[bones.Count * 6];
        for (int i = 0; i < bones.Count; i++)
        {
            var b = bones[i];
            float x = b.X, y = b.Y, rot = b.Rot, sx = b.Sx, sy = b.Sy, shx = b.Shx, shy = b.Shy;
            if (anim.Bones.TryGetValue(i, out var tls))
            {
                // Bone timeline ids: 0 rotate, 1 translate, 4 scale, 7 shear.
                if (tls.Two.TryGetValue(1, out var tr) && Sample2(tr, t) is float[] p)
                { x += p[0]; y += p[1]; }
                if (tls.One.TryGetValue(0, out var ro) && Sample1(ro, t) is float r) rot += r;
                if (tls.Two.TryGetValue(4, out var sc) && Sample2(sc, t) is float[] q)
                { sx *= q[0]; sy *= q[1]; }
                if (tls.Two.TryGetValue(7, out var sh) && Sample2(sh, t) is float[] h)
                { shx += h[0]; shy += h[1]; }
            }
            double c = Math.Cos(rot * Math.PI / 180), s = Math.Sin(rot * Math.PI / 180);
            double shxC = Math.Cos(shx * Math.PI / 180), shxS = Math.Sin(shx * Math.PI / 180);
            double shyC = Math.Cos(shy * Math.PI / 180), shyS = Math.Sin(shy * Math.PI / 180);
            float la = (float)(sx * (c * shxC - s * shxS)), lb = (float)(-sy * (c * shyS + s * shyC));
            float lc = (float)(sx * (s * shxC + c * shxS)), ld = (float)(sy * (c * shyC - s * shyS));
            int o = i * 6;
            if (b.Parent < 0)
            {
                m[o] = la; m[o + 1] = lb; m[o + 2] = lc; m[o + 3] = ld; m[o + 4] = x; m[o + 5] = y;
            }
            else
            {
                int p = b.Parent * 6;
                m[o] = m[p] * la + m[p + 1] * lc;
                m[o + 1] = m[p] * lb + m[p + 1] * ld;
                m[o + 2] = m[p + 2] * la + m[p + 3] * lc;
                m[o + 3] = m[p + 2] * lb + m[p + 3] * ld;
                m[o + 4] = m[p] * x + m[p + 1] * y + m[p + 4];
                m[o + 5] = m[p + 2] * x + m[p + 3] * y + m[p + 5];
            }
        }
        return m;
    }

    // Mesh verts to world space. Unweighted data is raw xy pairs; weighted
    // data is per-vert counts of bone/x/y/weight quads. Deform offsets add
    // to each influence point in packed pair order before its transform.
    private static float[] Skin(SkelReader.MeshAtt mesh, int slotBone, float[] m, float[] deform)
    {
        int vCount = mesh.Uvs.Length / 2;
        float[] world = new float[vCount * 2];
        float[] data = mesh.Verts;
        if (data.Length == vCount * 2)
        {
            int b = slotBone * 6;
            for (int v = 0; v < vCount; v++)
            {
                float x = data[v * 2] + deform[v * 2], y = data[v * 2 + 1] + deform[v * 2 + 1];
                world[v * 2] = m[b] * x + m[b + 1] * y + m[b + 4];
                world[v * 2 + 1] = m[b + 2] * x + m[b + 3] * y + m[b + 5];
            }
            return world;
        }
        int p = 0, j = 0;
        for (int v = 0; v < vCount; v++)
        {
            float wx = 0, wy = 0;
            int n = (int)data[p++];
            for (int k = 0; k < n; k++, j++)
            {
                int b = (int)data[p++] * 6;
                float x = data[p++] + deform[j * 2], y = data[p++] + deform[j * 2 + 1], w = data[p++];
                wx += (m[b] * x + m[b + 1] * y + m[b + 4]) * w;
                wy += (m[b + 2] * x + m[b + 3] * y + m[b + 5]) * w;
            }
            world[v * 2] = wx; world[v * 2 + 1] = wy;
        }
        return world;
    }

    // Influence pair count: verts when unweighted, quads when weighted.
    private static int PairCount(SkelReader.MeshAtt mesh)
    {
        int vCount = mesh.Uvs.Length / 2;
        if (mesh.Verts.Length == vCount * 2) return vCount;
        int q = 0, p = 0;
        for (int v = 0; v < vCount; v++)
        {
            int n = (int)mesh.Verts[p++];
            q += n; p += n * 4;
        }
        return q;
    }

    private static float[] DeformAt(SkelReader.Anim anim, int slot, string attach, int len, float t)
    {
        var zero = new float[len];
        SkelReader.FrameDeform? prev = null, next = null;
        foreach (var d in anim.Deforms)
        {
            if (d.Slot != slot || d.Attach != attach) continue;
            foreach (var f in d.Frames)
            {
                if (f.Time <= t) prev = f;
                if (f.Time > t && next == null) next = f;
            }
        }
        if (prev == null) return zero;
        float[] a = Overlay(len, prev), b = next == null ? a : Overlay(len, next);
        if (next == null || prev.Curve?.Length == 0) return a;
        float pct = prev.Curve == null ? (t - prev.Time) / (next.Time - prev.Time)
            : BezierVal(prev.Time, 0, next.Time, 1, prev.Curve, t);
        for (int i = 0; i < len; i++) a[i] += (b[i] - a[i]) * pct;
        return a;
    }

    private static float[] Overlay(int len, SkelReader.FrameDeform f)
    {
        var a = new float[len];
        if (f.Start < 0 || f.Start + f.Verts.Length > len)
            throw new InvalidDataException($"deform [{f.Start}..{f.Start + f.Verts.Length}) overflows {len} verts");
        Array.Copy(f.Verts, 0, a, f.Start, f.Verts.Length);
        return a;
    }

    // Final vertex color: attachment tint times the slot part. The rgba
    // timeline replaces the slot setup color; without one setup shows.
    private static float[] ColorAt(SkelReader.Slot slot, SkelReader.MeshAtt? mesh,
        SkelReader.Anim anim, int slotIndex, float t)
    {
        const int white = unchecked((int)0xFFFFFFFF); // slot without attachment: untinted
        float[] c = Rgba(slot.Color), m = Rgba(mesh?.Color ?? white);
        float r = c[0] * m[0], g = c[1] * m[1], b = c[2] * m[2], a = c[3] * m[3];
        if (anim.Slots.TryGetValue(slotIndex, out var tls) && tls.Rgba.Count > 0)
        {
            var fr = tls.Rgba;
            int i = fr.Count - 1;
            while (i > 0 && fr[i].Time > t) i--;
            if (t >= fr[0].Time)
            {
                if (i == fr.Count - 1 || fr[i].Curve?.Length == 0)
                {
                    r = fr[i].R * m[0]; g = fr[i].G * m[1]; b = fr[i].B * m[2]; a = fr[i].A * m[3];
                }
                else
                {
                    float pct = fr[i].Curve == null ? (t - fr[i].Time) / (fr[i + 1].Time - fr[i].Time)
                        : BezierVal(fr[i].Time, 0, fr[i + 1].Time, 1, fr[i].Curve!, t);
                    r = (fr[i].R + (fr[i + 1].R - fr[i].R) * pct) * m[0];
                    g = (fr[i].G + (fr[i + 1].G - fr[i].G) * pct) * m[1];
                    b = (fr[i].B + (fr[i + 1].B - fr[i].B) * pct) * m[2];
                    a = (fr[i].A + (fr[i + 1].A - fr[i].A) * pct) * m[3];
                }
            }
        }
        return new float[] { r, g, b, a };
    }

    private static float[] Rgba(int c) => new float[]
        { (c >> 24 & 0xFF) / 255f, (c >> 16 & 0xFF) / 255f, (c >> 8 & 0xFF) / 255f, (c & 0xFF) / 255f };

    // Samples the key value at t, or null before the first frame (the
    // caller then keeps setup). Stepped holds the key; bezier bends time.
    private static float? Sample1(List<SkelReader.Frame1> fr, float t)
    {
        int i = fr.Count - 1;
        while (i > 0 && fr[i].Time > t) i--;
        if (t < fr[0].Time) return null;
        if (i == fr.Count - 1 || fr[i].Curve?.Length == 0) return fr[i].Value;
        if (fr[i].Curve == null) return fr[i].Value + (fr[i + 1].Value - fr[i].Value) * ((t - fr[i].Time) / (fr[i + 1].Time - fr[i].Time));
        return BezierVal(fr[i].Time, fr[i].Value, fr[i + 1].Time, fr[i + 1].Value, fr[i].Curve!, t);
    }

    private static float[]? Sample2(List<SkelReader.Frame2> fr, float t)
    {
        int i = fr.Count - 1;
        while (i > 0 && fr[i].Time > t) i--;
        if (t < fr[0].Time) return null;
        if (i == fr.Count - 1) return new float[] { fr[i].X, fr[i].Y };
        float lin = (t - fr[i].Time) / (fr[i + 1].Time - fr[i].Time);
        float vx = fr[i].CurveX?.Length == 0 ? fr[i].X : fr[i].CurveX == null
            ? fr[i].X + (fr[i + 1].X - fr[i].X) * lin
            : BezierVal(fr[i].Time, fr[i].X, fr[i + 1].Time, fr[i + 1].X, fr[i].CurveX!, t);
        float vy = fr[i].CurveY?.Length == 0 ? fr[i].Y : fr[i].CurveY == null
            ? fr[i].Y + (fr[i + 1].Y - fr[i].Y) * lin
            : BezierVal(fr[i].Time, fr[i].Y, fr[i + 1].Time, fr[i + 1].Y, fr[i].CurveY!, t);
        return new float[] { vx, vy };
    }

    // Cubic Bezier sampled through a 10-segment linearized table, uniform
    // in s: matches the posing probe far better than the true cubic.
    private static float BezierVal(float t0, float v0, float t1, float v1, float[] c, float t)
    {
        float px = t0, py = v0;
        for (int j = 1; j <= 10; j++)
        {
            float x = Bez(t0, c[0], c[2], t1, j / 10f), y = Bez(v0, c[1], c[3], v1, j / 10f);
            if (t <= x) return py + (y - py) * ((t - px) / (x - px));
            px = x; py = y;
        }
        return v1;
    }

    private static float Bez(float p0, float p1, float p2, float p3, float s)
    {
        float u = 1 - s;
        return u * u * u * p0 + 3 * u * u * s * p1 + 3 * u * s * s * p2 + s * s * s * p3;
    }

    // Active sequence region index at time t: the last frame at or before t
    // gives a base index, advancing one region per delay. Absolute counters
    // wrap around the region count.
    private static int SeqIndexAt(SkelReader.Anim anim, int slot, int[] seq, float t)
    {
        if (!anim.Slots.TryGetValue(slot, out var tls) || tls.Seq.Count == 0)
            return seq[2] - seq[0]; // no timeline: the setup frame shows
        var fr = tls.Seq;
        if (t < fr[0].Time) return seq[2] - seq[0]; // before the first key: setup shows
        int i = fr.Count - 1;
        while (i > 0 && fr[i].Time > t) i--;
        int start = seq[0], count = seq[1];
        int abs = fr[i].Index + (fr[i].Delay > 0 ? (int)((t - fr[i].Time) / fr[i].Delay) : 0);
        return ((abs - start) % count + count) % count;
    }

    private static Atlas.Region RegionFor(Atlas atlas, string path, int seqIndex, int[]? seq)
    {
        if (seqIndex < 0 && seq != null) seqIndex = seq[2] - seq[0]; // setup frame
        string name = seq == null || seqIndex < 0 ? path
            : path + (seqIndex + seq[0]).ToString().PadLeft(seq[3], '0');
        if (!atlas.Regions.TryGetValue(name, out var region))
            throw new InvalidDataException($"atlas has no region '{name}'");
        return region;
    }

    // File uvs live in orig-canvas space (y up); the region maps them onto
    // its atlas page. Per-rotation formulas verified number by number.
    private static float[] PageUvs(float[] file, Atlas atlas, Atlas.Region r)
    {
        var page = atlas.Pages[r.Page];
        float[] uv = new float[file.Length];
        for (int i = 0; i < file.Length; i += 2)
        {
            float cx = file[i] * r.OrigW, cy = file[i + 1] * r.OrigH;
            float tx = cx - r.Ox, tyU = cy - (r.OrigH - r.Oy - r.H);
            float tyD = (r.OrigH - r.Oy) - cy, txR = r.W - tx;
            float px = r.Rot switch
            {
                90 => r.X + tyU,
                180 => r.X + txR,
                270 => r.X + tyD,
                _ => r.X + tx,
            };
            float py = r.Rot switch
            {
                90 => r.Y + txR,
                180 => r.Y + tyD,
                270 => r.Y + tx,
                _ => r.Y + tyU,
            };
            uv[i] = px / page.W; uv[i + 1] = py / page.H;
        }
        return uv;
    }

    public static string ToJson(BakedAnim anim)
    {
        var o = new SpineData
        {
            Fps = anim.Fps,
            Pages = anim.Pages,
            Slots = anim.Slots.Select(s => new SpineSlot
            {
                Blend = s.Blend,
                Tris = s.Tris,
                Uvs = Round(s.Uvs, 5),
                Frames = s.Frames.Select(f => new SpineFrame
                {
                    V = Round(f.Verts, 2),
                    C = Round(f.Color, 3),
                    P = f.Page,
                    Uv = f.Uvs == null ? null : Round(f.Uvs, 5),
                }).ToList(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(o);
    }

    // Digits: verts land on screen pixels, uv and color keep more.
    private static float[] Round(float[] a, int digits)
    {
        float[] o = new float[a.Length];
        for (int i = 0; i < a.Length; i++) o[i] = (float)Math.Round(a[i], digits);
        return o;
    }
}
