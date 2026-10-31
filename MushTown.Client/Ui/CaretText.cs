using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The chat input line: the official bar, the typed text and the caret that
// blinks while the input has the focus. The keys come from the window (the
// game hands it the keyboard); this component owns the text.
public sealed class CaretText : Component, IDrawable
{
    private const int MaxChars = 100; // the typed text stops here
    private const float BlinkMs = 500f; // the caret is on for one, off for the next

    private readonly PanelLayout _lay;
    private string _text = "";

    public CaretText(PanelLayout lay) => _lay = lay;

    public bool Focused { get; set; }
    public string Text => _text;

    public void Type(char c)
    {
        if (_text.Length < MaxChars) _text += c;
    }

    public void Backspace()
    {
        if (_text.Length > 0) _text = _text[..^1];
    }

    public void Clear() => _text = "";

    public void Draw(UiGfx gfx)
    {
        var bar = Owner.Get<Rect>()!.Screen;
        int cap = _lay.Get("capW", 7);
        gfx.Sb.Draw(gfx.Tex("ui/chat_input_w.png"), new Vector2(bar.X, bar.Y), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/chat_input_c.png"), new Rectangle(bar.X + cap, bar.Y, bar.Width - 2 * cap, bar.Height), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/chat_input_e.png"), new Vector2(bar.Right - cap, bar.Y), Color.White);

        // The field is the bar less the caps and the chat target button: a
        // longer line shows its end, so the caret stays inside the field.
        int tx = bar.X + _lay.Get("fieldX", 71);
        int avail = bar.Width - _lay.Get("fieldX", 71) - cap;
        var t = gfx.Ui.GetSmall(_text);
        int w = Math.Min(t.Width, avail);
        if (w > 0)
            gfx.Sb.Draw(t, new Rectangle(tx, bar.Y + (bar.Height - t.Height) / 2, w, t.Height),
                new Rectangle(t.Width - w, 0, w, t.Height), Color.White); // the tail of a long line
        if (!Focused || (int)(gfx.Time / BlinkMs) % 2 == 1) return;
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(tx + w + _lay.Get("caretDx", 1), bar.Y + _lay.Get("caretDy", 6),
            _lay.Get("caretW", 1), _lay.Get("caretH", 12)), Color.White);
    }
}
