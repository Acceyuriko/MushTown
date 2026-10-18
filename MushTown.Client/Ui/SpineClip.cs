using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Baked Spine player for the class screen. The exporter samples the 4.1
// rig to plain per-frame meshes (assets/spine/{id}/{id}.json plus atlas
// pages); this only steps frames and draws triangle lists. Classes without
// baked data keep their static art.
public sealed class SpineClip : IDisposable
{
    public SpineData Data = new();
    public readonly List<Texture2D> Tex = new();
    public int Frame;
    public float Timer;
    private readonly BasicEffect _fx;
    private readonly float _ox, _oy; // baked rig origin on the page
    private VertexPositionColorTexture[] _verts = new VertexPositionColorTexture[256]; // reused: Draw runs every frame
    private static readonly BlendState _multiply = new() // 2: src*dst
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };
    private static readonly BlendState _screen = new() // 3: src+dst-src*dst
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.InverseSourceColor,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    private SpineClip(BasicEffect fx, float ox, float oy)
    {
        _fx = fx;
        _ox = ox; _oy = oy;
    }

    // Baked units are page pixels: rig (0,0) draws at (ox, oy) with y up.
    // No fitting, no centering, no cropping: each rig is authored in page
    // space, so its own origin is the official place (Saitama and Ren are
    // close-ups by design, exactly like their static art).
    public static SpineClip Load(GraphicsDevice gd, string jsonPath, int viewW, int viewH)
    {
        var data = MushTownGame.LoadJson<SpineData>(jsonPath);
        if (data.Fps <= 0 || data.Slots.Count == 0 || data.Slots[0].Frames.Count == 0)
            throw new InvalidDataException($"{jsonPath} is not a spine clip");
        string dir = Path.GetDirectoryName(jsonPath)!;
        var tex = new List<Texture2D>(data.Pages.Count);
        foreach (string page in data.Pages)
        {
            using var stream = File.OpenRead(Path.Combine(dir, page));
            tex.Add(Texture2D.FromStream(gd, stream));
        }
        var fx = new BasicEffect(gd)
        {
            Projection = Matrix.CreateOrthographicOffCenter(0, viewW, viewH, 0, 0, 1),
            TextureEnabled = true,
            VertexColorEnabled = true,
        };
        var clip = new SpineClip(fx, data.Ox, data.Oy);
        clip.Data = data;
        clip.Tex.AddRange(tex);
        return clip;
    }

    public void Advance(float dtMs)
    {
        Timer += dtMs;
        float step = 1000 / Data.Fps;
        while (Timer >= step)
        {
            Timer -= step;
            Frame = (Frame + 1) % Data.Slots[0].Frames.Count;
        }
    }

    public void Draw(UiGfx gfx)
    {
        var gd = gfx.Sb.GraphicsDevice;
        gfx.Sb.End();
        var rs = gd.RasterizerState;
        gd.RasterizerState = RasterizerState.CullNone; // mirrored bones flip winding; Spine shows both sides
        foreach (var s in Data.Slots)
        {
            var f = s.Frames[Frame];
            if (f.V.Length == 0) continue;
            _fx.Texture = Tex[f.P];
            gd.BlendState = s.Blend switch
            {
                1 => BlendState.Additive,
                2 => _multiply,
                3 => _screen,
                // Atlas pages carry their own alpha convention: pma:t pages
                // are premultiplied and MonoGame's FromStream leaves them
                // as-is, so they need premultiplied blending. Straight-alpha
                // pages (Sia) keep the plain blend. Blending a premultiplied
                // page as straight alpha darkened every glow layer: Erel's
                // light beams drew as gray slabs.
                _ => Data.Pma ? BlendState.AlphaBlend : BlendState.NonPremultiplied,
            };
            float[] uvs = f.Uv ?? s.Uvs;
            var color = new Color(f.C[0], f.C[1], f.C[2], f.C[3]);
            int n = f.V.Length / 2;
            if (_verts.Length < n) _verts = new VertexPositionColorTexture[n];
            for (int i = 0; i < n; i++)
                _verts[i] = new VertexPositionColorTexture(
                    new Vector3(_ox + f.V[i * 2], _oy - f.V[i * 2 + 1], 0),
                    color, new Vector2(uvs[i * 2], uvs[i * 2 + 1]));
            foreach (var pass in _fx.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _verts, 0, n, s.Tris, 0, s.Tris.Length / 3);
            }
        }
        gd.BlendState = BlendState.NonPremultiplied;
        gd.RasterizerState = rs;
        gfx.Sb.Begin(blendState: BlendState.NonPremultiplied);
    }

    public void Dispose()
    {
        _fx.Dispose();
        foreach (var t in Tex) t.Dispose();
    }
}

// One animated class figure on the job screen. The clip owns its page
// textures; BuildJobScene evicts the clip when the pick has no bake.
public sealed class SpineMesh : Component, IDrawable
{
    private readonly SpineClip _clip;
    public SpineMesh(SpineClip clip) => _clip = clip;
    public void Draw(UiGfx gfx) => _clip.Draw(gfx);
}
