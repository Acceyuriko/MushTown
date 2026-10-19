using System.Buffers.Binary;
using System.Text;

namespace MushTown.Exporter;

// Minimal reader for Spine 4.1 binary (.skel) files, written from the WZ
// bytes plus a third-party MIT-licensed 4.2/4.3 converter used as format
// reference. No Esoteric code is used or shipped: the exporter bakes the
// animation to plain frames and the client never sees .skel.
// Covers only what the ClassSelect splash rigs use; unknown sections fail
// loud with the byte offset so divergences are easy to pin down.
public sealed class SkelReader
{
    private readonly byte[] _data;
    private int _pos;

    public SkelReader(byte[] data) { _data = data; }

    public int Position => _pos;
    public int Length => _data.Length;

    // Header flag: fps, image paths, bone colors and mesh edges only exist
    // when it is set. 6 of the 56 splash rigs leave it off.
    public bool Nonessential { get; private set; }

    public byte ReadByte() => _data[_pos++];

    // 7-bit little-endian groups, high bit continues.
    public int ReadVarint(bool optimizePositive)
    {
        int b = ReadByte(), value = b & 0x7F;
        if ((b & 0x80) != 0)
        {
            b = ReadByte(); value |= (b & 0x7F) << 7;
            if ((b & 0x80) != 0)
            {
                b = ReadByte(); value |= (b & 0x7F) << 14;
                if ((b & 0x80) != 0)
                {
                    b = ReadByte(); value |= (b & 0x7F) << 21;
                    if ((b & 0x80) != 0) value |= (ReadByte() & 0x7F) << 28;
                }
            }
        }
        return optimizePositive ? value : (value >> 1) ^ -(value & 1);
    }

    public float ReadFloat()
    {
        float v = BinaryPrimitives.ReadSingleBigEndian(_data.AsSpan(_pos));
        _pos += 4;
        return v;
    }

    public int ReadInt32()
    {
        int v = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(_pos));
        _pos += 4;
        return v;
    }

    // Length-prefixed modified UTF-8: 0 is null, 1 is empty, else len + 1.
    // The rigs use ASCII names, so bytes map straight to chars.
    public string? ReadString()
    {
        int len = ReadVarint(true);
        if (len == 0) return null;
        if (len == 1) return "";
        string s = Encoding.Latin1.GetString(_data, _pos, len - 1);
        _pos += len - 1;
        return s;
    }

    public string? ReadStringRef(List<string> table)
    {
        int i = ReadVarint(true);
        return i == 0 ? null : table[i - 1];
    }

    public sealed record Bone(string Name, int Parent, float Rot, float X, float Y,
        float Sx, float Sy, float Shx, float Shy, float Len);

    public sealed record Slot(string Name, int Bone, string? Attachment, int Color, int Blend, int Dark);

    public abstract record SkinAtt(string Name, string Path, int Color);

    // Sequence attachment: switches between the numbered regions
    // 'path{start..start+count-1}', zero padded to digits, starting at setup.
    public sealed record SeqAtt(int Count, int Start, int Digits, int Setup);

    public sealed record MeshAtt(string Name, string Path, float[] Verts, float[] Uvs, int[] Tris,
        SeqAtt? Seq, int Color) : SkinAtt(Name, Path, Color);

    // Bounding boxes, paths, points and clipping masks never draw. Paths do
    // drive path constraints, which the bake does not solve.
    public sealed record NoDrawAtt(string Name, string Path) : SkinAtt(Name, Path, -1);

    public sealed record RegionAtt(string Name, string Path, float X, float Y, float Rot,
        float Sx, float Sy, float W, float H, int Color, SeqAtt? Seq) : SkinAtt(Name, Path, Color);

    public sealed record Frame1(float Time, float Value, float[]? Curve); // curve null is linear
    public sealed record Frame2(float Time, float X, float Y, float[]? CurveX, float[]? CurveY);

    public sealed record FrameRgba(float Time, float R, float G, float B, float A, float[]? Curve); // null linear, empty stepped, 16 bezier
    public sealed record FrameSeq(float Time, int Index, float Delay);
    public sealed record FrameDeform(float Time, int Start, float[] Verts, float[]? Curve);

    public sealed record DeformTls(int Slot, string Attach, List<FrameDeform> Frames);

    public sealed record PathConstraint(string Name, int[] Bones, int TargetSlot, int PositionMode,
        int SpacingMode, int RotateMode, float OffsetRotation, float Position, float Spacing,
        float MixRotate, float MixX, float MixY);

    public sealed record BoneTls(Dictionary<int, List<Frame1>> One, Dictionary<int, List<Frame2>> Two);
    public sealed record SlotTls(List<(float Time, string? Name)> Attach, List<FrameRgba> Rgba,
        List<FrameSeq> Seq);

    // ConstraintEnd is the last constraint timeline key: the animation lasts
    // at least that long even when nothing else is that late.
    public sealed record Anim(string Name, Dictionary<int, SlotTls> Slots, Dictionary<int, BoneTls> Bones,
        List<DeformTls> Deforms, int DrawOrderCount, int EventCount, float ConstraintEnd);

    // A parsed rig, whatever it was parsed from: the binary reader and the
    // JSON one both hand this to the baker.
    public sealed record Rig(float Fps, List<Bone> Bones, List<Slot> Slots,
        Dictionary<string, Dictionary<string, Dictionary<string, SkinAtt>>> Skins, List<Anim> Anims);

    // Whole binary rig in one call.
    public static Rig Read(byte[] data)
    {
        var r = new SkelReader(data);
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
        if (r.Position != r.Length)
            throw new InvalidDataException($"bake stopped at {r.Position}, file is {r.Length}");
        return new Rig(fps, bones, slots, skins, anims);
    }

    // File header: hash, version, frame rect, nonessential flag, then fps and
    // the image/audio paths only if the flag is set. Returns fps; a rig
    // without one plays at Spine's default 30.
    public float ReadHeader()
    {
        ReadInt32(); ReadInt32(); // hash
        string? version = ReadString();
        if (version == null || !version.StartsWith("4.1"))
            throw new InvalidDataException($"want spine 4.1, got {version}");
        ReadFloat(); ReadFloat(); ReadFloat(); ReadFloat(); // frame rect
        Nonessential = ReadByte() != 0;
        if (!Nonessential) return 30;
        float fps = ReadFloat();
        ReadString(); ReadString(); // images, audio paths
        return fps;
    }

    public List<string> ReadStrings()
    {
        int n = ReadVarint(true);
        var table = new List<string>(n);
        for (int i = 0; i < n; i++) table.Add(ReadString() ?? "");
        return table;
    }

    // Slot section. Verified: inline name, bone index, color, dark color,
    // default attachment as a string-table ref, blend mode. No visible flag.
    public List<Slot> ReadSlots(List<string> table)
    {
        int n = ReadVarint(true);
        var slots = new List<Slot>(n);
        for (int i = 0; i < n; i++)
        {
            string name = ReadString() ?? $"slot{i}";
            int bone = ReadVarint(true);
            int color = ReadInt32();
            int dark = ReadInt32();
            string? att = ReadStringRef(table);
            int blend = ReadVarint(true);
            slots.Add(new Slot(name, bone, att, color, blend, dark));
        }
        return slots;
    }

    // Constraint sections. IK and transform constraints never show up in
    // the splash rigs and would need solving, so a nonzero count fails loud.
    // The four path-constraint rigs (120, 320, 410, 2500) are read past: the
    // bake draws their bones at their own pose.
    public List<PathConstraint> ReadConstraints()
    {
        int ik = ReadVarint(true), tc = ReadVarint(true);
        if (ik != 0 || tc != 0)
            throw new InvalidDataException($"skeleton has ik={ik} transform={tc} constraints, not baked");
        int n = ReadVarint(true);
        var constraints = new List<PathConstraint>(n);
        for (int i = 0; i < n; i++)
        {
            string name = ReadString() ?? $"path{i}";
            ReadVarint(true); // order
            ReadByte(); // skin required
            int bones = ReadVarint(true);
            var linked = new int[bones];
            for (int k = 0; k < bones; k++) linked[k] = ReadVarint(true);
            int slot = ReadVarint(true);
            int positionMode = ReadVarint(true), spacingMode = ReadVarint(true), rotateMode = ReadVarint(true);
            constraints.Add(new PathConstraint(name, linked, slot, positionMode, spacingMode, rotateMode,
                ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat()));
        }
        return constraints;
    }

    // Skin section. Verified: slot count, then per slot a slot index,
    // attachment count, and per attachment a name ref (null means the slot
    // name), flags (always 0), a type byte (0 region, 2 mesh), and data.
    public Dictionary<string, Dictionary<string, Dictionary<string, SkinAtt>>> ReadSkins(
        List<Slot> slots, List<string> table)
    {
        var skins = new Dictionary<string, Dictionary<string, Dictionary<string, SkinAtt>>>();
        int slotCount = ReadVarint(true);
        if (slotCount > 0) skins["default"] = ReadSkinAttachments(slots, table, slotCount);
        int n = ReadVarint(true);
        for (int i = 0; i < n; i++)
        {
            string name = ReadString() ?? $"skin{i}";
            ReadInt32(); // color
            for (int r = 0; r < 4; r++) // bone + ik/transform/path refs
                for (int k = 0, m = ReadVarint(true); k < m; k++) ReadVarint(true);
            skins[name] = ReadSkinAttachments(slots, table, ReadVarint(true));
        }
        return skins;
    }

    private Dictionary<string, Dictionary<string, SkinAtt>> ReadSkinAttachments(
        List<Slot> slots, List<string> table, int slotCount)
    {
        var sk = new Dictionary<string, Dictionary<string, SkinAtt>>();
        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = ReadVarint(true);
            string slotName = slots[slotIndex].Name;
            var atts = new Dictionary<string, SkinAtt>();
            int m = ReadVarint(true);
            for (int j = 0; j < m; j++)
            {
                string name = ReadStringRef(table) ?? slotName;
                int flags = ReadByte(), type = ReadByte();
                if (flags != 0)
                    throw new InvalidDataException($"attachment {name} has flags {flags}, want 0, at {_pos}");
                atts[name] = type switch
                {
                    2 => ReadMesh(name, table),
                    0 => ReadRegion(name, table),
                    1 or 4 or 5 or 6 => ReadNoDraw(name, type),
                    _ => throw new InvalidDataException($"attachment {name} has type {type}, want 0/2, at {_pos}"),
                };
            }
            sk[slotName] = atts;
        }
        return sk;
    }

    // 4.1 mesh, learned from Hero bytes against black-box ground truth:
    // path ref (null means the attachment name), color, vert count, uvs,
    // tri count, BE int16 triangles, weighted flag, then the verts
    // (raw xy pairs, or per-vert counts of bone/x/y/weight quads).
    private MeshAtt ReadMesh(string name, List<string> table)
    {
        string path = ReadStringRef(table) ?? name;
        int color = ReadInt32();
        int verts = ReadVarint(true);
        var uvs = new float[verts * 2];
        for (int i = 0; i < uvs.Length; i++) uvs[i] = ReadFloat();
        int triCount = ReadVarint(true);
        var tris = new int[triCount];
        for (int i = 0; i < triCount; i++) tris[i] = (ReadByte() << 8) | ReadByte();
        bool weighted = ReadByte() != 0;
        float[] data;
        if (!weighted)
        {
            data = new float[verts * 2];
            for (int i = 0; i < data.Length; i++) data[i] = ReadFloat();
        }
        else
        {
            var list = new List<float>();
            for (int v = 0; v < verts; v++)
            {
                int n = ReadVarint(true);
                list.Add(n);
                for (int k = 0; k < n; k++)
                {
                    list.Add(ReadVarint(true));
                    list.Add(ReadFloat()); list.Add(ReadFloat()); list.Add(ReadFloat());
                }
            }
            data = list.ToArray();
        }
        // Trailer: hull (half the runtime value), an optional sequence
        // (flag byte plus count/start/digits/setup index; region names then
        // carry the frame number), then edge count plus BE int16 edges and
        // width/height, which exist only when the header flag is set.
        ReadVarint(true); // hull, unused: the tris already bound the mesh
        SeqAtt? seq = ReadSequence();
        if (Nonessential)
        {
            int edges = ReadVarint(true);
            _pos += edges * 2;
            ReadFloat(); ReadFloat(); // width, height; the atlas already has them
        }
        return new MeshAtt(name, path, data, uvs, tris, seq, color);
    }

    // 4.1 region order, straight from SkeletonBinary.readAttachment:
    // path, rotation, x, y, scaleX, scaleY, width, height, color, sequence.
    // Rotation comes FIRST; reading x first rotated every region by its own y
    // (Sia's face by 171.8 degrees, her foot stars all over) and shifted it.
    private RegionAtt ReadRegion(string name, List<string> table)
    {
        string path = ReadStringRef(table) ?? name;
        float rot = ReadFloat(), x = ReadFloat(), y = ReadFloat();
        float sx = ReadFloat(), sy = ReadFloat(), w = ReadFloat(), h = ReadFloat();
        int color = ReadInt32();
        return new RegionAtt(name, path, x, y, rot, sx, sy, w, h, color, ReadSequence());
    }

    // Sequence: flag byte, then region count, first index, digit count and
    // setup index. A sequence attachment switches between the numbered
    // regions 'path00', 'path01', ... over time.
    private SeqAtt? ReadSequence()
    {
        if (ReadByte() == 0) return null;
        int count = ReadVarint(true), start = ReadVarint(true);
        return new SeqAtt(count, start, ReadVarint(true), ReadVarint(true));
    }

    // The four attachment types that never draw, read past so the skin
    // section stays aligned: bounding box 1, path 4, point 5, clipping 6.
    // Only regions and meshes carry a path ref, so these start straight in.
    private NoDrawAtt ReadNoDraw(string name, int type)
    {
        switch (type)
        {
            case 1:
                ReadVertices(ReadVarint(true));
                break;
            case 4:
                ReadByte(); ReadByte(); // closed, constant speed
                int verts = ReadVarint(true);
                ReadVertices(verts);
                _pos += verts / 3 * 4; // one length float per three components
                break;
            case 5:
                ReadFloat(); ReadFloat(); ReadFloat(); // rotation, x, y
                break;
            default:
                ReadVarint(true); // clipping end slot
                ReadVertices(ReadVarint(true));
                break;
        }
        if (Nonessential) ReadInt32(); // color
        return new NoDrawAtt(name, name);
    }

    // Vertices as the mesh reader builds them: a leading flag, then raw xy
    // floats or per-vertex counts of bone/x/y/weight quads.
    private void ReadVertices(int verts)
    {
        if (ReadByte() == 0)
        {
            _pos += verts * 2 * 4;
            return;
        }
        for (int v = 0; v < verts; v++)
            for (int k = 0, n = ReadVarint(true); k < n; k++)
            {
                ReadVarint(true); ReadFloat(); ReadFloat(); ReadFloat();
            }
    }

    // Events section. Hero has none; anything else fails loud.
    public void ReadEvents()
    {
        int n = ReadVarint(true);
        if (n != 0)
            throw new InvalidDataException($"skeleton has {n} events at {_pos}");
    }

    // Animation section. 4.1 guess from the 4.2 reference: slot timelines,
    // bone timelines, empty constraint blocks, deform/sequence timelines,
    // draw order, events. Timeline type ids match 4.2 (verified by counts).
    public List<Anim> ReadAnims(List<string> table)
    {
        int n = ReadVarint(true);
        var anims = new List<Anim>(n);
        for (int a = 0; a < n; a++)
        {
            string name = ReadString() ?? "";
            ReadVarint(true); // total timeline count, ignored: each group below reads its own count
            var slots = new Dictionary<int, SlotTls>();
            for (int i = 0, sn = ReadVarint(true); i < sn; i++)
            {
                int slot = ReadVarint(true);
                var tls = new SlotTls(new(), new(), new());
                for (int j = 0, tn = ReadVarint(true); j < tn; j++)
                {
                    int type = ReadByte(), frames = ReadVarint(true);
                    if (type == 0) // attachment
                        for (int f = 0; f < frames; f++)
                            tls.Attach.Add((ReadFloat(), ReadStringRef(table)));
                    else if (type == 1) // rgba
                        tls.Rgba.AddRange(ReadRgba(frames));
                    else throw new InvalidDataException($"slot timeline type {type} at {_pos}");
                }
                slots[slot] = tls;
            }
            var bones = new Dictionary<int, BoneTls>();
            for (int i = 0, bn = ReadVarint(true); i < bn; i++)
            {
                int bone = ReadVarint(true);
                var tls = new BoneTls(new(), new());
                for (int j = 0, tn = ReadVarint(true); j < tn; j++)
                {
                    int type = ReadByte(), frames = ReadVarint(true);
                    if (type is 0 or 2 or 3 or 5 or 6 or 8 or 9)
                    {
                        ReadVarint(true); // bezier count
                        tls.One[type] = ReadTimeline1(frames);
                    }
                    else if (type is 1 or 4 or 7)
                    {
                        ReadVarint(true); // bezier count
                        tls.Two[type] = ReadTimeline2(frames);
                    }
                    else throw new InvalidDataException($"bone timeline type {type} at {_pos}");
                }
                bones[bone] = tls;
            }
            // Constraint timelines: ik, transform, then path. Only the four
            // path-constraint rigs carry any, and since the bake leaves the
            // constrained bones at their own pose, they are only read for
            // the length of the animation.
            float constraintEnd = 0;
            for (int c = 0; c < 2; c++)
                if (ReadVarint(true) != 0)
                    throw new InvalidDataException($"animation has ik or transform timelines at {_pos}");
            for (int i = 0, pn = ReadVarint(true); i < pn; i++)
            {
                ReadVarint(true); // path constraint index
                for (int j = 0, tn = ReadVarint(true); j < tn; j++)
                {
                    int type = ReadByte(), frames = ReadVarint(true);
                    ReadVarint(true); // bezier count
                    if (type == 2) constraintEnd = Math.Max(constraintEnd, ReadPathMix(frames));
                    else if (type is 0 or 1)
                    {
                        var keys = ReadTimeline1(frames);
                        constraintEnd = Math.Max(constraintEnd, keys[^1].Time);
                    }
                    else throw new InvalidDataException($"path timeline type {type} at {_pos}");
                }
            }
            var deforms = new List<DeformTls>();
            for (int i = 0, dn = ReadVarint(true); i < dn; i++)
            {
                ReadVarint(true); // skin index
                for (int j = 0, dsn = ReadVarint(true); j < dsn; j++)
                {
                    int slot = ReadVarint(true);
                    for (int k = 0, dan = ReadVarint(true); k < dan; k++)
                    {
                        string attach = ReadStringRef(table) ?? "";
                        int type = ReadByte(), frames = ReadVarint(true);
                        if (type == 0) { deforms.Add(new DeformTls(slot, attach, ReadDeform(frames))); }
                        else if (type == 1)
                        {
                            if (!slots.TryGetValue(slot, out var tls))
                                slots[slot] = tls = new SlotTls(new(), new(), new());
                            // 4.1 sequence frame: time, one BE int32 packing
                            // the mode (low nibble) and the sequence index.
                            for (int f = 0; f < frames; f++)
                            {
                                float time = ReadFloat();
                                int modeAndIndex = ReadInt32();
                                tls.Seq.Add(new FrameSeq(time, modeAndIndex >> 4, ReadFloat()));
                            }
                        }
                        else throw new InvalidDataException($"attachment timeline type {type} at {_pos}");
                    }
                }
            }
            int draw = ReadVarint(true);
            for (int i = 0; i < draw; i++)
            {
                ReadFloat();
                for (int j = 0, on = ReadVarint(true); j < on; j++) { ReadVarint(true); ReadVarint(true); }
            }
            int ev = ReadVarint(true);
            for (int i = 0; i < ev; i++)
            {
                ReadFloat(); ReadVarint(true); ReadVarint(false); ReadFloat(); ReadString();
            }
            anims.Add(new Anim(name, slots, bones, deforms, draw, ev, constraintEnd));
        }
        return anims;
    }

    // Path mix frames: time plus three mixes per frame. Returns the last
    // frame's time.
    private float ReadPathMix(int frames)
    {
        float time = ReadFloat();
        ReadFloat(); ReadFloat(); ReadFloat();
        for (int f = 0; f < frames - 1; f++)
        {
            time = ReadFloat();
            ReadFloat(); ReadFloat(); ReadFloat();
            int curve = ReadByte();
            if (curve == 2) { for (int i = 0; i < 12; i++) ReadFloat(); }
            else if (curve is not (0 or 1))
                throw new InvalidDataException($"path mix curve {curve} at {_pos}");
        }
        return time;
    }

    private float[]? ReadCurve()
    {
        int curve = ReadByte();
        if (curve == 0) return null;
        if (curve == 1) return Array.Empty<float>(); // stepped
        if (curve != 2) throw new InvalidDataException($"curve {curve} at {_pos}");
        return new float[] { ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat() };
    }

    private List<Frame1> ReadTimeline1(int frames)
    {
        var list = new List<Frame1>(frames);
        float time = ReadFloat(), value = ReadFloat();
        for (int f = 0; f < frames; f++)
        {
            if (f < frames - 1)
            {
                float time2 = ReadFloat(), value2 = ReadFloat();
                list.Add(new Frame1(time, value, ReadCurve()));
                time = time2; value = value2;
            }
            else list.Add(new Frame1(time, value, null));
        }
        return list;
    }

    // 4.1 timeline2 (translate/scale/shear) has ONE curve byte per
    // transition, shared by both components: linear/stepped carry no data,
    // bezier carries x data then y data. (4.2 has a byte per component.)
    private List<Frame2> ReadTimeline2(int frames)
    {
        var list = new List<Frame2>(frames);
        float time = ReadFloat(), x = ReadFloat(), y = ReadFloat();
        for (int f = 0; f < frames; f++)
        {
            if (f < frames - 1)
            {
                float time2 = ReadFloat(), x2 = ReadFloat(), y2 = ReadFloat();
                int curve = ReadByte();
                float[]? cx = null, cy = null;
                if (curve == 1) { cx = Array.Empty<float>(); cy = Array.Empty<float>(); }
                else if (curve == 2)
                {
                    cx = new float[] { ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat() };
                    cy = new float[] { ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat() };
                }
                else if (curve != 0)
                    throw new InvalidDataException($"timeline2 curve {curve} at {_pos}");
                list.Add(new Frame2(time, x, y, cx, cy));
                time = time2; x = x2; y = y2;
            }
            else list.Add(new Frame2(time, x, y, null, null));
        }
        return list;
    }

    private List<FrameRgba> ReadRgba(int frames)
    {
        var list = new List<FrameRgba>(frames);
        ReadVarint(true); // bezier count
        float[] Frame()
        {
            float t = ReadFloat();
            return new float[] { t, ReadByte(), ReadByte(), ReadByte(), ReadByte() };
        }
        FrameRgba Mk(float[] f, float[]? c) =>
            new(f[0], f[1] / 255, f[2] / 255, f[3] / 255, f[4] / 255, c);
        float[] cur = Frame();
        for (int f = 0; f < frames; f++)
        {
            if (f < frames - 1)
            {
                float[] next = Frame();
                int curve = ReadByte();
                float[]? c = null;
                if (curve == 1) c = Array.Empty<float>(); // stepped
                else if (curve == 2)
                {
                    c = new float[16]; // one 4-float bezier per channel, like Timeline2
                    for (int i = 0; i < 16; i++) c[i] = ReadFloat();
                }
                else if (curve != 0)
                    throw new InvalidDataException($"rgba curve {curve} at {_pos}");
                list.Add(Mk(cur, c));
                cur = next;
            }
            else list.Add(Mk(cur, null));
        }
        return list;
    }

    private List<FrameDeform> ReadDeform(int frames)
    {
        var list = new List<FrameDeform>(frames);
        ReadVarint(true); // bezier count
        float time = ReadFloat();
        for (int f = 0; f < frames; f++)
        {
            int start = 0;
            float[] verts = Array.Empty<float>();
            int end = ReadVarint(true);
            if (end > 0)
            {
                start = ReadVarint(true);
                verts = new float[end];
                for (int i = 0; i < end; i++) verts[i] = ReadFloat();
            }
            if (f < frames - 1)
            {
                float time2 = ReadFloat();
                list.Add(new FrameDeform(time, start, verts, ReadCurve()));
                time = time2;
            }
            else list.Add(new FrameDeform(time, start, verts, null));
        }
        return list;
    }

    // Bone section. Layout verified against Hero bytes: inline name, direct
    // parent index except bone 0 (the root), then rotation, x, y, scale,
    // shear, length, inherit varint, skin flag, gray color.
    public List<Bone> ReadBones()
    {
        int n = ReadVarint(true);
        var bones = new List<Bone>(n);
        for (int i = 0; i < n; i++)
        {
            string name = ReadString() ?? $"bone{i}";
            int parent = i == 0 ? -1 : ReadVarint(true); // direct index; bone 0 is the root
            float rot = ReadFloat(), x = ReadFloat(), y = ReadFloat();
            float sx = ReadFloat(), sy = ReadFloat(), shx = ReadFloat(), shy = ReadFloat();
            float len = ReadFloat();
            ReadVarint(true); // inherit mode, baked as-is (always normal here)
            ReadByte(); // skin required, always false here
            if (Nonessential) ReadInt32(); // gray color
            bones.Add(new Bone(name, parent, rot, x, y, sx, sy, shx, shy, len));
        }
        return bones;
    }
}
