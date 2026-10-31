using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The chat window: the official MapleStory chat (UI/StatusBar3.img/chat).
// The log frame, the filter tabs, the scroll bar, the chevron and the input
// line are official art; the log text uses the game font. The window is on
// the screen all the time. The lines live in ChatLog and the typed text in
// CaretText: the window keeps the frame, the chevron and the keyboard.
public sealed class ChatWindow : UiWindow
{
    // The art the exporter writes (assets/ui/chat_*.png).
    private static readonly string[] FramePieces = { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" };
    private static readonly string[] TabStems = { "chat_taball", "chat_tabetc" };
    // The one window button that is left: the chevron of the tab row. It
    // folds the chat into one line; the folded line shows a "+" instead.
    private const string ChevStem = "chat_btmin";

    // Every number of this window lives in layouts/panel-chat.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-chat.json"));

    // The box sizes of the art (the art is stretched to its box): chevW/chevH,
    // tabW/tabH, targetW/targetH and scrollW are in the layout file.
    private int ChevW => _lay.Get("chevW", 26);
    private int ChevH => _lay.Get("chevH", 20);
    private int TabW => _lay.Get("tabW", 54);
    private int TabH => _lay.Get("tabH", 20);
    private int ScrollW => _lay.Get("scrollW", 5);

    private readonly MushTownGame _game;
    private readonly Action<string> _send;
    private readonly ChatLog _log;
    private readonly CaretText _caret;
    private readonly HashSet<Keys> _down = new(); // the keys that were down last frame
    private bool _minimised; // the chat is folded into one line
    private bool _partyTarget; // the line goes to the party instead of the world
    private int _wheel; // the wheel value of the last frame
    private Keys _repeatKey = Keys.None; // the key the repeat is counting
    private float _repeatMs;             // how long it has been held
    private bool _foldWhenDone;          // Enter opened the folded chat: it folds again when the input leaves
    private bool _foldPending;           // the fold waits for the next update: the click that caused it must reach the log or a tab first
    private readonly List<ChatLog.Run> _runs = new(); // the newest line, in the log colors (the log clears it each draw)

    // The chat opens from Enter or from the Chat key.
    public ChatWindow(MushTownGame game, Action<string> send) : base(HotkeyAction.Chat)
    {
        _game = game;
        _send = send;
        _log = new ChatLog(_lay);
        _caret = new CaretText(_lay);
        Show = true;
        Pos = LayoutPos(); // the layout file gives the spot (the game sets it again on a reload)
        AddSystem("Welcome to MushTown."); // dummy lines: the log shows its two kinds
        AddLine("Guide", "This is a sample chat line.");
    }

    // The chat is never closed by the game: Esc leaves the input instead.
    public override void OnClosed() => Show = true;

    // The window box. The folded chat is one row at the bottom of the open
    // box (the newest line and a "+"), so folding pulls the chat down and
    // opening it grows upwards from that row.
    public override Rectangle Rect() => _minimised
        ? new Rectangle(0, _lay.Get("h", 150) - _lay.Get("minH", 20), _lay.Get("w", 315), _lay.Get("minH", 20))
        : new Rectangle(0, 0, _lay.Get("w", 315), _lay.Get("h", 150));
    // The empty part of the tab row drags the window. The folded chat has no
    // tab row: the rect is read in the open state only (its whole bar drags,
    // built there).
    public override Rectangle TitleRect()
    {
        if (_minimised) return Rect(); // the whole folded bar drags (built there)
        int x = TabRect(TabStems.Length - 1).Right;
        return new Rectangle(x, _lay.Get("tabY", 0), ChevRect().X - x, TabH);
    }
    public override Point LayoutPos() => new(_lay.Get("x", 6), _lay.Get("y", 566));

    // The chat swallows the clicks it draws. The typing row is not drawn while
    // no line is typed, so the bar under the log has to go to the world; the
    // folded chat is one line.
    public override Point HitSize() => new(_lay.Get("w", 315), _minimised
        ? _lay.Get("minH", 20)
        : _caret.Focused ? _lay.Get("inputY", 125) + _lay.Get("inputH", 25)
        : _lay.Get("logY", 22) + _lay.Get("logH", 100));

    // The log frame: the official nine piece slice under the tabs.
    private Rectangle LogRect() => new(0, _lay.Get("logY", 22), _lay.Get("w", 315), _lay.Get("logH", 100));

    // The width a log line wraps at: the frame, less the padding, the frame
    // border and the scroll bar column.
    private int LogTextW() =>
        _lay.Get("w", 315) - _lay.Get("logPadX", 10) - _lay.Get("border", 8) - ScrollW - _lay.Get("logGap", 2);

    // The scroll bar of the log: on the right of the log, inside the frame.
    private Rectangle ScrollRect()
    {
        var log = LogRect();
        int b = _lay.Get("border", 8);
        return new Rectangle(log.Right - b - ScrollW, log.Y + b, ScrollW, log.Height - 2 * b);
    }

    // The input line: the official bar, the whole width of the window.
    private Rectangle InputRect() => new(0, _lay.Get("inputY", 125), _lay.Get("w", 315), _lay.Get("inputH", 25));

    // The chevron at the top right of the tab row: it folds the chat. It rides
    // the tab row, so moving the tabs keeps it level with them; chevY moves it
    // on its own.
    private Rectangle ChevRect() => new(_lay.Get("w", 315) - ChevW - _lay.Get("chevGap", 4),
        _lay.Get("chevY", _lay.Get("tabY", 0)), ChevW, ChevH);

    // The "+" on the folded line (its own top-left is the bar's): the only
    // spot that opens the chat again.
    private Rectangle PlusRect()
    {
        int w = _lay.Get("plusW", 18);
        return new Rectangle(_lay.Get("w", 315) - w, 0, w, _lay.Get("minH", 20));
    }

    // The chat target button: it says where the line goes when it is
    // clicked (the world or the party).
    private Rectangle TargetRect() => new(_lay.Get("targetX", 6), _lay.Get("inputY", 125) + _lay.Get("targetY", 1),
        _lay.Get("targetW", 60), _lay.Get("targetH", 24));

    // One filter tab: the row starts at tabX and each tab keeps its art size
    // with tabGap between them.
    private Rectangle TabRect(int i) => new(_lay.Get("tabX", 0) + i * (TabW + _lay.Get("tabGap", 3)),
        _lay.Get("tabY", 0), TabW, TabH);

    // Once a frame, before the input poll: the wheel scrolls the log and the
    // fold a click asked for happens. The click that asked for it had to reach
    // the log or a tab of the open window first.
    public void UpdateFrame()
    {
        ReadWheel(); // the wheel is read here, so Draw only draws
        if (!_foldPending) return;
        _foldPending = false;
        _minimised = true;
        Dirty = true;
    }

    // True while the input has the focus: the game gives the keys to the chat.
    public bool Focused => _caret.Focused;

    // Enter on the chat (or the Chat key): unfold the window if it is
    // minimised (the input row is not drawn there), and seed the held keys so
    // the key that opened the input is not typed into it on the next frame.
    public void FocusInput(KeyboardState kb)
    {
        if (_minimised) { Restore(); _foldWhenDone = true; } // Enter on the folded chat: it folds again when the line is done
        _caret.Focused = true;
        Dirty = true; // the typing row shows up
        _repeatKey = Keys.None;
        _repeatMs = 0;
        _down.Clear();
        foreach (var key in kb.GetPressedKeys()) _down.Add(key);
    }

    // The chat target: the line goes to the party instead of the world.
    public bool PartyTarget => _partyTarget;

    // The input leaves: the typing row hides again (and the chat folds if Enter opened it).
    private void Unfocus()
    {
        if (!_caret.Focused) return;
        _caret.Focused = false;
        _repeatKey = Keys.None; // a held key must not type into the input that just left
        _repeatMs = 0;
        if (_foldWhenDone) { _foldWhenDone = false; _foldPending = true; } // sending or a click outside folds it again, on the next update
        Dirty = true;
    }

    // A shot never runs Update: these pose the window by the file name (the
    // folded bar and a line longer than the field).
    internal void FoldForShot() => _minimised = true;

    // A name and one long word: the log wraps it over two rows (no space to
    // break at), so the folded bar has to cut it at the end.
    internal void LongForShot() => AddLine("asdfadsf", "sdfads" + new string('f', 45));

    internal void TypeForShot(string text)
    {
        _caret.Focused = true;
        foreach (char c in text) _caret.Type(c);
    }

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
    public void AddLine(string name, string text) => _log.AddLine(name, text);

    // A system line (the Etc. tab).
    public void AddSystem(string text) => _log.AddSystem(text);

    // How long a key is held before it repeats, and how fast it repeats after
    // that (the OS defaults: about half a second, then 25 a second).
    private const float RepeatDelayMs = 400f;
    private const float RepeatRateMs = 40f;

    // The keys go into the input while it has the focus: Enter sends the
    // line, Backspace deletes, Esc leaves the input, a held key repeats like
    // it does in the rest of the system. A click outside the input line
    // leaves the input too.
    public void UpdateKeys(KeyboardState kb, float dtMs)
    {
        var input = _game.Input;
        if (input.Click && !OnScreen(InputRect()).Contains(input.Mouse.X, input.Mouse.Y)) Unfocus();

        var now = kb.GetPressedKeys();
        foreach (var key in now)
        {
            if (!_caret.Focused) break; // Enter sent the line: those keys go back to the game
            if (_down.Contains(key)) continue; // held down: the repeat below handles it
            if (key == Keys.Enter) { Send(); continue; }
            if (key == Keys.Escape) { Unfocus(); continue; }
            Press(key);
            if (CharOf(key) == '\0' && key != Keys.Back) continue; // a key that types nothing does not stop the repeat
            _repeatKey = key; // keep typing while it stays down
            _repeatMs = 0;
        }
        if (_repeatKey != Keys.None && !kb.IsKeyDown(_repeatKey)) _repeatKey = Keys.None;
        else if (_repeatKey != Keys.None)
        {
            _repeatMs += dtMs;
            if (_repeatMs >= RepeatDelayMs)
            {
                Press(_repeatKey);
                _repeatMs = Math.Min(_repeatMs - RepeatRateMs, RepeatDelayMs - RepeatRateMs); // a long stall must not dump a burst of characters
            }
        }
        _down.Clear();
        foreach (var key in now) _down.Add(key);
    }

    // One press: a character goes into the text, Backspace deletes.
    private void Press(Keys key)
    {
        if (key == Keys.Back) { _caret.Backspace(); return; }
        char c = CharOf(key);
        if (c != '\0') _caret.Type(c);
    }

    // Enter: the line goes out, the input is cleared and the focus leaves
    // the input (the player is back in the game). The bubble over the
    // character shows the same line for a few seconds.
    private void Send()
    {
        string text = _caret.Text.Trim();
        _caret.Clear();
        Unfocus(); // clears the repeat state too
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
        foreach (string piece in FramePieces) _game.LoadTexture($"ui/chat_frame_min_{piece}.png");
        foreach (string stem in TabStems)
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        foreach (string piece in new[] { "w", "c", "e" }) _game.LoadTexture($"ui/chat_input_{piece}.png");
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{ChevStem}{state}");
        foreach (string stem in new[] { "chat_target_all", "chat_target_party" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" }) _game.LoadTexture($"ui/{stem}{state}");
        foreach (string stem in new[] { "chat_scroll", "chat_scroll_off" })
            foreach (string piece in new[] { "base_t", "base_c", "base_b" }) _game.LoadTexture($"ui/{stem}_{piece}.png");
        foreach (string piece in new[] { "thumb0", "thumb1", "thumb2" }) _game.LoadTexture($"ui/chat_scroll_{piece}.png");

        _log.WrapW = LogTextW(); // the layout can change the log width between builds
        Host.Clear();
        var root = new GameObject();
        // the host carries the window offset; the root starts at its own box.
        // Without a line typed the typing row is not drawn, so the window does
        // not eat clicks there either: it ends with the log frame.
        root.Add(new Rect(new Rectangle(0, 0, Rect().Width, HitSize().Y))); // the same box the host swallows clicks with
        root.Add(new Invoke(Draw));

        if (_minimised)
        {
            // the folded chat: the line drags the window, only the "+" opens it.
            // The children of the root hang at the bar, so their rects start at 0.
            var bar = new Rectangle(0, 0, Rect().Width, Rect().Height);
            var box = new GameObject();
            box.Add(new Rect(bar));
            box.Add(new Button { OnClick = StartDrag });
            root.AddChild(box);

            var plus = new GameObject();
            plus.Add(new Rect(PlusRect()));
            plus.Add(new Button { Kind = ButtonKind.Click, OnClick = Restore });
            root.AddChild(plus);
            Host.AddChild(root);
            return;
        }

        // the filter tabs: the click picks the tab (the art carries the text)
        for (int i = 0; i < TabStems.Length; i++)
        {
            int n = i;
            var tab = new GameObject();
            tab.Add(new Rect(TabRect(i)));
            tab.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _log.Current = (ChatLog.Tab)n });
            tab.Add(new Image($"ui/{TabStems[i]}"));
            root.AddChild(tab);
        }

        // the log and its scroll bar: a click leaves the input
        var log = new GameObject();
        log.Add(new Rect(LogRect()));
        log.Add(new Button { Kind = ButtonKind.Click, OnClick = Unfocus });
        log.Add(new Box("chat_frame")); // the official nine piece frame: the corners stay, the edges stretch
        log.Add(_log);
        root.AddChild(log);

        var scroll = new GameObject();
        scroll.Add(new Rect(ScrollRect()));
        scroll.Add(new Button { Kind = ButtonKind.Silent, OnClick = Unfocus });
        scroll.Add(new ChatScrollBar(() => _log.ScrollPos(), () => _log.Scrollable));
        root.AddChild(scroll);

        // the input line and the chat target show only while a line is typed
        // (Enter starts one): the log is on the screen all the time, the
        // typing row is not.
        if (_caret.Focused)
        {
            var input = new GameObject();
            input.Add(new Rect(InputRect()));
            input.Add(_caret);
            root.AddChild(input);

            // the chat target button: it shows where the line goes now (the
            // official art: "To All" or "To Party")
            var target = new GameObject();
            target.Add(new Rect(TargetRect()));
            target.Add(new Button { Kind = ButtonKind.Click, OnClick = ToggleTarget });
            target.Add(new Image(_partyTarget ? "ui/chat_target_party" : "ui/chat_target_all"));
            root.AddChild(target);
        }

        // the free part of the tab row drags the window
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));

        // the chevron: the top right of the tab row, it folds the chat
        var chev = new GameObject();
        chev.Add(new Rect(ChevRect()));
        chev.Add(new Button { Kind = ButtonKind.Click, OnClick = Minimise });
        chev.Add(new Image($"ui/{ChevStem}"));
        root.AddChild(chev);

        Host.AddChild(root);
    }

    // The one part the window still draws: the folded chat. The row shows the
    // newest line and the "+" says that a click there opens it. Everything
    // else is a component.
    private void Draw(UiGfx gfx)
    {
        if (!_minimised) return;
        var r = OnScreen(Rect());
        NineSlice.Draw(gfx, "chat_frame_min", r);
        int pad = _lay.Get("minPad", 6);
        int plusW = _lay.Get("plusW", 18);
        var plus = gfx.Ui.GetSmall("+");
        gfx.Sb.Draw(plus, new Vector2(r.Right - plusW + (plusW - plus.Width) / 2,
            r.Y + (r.Height - plus.Height) / 2), Color.White);

        // The newest line in the colors of the log: the name, the gap of a log
        // row, the text. It is drawn from the start and cut at the end when it
        // is wider than the bar.
        _log.NewestRuns(_runs, _lay.Get("nameGap", 6));
        int x = r.X + pad;
        int end = r.Right - plusW;
        foreach (var run in _runs)
        {
            x += run.Gap;
            var t = gfx.Ui.GetSmall(run.Text);
            int w = Math.Min(t.Width, end - x);
            if (w <= 0) break;
            gfx.Sb.Draw(t, new Rectangle(x, r.Y + (r.Height - t.Height) / 2, w, t.Height),
                new Rectangle(0, 0, w, t.Height), run.Color);
            x += w;
        }
    }

    // The mouse wheel scrolls the log. The chat is on the screen all the
    // time, so the wheel is read where the chat draws.
    private void ReadWheel()
    {
        int wheel = _game.Input.Wheel;
        int delta = wheel - _wheel;
        _wheel = wheel;
        if (delta == 0 || _minimised) return;
        if (!OnScreen(LogRect()).Contains(_game.Input.Mouse.X, _game.Input.Mouse.Y)) return;
        _log.Scroll(delta / 120);
    }

    // The chevron folds the chat into one line; the "+" on that line brings
    // it back (a drag on the line only moves it).
    private void Minimise()
    {
        _foldWhenDone = false; // the chevron or the "+" decide from here, not the Enter that opened the input
        Unfocus(); // drops the caret and the repeat state
        _minimised = true;
        _foldPending = false;
        Dirty = true; // the folded bar replaces the log and the typing row
    }

    private void Restore()
    {
        _minimised = false;
        _foldPending = false;
        _foldWhenDone = false; // opened by the "+": sending a line must not fold it back
        Dirty = true;
    }
}
