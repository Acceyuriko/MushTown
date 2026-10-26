using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The chat window: the official MapleStory chat (UI/StatusBar3.img/chat).
// The log frame, the filter tabs, the scroll bar, the window buttons and
// the input line are official art; the log text uses the game font. The
// window is on the screen all the time.
public sealed class ChatWindow : UiWindow
{
    // The filter tabs, in the order they sit in the window. The official
    // chat has four tabs, but Friend needs a whisper: the protocol has
    // none, so that tab is not drawn. The party lines show in the All tab
    // and the party chat button in the input row sends to the party.
    private enum Tab { All, Etc }

    // One log line. The tab says where the line shows up: a normal chat
    // line (and a party line) is All, a system line is Etc.
    private sealed record Line(string Name, string Text, Tab Tab);

    // The art the exporter writes (assets/ui/chat_*.png).
    private static readonly string[] FramePieces = { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" };
    private static readonly string[] TabStems = { "chat_taball", "chat_tabetc" };
    // Only the settings button has a function in this game: the other
    // official buttons (whisper, link, command, emoticon) are not drawn.
    private static readonly string[] BtnStems = { "chat_btn_set" };
    // The official window buttons of the chat. Only the minimise works in
    // this game, so the other three are art without a click. The width and
    // the height are the art sizes.
    private static readonly (string Stem, int W, int H)[] WinArtBtns =
    {
        ("chat_btaddtab", 25, 20),
        ("chat_btmax", 15, 15),
        ("chat_btoutchat", 26, 20),
    };
    private const string MinBtnStem = "chat_btmin";
    private const int MinBtnW = 26, MinBtnH = 20; // the art size
    private const int ScrollW = 5; // the width of the scroll bar art

    // Every number of this window lives in layouts/panel-chat.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-chat.json"));

    private const int MaxInput = 100; // the typed text stops here
    private const int MaxLines = 50; // the log keeps the last lines

    private readonly MushTownGame _game;
    private readonly Action<string> _send;
    private readonly List<Line> _lines = new();
    private readonly List<Line> _visible = new(); // the lines of the tab in use
    private readonly HashSet<Keys> _down = new(); // the keys that were down last frame
    private readonly GameObject[] _tabObjs = new GameObject[TabStems.Length];
    private Tab _tab = Tab.All;
    private string _input = "";
    private bool _focused;
    private bool _mouseDown; // the mouse button was down last frame
    private bool _minimised; // the chat is folded into a small box
    private bool _partyTarget; // the line goes to the party instead of the world
    private int _scroll; // the lines the log is scrolled up from the newest
    private int _wheel; // the wheel value of the last frame
    private GameObject? _minBox; // the minimised box (the hover changes its art)

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

    // The minimised chat is a small box at the same spot.
    public override Rectangle Rect() => _minimised
        ? new Rectangle(Pos.X, Pos.Y, _lay.Get("minW", 60), _lay.Get("minH", 20))
        : new Rectangle(Pos.X, Pos.Y, _lay.Get("w", 315), _lay.Get("h", 150));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, 0); // the chat has no title bar
    public override Point LayoutPos() => new(_lay.Get("x", 6), _lay.Get("y", 566));

    // The log frame: the official nine piece slice under the tabs.
    private Rectangle LogRect() => new(Pos.X, Pos.Y + _lay.Get("logY", 22), _lay.Get("w", 315), _lay.Get("logH", 100));

    // The scroll bar of the log: on the right of the log, inside the frame.
    private Rectangle ScrollRect()
    {
        var log = LogRect();
        int b = _lay.Get("border", 8);
        return new Rectangle(log.Right - b - ScrollW, log.Y + b, ScrollW, log.Height - 2 * b);
    }

    // The input line: the official bar, the buttons to the right of it.
    private int ButtonsW()
    {
        int w = _lay.Get("btnW", 24); // the settings button
        foreach (var (_, bw, _) in WinArtBtns) w += bw + _lay.Get("btnGap", 1);
        return w + MinBtnW + _lay.Get("btnGap", 1);
    }
    private int BarW() => _lay.Get("w", 315) - ButtonsW();
    // The input field starts after the chat target button (the official
    // spot: the target button is 60 wide at x=6, the field at x=71).
    private int FieldX() => _lay.Get("fieldX", 71);
    private Rectangle InputRect() => new(Pos.X, Pos.Y + _lay.Get("inputY", 125), BarW(), _lay.Get("inputH", 25));
    private Rectangle SettingsRect() => new(Pos.X + BarW(), Pos.Y + _lay.Get("inputY", 125), _lay.Get("btnW", 24), _lay.Get("inputH", 25));

    // The chat target button: it says where the line goes when it is
    // clicked (the world or the party).
    private Rectangle TargetRect() => new(Pos.X + _lay.Get("targetX", 6), Pos.Y + _lay.Get("inputY", 125) + _lay.Get("targetY", 1), 60, 24);

    // The window buttons follow the settings button, the minimise last.
    private Rectangle WinArtRect(int i)
    {
        int x = SettingsRect().Right + _lay.Get("btnGap", 1);
        for (int k = 0; k < i; k++) x += WinArtBtns[k].W + _lay.Get("btnGap", 1);
        return new(x, Pos.Y + _lay.Get("inputY", 125) + (_lay.Get("inputH", 25) - WinArtBtns[i].H) / 2, WinArtBtns[i].W, WinArtBtns[i].H);
    }
    private Rectangle MinBtnRect() => new(WinArtRect(WinArtBtns.Length - 1).Right + _lay.Get("btnGap", 1),
        Pos.Y + _lay.Get("inputY", 125) + (_lay.Get("inputH", 25) - MinBtnH) / 2, MinBtnW, MinBtnH);

    // One filter tab.
    private Rectangle TabRect(int i) => new(Pos.X + i * (_lay.Get("tabW", 54) + _lay.Get("tabGap", 3)), Pos.Y + _lay.Get("tabY", 0),
        _lay.Get("tabW", 54), _lay.Get("tabH", 20));

    // The empty tab slot: the official plain tab, the same size as the
    // text tabs. It stands for the tabs this game does not have.
    private Rectangle EmptyTabRect() => new(Pos.X + TabStems.Length * (_lay.Get("tabW", 54) + _lay.Get("tabGap", 3)),
        Pos.Y + _lay.Get("tabY", 0), _lay.Get("tabW", 54), _lay.Get("tabH", 20));

    // True while the input has the focus: the game gives the keys to the chat.
    public bool Focused => _focused;

    // The chat target: the line goes to the party instead of the world.
    public bool PartyTarget => _partyTarget;

    // The target button swaps the target. Without a party there is no party
    // to write to: the button says so instead of doing nothing.
    private void ToggleTarget()
    {
        if (!_partyTarget && _game.PartyMembers.Length == 0)
        {
            AddSystem("you are not in a party.");
            return;
        }
        SetPartyTarget(!_partyTarget);
    }

    // The chat target: the line goes to the party or to the world. The
    // scene is built again so the button shows the other word.
    public void SetPartyTarget(bool on)
    {
        if (_partyTarget == on) return;
        _partyTarget = on;
        Dirty = true; // the game rebuilds the scene on the next frame
    }

    // A chat line from a player. A line without a name is a system line
    // from the server: it belongs to the Etc. tab.
    public void AddLine(string name, string text)
    {
        _lines.Add(new Line(name, text, name == "" ? Tab.Etc : Tab.All));
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
        foreach (string stem in new[] { "chat_frame_min", "chat_frame_min1" })
            foreach (string piece in FramePieces) _game.LoadTexture($"ui/{stem}_{piece}.png");
        foreach (string stem in TabStems)
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        _game.LoadTexture("ui/chat_tab_n.png");
        foreach (string piece in new[] { "w", "c", "e" }) _game.LoadTexture($"ui/chat_input_{piece}.png");
        foreach (string stem in BtnStems)
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        foreach (string stem in new[] { "chat_btaddtab", "chat_btmax", "chat_btoutchat", MinBtnStem })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        foreach (string stem in new[] { "chat_target_all", "chat_target_party" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        foreach (string stem in new[] { "chat_scroll", "chat_scroll_off" })
            foreach (string piece in new[] { "base_t", "base_c", "base_b" }) _game.LoadTexture($"ui/{stem}_{piece}.png");
        foreach (string piece in new[] { "thumb0", "thumb1", "thumb2" }) _game.LoadTexture($"ui/chat_scroll_{piece}.png");

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Invoke(Draw));

        if (_minimised)
        {
            // the minimised chat: a click brings the chat back
            var box = new GameObject();
            box.Add(new Rect(Rect()));
            box.Add(new Button { Kind = ButtonKind.Click, OnClick = Restore });
            _minBox = box;
            root.AddChild(box);
            Host.AddChild(root);
            return;
        }

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

        // the empty tab slot: the official plain tab
        var empty = new GameObject();
        empty.Add(new Rect(EmptyTabRect()));
        empty.Add(new Picture("ui/chat_tab_n.png"));
        root.AddChild(empty);

        // the log: a click leaves the input
        var log = new GameObject();
        log.Add(new Rect(LogRect()));
        log.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _focused = false });
        root.AddChild(log);

        // the scroll bar of the log
        var scroll = new GameObject();
        scroll.Add(new Rect(ScrollRect()));
        scroll.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => _focused = false });
        scroll.Add(new ChatScrollBar(() => ScrollPos(), () => MaxScroll() > 0));
        root.AddChild(scroll);

        // the input line: a click focuses it
        var input = new GameObject();
        input.Add(new Rect(InputRect()));
        input.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _focused = true });
        root.AddChild(input);

        // the chat target button: it shows where the line goes now (the
        // official art: "To All" or "To Party")
        var target = new GameObject();
        target.Add(new Rect(TargetRect()));
        target.Add(new Button { Kind = ButtonKind.Click, OnClick = ToggleTarget });
        target.Add(new Image(_partyTarget ? "ui/chat_target_party" : "ui/chat_target_all"));
        root.AddChild(target);

        // the settings button: the official art, it opens the options window.
        // The other official buttons (whisper, link, command, emoticon) have
        // no function in this game yet, so they are not drawn.
        var set = new GameObject();
        set.Add(new Rect(SettingsRect()));
        set.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _game.OpenSettings() });
        set.Add(new Image($"ui/{BtnStems[0]}"));
        root.AddChild(set);

        // the three official window buttons without a function: art only
        for (int i = 0; i < WinArtBtns.Length; i++)
        {
            var b = new GameObject();
            b.Add(new Rect(WinArtRect(i)));
            b.Add(new Picture($"ui/{WinArtBtns[i].Stem}_n.png"));
            root.AddChild(b);
        }

        // the minimise button: the chat folds into a small box
        var min = new GameObject();
        min.Add(new Rect(MinBtnRect()));
        min.Add(new Button { Kind = ButtonKind.Click, OnClick = Minimise });
        min.Add(new Image($"ui/{MinBtnStem}"));
        root.AddChild(min);

        Host.AddChild(root);
    }

    // The whole chat: the frame, the tabs, the log and the input line.
    private void Draw(UiGfx gfx)
    {
        FillVisible(); // the line list comes first: the wheel needs the new scroll range
        ReadWheel();
        if (_minimised)
        {
            DrawMinimised(gfx);
            return;
        }
        DrawFrame(gfx, LogRect());
        DrawTabs(gfx);
        DrawLog(gfx);
        DrawInput(gfx);
    }

    // The official nine piece frame: the corners stay, the edges stretch.
    private void DrawFrame(UiGfx gfx, Rectangle r) => NineSlice.Draw(gfx, "chat_frame", r);

    // The minimised chat: the official small frame. The mouse over it
    // shows the plainer variant of the same frame.
    private void DrawMinimised(UiGfx gfx)
    {
        bool over = gfx.Hovered == _minBox;
        NineSlice.Draw(gfx, over ? "chat_frame_min1" : "chat_frame_min", Rect());
    }

    // The filter tabs: All and Etc. carry their text in the official art.
    // The official art has no "selected" look (the checked state of this WZ
    // is the same file as the normal one), so the art is never recolored.
    private void DrawTabs(UiGfx gfx)
    {
        for (int i = 0; i < TabStems.Length; i++)
        {
            var r = TabRect(i);
            bool over = gfx.Hovered == _tabObjs[i];
            string state = over ? (gfx.Pressed ? "_p.png" : "_h.png") : "_n.png";
            gfx.Sb.Draw(gfx.Tex($"ui/{TabStems[i]}{state}"), new Vector2(r.X, r.Y), Color.White);
        }
    }

    // The log lines of the tab in use. The scroll says how far the log is
    // scrolled up from the newest line.
    private void DrawLog(UiGfx gfx)
    {
        var box = LogRect();
        int x = box.X + _lay.Get("logPadX", 10);
        int y = box.Y + _lay.Get("logPadY", 9);
        int lineH = _lay.Get("lineH", 14);
        int rows = LogRows();
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _visible.Count - rows));
        int first = Math.Max(0, _visible.Count - rows - _scroll);
        for (int i = first; i < Math.Min(_visible.Count, first + rows); i++)
        {
            var line = _visible[i];
            int ty = y + (i - first) * lineH;
            if (line.Name == "")
                gfx.Sb.Draw(gfx.Ui.GetSmall(line.Text), new Vector2(x, ty), new Color(255, 220, 120));
            else
            {
                var name = gfx.Ui.GetSmall(line.Name);
                gfx.Sb.Draw(name, new Vector2(x, ty), new Color(120, 190, 255));
                gfx.Sb.Draw(gfx.Ui.GetSmall(line.Text), new Vector2(x + name.Width + _lay.Get("nameGap", 6), ty), Color.White);
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
        int tx = bar.X + FieldX(); // the text starts after the target button
        gfx.Sb.Draw(t, new Vector2(tx, bar.Y + (bar.Height - t.Height) / 2), Color.White);
        if (_focused) gfx.Sb.Draw(gfx.Pixel, new Rectangle(tx + t.Width + _lay.Get("caretDx", 1), bar.Y + _lay.Get("caretDy", 6),
            _lay.Get("caretW", 1), _lay.Get("caretH", 12)), Color.White);
    }

    // The lines the tab in use shows: All shows everything, the other tabs
    // show the lines of their own kind.
    private void FillVisible()
    {
        _visible.Clear();
        foreach (var line in _lines)
            if (_tab == Tab.All || line.Tab == _tab) _visible.Add(line);
    }

    // The lines that fit in the log.
    private int LogRows() => (LogRect().Height - 2 * _lay.Get("border", 8)) / _lay.Get("lineH", 14);

    // How far the log can be scrolled up: the lines that do not fit.
    private int MaxScroll() => Math.Max(0, _visible.Count - LogRows());

    // The thumb of the scroll bar: 0 at the top of the track, 1 at the
    // bottom. The newest line sits at the bottom, so it starts at 1.
    private float ScrollPos()
    {
        int max = MaxScroll();
        return max == 0 ? 1f : 1f - _scroll / (float)max;
    }

    // The mouse wheel scrolls the log. The chat is on the screen all the
    // time, so the wheel is read where the chat draws.
    private void ReadWheel()
    {
        var ms = Mouse.GetState();
        int delta = ms.ScrollWheelValue - _wheel;
        _wheel = ms.ScrollWheelValue;
        if (delta == 0 || _minimised) return;
        var (gx, gy) = _game.MouseGamePos(ms);
        if (!LogRect().Contains(gx, gy)) return;
        _scroll = Math.Clamp(_scroll + delta / 120, 0, MaxScroll());
    }

    // The minimise button folds the chat into a small box; a click on the
    // box brings the chat back.
    private void Minimise()
    {
        _minimised = true;
        _focused = false;
        Dirty = true; // the game rebuilds the scene on the next frame
    }

    private void Restore()
    {
        _minimised = false;
        Dirty = true;
    }
}
