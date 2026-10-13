using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Xna.Framework.Graphics;

namespace MushTown.Client;

// Bakes UI strings into textures with a system font. MonoGame has no text
// without the content pipeline, and menu labels need real Chinese glyphs,
// so GDI+ renders white-on-transparent once per string and the sprite batch
// tints it. Every string renders at most once (input text is length-capped).
public sealed class UiText : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly Font _font;
    private readonly Font _small;
    private readonly Font _meso;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private readonly Dictionary<string, Texture2D> _cacheSmall = new();
    private readonly Dictionary<string, Texture2D> _cacheMeso = new();

    public UiText(GraphicsDevice gd)
    {
        _gd = gd;
        _font = new Font("Microsoft YaHei", 22, GraphicsUnit.Pixel);
        _small = new Font("Microsoft YaHei", 12, GraphicsUnit.Pixel);
        _meso = new Font("Arial", 12, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    public Texture2D Get(string text) => GetFrom(_cache, _font, text);

    // Small sibling for HUD labels (names, tabs, rows, slots).
    public Texture2D GetSmall(string text) => GetFrom(_cacheSmall, _small, text);

    // Official meso/point numbers: Arial Bold 12 (font:Meso). Draw tinted #606871.
    public Texture2D GetMeso(string text) => GetFrom(_cacheMeso, _meso, text);

    private Texture2D GetFrom(Dictionary<string, Texture2D> cache, Font font, string text)
    {
        if (!cache.TryGetValue(text, out var tex))
        {
            tex = Bake(text, font);
            cache[text] = tex;
        }
        return tex;
    }

    // One texture for a stack of lines (the log panel rebuilds this on change).
    public Texture2D BakeBlock(List<string> lines, int width)
    {
        int height = Math.Max(1, lines.Count) * _font.Height;
        using var bmp = new Bitmap(Math.Max(1, width), height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var brush = new SolidBrush(System.Drawing.Color.White);
        for (int i = 0; i < lines.Count; i++)
            g.DrawString(lines[i], _font, brush, 0, i * _font.Height);
        return ToTexture(bmp);
    }

    private Texture2D Bake(string text, Font font)
    {
        using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        using var meas = Graphics.FromImage(bmp);
        var size = meas.MeasureString(text, font);
        using var img = new Bitmap(Math.Max(1, (int)Math.Ceiling(size.Width)), font.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(img);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var brush = new SolidBrush(System.Drawing.Color.White);
        g.DrawString(text, font, brush, 0, 0);
        return ToTexture(img);
    }

    // GDI+ hands us BGRA bytes and MonoGame reads them as RGBA: the red and
    // blue channels come out swapped, which is invisible only because every
    // baked string is white. Bake a color here and the text comes out wrong.
    private Texture2D ToTexture(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] bytes = new byte[data.Stride * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var tex = new Texture2D(_gd, bmp.Width, bmp.Height);
            tex.SetData(bytes);
            return tex;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    public void Dispose()
    {
        _font.Dispose();
        _small.Dispose();
        _meso.Dispose();
        foreach (var tex in _cache.Values) tex.Dispose();
        foreach (var tex in _cacheSmall.Values) tex.Dispose();
        foreach (var tex in _cacheMeso.Values) tex.Dispose();
    }
}
