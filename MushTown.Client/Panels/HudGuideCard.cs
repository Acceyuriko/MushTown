using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The small guide window (UIContentsGuide.img/Minimum): the GUIDE popup
// with one card. The green plus opens the big guide window, the arrows
// switch between the cards and the dots show which card is open. Every
// number lives in layouts/panel-guide-card.json.
public sealed class GuideCardWindow : UiWindow
{
    private const int W = 135, H = 115; // the official frame size

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
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("cardX", 25), Pos.Y + _lay.Get("cardY", 18), 84, 67)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = OpenCard });
        o.Add(new GuideCardArt(() => _card));
        return o;
    }

    private void OpenCard()
    {
        if (_card == 0) _game.OpenQuestWindow();
        else _game.OpenStatWindow(0);
    }

    // The arrow that switches to the card on its side.
    private GameObject Arrow(string name, int card)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get(name + "X", 24), Pos.Y + _lay.Get("arrowY", 88), 9, 16)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _card = card });
        o.Add(new GuideArrowArt(name));
        return o;
    }

    // The dots under the card: the open one is the big dot.
    private GameObject Dots()
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("dotX", 32), Pos.Y + _lay.Get("dotY", 100),
            _lay.Get("dotW", 12), _lay.Get("dotH", 6))));
        o.Add(new GuideDotArt(() => _card, _lay.Get("dotPitch", 7)));
        return o;
    }

    // The green plus: it opens the big guide window.
    private GameObject MaxButton()
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("maxX", 107), Pos.Y + _lay.Get("maxY", 3), 11, 11)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _game.ShowWindow(HotkeyAction.Guide) });
        o.Add(new Image("ui/guide_small_max"));
        return o;
    }

    // The X in the corner: it closes the window.
    private GameObject CloseButton()
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("closeX", 120), Pos.Y + _lay.Get("closeY", 3), 13, 13)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Show = false });
        o.Add(new Image("ui/guide_small_close"));
        return o;
    }
}

// The arrow of the small guide window: the official art at its own size
// (the hover art of this WZ is bigger than the normal one).
public sealed class GuideArrowArt : Component, IDrawable
{
    private readonly string _name;
    public GuideArrowArt(string name) { _name = name; }
    public void Draw(UiGfx gfx)
    {
        string state = gfx.Hovered == Owner ? (gfx.Pressed ? "_p.png" : "_h.png") : "_n.png";
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex($"ui/guide_small_{_name}{state}"), new Vector2(r.X, r.Y), Color.White);
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

// The two dots of the small guide window: the open card has the big dot.
public sealed class GuideDotArt : Component, IDrawable
{
    private readonly Func<int> _card;
    private readonly int _pitch;
    public GuideDotArt(Func<int> card, int pitch) { _card = card; _pitch = pitch; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        for (int i = 0; i < 2; i++)
        {
            var dot = gfx.Tex(i == _card() ? "ui/guide_small_dot1.png" : "ui/guide_small_dot0.png");
            gfx.Sb.Draw(dot, new Vector2(r.X + i * _pitch, r.Y), Color.White);
        }
    }
}
