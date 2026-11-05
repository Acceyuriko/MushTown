using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The quest helper bar at the top right of the HUD: the official bar of
// UI/Quest.img/Helper. It shows the count of the quests the player registered
// from the quest window. The chevron opens or closes the quest list under the
// bar, the X hides the bar (a new quest brings the bar back).
public partial class MushTownGame
{
    // Every number of the bar lives in layouts/panel-qhelper.json.
    internal static readonly PanelLayout _qhLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-qhelper.json"));

    // The art sizes of the official bar: the bar is 222x28, the row 206 wide,
    // the buttons 11x11, the count digits 6x5. The spots come from the layout.
    public const int HelperBarW = 222;
    public const int HelperBarH = 28;
    public const int HelperButtonSize = 11;
    private const int HelperRowCloseW = 9;
    private const int HelperRowCloseH = 10;

    private bool _qhHidden; // the X hid the bar
    private bool _qhListOpen = true; // the chevron shows the quest list
    private int _qhCount; // the quests of the last frame
    private Point _qhPos = QuestHelperLayoutPos(); // the bar top-left: the drag moves it
    private Point? _qhDrag; // the grab offset while the bar is held
    private GameObject _qhRoot = null!; // the bar root: its Rect carries the screen spot
    private GameObject _qhClose = null!; // the X button
    private GameObject _qhChevron = null!; // the chevron button
    private GameObject _qhList = null!; // the quest rows under the bar
    private string _qhBuiltKey = ""; // the rows currently in _qhList

    // One line pair of the list: the row name and the detail under it.
    private sealed record QuestHelperRow(int QuestId, string Name, string Detail);

    // The art of the quest helper: the bar (closed and open), the list body
    // pieces, the ten count digits, and the buttons.
    private static readonly string[] HelperFiles = HelperArtFiles();

    // The bar and the quest list under it. The HUD chain puts it in the
    // game scene. The rows are filled by SyncQuestHelper when the tracked
    // list or its counters change.
    internal GameObject BuildQuestHelper()
    {
        foreach (string f in HelperFiles) LoadTexture("ui/" + f);
        _qhRoot = new GameObject();
        _qhRoot.Add(new Rect(QuestHelperBarRect()));
        var art = new GameObject { NoHit = true }; // the bar art: the buttons and the drag strip take clicks
        art.Add(new Rect(HelperBarLocal()));
        art.Add(new QuestHelperBarArt(QuestHelperState, _qhLay));
        _qhRoot.AddChild(art);
        var list = new GameObject(); // the row blocks and their X buttons
        list.Add(new Rect(HelperBarLocal())); // the rows are local to the bar
        _qhRoot.AddChild(list);
        var drag = new GameObject(); // the bar strip moves the whole helper
        drag.Add(new Rect(HelperBarLocal()));
        drag.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => _qhDrag = _uiScene.LastClick - _qhPos });
        _qhRoot.AddChild(drag);
        // The bar buttons sit after the list and the drag strip: Hit walks
        // children backwards, so they win over both bar-sized hit boxes.
        _qhClose = HelperButton("qhelperclose", _qhLay.Get("closeDx", 201), _qhLay.Get("closeDy", 8), HideQuestHelper, QuestHelperCloseArt);
        _qhChevron = HelperButton("qhelperlist", _qhLay.Get("chevronDx", 184), ChevronDy(), ToggleQuestHelperList, QuestHelperChevronArt);
        _qhRoot.AddChild(_qhClose);
        _qhRoot.AddChild(_qhChevron);
        _qhList = list;
        return _qhRoot;
    }

    // Rebuild the rows only when the tracked list or its live text changes.
    // The bar art draws the body behind them; the row children carry the two
    // text lines and the row X.
    private void SyncQuestHelper()
    {
        var rows = QuestHelperRows();
        if (rows.Count > _qhCount) _qhHidden = false; // a new tracked quest: the bar comes back
        _qhCount = rows.Count;
        bool show = !_qhHidden && rows.Count > 0;
        _qhRoot.Enabled = show;
        _qhList.Enabled = show && _qhListOpen;
        ClampQuestHelper();
        string key = string.Join(";", rows.Select(r => $"{r.QuestId}:{r.Name}:{r.Detail}"));
        if (key == _qhBuiltKey) return;
        _qhBuiltKey = key;
        _qhList.Clear();

        int rowX = _qhLay.Get("rowX", 8);
        int rowW = _qhLay.Get("rowW", 206);
        int rowH = _qhLay.Get("rowH", 42);
        int textPad = _qhLay.Get("rowTextPad", 30);
        int nameX = _qhLay.Get("rowNameX", 12);
        int detailX = _qhLay.Get("rowDetailX", 12);
        int textW = rowW - nameX - textPad;
        int detailW = rowW - detailX - textPad;
        int y = _qhLay.Get("listTop", 32);
        // One hit box behind the rows: the body margins, the gaps between the
        // rows and the 12px bottom cap must not fall through to the world.
        // The rows sit after it, and the bar buttons after the whole list, so
        // those still win.
        var body = new GameObject();
        body.Add(new Rect(new Rectangle(0, 0, HelperBarW,
            QuestHelperBodyH(_qhLay, rows.Count, Tex("qhelper_bottom.png").Height))));
        _qhList.AddChild(body);
        foreach (var r in rows)
        {
            int quest = r.QuestId;
            string name = r.Name;
            string detail = r.Detail;
            var row = new GameObject();
            row.Add(new Rect(new Rectangle(rowX, y, rowW, rowH)));
            // No row plate: the official questTitle background is a black box;
            // the text and X sit directly on the helper body.

            var nameLine = new GameObject { NoHit = true };
            nameLine.Add(new Rect(new Rectangle(nameX, _qhLay.Get("rowNameY", 4),
                textW, _qhLay.Get("rowTextH", 14))));
            nameLine.Add(new Text { Value = () => name, Ink = new Color(255, 220, 0) });
            row.AddChild(nameLine);

            var detailLine = new GameObject { NoHit = true };
            detailLine.Add(new Rect(new Rectangle(detailX, _qhLay.Get("rowDetailY", 23),
                detailW, _qhLay.Get("rowTextH", 14))));
            detailLine.Add(new Text { Value = () => detail, Ink = Color.White });
            row.AddChild(detailLine);

            var close = new GameObject();
            close.Add(new Rect(new Rectangle(_qhLay.Get("rowCloseX", 192), _qhLay.Get("rowCloseY", 4),
                HelperRowCloseW, HelperRowCloseH)));
            close.Add(new Button { LegacyId = "qhelperrowclose" + quest, Kind = ButtonKind.Click, OnClick = () => UntrackQuest(quest) });
            close.Add(new Image("ui/qhelper_row_close"));
            row.AddChild(close);

            _qhList.AddChild(row);
            y += rowH;
        }
    }

    // The height of the chevron in the bar: the WZ puts the up chevron one
    // pixel higher than the down one.
    private int ChevronDy() => _qhListOpen ? _qhLay.Get("chevronUpDy", 7) : _qhLay.Get("chevronDownDy", 8);

    // The bar box: the right edge and the top are the file spot until the
    // player drags the bar.
    internal Rectangle QuestHelperBarRect() => new(_qhPos, new Point(HelperBarW, HelperBarH));

    // The child boxes are local to the bar root.
    private static Rectangle HelperBarLocal() => new(0, 0, HelperBarW, HelperBarH);

    // The bar plus its open list: the hit box, the art and the drag clamp
    // must all use the same height.
    internal static int QuestHelperBodyH(PanelLayout lay, int count, int bottomH) =>
        lay.Get("listTop", 32) + count * lay.Get("rowH", 42) + lay.Get("listBottomPad", 4) + bottomH;

    // The drag moves the root only: every child stays local and rides it.
    private void PlaceQuestHelper() => _qhRoot.Get<Rect>()!.Offset = _qhPos;

    // An open list grows downward, so the bar must move up when the player
    // opens it near the bottom or tracks another quest.
    private void ClampQuestHelper()
    {
        if (_qhHidden || _qhCount == 0) { PlaceQuestHelper(); return; }
        int h = _qhListOpen ? QuestHelperBodyH(_qhLay, _qhCount, Tex("qhelper_bottom.png").Height) : HelperBarH;
        _qhPos = ClampPanel(_qhPos, HelperBarW, h);
        PlaceQuestHelper();
    }

    // The file spot of the bar top-left.
    private static Point QuestHelperLayoutPos() =>
        new(_qhLay.Get("right", 1362) - HelperBarW, _qhLay.Get("top", 44));

    // The state of the quest helper: the quests in the list and the list open.
    // SyncQuestHelper brings a hidden bar back when a new row lands; the bar
    // stays away when there is no tracked quest at all. It only reads:
    // SyncQuestHelper writes Enabled, so the hit test and the draw see the
    // same frame.
    private (int Count, bool ListOpen) QuestHelperState() => (_qhCount, _qhListOpen);

    // The chevron sits on the bar at the WZ spot for the state it shows: the
    // up chevron is one pixel higher than the down one. Only the spot moves,
    // the box never changes.
    private void MoveChevron() => _qhChevron.Get<Rect>()!.Offset = new Point(_qhLay.Get("chevronDx", 184), ChevronDy());

    // One button of the quest helper: the X or the chevron. The art sits
    // where the WZ puts it inside the bar.
    private GameObject HelperButton(string id, int dx, int dy, Action onClick, Func<bool, bool, string> art)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(new Point(dx, dy), new Point(HelperButtonSize, HelperButtonSize))));
        o.Add(new Button { LegacyId = id, Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new QuestHelperButtonArt(art));
        return o;
    }

    // The X hides the bar and the list under it.
    private void HideQuestHelper()
    {
        _qhHidden = true;
        _qhDrag = null;
    }

    // The chevron opens or closes the quest list.
    private void ToggleQuestHelperList()
    {
        _qhListOpen = !_qhListOpen;
        MoveChevron(); // the up and the down chevron sit one pixel apart
        ClampQuestHelper();
    }

    // The X art: the official close button of the helper.
    private string QuestHelperCloseArt(bool hover, bool press) =>
        hover ? (press ? "ui/qhelper_close_p.png" : "ui/qhelper_close_h.png") : "ui/qhelper_close_n.png";

    // The chevron art: it points down while the list is closed and up
    // while the list is open.
    private string QuestHelperChevronArt(bool hover, bool press)
    {
        if (_qhListOpen)
            return hover ? (press ? "ui/qhelper_closelist_p.png" : "ui/qhelper_closelist_h.png") : "ui/qhelper_closelist_n.png";
        return hover ? (press ? "ui/qhelper_open_p.png" : "ui/qhelper_open_h.png") : "ui/qhelper_open_n.png";
    }

    // The file names of the art: the bar (open and closed), the list body
    // pieces, the ten count digits, and the buttons.
    private static string[] HelperArtFiles()
    {
        var files = new List<string>
        {
            "qhelper_top.png", "qhelper_topopen.png", "qhelper_mid.png", "qhelper_bottom.png",
        };
        for (int d = 0; d < 10; d++) files.Add($"qhelper_num_{d}.png");
        files.AddRange(new[]
        {
            "qhelper_row_close_n.png", "qhelper_row_close_h.png", "qhelper_row_close_p.png",
            "qhelper_open_n.png", "qhelper_open_h.png", "qhelper_open_p.png",
            "qhelper_closelist_n.png", "qhelper_closelist_h.png", "qhelper_closelist_p.png",
            "qhelper_close_n.png", "qhelper_close_h.png", "qhelper_close_p.png",
        });
        return files.ToArray();
    }
}

// The bar of the quest helper: the official bar art with the count of
// the tracked quests (the official qNum digits). The open bar is the art the
// official shows while the quest list is open; the body ends after the rows.
public sealed class QuestHelperBarArt : Component, IDrawable
{
    private readonly Func<(int Count, bool ListOpen)> _state;
    private readonly PanelLayout _lay;
    public QuestHelperBarArt(Func<(int Count, bool ListOpen)> state, PanelLayout lay) { _state = state; _lay = lay; }

    public void Draw(UiGfx gfx)
    {
        var (count, listOpen) = _state();
        var r = Owner.Get<Rect>()!.Screen;
        var top = gfx.Tex(listOpen ? "ui/qhelper_topopen.png" : "ui/qhelper_top.png");
        gfx.Sb.Draw(top, new Vector2(r.X, r.Y), Color.White);
        if (listOpen)
        {
            // The open helper puts the 1px middle strip under the rows and the
            // rounded bottom strip at the end; the full list is one panel.
            var mid = gfx.Tex("ui/qhelper_mid.png");
            var bottom = gfx.Tex("ui/qhelper_bottom.png");
            int bottomY = r.Y + MushTownGame.QuestHelperBodyH(_lay, count, bottom.Height) - bottom.Height;
            int y = r.Y + top.Height;
            gfx.Sb.Draw(mid, new Rectangle(r.X, y, mid.Width, bottomY - y), Color.White);
            gfx.Sb.Draw(bottom, new Vector2(r.X, bottomY), Color.White);
        }
        string text = count.ToString();
        int x = r.X + NumSpot(text.Length);
        foreach (char ch in text)
        {
            var digit = gfx.Tex($"ui/qhelper_num_{ch}.png");
            gfx.Sb.Draw(digit, new Vector2(x, r.Y + _lay.Get("numTop", 12)), Color.White);
            x += digit.Width;
        }
    }

    // The spot of the count in the plate of the bar: the WZ gives one
    // for one, two and three digits.
    private int NumSpot(int digits) =>
        digits == 1 ? _lay.Get("numX1", 113)
            : digits == 2 ? _lay.Get("numX2", 109)
            : _lay.Get("numX3", 106);
}

// The art of one quest helper button: the official art of the state that
// is showing (normal, hover, pressed). The hover art is a bit wider than
// the button, so it is centered on the box.
public sealed class QuestHelperButtonArt : Component, IDrawable
{
    private readonly Func<bool, bool, string> _art;
    public QuestHelperButtonArt(Func<bool, bool, string> art) { _art = art; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var tex = gfx.Tex(_art(gfx.Hovered == Owner, gfx.Pressed));
        gfx.Sb.Draw(tex, new Vector2(r.X + (r.Width - tex.Width) / 2, r.Y + (r.Height - tex.Height) / 2), Color.White);
    }
}
