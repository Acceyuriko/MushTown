using System.Text.Json;

namespace MushTown.Exporter;

// Reader for the ClassSelect rigs that ship a Spine 2.1 skeleton.json
// instead of a .skel: 23 of the 56 classes. Names, the 2.x "ffd" deform
// block and the interleaved weighted mesh layout come straight from the
// files. Timeline values are offsets from the setup pose and deform values
// are per-influence offsets, exactly like the binary rigs, so the baker
// takes both unchanged. Sections the splash rigs do not use fail loud.
public static class JsonSkelReader
{
    private static readonly Dictionary<string, int> BoneOne = new()
    {
        ["rotate"] = 0,
        ["translateX"] = 2,
        ["translateY"] = 3,
        ["scaleX"] = 5,
        ["scaleY"] = 6,
        ["shearX"] = 8,
        ["shearY"] = 9,
    };

    private static readonly Dictionary<string, int> BoneTwo = new()
    {
        ["translate"] = 1,
        ["scale"] = 4,
        ["shear"] = 7,
    };

    public static SkelReader.Rig Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        float fps = root.TryGetProperty("skeleton", out var skeleton) && skeleton.TryGetProperty("fps", out var f)
            ? f.GetSingle() : 30;
        var bones = new List<SkelReader.Bone>();
        var boneAt = new Dictionary<string, int>();
        foreach (var b in root.GetProperty("bones").EnumerateArray())
        {
            string name = Str(b, "name", "");
            int parent = bones.Count == 0 ? -1 : boneAt[Str(b, "parent", "")];
            bones.Add(new SkelReader.Bone(name, parent, Num(b, "rotation", 0), Num(b, "x", 0), Num(b, "y", 0),
                Num(b, "scaleX", 1), Num(b, "scaleY", 1), Num(b, "shearX", 0), Num(b, "shearY", 0),
                Num(b, "length", 0)));
            boneAt[name] = bones.Count - 1;
        }
        if (bones.Count == 0)
            throw new InvalidDataException("rig has no bones");
        var skins = ReadSkins(root.GetProperty("skins"));
        var slots = new List<SkelReader.Slot>();
        var slotAt = new Dictionary<string, int>();
        foreach (var s in root.GetProperty("slots").EnumerateArray())
        {
            string name = Str(s, "name", "");
            int blend = Str(s, "blend", "normal") switch
            {
                "normal" => 0,
                "additive" => 1,
                "multiply" => 2,
                "screen" => 3,
                var other => throw new InvalidDataException($"slot {name} blends {other}, want normal/additive/multiply/screen"),
            };
            slots.Add(new SkelReader.Slot(name, boneAt[Str(s, "bone", "")],
                s.TryGetProperty("attachment", out var a) ? a.GetString() : null,
                s.TryGetProperty("color", out var c) ? Color(c.GetString()!) : -1,
                blend,
                s.TryGetProperty("dark", out var d) ? Color(d.GetString()!) : -1));
            slotAt[name] = slots.Count - 1;
        }
        if (slots.Count == 0)
            throw new InvalidDataException("rig has no slots");
        foreach (string key in new[] { "ik", "transform", "path" })
            if (root.TryGetProperty(key, out var c) && c.GetArrayLength() > 0)
                throw new InvalidDataException($"rig has {key} constraints, not baked");
        var anims = new List<SkelReader.Anim>();
        foreach (var a in root.GetProperty("animations").EnumerateObject())
            anims.Add(ReadAnim(a.Name, a.Value, boneAt, slotAt));
        return new SkelReader.Rig(fps, bones, slots, skins, anims);
    }

    private static Dictionary<string, Dictionary<string, Dictionary<string, SkelReader.SkinAtt>>> ReadSkins(
        JsonElement skins)
    {
        if (skins.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("skins as an array are not baked");
        var all = new Dictionary<string, Dictionary<string, Dictionary<string, SkelReader.SkinAtt>>>();
        foreach (var skin in skins.EnumerateObject())
        {
            var bySlot = new Dictionary<string, Dictionary<string, SkelReader.SkinAtt>>();
            foreach (var slot in skin.Value.EnumerateObject())
            {
                var atts = new Dictionary<string, SkelReader.SkinAtt>();
                foreach (var att in slot.Value.EnumerateObject())
                {
                    var parsed = ReadAttachment(att.Name, att.Value);
                    if (parsed != null) atts[att.Name] = parsed;
                }
                bySlot[slot.Name] = atts;
            }
            all[skin.Name] = bySlot;
        }
        return all;
    }

    // Attachments that never draw (bounding boxes, paths, points, clipping
    // masks) are dropped: nothing in the bake asks for them.
    private static SkelReader.SkinAtt? ReadAttachment(string name, JsonElement map)
    {
        string type = Str(map, "type", "region");
        string path = Str(map, "path", name);
        int color = map.TryGetProperty("color", out var c) ? Color(c.GetString()!) : -1;
        switch (type)
        {
            case "region":
                return new SkelReader.RegionAtt(name, path, Num(map, "x", 0), Num(map, "y", 0),
                    Num(map, "rotation", 0), Num(map, "scaleX", 1), Num(map, "scaleY", 1),
                    Num(map, "width", 0), Num(map, "height", 0), color, Seq(map));
            case "mesh":
            case "weightedmesh":
            case "skinnedmesh":
                // 2.x weighted verts are already the baker's layout: per-vert
                // counts of bone/x/y/weight quads. 4.x keeps the bone list
                // apart, which would need conversion.
                if (map.TryGetProperty("bones", out _))
                    throw new InvalidDataException($"mesh {name} uses the 4.x vertex layout, not baked");
                return new SkelReader.MeshAtt(name, path, Floats(map.GetProperty("vertices")),
                    Floats(map.GetProperty("uvs")), Ints(map.GetProperty("triangles")), Seq(map), color);
            case "linkedmesh":
                throw new InvalidDataException($"linked mesh {name} is not baked");
            default:
                return null;
        }
    }

    private static SkelReader.Anim ReadAnim(string name, JsonElement map, Dictionary<string, int> boneAt,
        Dictionary<string, int> slotAt)
    {
        var slots = new Dictionary<int, SkelReader.SlotTls>();
        if (map.TryGetProperty("slots", out var sm))
            foreach (var s in sm.EnumerateObject())
            {
                var tls = new SkelReader.SlotTls(new(), new(), new());
                if (s.Value.TryGetProperty("attachment", out var at))
                    foreach (var fr in at.EnumerateArray())
                        tls.Attach.Add((Num(fr, "time", 0),
                            fr.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                                ? n.GetString() : null));
                if (s.Value.TryGetProperty("color", out var co)) tls.Rgba.AddRange(Rgba(co));
                if (s.Value.TryGetProperty("twoColor", out _))
                    throw new InvalidDataException($"slot {s.Name} has two-color timelines, not baked");
                slots[slotAt[s.Name]] = tls;
            }
        var bonesTls = new Dictionary<int, SkelReader.BoneTls>();
        if (map.TryGetProperty("bones", out var bm))
            foreach (var b in bm.EnumerateObject())
            {
                var tls = new SkelReader.BoneTls(new(), new());
                foreach (var t in b.Value.EnumerateObject())
                {
                    // One-component keys carry "x" for the x half and "y" for
                    // the y half; rotate reads "angle".
                    if (BoneOne.TryGetValue(t.Name, out int one))
                    {
                        var oneFrames = Frames1(t.Value, one == 0 ? "angle" : char.ToLowerInvariant(t.Name[^1]).ToString());
                        // Curve rewrite runs after Unwrap: its value span is the
                        // one the baker interpolates.
                        tls.One[one] = Abs1(one == 0 ? Unwrap(oneFrames) : oneFrames);
                    }
                    else if (BoneTwo.TryGetValue(t.Name, out int two))
                        tls.Two[two] = Frames2(t.Value);
                    else throw new InvalidDataException($"bone timeline {t.Name} is not baked");
                }
                bonesTls[boneAt[b.Name]] = tls;
            }
        var deforms = new List<SkelReader.DeformTls>();
        foreach (string key in new[] { "deform", "ffd" })
            if (map.TryGetProperty(key, out var dm))
                foreach (var skin in dm.EnumerateObject())
                    foreach (var slot in skin.Value.EnumerateObject())
                        foreach (var att in slot.Value.EnumerateObject())
                            deforms.Add(new SkelReader.DeformTls(slotAt[slot.Name], att.Name, Deform(att.Value)));
        foreach (string key in new[] { "ik", "transform", "path", "attachments", "sequence" })
            if (map.TryGetProperty(key, out var other) && other.GetArrayLength() > 0)
                throw new InvalidDataException($"animation {name} has {key} timelines, not baked");
        return new SkelReader.Anim(name, slots, bonesTls, deforms,
            map.TryGetProperty("drawOrder", out var dr) ? dr.GetArrayLength() : 0,
            map.TryGetProperty("events", out var ev) ? ev.GetArrayLength() : 0, 0);
    }

    private static List<SkelReader.FrameDeform> Deform(JsonElement frames)
    {
        int n = frames.GetArrayLength();
        var list = new List<SkelReader.FrameDeform>(n);
        for (int i = 0; i < n; i++)
        {
            var fr = frames[i];
            list.Add(new SkelReader.FrameDeform(Num(fr, "time", 0), (int)Num(fr, "offset", 0),
                fr.TryGetProperty("vertices", out var v) ? Floats(v) : Array.Empty<float>(),
                i < n - 1 ? Curves(fr, 1)[0] : null));
        }
        for (int i = 0; i + 1 < list.Count; i++)
            if (list[i].Curve is { Length: 4 } c)
                list[i] = list[i] with { Curve = Abs(c, list[i].Time, list[i + 1].Time, 0, 1) };
        return list;
    }

    // These rigs write curves the way Spine's JSON does: control points are
    // percentages of the key's span. The baker wants the absolute form the
    // 4.x binary rigs carry (see Paladin 120 body: 0.7654,0.9074,0.7346,0
    // for a key at 0 going to 0 at 1.5), so rewrite each key's curve into
    // time and value units. Deform curves stay 0..1 percentages of the
    // offset, which is what DeformAt asks for.
    private static float[] Abs(float[] c, float t0, float t1, float v0, float v1) =>
        new[]
        {
            t0 + (t1 - t0) * c[0], v0 + (v1 - v0) * c[1],
            t0 + (t1 - t0) * c[2], v0 + (v1 - v0) * c[3],
        };

    private static List<SkelReader.Frame1> Abs1(List<SkelReader.Frame1> frames)
    {
        for (int i = 0; i + 1 < frames.Count; i++)
            if (frames[i].Curve is { Length: 4 } c)
                frames[i] = frames[i] with
                {
                    Curve = Abs(c, frames[i].Time, frames[i + 1].Time, frames[i].Value, frames[i + 1].Value),
                };
        return frames;
    }

    // These rigs wrap rotation keys into (-360, 360): a skirt bone that sways
    // -4.64 degrees is stored as 355.36, a wing that flaps -7.22 as 352.78.
    // The 2.x runtime turned such a key the short way, so pull every key to
    // within 180 degrees of the one before it. A real spin written as several
    // keys (0, 120, 240) keeps its direction, because each step stays under 180.
    private static List<SkelReader.Frame1> Unwrap(List<SkelReader.Frame1> frames)
    {
        for (int i = 1; i < frames.Count; i++)
        {
            float prev = frames[i - 1].Value;
            float value = frames[i].Value;
            while (value - prev > 180)
                value -= 360;
            while (value - prev < -180)
                value += 360;
            frames[i] = frames[i] with { Value = value };
        }
        return frames;
    }

    private static List<SkelReader.Frame1> Frames1(JsonElement frames, string value)
    {
        int n = frames.GetArrayLength();
        var list = new List<SkelReader.Frame1>(n);
        for (int i = 0; i < n; i++)
            list.Add(new SkelReader.Frame1(Num(frames[i], "time", 0), Num(frames[i], value, 0),
                i < n - 1 ? Curves(frames[i], 1)[0] : null));
        return list;
    }

    private static List<SkelReader.Frame2> Frames2(JsonElement frames)
    {
        int n = frames.GetArrayLength();
        var list = new List<SkelReader.Frame2>(n);
        for (int i = 0; i < n; i++)
        {
            float[]?[] c = i < n - 1 ? Curves(frames[i], 2) : new float[]?[2];
            list.Add(new SkelReader.Frame2(Num(frames[i], "time", 0), Num(frames[i], "x", 0),
                Num(frames[i], "y", 0), c[0], c[1]));
        }
        for (int i = 0; i + 1 < list.Count; i++)
        {
            var f = list[i];
            var next = list[i + 1];
            list[i] = f with
            {
                CurveX = f.CurveX is { Length: 4 } cx ? Abs(cx, f.Time, next.Time, f.X, next.X) : f.CurveX,
                CurveY = f.CurveY is { Length: 4 } cy ? Abs(cy, f.Time, next.Time, f.Y, next.Y) : f.CurveY,
            };
        }
        return list;
    }

    private static List<SkelReader.FrameRgba> Rgba(JsonElement frames)
    {
        int n = frames.GetArrayLength();
        var list = new List<SkelReader.FrameRgba>(n);
        for (int i = 0; i < n; i++)
        {
            int c = Color(Str(frames[i], "color", "ffffffff"));
            float[]? curve = null;
            if (i < n - 1)
            {
                float[]?[] channels = Curves(frames[i], 4);
                if (channels.All(ch => ch is { Length: 0 })) curve = Array.Empty<float>();
                else if (channels.All(ch => ch is { Length: 4 }))
                {
                    var all = new float[16]; // one Bezier per channel, r g b a
                    for (int k = 0; k < 4; k++) Array.Copy(channels[k]!, 0, all, k * 4, 4);
                    curve = all;
                }
                else if (channels.Any(ch => ch != null))
                    throw new InvalidDataException("rgba curve mixes linear and bezier channels");
            }
            list.Add(new SkelReader.FrameRgba(Num(frames[i], "time", 0),
                ((c >> 24) & 0xff) / 255f, ((c >> 16) & 0xff) / 255f,
                ((c >> 8) & 0xff) / 255f, (c & 0xff) / 255f, curve));
        }
        for (int i = 0; i + 1 < list.Count; i++)
        {
            if (list[i].Curve is not { Length: 16 } c16)
                continue;
            var a = list[i];
            var b = list[i + 1];
            float[] v0 = { a.R, a.G, a.B, a.A }, v1 = { b.R, b.G, b.B, b.A };
            var all = new float[16];
            for (int k = 0; k < 4; k++)
                Array.Copy(Abs(c16[(k * 4)..(k * 4 + 4)], a.Time, b.Time, v0[k], v1[k]), 0, all, k * 4, 4);
            list[i] = a with { Curve = all };
        }
        return list;
    }

    // JSON curve: absent or "linear" is linear (null), "stepped" is empty,
    // a 4-number array is one Bezier shared by every channel, and a nested
    // array carries one per channel.
    private static float[]?[] Curves(JsonElement frame, int channels)
    {
        var none = new float[]?[channels];
        if (!frame.TryGetProperty("curve", out var c) || c.ValueKind == JsonValueKind.Null) return none;
        if (c.ValueKind == JsonValueKind.String)
        {
            string kind = c.GetString()!;
            if (kind == "linear") return none;
            if (kind == "stepped") return Stepped(channels);
            throw new InvalidDataException($"curve kind {kind} is not baked");
        }
        if (c.GetArrayLength() == 0) return none;
        if (c[0].ValueKind == JsonValueKind.Number)
        {
            var one = Bezier(c);
            var same = new float[]?[channels];
            Array.Fill(same, one);
            return same;
        }
        var per = new float[]?[channels];
        for (int i = 0; i < channels; i++)
            per[i] = i >= c.GetArrayLength() ? null : c[i].ValueKind switch
            {
                JsonValueKind.Array => Bezier(c[i]),
                JsonValueKind.String => c[i].GetString() == "stepped" ? Array.Empty<float>() : null,
                _ => null,
            };
        return per;
    }

    private static float[]?[] Stepped(int channels)
    {
        var stepped = new float[]?[channels];
        Array.Fill(stepped, Array.Empty<float>());
        return stepped;
    }

    private static float[] Bezier(JsonElement c)
    {
        if (c.GetArrayLength() != 4)
            throw new InvalidDataException($"bezier curve has {c.GetArrayLength()} numbers, want 4");
        return new[] { c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle(), c[3].GetSingle() };
    }

    // Name colors are the runtime's rgba8888 hex, so the int layout matches
    // the binary reader's.
    private static int Color(string hex) => unchecked((int)Convert.ToUInt32(hex, 16));

    private static float[] Floats(JsonElement array)
    {
        var list = new float[array.GetArrayLength()];
        for (int i = 0; i < list.Length; i++) list[i] = array[i].GetSingle();
        return list;
    }

    private static int[] Ints(JsonElement array)
    {
        var list = new int[array.GetArrayLength()];
        for (int i = 0; i < list.Length; i++) list[i] = array[i].GetInt32();
        return list;
    }

    private static SkelReader.SeqAtt? Seq(JsonElement map) =>
        map.TryGetProperty("sequence", out var s)
            ? new SkelReader.SeqAtt((int)Num(s, "count", 0), (int)Num(s, "start", 1),
                (int)Num(s, "digits", 0), (int)Num(s, "setup", 0))
            : null;

    private static float Num(JsonElement map, string name, float fallback) =>
        map.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : fallback;

    private static string Str(JsonElement map, string name, string fallback) =>
        map.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
}
