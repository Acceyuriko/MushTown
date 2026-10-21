using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The contents guide window (UIContentsGuide.img): the modern client guide
// window. It has three tabs (field, boss, special) and shows the new
// content. The game has no new content yet, so the lists show sample
// entries. Every number lives in layouts/panel-guide.json.
public sealed class GuideWindow : UiWindow
{
    private readonly MushTownGame _game;
    public GuideWindow(MushTownGame game) : base(HotkeyAction.Guide) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-guide.json"));

    private int _tab; // 0 = field, 1 = boss, 2 = special

    // The sample entries of the lists: the game has no new content yet.
    private static readonly string[] FieldNames = { "New Field", "New Quest", "New Field", "New Quest" };
    private static readonly string[] BossNames = { "New Boss", "New Boss", "New Boss" };
    private static readonly string[] SpecialNames = { "New Story", "New Growth", "New Contest" };

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 642), _lay.Get("h", 627));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 60));
    public override Point LayoutPos() => new(_lay.Get("x", 362), _lay.Get("y", 70));

    public override void Build()
    {
        _game.LoadTexture("ui/guide_bg.png");
        _game.LoadTexture("ui/guide_field.png");
        _game.LoadTexture("ui/guide_boss.png");
        _game.LoadTexture("ui/guide_special.png");
        for (int i = 0; i < 4; i++) _game.LoadTexture($"ui/guide_item_{i}.png");
        for (int i = 0; i < 3; i++)
        {
            _game.LoadTexture($"ui/guide_tab_{i}.png");
            _game.LoadTexture($"ui/guide_tabsel_{i}.png");
        }
        foreach (string b in new[] { "min", "close", "help" })
            foreach (string s in new[] { "_n", "_h", "_p", "_d" })
                _game.LoadTexture($"ui/guide_{b}{s}.png");

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window swallows the clicks inside it
        root.Add(new Picture("ui/guide_bg.png"));
        root.Add(new Invoke(Draw));
        for (int i = 0; i < 3; i++) root.AddChild(Tab(i));
        root.AddChild(CloseButton());
        root.AddChild(DisabledButton("min", _lay.Get("minX", 603), _lay.Get("minY", 4)));
        root.AddChild(DisabledButton("help", _lay.Get("helpX", 589), _lay.Get("helpY", 3)));
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        Scene.Add(root);
    }

    // One tab of the window: it shows its list and it is drawn as selected
    // while it is the open one.
    private GameObject Tab(int i)
    {
        int x = i == 0 ? _lay.Get("tab0X", 18) : i == 1 ? _lay.Get("tab1X", 222) : _lay.Get("tab2X", 426);
        int y = _lay.Get("tabY", 75);
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, 196, 27)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _tab = i });
        o.Add(new GuideTabArt(i, () => _tab));
        return o;
    }

    // The X in the corner: it closes the window.
    private GameObject CloseButton()
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(Pos.X + _lay.Get("closeX", 620), Pos.Y + _lay.Get("closeY", 4), 13, 13)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Show = false });
        o.Add(new Image("ui/guide_close"));
        return o;
    }

    // A button the game cannot use yet: the official greyed art, so it is
    // not a button at all.
    private GameObject DisabledButton(string name, int x, int y)
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, 13, 13)));
        o.Add(new Picture($"ui/guide_{name}_d.png"));
        return o;
    }

    // The content of the open tab.
    private void Draw(UiGfx gfx)
    {
        var r = Rect();
        if (_tab == 0) DrawField(gfx, r);
        else if (_tab == 1) DrawList(gfx, r, "ui/guide_boss.png", BossNames);
        else DrawList(gfx, r, "ui/guide_special.png", SpecialNames);
    }

    // The field tab: the list background and the new content icons.
    private void DrawField(UiGfx gfx, Rectangle r)
    {
        gfx.Sb.Draw(gfx.Tex("ui/guide_field.png"), new Vector2(r.X + _lay.Get("fieldX", 19), r.Y + _lay.Get("fieldY", 105)), Color.White);
        for (int i = 0; i < FieldNames.Length; i++)
        {
            int x = r.X + _lay.Get($"item{i}X", 127 + i * 87);
            int y = r.Y + _lay.Get("itemY", 130);
            gfx.Sb.Draw(gfx.Tex($"ui/guide_item_{i}.png"), new Vector2(x, y), Color.White);
            gfx.Sb.Draw(gfx.Ui.GetSmall(FieldNames[i]), new Vector2(x, y + _lay.Get("nameDy", 66)), Color.White);
        }
    }

    // The boss and special tabs: the list background and the names.
    private void DrawList(UiGfx gfx, Rectangle r, string bg, string[] names)
    {
        gfx.Sb.Draw(gfx.Tex(bg), new Vector2(r.X + _lay.Get("listX", 18), r.Y + _lay.Get("listY", 53)), Color.White);
        for (int i = 0; i < names.Length; i++)
        {
            int y = r.Y + _lay.Get("listY", 53) + 20 + i * _lay.Get("listLineH", 16);
            gfx.Sb.Draw(gfx.Ui.GetSmall(names[i]), new Vector2(r.X + _lay.Get("listX", 18) + 20, y), Color.White);
        }
    }
}

// The tab of the guide window: the official tab art, the selected one when
// the tab is open.
public sealed class GuideTabArt : Component, IDrawable
{
    private readonly int _tab;
    private readonly Func<int> _open;
    public GuideTabArt(int tab, Func<int> open) { _tab = tab; _open = open; }
    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        gfx.Sb.Draw(gfx.Tex($"ui/guide_tab{(_open() == _tab ? "sel" : "")}_{_tab}.png"), new Vector2(r.X, r.Y), Color.White);
    }
}
