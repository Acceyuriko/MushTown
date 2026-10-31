using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The chat log: the lines of the tab in use, wrapped to the log width and
// scrolled by the wheel. The tab and the scroll live here, so the window
// keeps no text state and the scroll bar reads this component.
public sealed class ChatLog : Component, IDrawable
{
    // One log line. A named line (a player's chat) belongs to the All tab, a
    // line without a name is a system line: it belongs to Etc.
    public enum Tab { All, Etc }

    private sealed record Line(string Name, string Text, Tab Tab);

    // One row as it is drawn: a line can take more than one row once it is
    // wrapped, and only the first row carries the name.
    private readonly record struct Row(string Name, string Text, bool System, int Indent);

    private const int MaxLines = 50; // the log keeps the last lines

    // The colors of the log: a name is blue, a system line is gold, the text
    // is white. The folded chat draws the newest line in the same colors.
    public static readonly Color NameColor = new(120, 190, 255);
    public static readonly Color SystemColor = new(255, 220, 120);

    private readonly List<Line> _lines = new();
    private readonly List<Line> _visible = new(); // the lines of the tab in use
    private readonly List<Row> _rows = new(); // the wrapped rows of the tab in use
    private readonly PanelLayout _lay;

    private int _scroll; // the rows the log is scrolled up from the newest

    public ChatLog(PanelLayout lay) => _lay = lay;

    // The width a line wraps at: the window sets it from its layout, so a hot
    // reload that changes the log width wraps the lines again.
    public int WrapW { get; set; } // the window sets it on every build

    public Tab Current { get; set; }

    // A chat line from a player. A line without a name is a system line from
    // the server.
    public void AddLine(string name, string text) => Add(new Line(name, text, name == "" ? Tab.Etc : Tab.All));

    // A system line (the Etc. tab).
    public void AddSystem(string text) => Add(new Line("", text, Tab.Etc));

    private void Add(Line line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
    }

    // The wheel scrolls the log: a positive count goes further back in time (the text moves down), a negative one returns to the newest line.
    public void Scroll(int rows) => _scroll = Math.Clamp(_scroll + rows, 0, MaxScroll());

    // The thumb of the scroll bar: 0 at the top of the track, 1 at the
    // bottom. The newest row sits at the bottom, so a log that fits is at 1.
    public float ScrollPos()
    {
        int max = MaxScroll();
        return max == 0 ? 1f : 1f - _scroll / (float)max;
    }

    // The log has more rows than fit: the bar shows its thumb.
    public bool Scrollable => MaxScroll() > 0;

    // One piece of a line as it is drawn: the text, its color, and the gap
    // before it (the name gap of a named line).
    public readonly record struct Run(string Text, Color Color, int Gap);

    // The newest line of the log, as the runs the folded chat draws (the list is
    // replaced, not appended: the caller draws every frame): the name in its
    // color, then the gap of a log row, then the text. The tab in use does not
    // matter: the folded bar shows the last message whatever tab is open. The
    // folded bar draws it from the start and cuts the end when it does not fit.
    public void NewestRuns(List<Run> runs, int nameGap)
    {
        runs.Clear();
        if (_lines.Count == 0) return;
        var line = _lines[^1];
        if (line.Name == "")
        {
            runs.Add(new Run(line.Text, SystemColor, 0));
            return;
        }
        runs.Add(new Run(line.Name, NameColor, 0));
        runs.Add(new Run(line.Text, Color.White, nameGap));
    }

    public void Draw(UiGfx gfx)
    {
        var rect = Owner.Get<Rect>()!;
        var box = rect.Screen;
        int gap = _lay.Get("nameGap", 6);
        int lineH = _lay.Get("lineH", 14);

        FillVisible();
        WrapRows(gfx, gap);
        int fits = Fits(rect.Size.Y);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll());

        int x = box.X + _lay.Get("logPadX", 10);
        int y = box.Y + _lay.Get("logPadY", 9);
        int first = Math.Max(0, _rows.Count - fits - _scroll);
        for (int i = first; i < Math.Min(_rows.Count, first + fits); i++)
        {
            var row = _rows[i];
            int tx = x + row.Indent;
            if (row.Name != "")
            {
                var name = gfx.Ui.GetSmall(row.Name);
                gfx.Sb.Draw(name, new Vector2(x, y), NameColor);
                tx = x + name.Width + gap;
            }
            gfx.Sb.Draw(gfx.Ui.GetSmall(row.Text), new Vector2(tx, y), row.System ? SystemColor : Color.White);
            y += lineH;
        }
    }

    // The lines the tab in use shows: All shows everything, Etc only the
    // system lines.
    private void FillVisible()
    {
        _visible.Clear();
        foreach (var line in _lines)
            if (Current == Tab.All || line.Tab == Current) _visible.Add(line);
    }

    // The rows of the tab in use: every line wrapped to the log width. A named
    // line wraps its text after the name, so its next rows line up with it.
    private void WrapRows(UiGfx gfx, int gap)
    {
        _rows.Clear();
        foreach (var line in _visible)
        {
            if (line.Name == "")
            {
                foreach (string part in gfx.Ui.Wrap(line.Text, WrapW)) _rows.Add(new Row("", part, true, 0));
                continue;
            }
            var name = gfx.Ui.GetSmall(line.Name);
            int indent = name.Width + gap;
            var parts = gfx.Ui.Wrap(line.Text, Math.Max(WrapW - indent, 1)); // a name wider than the log still wraps the text at one character a line
            _rows.Add(new Row(line.Name, parts[0], false, 0));
            for (int i = 1; i < parts.Count; i++) _rows.Add(new Row("", parts[i], false, indent));
        }
    }

    // The rows that fit in the log frame.
    private int Fits(int height) => (height - 2 * _lay.Get("border", 8)) / _lay.Get("lineH", 14);

    // How far the log can be scrolled up: the rows that do not fit.
    private int MaxScroll() => Math.Max(0, _rows.Count - Fits(Owner.Get<Rect>()!.Size.Y));
}
