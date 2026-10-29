using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The quest helper bar at the top right of the HUD: the official bar of
// UI/Quest.img/Helper. It shows the count of the quests in the list.
// The chevron opens or closes the quest list under the bar, the X hides
// the bar (a new quest brings the bar back).
public partial class MushTownGame
{
    // Every number of the bar lives in layouts/panel-qhelper.json.
    internal static readonly PanelLayout _qhLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-qhelper.json"));

    // The art sizes of the official bar: the bar is 222x28, the buttons
    // 11x11, the count digits 6x5. The spots come from the layout file.
    public const int HelperBarW = 222;
    public const int HelperBarH = 28;
    public const int HelperButtonSize = 11;

    private bool _qhHidden; // the X hid the bar
    private bool _qhListOpen = true; // the chevron shows the quest list
    private int _qhCount; // the quests of the last frame
    private GameObject _qhClose = null!; // the X button
    private GameObject _qhChevron = null!; // the chevron button
    private GameObject _qhList = null!; // the quest lines under the bar

    // The art of the quest helper: the bar (closed and open), the ten count
    // digits, and the two buttons in their three states.
    private static readonly string[] HelperFiles = HelperArtFiles();

    // The bar and the quest list under it. The HUD chain puts it in the
    // game scene.
    internal GameObject BuildQuestHelper()
    {
        foreach (string f in HelperFiles) LoadTexture("ui/" + f);
        var bar = new GameObject();
        var art = new GameObject { NoHit = true }; // the bar art: only the buttons take clicks
        art.Add(new Rect(QuestHelperBarRect()));
        art.Add(new QuestHelperBarArt(QuestHelperState));
        bar.AddChild(art);
        _qhClose = HelperButton(_qhLay.Get("closeDx", 201), _qhLay.Get("closeDy", 8), HideQuestHelper, QuestHelperCloseArt);
        _qhChevron = HelperButton(_qhLay.Get("chevronDx", 184), ChevronDy(), ToggleQuestHelperList, QuestHelperChevronArt);
        bar.AddChild(_qhClose);
        bar.AddChild(_qhChevron);
        var list = new GameObject { NoHit = true }; // the lines only: no clicks on them
        list.Add(new Tracker(TrackerLines, _qhLay.Get("listRight", 1358), _qhLay.Get("listTop", 74)));
        bar.AddChild(list);
        _qhList = list;
        return bar;
    }

    // The height of the chevron in the bar: the WZ puts the up chevron one
    // pixel higher than the down one.
    private int ChevronDy() => _qhListOpen ? _qhLay.Get("chevronUpDy", 7) : _qhLay.Get("chevronDownDy", 8);

    // The bar box: the right edge and the top come from the layout file.
    internal Rectangle QuestHelperBarRect()
    {
        int right = _qhLay.Get("right", 1362);
        int top = _qhLay.Get("top", 44);
        return new Rectangle(right - HelperBarW, top, HelperBarW, HelperBarH);
    }

    // The state of the quest helper: the quests in the list, the hidden flag
    // and the list open. A new quest brings a hidden bar back. The bar stays
    // away when there is no quest at all (that is what the official does).
    // It never moves a Rect: Draw runs after Layout, so a write here would
    // leave this frame reading a stale Screen.
    private (int Count, bool Hidden, bool ListOpen) QuestHelperState()
    {
        int count = QuestCount();
        if (count > _qhCount) _qhHidden = false; // a new quest: the bar comes back
        _qhCount = count;
        bool hidden = _qhHidden || count == 0;
        _qhClose.Enabled = !hidden;
        _qhChevron.Enabled = !hidden;
        _qhList.Enabled = !hidden && _qhListOpen;
        return (count, hidden, _qhListOpen);
    }

    // Where a spot of the bar sits on the screen. The bar root has no Rect, so
    // its children carry the screen spot themselves.
    private Point BarSpot(int dx, int dy)
    {
        var bar = QuestHelperBarRect();
        return new Point(bar.X + dx, bar.Y + dy);
    }

    // The chevron sits on the bar at the WZ spot for the state it shows: the
    // up chevron is one pixel higher than the down one. Only the spot moves,
    // the box never changes.
    private void MoveChevron() => _qhChevron.Get<Rect>()!.Offset = BarSpot(_qhLay.Get("chevronDx", 184), ChevronDy());

    // One button of the quest helper: the X or the chevron. The art sits
    // where the WZ puts it inside the bar.
    private GameObject HelperButton(int dx, int dy, Action onClick, Func<bool, bool, string> art)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(BarSpot(dx, dy), new Point(HelperButtonSize, HelperButtonSize))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new QuestHelperButtonArt(art));
        return o;
    }

    // The X hides the bar and the list under it.
    private void HideQuestHelper() => _qhHidden = true;

    // The chevron opens or closes the quest list.
    private void ToggleQuestHelperList()
    {
        _qhListOpen = !_qhListOpen;
        MoveChevron(); // the up and the down chevron sit one pixel apart
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

    // The file names of the art: the two bars, the ten count digits, and the
    // two buttons in their three states.
    private static string[] HelperArtFiles()
    {
        var files = new List<string> { "qhelper_top.png", "qhelper_topopen.png" };
        for (int d = 0; d < 10; d++) files.Add($"qhelper_num_{d}.png");
        files.AddRange(new[]
        {
            "qhelper_open_n.png", "qhelper_open_h.png", "qhelper_open_p.png",
            "qhelper_closelist_n.png", "qhelper_closelist_h.png", "qhelper_closelist_p.png",
            "qhelper_close_n.png", "qhelper_close_h.png", "qhelper_close_p.png",
        });
        return files.ToArray();
    }
}

// The bar of the quest helper: the official bar art with the count of
// the quests in the list (the official qNum digits). The open bar is the
// art the official shows while the quest list is open.
public sealed class QuestHelperBarArt : Component, IDrawable
{
    private readonly Func<(int Count, bool Hidden, bool ListOpen)> _state;
    public QuestHelperBarArt(Func<(int Count, bool Hidden, bool ListOpen)> state) { _state = state; }
    public void Draw(UiGfx gfx)
    {
        var (count, hidden, listOpen) = _state();
        if (hidden) return;
        var r = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex(listOpen ? "ui/qhelper_topopen.png" : "ui/qhelper_top.png"), new Vector2(r.X, r.Y), Color.White);
        string text = count.ToString();
        int x = r.X + NumSpot(text.Length);
        foreach (char ch in text)
        {
            var digit = gfx.Tex($"ui/qhelper_num_{ch}.png");
            gfx.Sb.Draw(digit, new Vector2(x, r.Y + MushTownGame._qhLay.Get("numTop", 12)), Color.White);
            x += digit.Width;
        }
    }

    // The spot of the count in the plate of the bar: the WZ gives one
    // for one, two and three digits.
    private static int NumSpot(int digits) =>
        digits == 1 ? MushTownGame._qhLay.Get("numX1", 113)
            : digits == 2 ? MushTownGame._qhLay.Get("numX2", 109)
            : MushTownGame._qhLay.Get("numX3", 106);
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
