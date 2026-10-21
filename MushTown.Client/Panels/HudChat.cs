using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The chat window: the official MapleStory chat (UI/StatusBar3.img/chat).
// The log frame, the filter tabs and the input line are official art; the
// log text uses the game font. The window is on the screen all the time.
public sealed class ChatWindow : UiWindow
{
    // The filter tabs, in the order they sit in the window. The official
    // chat has four tabs, but Party and Friend need a party and a whisper:
    // the protocol has neither, so those tabs are not drawn.
    private enum Tab { All, Etc }

    // One log line. The tab says where the line shows up: a normal chat
    // line is All, a system line is Etc.
    private sealed record Line(string Name, string Text, Tab Tab);

    // The art the exporter writes (assets/ui/chat_*.png).
    private static readonly string[] FramePieces = { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" };
    private static readonly string[] TabStems = { "chat_taball", "chat_tabetc" };
    // Only the settings button has a function in this game: the other
    // official buttons (whisper, link, command, emoticon) are not drawn.
    private static readonly string[] BtnStems = { "chat_btn_set" };

    // Every number of this window lives in layouts/panel-chat.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-chat.json"));

    private const int MaxInput = 100; // the typed text stops here
    private const int MaxLines = 50; // the log keeps the last lines

    private readonly MushTownGame _game;
    private readonly Action<string> _send;
    private readonly List<Line> _lines = new();
    private readonly HashSet<Keys> _down = new(); // the keys that were down last frame
    private readonly GameObject[] _tabObjs = new GameObject[TabStems.Length];
    private Tab _tab = Tab.All;
    private string _input = "";
    private bool _focused;
    private bool _mouseDown; // the mouse button was down last frame

    // The chat has no key of its own: the input line is clicked.
    public ChatWindow(MushTownGame game, Action<string> send) : base(HotkeyAction.Chat)
    {
        _game = game;
        _send = send;
        Show = true;
        Pos = LayoutPos(); // the layout file gives the spot (the game sets it again on a reload)
        AddSystem("Welcome to MushTown."); // dummy lines: the log shows its two kinds
        AddLine("Guide", "This is a sample chat line.");
    }

    // The chat is never closed by the game: Esc leaves the input instead.
    public override void OnClosed() => Show = true;

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 315), _lay.Get("h", 150));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, 0); // the chat has no title bar
    public override Point LayoutPos() => new(_lay.Get("x", 6), _lay.Get("y", 566));

    // The log frame: the official nine piece slice under the tabs.
    private Rectangle LogRect() => new(Pos.X, Pos.Y + _lay.Get("logY", 22), _lay.Get("w", 315), _lay.Get("logH", 100));

    // The input line: the official bar, the buttons to the right of it.
    private int BarW() => _lay.Get("w", 315) - BtnStems.Length * (_lay.Get("btnW", 24) + _lay.Get("btnGap", 1));
    private Rectangle InputRect() => new(Pos.X, Pos.Y + _lay.Get("inputY", 125), BarW(), _lay.Get("inputH", 25));
    private Rectangle BtnRect(int i) => new(Pos.X + BarW() + i * (_lay.Get("btnW", 24) + _lay.Get("btnGap", 1)),
        Pos.Y + _lay.Get("inputY", 125), _lay.Get("btnW", 24), _lay.Get("inputH", 25));

    // One filter tab.
    private Rectangle TabRect(int i) => new(Pos.X + i * (_lay.Get("tabW", 54) + _lay.Get("tabGap", 3)), Pos.Y + _lay.Get("tabY", 0),
        _lay.Get("tabW", 54), _lay.Get("tabH", 20));

    // True while the input has the focus: the game gives the keys to the chat.
    public bool Focused => _focused;

    // A chat line from a player.
    public void AddLine(string name, string text)
    {
        _lines.Add(new Line(name, text, Tab.All));
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
    }

    // A system line (the Etc. tab).
    public void AddSystem(string text)
    {
        _lines.Add(new Line("", text, Tab.Etc));
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
    }

    // The keys go into the input while it has the focus: Enter sends the
    // line, Backspace deletes, Esc leaves the input. A click outside the
    // input line leaves the input too.
    public void UpdateKeys(KeyboardState kb)
    {
        var ms = Mouse.GetState();
        bool click = ms.LeftButton == ButtonState.Pressed;
        if (click && !_mouseDown)
        {
            var (gx, gy) = _game.MouseGamePos(ms);
            if (!InputRect().Contains(gx, gy)) _focused = false;
        }
        _mouseDown = click;

        var now = kb.GetPressedKeys();
        foreach (var key in now)
        {
            if (_down.Contains(key)) continue; // held down: not a new press
            if (key == Keys.Enter) { Send(); continue; }
            if (key == Keys.Escape) { _focused = false; continue; }
            if (key == Keys.Back) { if (_input.Length > 0) _input = _input[..^1]; continue; }
            char c = CharOf(key);
            if (c != '\0' && _input.Length < MaxInput) _input += c;
        }
        _down.Clear();
        foreach (var key in now) _down.Add(key);
    }

    // Enter: the line goes out, the input is cleared and the focus leaves
    // the input (the player is back in the game). The bubble over the
    // character shows the same line for a few seconds.
    private void Send()
    {
        string text = _input.Trim();
        _input = "";
        _focused = false;
        if (text == "") return;
        _send(text);
        _game.ShowBalloon(text);
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

    public override void Build()
    {
        foreach (string piece in FramePieces) _game.LoadTexture($"ui/chat_frame_{piece}.png");
        foreach (string stem in TabStems)
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        foreach (string piece in new[] { "w", "c", "e" }) _game.LoadTexture($"ui/chat_input_{piece}.png");
        foreach (string stem in BtnStems)
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Invoke(Draw));

        // the filter tabs: the click picks the tab
        for (int i = 0; i < TabStems.Length; i++)
        {
            int n = i;
            var tab = new GameObject();
            tab.Add(new Rect(TabRect(i)));
            tab.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _tab = (Tab)n });
            _tabObjs[i] = tab;
            root.AddChild(tab);
        }

        // the log: a click leaves the input
        var log = new GameObject();
        log.Add(new Rect(LogRect()));
        log.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _focused = false });
        root.AddChild(log);

        // the input line: a click focuses it
        var input = new GameObject();
        input.Add(new Rect(InputRect()));
        input.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _focused = true });
        root.AddChild(input);

        // the settings button: the official art, it opens the options window.
        // The other official buttons (whisper, link, command, emoticon) have
        // no function in this game yet, so they are not drawn.
        var set = new GameObject();
        set.Add(new Rect(BtnRect(BtnStems.Length - 1)));
        set.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _game.OpenSettings() });
        set.Add(new Image($"ui/{BtnStems[^1]}"));
        root.AddChild(set);

        Scene.Add(root);
    }

    // The whole chat: the frame, the tabs, the log and the input line.
    private void Draw(UiGfx gfx)
    {
        DrawFrame(gfx, LogRect());
        DrawTabs(gfx);
        DrawLog(gfx);
        DrawInput(gfx);
    }

    // The official nine piece frame: the corners stay, the edges stretch.
    private void DrawFrame(UiGfx gfx, Rectangle r) => NineSlice.Draw(gfx, "chat_frame", r);

    // The filter tabs: All and Etc. carry their text in the official art.
    // The tab that is not in use is dim.
    private void DrawTabs(UiGfx gfx)
    {
        for (int i = 0; i < TabStems.Length; i++)
        {
            var r = TabRect(i);
            bool over = gfx.Hovered == _tabObjs[i];
            string state = over ? (gfx.Pressed ? "_p.png" : "_h.png") : "_n.png";
            var tint = _tab == (Tab)i ? Color.White : new Color(140, 145, 155);
            gfx.Sb.Draw(gfx.Tex($"ui/{TabStems[i]}{state}"), new Vector2(r.X, r.Y), tint);
        }
    }

    // The log lines of the tab in use, the newest at the bottom.
    private void DrawLog(UiGfx gfx)
    {
        var box = LogRect();
        int x = box.X + _lay.Get("logPadX", 10);
        int y = box.Y + _lay.Get("logPadY", 9);
        int lineH = _lay.Get("lineH", 14);
        var lines = VisibleLines();
        int first = Math.Max(0, lines.Count - (box.Height - 2 * _lay.Get("border", 8)) / lineH);
        for (int i = first; i < lines.Count; i++)
        {
            var line = lines[i];
            int ty = y + (i - first) * lineH;
            if (line.Name == "")
                gfx.Sb.Draw(gfx.Ui.GetSmall(line.Text), new Vector2(x, ty), new Color(255, 220, 120));
            else
            {
                var name = gfx.Ui.GetSmall(line.Name);
                gfx.Sb.Draw(name, new Vector2(x, ty), new Color(120, 190, 255));
                gfx.Sb.Draw(gfx.Ui.GetSmall(line.Text), new Vector2(x + name.Width + 6, ty), Color.White);
            }
        }
    }

    // The input line: the official bar (left cap, middle, right cap), the
    // typed text and the caret.
    private void DrawInput(UiGfx gfx)
    {
        var bar = InputRect();
        int cap = _lay.Get("capW", 7); // the width of the left and right cap
        gfx.Sb.Draw(gfx.Tex("ui/chat_input_w.png"), new Vector2(bar.X, bar.Y), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/chat_input_c.png"), new Rectangle(bar.X + cap, bar.Y, bar.Width - 2 * cap, bar.Height), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/chat_input_e.png"), new Vector2(bar.Right - cap, bar.Y), Color.White);
        var t = gfx.Ui.GetSmall(_input);
        int tx = bar.X + _lay.Get("inputPadX", 8);
        gfx.Sb.Draw(t, new Vector2(tx, bar.Y + (bar.Height - t.Height) / 2), Color.White);
        if (_focused) gfx.Sb.Draw(gfx.Pixel, new Rectangle(tx + t.Width + 1, bar.Y + 6, 1, 12), Color.White);
    }

    // The lines the tab in use shows: All shows everything, the other tabs
    // show the lines of their own kind.
    private List<Line> VisibleLines()
    {
        var result = new List<Line>();
        foreach (var line in _lines)
            if (_tab == Tab.All || line.Tab == _tab) result.Add(line);
        return result;
    }
}
