using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// What a key asked the window to do: nothing, send the line (Enter) or leave
// the field (Escape).
public enum FieldKey { None, Submit, Leave }

// A typed line: the text, the blinking caret and the key repeat. The window
// owns it and hands it the keys while it has the focus; the box it draws in is
// the owner's rect. The caret numbers come from the panel layout (caretDx = the
// gap from the end of the text, caretW/caretH = the size); its top is the middle
// of the field, so every input puts the caret on the same line.
public sealed class TextField : Component, IDrawable
{
    private const int MaxChars = 100; // the typed text stops here
    private const float BlinkMs = 500f; // the caret is on for one, off for the next
    // A held key starts repeating after this long, then this often (the OS defaults).
    private const float RepeatDelayMs = 400f;
    private const float RepeatRateMs = 40f;

    private readonly PanelLayout _lay;
    private readonly HashSet<Keys> _down = new(); // the keys of the last frame: a held key is not a new press
    public bool DigitsOnly; // the shop quantity accepts digits only
    public bool ReplaceOnType; // the next typed character replaces the whole line
    public int Limit = MaxChars;
    private string _text = "";
    private Keys _repeatKey = Keys.None; // the key the repeat is counting
    private float _repeatMs;             // how long it has been held

    public TextField(PanelLayout lay) => _lay = lay;

    public bool Focused { get; private set; }

    // The text and the caret (the chat writes white, the search box black).
    public Color Ink = Color.White;

    public string Text => _text;

    public void Type(char c)
    {
        if (DigitsOnly && !char.IsDigit(c)) return;
        if (ReplaceOnType) { _text = ""; ReplaceOnType = false; }
        if (_text.Length < Limit) _text += c;
    }

    public void SetText(string text) => _text = text.Length <= Limit ? text : text[..Limit];

    private void Backspace()
    {
        if (_text.Length > 0) _text = _text[..^1];
    }

    public void Clear() => _text = "";

    // The field takes the keys. The keys held right now count as already down, so
    // the one that opened the field does not type into it.
    public void Focus(KeyboardState kb)
    {
        Focused = true;
        _repeatKey = Keys.None;
        _repeatMs = 0;
        _down.Clear();
        foreach (var key in kb.GetPressedKeys()) _down.Add(key);
    }

    public void Unfocus()
    {
        Focused = false;
        _repeatKey = Keys.None; // a held key must not type into the field that just left
        _repeatMs = 0;
    }

    // One frame of keys. Enter and Escape come back to the window: sending and
    // leaving mean different things to the two windows.
    public FieldKey Read(KeyboardState kb, float dtMs)
    {
        var now = kb.GetPressedKeys();
        FieldKey answer = FieldKey.None;
        if (Focused)
        {
            foreach (var key in now)
            {
                if (_down.Contains(key)) continue; // held down: the repeat below handles it
                if (key == Keys.Enter) { answer = FieldKey.Submit; break; }
                if (key == Keys.Escape) { answer = FieldKey.Leave; break; }
                Press(key);
                if (CharOf(key) == '\0' && key != Keys.Back) continue; // a key that types nothing does not start the repeat
                _repeatKey = key;
                _repeatMs = 0;
            }
            if (answer == FieldKey.None) Repeat(kb, dtMs);
        }
        _down.Clear();
        foreach (var key in now) _down.Add(key);
        return answer;
    }

    private void Repeat(KeyboardState kb, float dtMs)
    {
        if (_repeatKey == Keys.None || !kb.IsKeyDown(_repeatKey)) { _repeatKey = Keys.None; return; }
        _repeatMs += dtMs;
        if (_repeatMs < RepeatDelayMs) return;
        Press(_repeatKey);
        _repeatMs = Math.Min(_repeatMs - RepeatRateMs, RepeatDelayMs - RepeatRateMs); // a long stall must not dump a burst of characters
    }

    // One press: a character goes into the text, Backspace deletes.
    private void Press(Keys key)
    {
        if (key == Keys.Back) { Backspace(); return; }
        char c = CharOf(key);
        if (c != '\0') Type(c);
    }

    public void Draw(UiGfx gfx)
    {
        var box = Owner.Get<Rect>()!.Screen;
        var t = gfx.Ui.GetSmall(_text);
        int w = Math.Min(t.Width, box.Width); // a long line shows its end, so the caret stays in the box
        if (w > 0)
            gfx.Sb.Draw(t, new Rectangle(box.X, box.Y + (box.Height - t.Height) / 2, w, t.Height),
                new Rectangle(t.Width - w, 0, w, t.Height), Ink);
        if (!Focused || (int)(gfx.Time / BlinkMs) % 2 == 1) return;
        int caretH = _lay.Get("caretH", 12);
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(box.X + w + _lay.Get("caretDx", 1),
            box.Y + (box.Height - caretH) / 2, _lay.Get("caretW", 1), caretH), Ink);
    }

    // The typed character for a key (letters, digits, space, the usual marks).
    private static char CharOf(Keys key)
    {
        if (key >= Keys.A && key <= Keys.Z) return (char)('a' + (key - Keys.A));
        if (key >= Keys.D0 && key <= Keys.D9) return (char)('0' + (key - Keys.D0));
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
        return key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => '.',
            Keys.OemComma => ',',
            Keys.OemMinus => '-',
            Keys.OemQuestion => '?',
            Keys.OemQuotes => '\'',
            Keys.OemSemicolon => ';',
            Keys.OemPlus => '=',
            Keys.OemOpenBrackets => '[',
            Keys.OemCloseBrackets => ']',
            Keys.OemBackslash => '\\',
            Keys.OemPipe => '|',
            Keys.Divide => '/',
            Keys.Multiply => '*',
            Keys.Add => '+',
            Keys.Subtract => '-',
            Keys.Decimal => '.',
            _ => '\0',
        };
    }
}
