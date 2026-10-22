using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Bakes UI strings into textures with the MapleStory font. MonoGame has no
// text without the content pipeline, so GDI+ renders white-on-transparent
// once per string and the sprite batch tints it. Every string renders at
// most once (input text is length-capped). The TTFs live in assets/fonts
// (Nexon IP, never committed); without them it falls back to system fonts.
public sealed class UiText : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly PrivateFontCollection? _privateFonts;
    private readonly FontFamily? _maple; // null without assets/fonts (falls back to system fonts)
    private readonly Font _font;
    private readonly Font _small;
    private readonly Font _mid;
    private readonly Font _meso;
    private Font _list;
    private Font _cancel;
    private Font _setupName;
    private Font _setupError;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private readonly Dictionary<string, Texture2D> _cacheSmall = new();
    private readonly Dictionary<string, Texture2D> _cacheMid = new();
    private readonly Dictionary<string, Texture2D> _cacheMeso = new();
    private readonly Dictionary<string, Texture2D> _cacheList = new();
    private readonly Dictionary<string, Texture2D> _cacheCancel = new();
    private readonly Dictionary<string, Texture2D> _cacheSetupName = new();
    private readonly Dictionary<string, Texture2D> _cacheSetupError = new();
    private readonly Dictionary<(string, float), List<string>> _wrapCache = new();
    private readonly List<Texture2D> _trash = new(); // the textures that left the cache
    private const int CacheCap = 512; // the cache size that makes room for new strings

    public UiText(GraphicsDevice gd, string fontsDir)
    {
        _gd = gd;
        // Every font below builds Bold: Light may be absent, Bold must not.
        string bold = Path.Combine(fontsDir, "Maplestory-Bold.ttf");
        if (File.Exists(bold))
        {
            _privateFonts = new PrivateFontCollection();
            _privateFonts.AddFontFile(bold);
            _maple = _privateFonts.Families[0];
        }
        _font = Maple(22);
        _small = Maple(12);
        _mid = Maple(16);
        // Meso keeps Arial as its missing-TTF fallback (Maple() falls back to YaHei).
        _meso = _maple != null
            ? new Font(_maple, 12, FontStyle.Bold, GraphicsUnit.Pixel)
            : new Font("Arial", 12, FontStyle.Bold, GraphicsUnit.Pixel);
        _list = Maple(MenuLayout.ListSize);
        _cancel = Maple(MenuLayout.CancelSize);
        _setupName = Maple(SetupLayout.NameFont);
        _setupError = Maple(SetupLayout.ErrorFont);
    }

    // MapleStory Bold, or YaHei when the TTFs are missing.
    private Font Maple(int px) => _maple != null
        ? new Font(_maple, px, FontStyle.Bold, GraphicsUnit.Pixel)
        : new Font("Microsoft YaHei", px, GraphicsUnit.Pixel);

    // Live layout reload rebuilds the menu fonts (sizes come from the file).
    public void RefreshFonts()
    {
        _list.Dispose();
        _cancel.Dispose();
        _setupName.Dispose();
        _setupError.Dispose();
        foreach (var tex in _cacheList.Values) tex.Dispose();
        foreach (var tex in _cacheCancel.Values) tex.Dispose();
        foreach (var tex in _cacheSetupName.Values) tex.Dispose();
        foreach (var tex in _cacheSetupError.Values) tex.Dispose();
        _cacheList.Clear();
        _cacheCancel.Clear();
        _cacheSetupName.Clear();
        _cacheSetupError.Clear();
        _list = Maple(MenuLayout.ListSize);
        _cancel = Maple(MenuLayout.CancelSize);
        _setupName = Maple(SetupLayout.NameFont);
        _setupError = Maple(SetupLayout.ErrorFont);
    }

    public Texture2D Get(string text) => GetFrom(_cache, _font, text);

    // Middle size for the class-select stat rows: 12px reads too small in the big panel.
    public Texture2D GetMid(string text) => GetFrom(_cacheMid, _mid, text);

    // Small sibling for HUD labels (names, tabs, rows, slots).
    public Texture2D GetSmall(string text) => GetFrom(_cacheSmall, _small, text);

    // Meso/point numbers in MapleStory Bold 12. Draw tinted #606871.
    public Texture2D GetMeso(string text) => GetFrom(_cacheMeso, _meso, text);

    // Server-list rows (baked at display size, stays crisp).
    public Texture2D GetList(string text) => GetFrom(_cacheList, _list, text);

    // The Cancel row gets its own size (menu-layout.json cancelSize).
    public Texture2D GetListCancel(string text) => GetFrom(_cacheCancel, _cancel, text);

    // Setup page text: the name field and the error line (setup-layout.json
    // nameFont / errorFont).
    public Texture2D GetSetupName(string text) => GetFrom(_cacheSetupName, _setupName, text);
    public Texture2D GetSetupError(string text) => GetFrom(_cacheSetupError, _setupError, text);

    private Texture2D GetFrom(Dictionary<string, Texture2D> cache, Font font, string text)
    {
        if (cache.TryGetValue(text, out var tex)) return tex;
        tex = Bake(text, font);
        // The cache must not grow forever: the chat input makes a new string
        // on every keystroke. The old textures go to the trash and are
        // disposed on the next trim (not in the middle of a draw).
        if (cache.Count >= CacheCap)
        {
            foreach (var old in cache.Values) _trash.Add(old);
            cache.Clear();
        }
        cache[text] = tex;
        return tex;
    }

    // Dispose the textures that left the cache. The game calls this before
    // the draw, so no texture is freed while the sprite batch holds it.
    public void Trim()
    {
        foreach (var tex in _trash) tex.Dispose();
        _trash.Clear();
    }

    // Word-wrap for dialog/quest bodies (small font; explicit \n splits
    // first, then greedy words; a word wider than the box splits by char).
    // The result is cached: the world map wraps every row on each rebuild and
    // GDI+ measuring is slow.
    public List<string> Wrap(string text, float maxWidth)
    {
        var key = (text, maxWidth);
        if (!_wrapCache.TryGetValue(key, out var lines))
        {
            lines = WrapWith(text, maxWidth, _small);
            _wrapCache[key] = lines;
        }
        return lines;
    }

    private List<string> WrapWith(string text, float maxWidth, Font font)
    {
        var lines = new List<string>();
        using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        using var meas = Graphics.FromImage(bmp);
        foreach (string para in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = "";
            foreach (string word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string test = line == "" ? word : line + " " + word;
                if (meas.MeasureString(test, font).Width <= maxWidth) { line = test; continue; }
                if (line != "") lines.Add(line);
                line = word;
                while (meas.MeasureString(line, font).Width > maxWidth && line.Length > 1)
                {
                    int n = line.Length - 1;
                    while (n > 1 && meas.MeasureString(line[..n], font).Width > maxWidth) n--;
                    lines.Add(line[..n]);
                    line = line[n..];
                }
            }
            lines.Add(line);
        }
        return lines;
    }

    private Texture2D Bake(string text, Font font)
    {
        using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        using var meas = Graphics.FromImage(bmp);
        var size = meas.MeasureString(text, font);
        using var img = new Bitmap(Math.Max(1, (int)Math.Ceiling(size.Width)), font.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(img);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
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
        _privateFonts?.Dispose();
        _font.Dispose();
        _small.Dispose();
        _mid.Dispose();
        _meso.Dispose();
        _list.Dispose();
        _cancel.Dispose();
        _setupName.Dispose();
        _setupError.Dispose();
        Trim();
        foreach (var tex in _cache.Values) tex.Dispose();
        foreach (var tex in _cacheSmall.Values) tex.Dispose();
        foreach (var tex in _cacheMid.Values) tex.Dispose();
        foreach (var tex in _cacheMeso.Values) tex.Dispose();
        foreach (var tex in _cacheList.Values) tex.Dispose();
        foreach (var tex in _cacheCancel.Values) tex.Dispose();
        foreach (var tex in _cacheSetupName.Values) tex.Dispose();
        foreach (var tex in _cacheSetupError.Values) tex.Dispose();
    }
}

// The text of a line, wrapped to the width of a box. The party popup and the
// world map list need it: both are windows of their own.
public partial class MushTownGame
{
    internal List<string> WrapText(string text, int width) => _ui.Wrap(text, width);
}
