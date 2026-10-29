using Microsoft.Xna.Framework;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// The party windows (UIParty.img/mainUI): the party window with the member
// list, the search window with the players of the map (the invite list) and
// the invite popup. The spots are the official ones: the WZ nodes carry
// them as the image origin. Every number lives in layouts/panel-party.json.
public sealed class PartyWindow : UiWindow
{
    private const int W = 528, H = 578; // the official frame size

    private readonly MushTownGame _game;
    internal static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-party.json"));

    public PartyWindow(MushTownGame game) : base(HotkeyAction.Party) { _game = game; }

    public override Rectangle Rect() => new(0, 0, W, H);
    public override Rectangle TitleRect() => new(0, 0, W, _lay.Get("titleH", 26));
    public override Point LayoutPos() => new(_lay.Get("x", 780), _lay.Get("y", 60));

    // The party panel inside the window (the WZ partyWnd).
    private Point Wnd() => new(_lay.Get("wndX", 12), _lay.Get("wndY", 63));

    // One member row: the official spot of the list.
    private Rectangle RowRect(int i)
    {
        var w = Wnd();
        return new Rectangle(w.X + _lay.Get("listX", 14), w.Y + _lay.Get("listY", 168) + i * _lay.Get("rowPitch", 42), 476, 39);
    }

    public override void Build()
    {
        foreach (string f in new[]
        {
            "party_frame.png", "party_list.png", "party_none.png", "party_row.png",
            "party_leader.png", "party_online.png",
            "party_close_n.png", "party_close_h.png", "party_close_p.png",
            "party_btn_make_n.png", "party_btn_make_h.png", "party_btn_make_p.png",
            "party_btn_out_n.png", "party_btn_out_h.png", "party_btn_out_p.png",
            "party_btn_invite_n.png", "party_btn_invite_h.png", "party_btn_invite_p.png",
            "party_btn_invite_d.png",
        })
            _game.LoadTexture("ui/" + f);

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it
        root.Add(new Picture("ui/party_frame.png", 0, 0));
        var wnd = Wnd();
        root.Add(new Picture("ui/party_list.png", wnd.X, wnd.Y));
        // the party name: the leader's name (the official default, the name
        // itself cannot be edited yet)
        root.Add(new LiveLabel(() => _game.PartyMembers.Length > 0 ? _game.PartyMembers[0].Name : "",
            wnd.X + _lay.Get("partyNameX", 52), wnd.Y + _lay.Get("partyNameY", 18),
            Color.White, LabelFont.Mid));
        // the member counter: the count and the size of a full party
        root.Add(new LiveLabel(() => _game.PartyMembers.Length.ToString(), wnd.X + _lay.Get("numX", 85), wnd.Y + _lay.Get("numY", 107), new Color(51, 51, 51)));
        root.Add(new LiveLabel(() => PartyWorld.MaxMembers.ToString(), wnd.X + _lay.Get("num2X", 103), wnd.Y + _lay.Get("numY", 107), new Color(51, 51, 51)));
        if (_game.PartyMembers.Length == 0)
            root.Add(new Picture("ui/party_none.png", wnd.X + _lay.Get("noneDx", -1), wnd.Y + _lay.Get("noneDy", -1)));
        root.AddChild(Members());

        // the buttons: alone we make a party, in a party we leave or invite
        if (_game.PartyMembers.Length == 0)
        {
            // without a server there is nobody to invite: no dead button
            if (_game.HasServer)
                root.AddChild(MushTownGame.ArtButton(new Rectangle(wnd.X + _lay.Get("btnMakeX", -2), wnd.Y + _lay.Get("btnMakeY", 465), 508, 40),
                    "ui/party_btn_make", _game.PartyMake));
        }
        else
        {
            root.AddChild(MushTownGame.ArtButton(new Rectangle(wnd.X + _lay.Get("btnOutX", -2), wnd.Y + _lay.Get("btnOutY", 465), 254, 40),
                "ui/party_btn_out", _game.PartyLeave));
            var invite = new Rectangle(wnd.X + _lay.Get("btnInviteX", 252), wnd.Y + _lay.Get("btnInviteY", 465), 254, 40);
            // only the leader of a party that is not full invites: the other
            // members see the official disabled button
            if (_game.PartyLeader && _game.PartyMembers.Length < PartyWorld.MaxMembers)
                root.AddChild(MushTownGame.ArtButton(invite, "ui/party_btn_invite", _game.OpenPartySearch));
            else
                root.Add(new Picture("ui/party_btn_invite_d.png", invite.X, invite.Y));
        }

        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MushTownGame.ArtButton(new Rectangle(_lay.Get("closeX", 497), _lay.Get("closeY", 4), 27, 27), "ui/party_close", () => Show = false));
        Host.AddChild(root);
    }

    // The member rows: the official row art, the leader mark, the online
    // mark, the name and the level.
    private GameObject Members()
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Invoke(DrawMembers));
        return o;
    }

    private void DrawMembers(UiGfx gfx)
    {
        var members = _game.PartyMembers;
        for (int i = 0; i < members.Length && i < PartyWorld.MaxMembers; i++)
        {
            var r = RowRect(i);
            r.Offset(ScreenBox().Location);
            gfx.Sb.Draw(gfx.Tex("ui/party_row.png"), new Vector2(r.X, r.Y), Color.White);
            if (i == 0) // the first member is the leader
                gfx.Sb.Draw(gfx.Tex("ui/party_leader.png"), new Vector2(r.X + _lay.Get("leaderX", 25), r.Y + _lay.Get("leaderY", 11)), Color.White);
            gfx.Sb.Draw(gfx.Tex("ui/party_online.png"), new Vector2(r.X + _lay.Get("stateX", 10), r.Y + _lay.Get("stateY", 13)), Color.White);
            gfx.Sb.Draw(gfx.Ui.GetSmall(members[i].Name), new Vector2(r.X + _lay.Get("nameX", 43), r.Y + _lay.Get("nameY", 13)), new Color(51, 51, 51));
            gfx.Sb.Draw(gfx.Ui.GetSmall(members[i].Level.ToString()), new Vector2(r.X + _lay.Get("levelX", 159), r.Y + _lay.Get("levelY", 13)), new Color(51, 51, 51));
        }
    }
}

// The search window (UIParty.img/mainUI/searchWnd): the players of the map
// with the invite button of every row.
public sealed class PartySearchWindow : UiWindow
{
    private const int W = 504, H = 504; // the official frame size

    private readonly MushTownGame _game;
    private static readonly PanelLayout _lay = PartyWindow._lay;

    public PartySearchWindow(MushTownGame game) : base(HotkeyAction.PartySearch) { _game = game; }

    public override Rectangle Rect() => new(0, 0, W, H);
    public override Rectangle TitleRect() => new(0, 0, W, _lay.Get("titleH", 26));
    public override Point LayoutPos() => new(_lay.Get("searchX", 240), _lay.Get("searchY", 60));

    // One row of the list: the official spot (the list window of the WZ).
    private Rectangle RowRect(int i) => new(_lay.Get("searchListX", 14),
        _lay.Get("searchListY", 154) + i * _lay.Get("rowPitch", 42), 468, 39);

    public override void Build()
    {
        foreach (string f in new[]
        {
            "party_search.png", "party_search_list.png", "party_search_row.png", "party_search_tab.png",
            "party_close_n.png", "party_close_h.png", "party_close_p.png",
            "party_btn_invite_row_n.png", "party_btn_invite_row_h.png", "party_btn_invite_row_p.png",
        })
            _game.LoadTexture("ui/" + f);

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it
        root.Add(new Picture("ui/party_search.png", 0, 0));
        // the tab of the page we show: the players of the map
        root.Add(new Picture("ui/party_search_tab.png", _lay.Get("searchTabX", 15), _lay.Get("searchTabY", 78)));
        // the list panel of the official search window
        root.Add(new Picture("ui/party_search_list.png", _lay.Get("searchPanelX", 14), _lay.Get("searchPanelY", 116)));

        var players = _game.PartyPlayers;
        for (int i = 0; i < players.Length && i < _lay.Get("maxRows", 8); i++)
        {
            int n = i;
            var r = RowRect(i);
            var row = new GameObject();
            row.Add(new Rect(r));
            row.Add(new Picture("ui/party_search_row.png", 0, 0));
            row.Add(new Invoke(gfx => DrawRow(gfx, row.Get<Rect>()!.Screen, players[n])));

            // the invite button of the row
            var btn = new GameObject();
            btn.Add(new Rect(new Rectangle(_lay.Get("searchInviteX", 430), _lay.Get("searchInviteY", 5), 27, 28)));
            btn.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _game.PartyInvite(players[n].Id) });
            btn.Add(new Image("ui/party_btn_invite_row"));
            row.AddChild(btn);
            root.AddChild(row);
        }

        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MushTownGame.ArtButton(new Rectangle(_lay.Get("searchCloseX", 473), _lay.Get("searchCloseY", 4), 27, 27), "ui/party_close", () => Show = false));
        Host.AddChild(root);
    }

    // The name and the level of a row: the official spots.
    private void DrawRow(UiGfx gfx, Rectangle r, PartyMember p)
    {
        gfx.Sb.Draw(gfx.Ui.GetSmall(p.Name), new Vector2(r.X + _lay.Get("searchNameX", 22), r.Y + _lay.Get("searchNameY", 12)), Color.Black);
        gfx.Sb.Draw(gfx.Ui.GetSmall(p.Level.ToString()), new Vector2(r.X + _lay.Get("searchLevelX", 143), r.Y + _lay.Get("searchLevelY", 12)), Color.Black);
    }
}

// The invite popup: the official dialogue box (UI/UIWindow2.img/UtilDlgEx)
// with the yes and no buttons. The name of the player who asks is on the
// tab of the box, the words are under it.
public sealed class PartyAskWindow : UiWindow
{
    private const int W = 519; // the official dialogue width
    private const int TopH = 28, BottomH = 44; // the official frame pieces
    private const int BtnW = 57, BtnH = 16; // the official button art

    private readonly MushTownGame _game;
    private static readonly PanelLayout _lay = PartyWindow._lay;

    // The words of the popup: the name is on the tab, the message under it.
    // The text helper of the game only exists after the content loaded, so
    // the lines are made when the window is used the first time. A new
    // width (the layout reloads in a debug build) makes them again.
    private List<string>? _lines;
    private int _linesWidth;

    public PartyAskWindow(MushTownGame game) : base(HotkeyAction.PartyAsk) { _game = game; }

    private List<string> Lines()
    {
        int w = _lay.Get("askTextW", 208);
        if (_lines == null || _linesWidth != w)
        {
            _lines = _game.WrapText("invited you to a party.", w);
            _linesWidth = w;
        }
        return _lines;
    }

    public override Rectangle Rect()
    {
        int h = TopH + Lines().Count * _lay.Get("askLineStep", 13) + BottomH;
        return new Rectangle(0, 0, W, h);
    }
    public override Rectangle TitleRect() => new(0, 0, Rect().Width, 0); // the popup has no title bar
    public override Point LayoutPos() => new(_lay.Get("askX", 130), _lay.Get("askY", 460));

    // The popup closes without an answer: that is a decline.
    public override void OnClosed()
    {
        if (_game.PartyAsk != null) _game.PartyAnswer(false);
    }

    // The yes and no buttons: they sit in the blue bar at the bottom.
    private Rectangle BtnRect(int i)
    {
        var r = Rect();
        int gap = _lay.Get("askBtnGap", 8);
        int x = r.X + (r.Width - (2 * BtnW + gap)) / 2 + i * (BtnW + gap);
        int y = r.Bottom - BtnH - _lay.Get("askBtnY", 10);
        return new Rectangle(x, y, BtnW, BtnH);
    }

    public override void Build()
    {
        foreach (string f in new[]
        {
            "talk_dlg_top.png", "talk_dlg_mid.png", "talk_dlg_bottom.png",
            "talk_dlg_btyes_n.png", "talk_dlg_btyes_h.png", "talk_dlg_btyes_p.png",
            "talk_dlg_btno_n.png", "talk_dlg_btno_h.png", "talk_dlg_btno_p.png",
        })
            _game.LoadTexture("ui/" + f);

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it
        root.Add(new TalkFrame());
        root.Add(new Label(_game.PartyAskName(), _lay.Get("askNameDx", 14), _lay.Get("askNameDy", 5)) { Tint = new Color(60, 60, 60) });
        root.Add(new TalkBody(Lines, _lay.Get("askTextDx", 295), _lay.Get("askTextDy", 28), _lay.Get("askLineStep", 13)));
        var yes = new GameObject();
        yes.Add(new Rect(BtnRect(0)));
        yes.Add(new Button { LegacyId = "partyask_yes", Kind = ButtonKind.Click, OnClick = () => _game.PartyAnswer(true) });
        yes.Add(new TalkButton("talk_dlg_btyes", "", "partyask_yes", () => _game.HoverId, 2));
        root.AddChild(yes);
        var no = new GameObject();
        no.Add(new Rect(BtnRect(1)));
        no.Add(new Button { LegacyId = "partyask_no", Kind = ButtonKind.Click, OnClick = () => _game.PartyAnswer(false) });
        no.Add(new TalkButton("talk_dlg_btno", "", "partyask_no", () => _game.HoverId, 2));
        root.AddChild(no);
        Host.AddChild(root);
    }
}

public partial class MushTownGame
{
    // The party state the server sends: the members of the party (the
    // leader first), the players of the map (the invite list) and the
    // invite that waits for an answer.
    internal PartyMember[] PartyMembers = Array.Empty<PartyMember>();
    internal PartyMember[] PartyPlayers = Array.Empty<PartyMember>();
    internal PartyAskEvent? PartyAsk;
    private readonly PartyWindow _partyWindow;
    private readonly PartySearchWindow _partySearchWindow;
    private readonly PartyAskWindow _partyAskWindow;

    // The local player leads the party when it is the first member of the
    // list. Alone (no server) the player leads it.
    internal bool PartyLeader => _net == null || (PartyMembers.Length > 0 && PartyMembers[0].Id == _net.PlayerId);

    // The game talks to a server: the party needs one.
    internal bool HasServer => _net != null && _net.Connected;

    // The name of the player who asked for a party.
    internal string PartyAskName() => PartyAsk?.Name ?? "";

    // The id of the button under the mouse (the talk buttons draw their lit
    // art with it).
    internal string HoverId => _hoverId;

    // The party actions the windows call.
    internal void PartyMake() => _net?.SendPartyMake();
    internal void PartyInvite(int id) => _net?.SendPartyInvite(id);
    internal void PartyAnswer(bool accept)
    {
        _net?.SendPartyAnswer(accept);
        PartyAsk = null;
        _partyAskWindow.Show = false;
    }
    internal void PartyLeave() => _net?.SendPartyLeave();

    // The search window lists the players of the map: the server sends them.
    internal void OpenPartySearch()
    {
        PartyPlayers = Array.Empty<PartyMember>(); // the old list is stale
        _net?.SendPartyList();
        ShowWindow(HotkeyAction.PartySearch);
    }

    // A new member list: the party window shows it.
    internal void SetPartyMembers(PartyMember[] members)
    {
        PartyMembers = members;
        if (members.Length == 0) _chatWindow.SetPartyTarget(false); // no party: the lines go to the world
        _partyWindow.Dirty = true;
    }

    // A new invite list: the search window shows it.
    internal void SetPartyPlayers(PartyMember[] players)
    {
        PartyPlayers = players;
        _partySearchWindow.Dirty = true;
    }

    // An invite came in: the popup asks the player.
    internal void SetPartyAsk(PartyAskEvent ask)
    {
        PartyAsk = ask;
        ShowWindow(HotkeyAction.PartyAsk); // the popup asks: it opens on top
    }

    // The link is lost: the party is gone with it.
    internal void ClearParty()
    {
        PartyMembers = Array.Empty<PartyMember>();
        PartyPlayers = Array.Empty<PartyMember>();
        PartyAsk = null;
        _chatWindow.SetPartyTarget(false);
        _partyWindow.Dirty = true;
        _partyAskWindow.Show = false;
        _partySearchWindow.Show = false;
    }
}
