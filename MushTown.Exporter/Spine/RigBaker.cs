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
    public sealed record BakedAnim(float Fps, List<string> Pages, List<SlotData> Slots,
        float Ox, float Oy, bool Pma);

    public static BakedAnim Bake(SkelReader.Rig rig, Atlas atlas, float ox, float oy, float scale)
    {
        float fps = rig.Fps;
        var bones = rig.Bones;
        var slots = rig.Slots;
        var skins = rig.Skins;
        var anims = rig.Anims;
        if (anims.Count != 1)
            throw new InvalidDataException($"want 1 animation, got {anims.Count}");
        var anim = anims[0];
        // Draw order timelines only reorder layers (class 220). The bake
        // draws slots in setup order, which is what the splash shows except
        // while the timeline runs.
        float duration = anim.ConstraintEnd;
        foreach (var tls in anim.Slots.Values)
        {
            foreach (var f in tls.Attach) duration = Math.Max(duration, f.Time);
            foreach (var f in tls.Rgba) duration = Math.Max(duration, f.Time);
            foreach (var f in tls.Seq) duration = Math.Max(duration, f.Time);
        }
        foreach (var tls in anim.Bones.Values)
        {
            // Bone timeline ids: 0/2/3 rotate, translate x/y; 5/6 scale x/y;
            // 8/9 shear x/y; 1/4/7 the two-component versions.
            foreach (int type in tls.One.Keys)
                if (type is not (0 or 2 or 3 or 5 or 6 or 8 or 9))
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
            atlas.Pages.Select(p => p.Name).ToList(), new List<SlotData>(slots.Count),
            ox, oy, atlas.Pages[0].Pma);
        var skin = skins["default"];
        for (int s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            if (slot.Blend is not (0 or 1 or 2 or 3))
                throw new InvalidDataException($"slot {slot.Name} blends {slot.Blend}: want 0..3");
            if (slot.Dark != -1) // -1 is white: a real dark tint is two-color, not baked
                throw new InvalidDataException($"slot {slot.Name} has a dark tint");
            var shown = ShownAttachments(slot, anim, s);
            var atts = new List<SkelReader.SkinAtt?>(shown.Count);
            foreach (string name in shown)
                atts.Add(skin.TryGetValue(slot.Name, out var map) && map.TryGetValue(name, out var a) ? a : null);
            if (atts.Count == 0 || atts.All(a => a is SkelReader.NoDrawAtt)) continue;
            if (atts.Any(a => a is null))
                throw new InvalidDataException($"slot {slot.Name} shows an attachment the skin lacks");
            var drawable = atts.Where(a => a is not SkelReader.NoDrawAtt).ToList();
            if (drawable[0] is SkelReader.RegionAtt)
            {
                if (anim.Deforms.Any(d => d.Slot == s))
                    throw new InvalidDataException($"slot {slot.Name} has deform timelines on a region");
            }
            else if (drawable.Any(a => a is not SkelReader.MeshAtt))
                throw new InvalidDataException($"slot {slot.Name} shows a non-mesh attachment");
            // Flipping attachments need not share a shape (an eye can flip
            // between two meshes). One baked slot per shape; every frame
            // shows the one the timeline picks and leaves the others empty.
            foreach (var group in ShapeGroups(atts))
            {
                var lead = atts[group[0]]!;
                bool multi = group.Count > 1;
                var outFrames = new List<FrameData>(frames);
                for (int f = 0; f < frames; f++)
                {
                    float t = duration * f / frames;
                    int cur = shown.IndexOf(AttachAt(slot, anim, s, t) ?? "");
                    outFrames.Add(cur >= 0 && group.Contains(cur)
                        ? BakeAtt(bones, slots, anim, s, atts[cur]!, atlas, multi, t)
                        : new FrameData(Array.Empty<float>(), ColorAt(slot, null, anim, s, t), 0, null));
                }
                baked.Slots.Add(new SlotData(slot.Blend, ShapeTris(lead),
                    multi ? Array.Empty<float>() : SlotUvs(lead, atlas), outFrames));
            }
        }
        // Without any normal-blend geometry there is nothing to show.
        if (!baked.Slots.Any(s => s.Blend == 0 && s.Frames.Any(f => f.Verts.Length > 0)))
            throw new InvalidDataException("rig has no normal-blend geometry");
        if (scale != 1)
            foreach (var s in baked.Slots)
                foreach (var f in s.Frames)
                    for (int i = 0; i < f.Verts.Length; i++) f.Verts[i] *= scale;
        return baked;
    }

    // Every attachment a slot ever shows, setup included, in first-shown
    // order. More than one means the timeline flips between them (effect
    // flipbooks); each frame then takes the one shown at that time.
    private static List<string> ShownAttachments(SkelReader.Slot slot, SkelReader.Anim anim, int s)
    {
        var names = new List<string>();
        if (!anim.Slots.TryGetValue(s, out var tls) || tls.Attach.Count == 0)
        {
            if (slot.Attachment != null) names.Add(slot.Attachment);
            return names;
        }
        if (tls.Attach[0].Time > 0 && slot.Attachment != null) names.Add(slot.Attachment);
        foreach (var k in tls.Attach)
            if (k.Name != null && !names.Contains(k.Name)) names.Add(k.Name);
        return names;
    }

    // The attachment a slot shows at time t: the setup shows before the
    // first key, after that the last key at or before t. Null is a hide.
    private static string? AttachAt(SkelReader.Slot slot, SkelReader.Anim anim, int s, float t)
    {
        if (!anim.Slots.TryGetValue(s, out var tls) || tls.Attach.Count == 0) return slot.Attachment;
        string? cur = slot.Attachment;
        foreach (var k in tls.Attach)
        {
            if (k.Time > t) break;
            cur = k.Name;
        }
        return cur;
    }

    // Shown attachments grouped by shape, first-shown order: all regions
    // share the quad, meshes share a triangle list. Attachments that never
    // draw are left out, so a frame showing one stays empty.
    private static List<List<int>> ShapeGroups(List<SkelReader.SkinAtt?> atts)
    {
        var keys = new List<string>();
        var groups = new List<List<int>>();
        for (int i = 0; i < atts.Count; i++)
        {
            if (atts[i] is SkelReader.NoDrawAtt) continue;
            string key = string.Join(",", ShapeTris(atts[i]!));
            int g = keys.IndexOf(key);
            if (g < 0) { keys.Add(key); groups.Add(new List<int>()); g = groups.Count - 1; }
            groups[g].Add(i);
        }
        return groups;
    }

    // Quad corners are BL, BR, TL, TR.
    private static int[] ShapeTris(SkelReader.SkinAtt att) =>
        att is SkelReader.RegionAtt ? new int[] { 0, 1, 2, 2, 1, 3 } : ((SkelReader.MeshAtt)att).Tris;

    private static float[] SlotUvs(SkelReader.SkinAtt att, Atlas atlas)
    {
        if (att is SkelReader.RegionAtt r)
        {
            var region = RegionFor(atlas, r.Path, -1, r.Seq);
            return PageUvs(RegionCanvasUvs(region), atlas, region);
        }
        var mesh = (SkelReader.MeshAtt)att;
        return PageUvs(mesh.Uvs, atlas, RegionFor(atlas, mesh.Path, -1, mesh.Seq));
    }

    // One frame of one attachment: a quad for regions, a skinned mesh for
    // meshes. perFrame says the slot flips between attachments, so every
    // frame has to carry its own uvs.
    private static FrameData BakeAtt(List<SkelReader.Bone> bones, List<SkelReader.Slot> slots,
        SkelReader.Anim anim, int slotIndex, SkelReader.SkinAtt att, Atlas atlas, bool perFrame, float t) =>
        att is SkelReader.RegionAtt reg
            ? BakeRegionFrame(bones, slots, anim, slotIndex, reg, atlas, perFrame, t)
            : BakeFrame(bones, slots, anim, slotIndex, (SkelReader.MeshAtt)att, atlas, perFrame, t);

    private static FrameData BakeFrame(List<SkelReader.Bone> bones, List<SkelReader.Slot> slots,
        SkelReader.Anim anim, int slotIndex, SkelReader.MeshAtt mesh, Atlas atlas, bool perFrame, float t)
    {
        float[] m = WorldMatrices(bones, anim, t);
        float[] deform = DeformAt(anim, slotIndex, mesh.Name, PairCount(mesh) * 2, t);
        float[] verts = Skin(mesh, slots[slotIndex].Bone, m, deform);
        var region = RegionFor(atlas, mesh.Path, mesh.Seq == null ? -1 : SeqIndexAt(anim, slotIndex, mesh.Seq, t), mesh.Seq);
        return new FrameData(verts, ColorAt(slots[slotIndex], mesh, anim, slotIndex, t), region.Page,
            perFrame || mesh.Seq != null ? PageUvs(mesh.Uvs, atlas, region) : null);
    }

    private static FrameData BakeRegionFrame(List<SkelReader.Bone> bones, List<SkelReader.Slot> slots,
        SkelReader.Anim anim, int slotIndex, SkelReader.RegionAtt reg, Atlas atlas, bool perFrame, float t)
    {
        float[] m = WorldMatrices(bones, anim, t);
        var region = reg.Seq == null ? RegionFor(atlas, reg.Path, -1, null)
            : RegionFor(atlas, reg.Path, SeqIndexAt(anim, slotIndex, reg.Seq, t), reg.Seq);
        float[] verts = RegionSkin(reg, slots[slotIndex].Bone, m, region);
        float[] color = ColorAt(slots[slotIndex], reg, anim, slotIndex, t);
        return new FrameData(verts, color, region.Page,
            perFrame || reg.Seq != null ? PageUvs(RegionCanvasUvs(region), atlas, region) : null);
    }

    // Region quad: the untrimmed rect scaled to the attachment size, with
    // the trimmed border left out, then rotated and placed, then posed by
    // the slot bone. This is RegionAttachment.updateRegion: a region whose
    // atlas entry is packed at 0.8 (a scale: page) draws 1.25x its packed
    // pixels. Corner order matches the uv order: BL, BR, TL, TR.
    private static float[] RegionSkin(SkelReader.RegionAtt reg, int slotBone, float[] m, Atlas.Region r)
    {
        float rsx = reg.Sx * reg.W / r.OrigW, rsy = reg.Sy * reg.H / r.OrigH;
        float left = -reg.W / 2 * reg.Sx + r.Ox * rsx;
        float bottom = -reg.H / 2 * reg.Sy + r.Oy * rsy;
        float right = left + r.W * rsx, top = bottom + r.H * rsy;
        double c = Math.Cos(reg.Rot * Math.PI / 180), s = Math.Sin(reg.Rot * Math.PI / 180);
        var world = new float[8];
        int b = slotBone * 6;
        for (int v = 0; v < 4; v++)
        {
            float lx = v % 2 == 0 ? left : right;
            float ly = v < 2 ? bottom : top;
            float x = (float)(c * lx - s * ly) + reg.X, y = (float)(s * lx + c * ly) + reg.Y;
            world[v * 2] = m[b] * x + m[b + 1] * y + m[b + 4];
            world[v * 2 + 1] = m[b + 2] * x + m[b + 3] * y + m[b + 5];
        }
        return world;
    }

    // The region's own corners as canvas-space uvs (y down), the space mesh
    // uvs and PageUvs already use, so both share one rotation-aware mapping.
    private static float[] RegionCanvasUvs(Atlas.Region r)
    {
        float u0 = (float)r.Ox / r.OrigW, u1 = (float)(r.Ox + r.W) / r.OrigW;
        float v0 = (float)(r.OrigH - r.Oy - r.H) / r.OrigH, v1 = (float)(r.OrigH - r.Oy) / r.OrigH;
        return new float[] { u0, v1, u1, v1, u0, v0, u1, v0 }; // BL, BR, TL, TR
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
                // Bone timeline ids, one and two-component alike: rotate 0,
                // translate 1 / x 2 / y 3, scale 4 / x 5 / y 6, shear 7 / x 8 / y 9.
                if (tls.Two.TryGetValue(1, out var tr) && Sample2(tr, t) is float[] p)
                { x += p[0]; y += p[1]; }
                else
                {
                    if (tls.One.TryGetValue(2, out var tx2) && Sample1(tx2, t) is float vx2) x += vx2;
                    if (tls.One.TryGetValue(3, out var ty2) && Sample1(ty2, t) is float vy2) y += vy2;
                }
                if (tls.One.TryGetValue(0, out var ro) && Sample1(ro, t) is float r) rot += r;
                if (tls.Two.TryGetValue(4, out var sc) && Sample2(sc, t) is float[] q)
                { sx *= q[0]; sy *= q[1]; }
                else
                {
                    if (tls.One.TryGetValue(5, out var sx2) && Sample1(sx2, t) is float vx5) sx *= vx5;
                    if (tls.One.TryGetValue(6, out var sy2) && Sample1(sy2, t) is float vy6) sy *= vy6;
                }
                if (tls.Two.TryGetValue(7, out var sh) && Sample2(sh, t) is float[] h)
                { shx += h[0]; shy += h[1]; }
                else
                {
                    if (tls.One.TryGetValue(8, out var shx2) && Sample1(shx2, t) is float vx8) shx += vx8;
                    if (tls.One.TryGetValue(9, out var shy2) && Sample1(shy2, t) is float vy9) shy += vy9;
                }
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
    private static float[] ColorAt(SkelReader.Slot slot, SkelReader.SkinAtt? att,
        SkelReader.Anim anim, int slotIndex, float t)
    {
        const int white = unchecked((int)0xFFFFFFFF); // slot without attachment: untinted
        float[] c = Rgba(slot.Color), m = Rgba(att?.Color ?? white);
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
                else if (fr[i].Curve == null)
                {
                    float pct = (t - fr[i].Time) / (fr[i + 1].Time - fr[i].Time);
                    r = (fr[i].R + (fr[i + 1].R - fr[i].R) * pct) * m[0];
                    g = (fr[i].G + (fr[i + 1].G - fr[i].G) * pct) * m[1];
                    b = (fr[i].B + (fr[i + 1].B - fr[i].B) * pct) * m[2];
                    a = (fr[i].A + (fr[i + 1].A - fr[i].A) * pct) * m[3];
                }
                else // one 4-float bezier per channel; insane controls are corrupt tool output
                {
                    float[] cv = fr[i].Curve!;
                    r = Ease(fr[i].Time, fr[i].R, fr[i + 1].Time, fr[i + 1].R, cv, 0, t) * m[0];
                    g = Ease(fr[i].Time, fr[i].G, fr[i + 1].Time, fr[i + 1].G, cv, 4, t) * m[1];
                    b = Ease(fr[i].Time, fr[i].B, fr[i + 1].Time, fr[i + 1].B, cv, 8, t) * m[2];
                    a = Ease(fr[i].Time, fr[i].A, fr[i + 1].Time, fr[i + 1].A, cv, 12, t) * m[3];
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
    // One channel eased by its bezier slice. Controls past 1e6 are corrupt
    // (p18200 slot142 frame2 has 2e35); linear then, the likely intent.
    private static float Ease(float t0, float v0, float t1, float v1, float[] c, int o, float t)
    {
        for (int k = 0; k < 4; k++)
            if (Math.Abs(c[o + k]) > 1e6)
                return v0 + (v1 - v0) * (t - t0) / (t1 - t0);
        return BezierVal(t0, v0, t1, v1, new float[] { c[o], c[o + 1], c[o + 2], c[o + 3] }, t);
    }

    // Curve control points arrive in absolute units (time and value), the way
    // the 4.x binary rigs store them; the 2.1 JSON reader converts its
    // percentage curves before handing them over. Values come out of the ten
    // samples linearized in s.
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

    // Active sequence region index at time t, 0-based like the timeline's
    // own index: the last frame at or before t picks the start, then one
    // region per delay. Without a timeline the setup region shows.
    private static int SeqIndexAt(SkelReader.Anim anim, int slot, SkelReader.SeqAtt seq, float t)
    {
        if (!anim.Slots.TryGetValue(slot, out var tls) || tls.Seq.Count == 0)
            return seq.Setup;
        var fr = tls.Seq;
        if (t < fr[0].Time) return seq.Setup;
        int i = fr.Count - 1;
        while (i > 0 && fr[i].Time > t) i--;
        int count = seq.Count;
        int abs = fr[i].Index + (fr[i].Delay > 0 ? (int)((t - fr[i].Time) / fr[i].Delay) : 0);
        return (abs % count + count) % count;
    }

    // Sequence regions are numbered 'path' + (start + index), zero padded.
    private static Atlas.Region RegionFor(Atlas atlas, string path, int seqIndex, SkelReader.SeqAtt? seq)
    {
        if (seqIndex < 0 && seq != null) seqIndex = seq.Setup; // setup pose
        string name = seq == null ? path
            : path + (seqIndex + seq.Start).ToString().PadLeft(seq.Digits, '0');
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
            Ox = anim.Ox,
            Oy = anim.Oy,
            Pma = anim.Pma,
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
