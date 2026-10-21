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
    // 11x11, the count digits 6x5. The spots come from the WZ.
    public const int HelperBarW = 222;
    public const int HelperBarH = 28;
    public const int HelperButtonSize = 11;
    public const int HelperNumX1 = 113; // the count spot of the bar: the WZ gives one for
    public const int HelperNumX2 = 109; // one digit (qNum vector:pos2), two (pos1), three (pos0)
    public const int HelperNumX3 = 106;
    public const int HelperNumTop = 12;
    public const int HelperCloseDx = 201, HelperCloseDy = 8; // the WZ origin of the X button
    public const int HelperChevronDx = 184, HelperChevronDy = 8; // the WZ origin of the chevron

    private bool _qhHidden; // the X hid the bar
    private bool _qhListOpen = true; // the chevron shows the quest list
    private int _qhCount; // the quests of the last frame
    private GameObject? _qhBar; // the bar with its buttons and the quest list
    private GameObject? _qhList; // the quest lines under the bar

    // The art of the quest helper: the bar, the ten count digits, and the
    // two buttons in their three states.
    private static readonly string[] HelperFiles = HelperArtFiles();

    // The bar and the quest list under it. The HUD chain puts it in the
    // game scene.
    internal GameObject BuildQuestHelper()
    {
        foreach (string f in HelperFiles) LoadTexture("ui/" + f);
        var bar = new GameObject();
        bar.Add(new Rect(QuestHelperBarRect()));
        bar.Add(new QuestHelperBarArt(QuestHelperState));
        bar.AddChild(HelperButton(HelperCloseDx, HelperCloseDy, HideQuestHelper, QuestHelperCloseArt));
        bar.AddChild(HelperButton(HelperChevronDx, HelperChevronDy, ToggleQuestHelperList, QuestHelperChevronArt));
        var list = new GameObject { NoHit = true }; // the lines only: no clicks on them
        list.Add(new Tracker(TrackerLines, _qhLay.Get("listRight", 1358), _qhLay.Get("listTop", 74)));
        bar.AddChild(list);
        _qhBar = bar;
        _qhList = list;
        return bar;
    }

    // The bar box: the right edge and the top come from the layout file.
    internal Rectangle QuestHelperBarRect()
    {
        int right = _qhLay.Get("right", 1362);
        int top = _qhLay.Get("top", 44);
        return new Rectangle(right - HelperBarW, top, HelperBarW, HelperBarH);
    }

    // The state of the quest helper: the quests in the list and the
    // hidden flag. A new quest brings a hidden bar back.
    private (int Count, bool Hidden) QuestHelperState()
    {
        int count = QuestRows().Count;
        if (count > _qhCount) _qhHidden = false; // a new quest: the bar comes back
        _qhCount = count;
        if (_qhBar != null)
        {
            _qhBar.Get<Rect>()!.Box = _qhHidden ? Rectangle.Empty : QuestHelperBarRect();
            foreach (var ch in _qhBar.Children) ch.Enabled = !_qhHidden;
        }
        if (_qhList != null) _qhList.Enabled = !_qhHidden && _qhListOpen;
        return (count, _qhHidden);
    }

    // One button of the quest helper: the X or the chevron. The art sits
    // where the WZ puts it inside the bar.
    private GameObject HelperButton(int dx, int dy, Action onClick, Func<bool, bool, string> art)
    {
        var bar = QuestHelperBarRect();
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(bar.X + dx, bar.Y + dy, HelperButtonSize, HelperButtonSize)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new QuestHelperButtonArt(art));
        return o;
    }

    // The X hides the bar and the list under it.
    private void HideQuestHelper() => _qhHidden = true;

    // The chevron opens or closes the quest list.
    private void ToggleQuestHelperList() => _qhListOpen = !_qhListOpen;

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

    // The file names of the art: the bar, the ten count digits, and the
    // two buttons in their three states.
    private static string[] HelperArtFiles()
    {
        var files = new List<string> { "qhelper_top.png" };
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
// the quests in the list (the official qNum digits).
public sealed class QuestHelperBarArt : Component, IDrawable
{
    private readonly Func<(int Count, bool Hidden)> _state;
    public QuestHelperBarArt(Func<(int Count, bool Hidden)> state) { _state = state; }
    public void Draw(UiGfx gfx)
    {
        var (count, hidden) = _state();
        if (hidden) return;
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex("ui/qhelper_top.png"), new Vector2(r.X, r.Y), Color.White);
        string text = count.ToString();
        int x = r.X + NumSpot(text.Length);
        foreach (char ch in text)
        {
            var digit = gfx.Tex($"ui/qhelper_num_{ch}.png");
            gfx.Sb.Draw(digit, new Vector2(x, r.Y + MushTownGame.HelperNumTop), Color.White);
            x += digit.Width;
        }
    }

    // The spot of the count in the plate of the bar: the WZ gives one
    // for one, two and three digits.
    private static int NumSpot(int digits) =>
        digits == 1 ? MushTownGame.HelperNumX1 : digits == 2 ? MushTownGame.HelperNumX2 : MushTownGame.HelperNumX3;
}

// The art of one quest helper button: the official art of the state that
// is showing (normal, hover, pressed).
public sealed class QuestHelperButtonArt : Component, IDrawable
{
    private readonly Func<bool, bool, string> _art;
    public QuestHelperButtonArt(Func<bool, bool, string> art) { _art = art; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex(_art(gfx.Hovered == Owner, gfx.Pressed)), new Vector2(r.X, r.Y), Color.White);
    }
}
