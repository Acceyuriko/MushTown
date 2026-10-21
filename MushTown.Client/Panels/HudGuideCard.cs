using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The small guide window (UIContentsGuide.img/Minimum): the GUIDE popup
// with one card. The green plus opens the big guide window, the arrows
// switch between the cards and the dots show which card is open. The
// spots are the official ones: the WZ nodes carry them as the image
// origin. Every number lives in layouts/panel-guide-card.json.
public sealed class GuideCardWindow : UiWindow
{
    private const int W = 135, H = 105; // the official frame size

    private readonly MushTownGame _game;
    private int _card; // 0 = main quest, 1 = job advance

    public GuideCardWindow(MushTownGame game) : base(HotkeyAction.GuideCard) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-guide-card.json"));

    public override Rectangle Rect() => new(Pos.X, Pos.Y, W, H);
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, W, _lay.Get("titleH", 16));
    public override Point LayoutPos() => new(_lay.Get("x", 0), _lay.Get("y", 165));

    public override void Build()
    {
        _game.LoadTexture("ui/guide_small_bg.png");
        _game.LoadTexture("ui/guide_small_mainquest.png");
        _game.LoadTexture("ui/guide_small_mainquest_p.png");
        _game.LoadTexture("ui/guide_small_job.png");
        _game.LoadTexture("ui/guide_small_dot0.png");
        _game.LoadTexture("ui/guide_small_dot1.png");
        foreach (string s in new[] { "_n", "_h", "_p" })
            foreach (string b in new[] { "max", "close", "left", "right" })
                _game.LoadTexture($"ui/guide_small_{b}{s}.png");

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Picture("ui/guide_small_bg.png", 0, 0));
        root.AddChild(Card());
        root.AddChild(Arrow("left", 0));
        root.AddChild(Arrow("right", 1));
        root.AddChild(Dots());
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        root.AddChild(MaxButton()); // the plus and the X sit on the title strip: they must be hit first
        root.AddChild(CloseButton());
        Scene.Add(root);
    }

    // The card of the open page: the main quest card opens the quest
    // window, the job advance card the stat window.
    private GameObject Card()
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("cardX", 25), Pos.Y + _lay.Get("cardY", 22), 84, 67)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = OpenCard });
        o.Add(new GuideCardArt(() => _card));
        return o;
    }

    private void OpenCard()
    {
        if (_card == 0) _game.OpenQuestWindow();
        else _game.OpenStatWindow(0);
    }

    // The arrow that switches to the card on its side. The box is the size
    // of the lit arrow, so the whole lit arrow can be clicked.
    private GameObject Arrow(string name, int card)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get(name + "X", name == "left" ? 10 : 116), Pos.Y + _lay.Get("arrowY", 45), 13, 21)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _card = card });
        o.Add(new GuideArrowArt(name, _lay.Get("arrowHoverDx", -2), _lay.Get("arrowHoverDy", -2)));
        return o;
    }

    // The dots under the card: the open card gets the white circle, the
    // other one the gray square. The two spots are the WZ ones.
    private GameObject Dots()
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("dotX", -1), Pos.Y + _lay.Get("dotY", 91),
            _lay.Get("dotW", 6), _lay.Get("dotH", 6))));
        o.Add(new GuideDotArt(() => _card, _lay.Get("dot2Dx", 2), _lay.Get("dot2Dy", -1)));
        return o;
    }

    // The green plus: it opens the big guide window.
    private GameObject MaxButton()
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("maxX", 104), Pos.Y + _lay.Get("maxY", 5), 11, 11)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _game.ShowWindow(HotkeyAction.Guide) });
        o.Add(new Image("ui/guide_small_max"));
        return o;
    }

    // The X in the corner: it closes the window.
    private GameObject CloseButton()
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("closeX", 116), Pos.Y + _lay.Get("closeY", 4), 13, 13)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Show = false });
        o.Add(new Image("ui/guide_small_close"));
        return o;
    }
}

// The arrow of the small guide window: the official art at its own size
// (the lit art is bigger and the WZ moves it up and to the left).
public sealed class GuideArrowArt : Component, IDrawable
{
    private readonly string _name;
    private readonly int _hoverDx, _hoverDy;
    public GuideArrowArt(string name, int hoverDx, int hoverDy) { _name = name; _hoverDx = hoverDx; _hoverDy = hoverDy; }
    public void Draw(UiGfx gfx)
    {
        bool over = gfx.Hovered == Owner;
        bool lit = over && !gfx.Pressed; // the WZ moves only the mouse over art
        string state = over ? (gfx.Pressed ? "_p.png" : "_h.png") : "_n.png";
        var r = Owner.Get<Rect>()!.Box;
        int dx = lit ? _hoverDx : 0, dy = lit ? _hoverDy : 0;
        gfx.Sb.Draw(gfx.Tex($"ui/guide_small_{_name}{state}"), new Vector2(r.X + dx, r.Y + dy), Color.White);
    }
}

// The card of the small guide window: the official card picture.
public sealed class GuideCardArt : Component, IDrawable
{
    private readonly Func<int> _card;
    public GuideCardArt(Func<int> card) { _card = card; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        // the main quest card has a pressed picture, the job card has not
        bool main = _card() == 0;
        string key = main ? "ui/guide_small_mainquest" : "ui/guide_small_job";
        string art = main && gfx.Hovered == Owner && gfx.Pressed ? key + "_p.png" : key + ".png";
        gfx.Sb.Draw(gfx.Tex(art), new Vector2(r.X, r.Y), Color.White);
    }
}

// The two dots of the small guide window: the open card gets the white
// circle, the other card the gray square. The two spots are the WZ ones.
public sealed class GuideDotArt : Component, IDrawable
{
    private readonly Func<int> _card;
    private readonly int _dx, _dy;
    public GuideDotArt(Func<int> card, int dx, int dy) { _card = card; _dx = dx; _dy = dy; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var circle = gfx.Tex("ui/guide_small_dot1.png");
        var square = gfx.Tex("ui/guide_small_dot0.png");
        if (_card() == 0)
        {
            gfx.Sb.Draw(circle, new Vector2(r.X, r.Y), Color.White);
            gfx.Sb.Draw(square, new Vector2(r.X + _dx, r.Y + _dy), Color.White);
        }
        else
        {
            gfx.Sb.Draw(square, new Vector2(r.X, r.Y), Color.White);
            gfx.Sb.Draw(circle, new Vector2(r.X + _dx, r.Y + _dy), Color.White);
        }
    }
}
