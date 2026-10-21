using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The MENU window: the official UITotalMenu art. Seven columns of menu
// items under the seven blue tabs and the bottom row of seven buttons.
// Every button PNG carries its own icon and label, so the window only
// draws the PNGs. Only the features the game has are clickable; the
// other rows show the official disabled art.
public sealed class MenuWindow : UiWindow
{
    private readonly MushTownGame _game;
    public MenuWindow(MushTownGame game) : base(HotkeyAction.Menu) { _game = game; }

    // Every number of this window lives in layouts/panel-menu.json.
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-menu.json"));

    // The button art is 136x40 (the official art size).
    private const int ItemW = 136, ItemH = 40;
    private const int CloseW = 20, CloseH = 20;

    public override Rectangle Rect() => new(Pos.X, Pos.Y, _lay.Get("w", 1022), _lay.Get("h", 698));
    public override Rectangle TitleRect() => new(Pos.X, Pos.Y, Rect().Width, _lay.Get("titleH", 46));
    public override Point LayoutPos() => new(_lay.Get("x", 172), _lay.Get("y", 18));

    // The rows the game has: column, row, what it opens.
    private static readonly (int Col, int Row, Feature Action)[] LiveRows =
    {
        (0, 0, Feature.Stat),      // Character Info
        (1, 0, Feature.Equip),     // Equipment
        (1, 2, Feature.Inventory), // Inventory
        (2, 0, Feature.Skill),     // Skills
        (3, 1, Feature.Quest),     // Quests
        (3, 3, Feature.WorldMap),  // World Map
    };

    // The bottom row: the button art stem and what it opens.
    private static readonly (string Stem, Feature Action)[] BottomButtons =
    {
        ("menu_cashshop", Feature.None),       // Cash Shop
        ("menu_auction", Feature.None),        // Auction House
        ("menu_channel", Feature.None),        // CH. Change Channel
        ("menu_switchchar", Feature.None),     // Switch Character
        ("menu_keys", Feature.Keys),           // Key Bindings
        ("menu_option", Feature.Settings),     // Settings
        ("menu_quit", Feature.Quit),           // Exit Game
    };

    // The official art: seven columns, the row counts of the WZ tree.
    private static readonly int[] ColumnRows = { 4, 8, 8, 9, 6, 4, 4 };

    // Open or close the window (the HUD menu button).
    public void Toggle()
    {
        Show = !Show;
        if (Show) Sync();
    }

    // Close the window (the X button, a menu row, or the game).
    public void Close()
    {
        Show = false;
    }

    public override void Build()
    {
        _game.LoadTexture("ui/menu_bg.png");
        for (int c = 0; c < ColumnRows.Length; c++)
            for (int r = 0; r < RowsOf(c); r++)
                LoadItemArt($"ui/menu_item_c{c}r{r}");
        foreach (var (stem, _) in BottomButtons) LoadItemArt($"ui/{stem}");
        LoadItemArt("ui/menu_close");

        Scene = new Scene();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new BlurBackdrop()); // the frosted glass: the screen at 1/4 size
        root.Add(new MenuArt()); // the 40% white panel, the tabs and the quick menu strip

        // the menu items: seven columns under the seven tabs
        for (int c = 0; c < ColumnRows.Length; c++)
        {
            int rows = RowsOf(c);
            int x = _lay.Get($"col{c}X", 23 + c * 140);
            for (int r = 0; r < rows; r++)
            {
                int y = _lay.Get($"row{r}Y", 82 + r * 40);
                string stem = $"ui/menu_item_c{c}r{r}";
                var action = ActionOf(c, r);
                var item = new GameObject();
                item.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + y, ItemW, ItemH)));
                if (action == Feature.None)
                    item.Add(new Picture(stem + "_d.png")); // the official disabled art
                else
                {
                    item.Add(new Button { LegacyId = $"menu{c}r{r}", Kind = ButtonKind.Click, OnClick = () => Open(action) });
                    item.Add(new Image(stem));
                    AddBadge(item, x, y, action);
                }
                root.AddChild(item);
            }
        }

        // the bottom row: seven buttons
        int botY = _lay.Get("botY", 521);
        for (int i = 0; i < BottomButtons.Length; i++)
        {
            var (stem, action) = BottomButtons[i];
            int x = _lay.Get($"bot{i}X", 23 + i * 140);
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(Pos.X + x, Pos.Y + botY, ItemW, ItemH)));
            if (action == Feature.None)
                o.Add(new Picture($"ui/{stem}_d.png")); // the official disabled art
            else
            {
                o.Add(new Button { LegacyId = $"menub{i}", Kind = ButtonKind.Click, OnClick = () => Open(action) });
                o.Add(new Image($"ui/{stem}"));
                AddBadge(o, x, botY, action);
            }
            root.AddChild(o);
        }

        // the title strip drags the window
        var title = new GameObject();
        title.Add(new Rect(TitleRect()));
        title.Add(new Button { OnClick = StartDrag });
        root.AddChild(title);

        // the X closes the window
        var close = new GameObject();
        close.Add(new Rect(new Rectangle(Pos.X + _lay.Get("closeX", 982), Pos.Y + _lay.Get("closeY", 12), CloseW, CloseH)));
        close.Add(new Button { LegacyId = "menuClose", Kind = ButtonKind.Click, OnClick = Close });
        close.Add(new Image("ui/menu_close")); // the official white X, on the glass
        root.AddChild(close);

        Scene.Add(root);
    }

    // How many menu items the column has (the layout file wins).
    private int RowsOf(int col) => _lay.Get($"col{col}Rows", ColumnRows[col]);

    // The key cap badge at the right of a row: the key the player bound to
    // that action. A row without a key gets no badge.
    private void AddBadge(GameObject row, int x, int y, Feature action)
    {
        var key = KeyOf(action);
        if (key == null) return;
        var badge = new GameObject { NoHit = true }; // the badge sits on the row: the row keeps the hover light
        badge.Add(new Rect(new Rectangle(Pos.X + x + _lay.Get("badgeX", 114), Pos.Y + y + _lay.Get("badgeY", 12), 16, 16)));
        badge.Add(new KeyBadge(() => _game.HotkeyKey(key.Value)));
        row.AddChild(badge);
    }

    // The hotkey of a menu row (the badge shows the bound key).
    private static HotkeyAction? KeyOf(Feature action) => action switch
    {
        Feature.Stat => HotkeyAction.Stat,
        Feature.Inventory => HotkeyAction.Inventory,
        Feature.Skill => HotkeyAction.Skill,
        Feature.Quest => HotkeyAction.Quest,
        Feature.WorldMap => HotkeyAction.WorldMap,
        Feature.Settings => HotkeyAction.Settings,
        _ => null,
    };

    // Load the four states of one button art.
    private void LoadItemArt(string stem)
    {
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png", "_d.png" })
            _game.LoadTexture(stem + state);
    }

    // What the menu row opens (the rows the game has).
    private static Feature ActionOf(int col, int row)
    {
        foreach (var (c, r, action) in LiveRows)
            if (c == col && r == row) return action;
        return Feature.None;
    }

    // A menu row opens the panel the game has and closes the window.
    private void Open(Feature action)
    {
        Close();
        _game.OpenFeature(action);
    }
}

// The panel art of the MENU window: the body (the tabs, the title and the
// 40% white) and the quick menu strip. The strip is drawn translucent, so
// the game shows through it like the rest of the glass.
public sealed class MenuArt : Component, IDrawable
{
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-menu.json"));

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var tex = gfx.Tex("ui/menu_bg.png");
        int sx = _lay.Get("stripX", 259), sy = _lay.Get("stripY", 611);
        int sw = _lay.Get("stripW", 504), sh = _lay.Get("stripH", 87);
        // the body: everything above the strip
        gfx.Sb.Draw(tex, new Rectangle(r.X, r.Y, r.Width, sy), new Rectangle(0, 0, r.Width, sy), Color.White);
        // the strip: the same art, but thin enough to see the game through it
        gfx.Sb.Draw(tex, new Rectangle(r.X + sx, r.Y + sy, sw, sh), new Rectangle(sx, sy, sw, sh),
            new Color(255, 255, 255, _lay.Get("stripAlpha", 102)));
        // the channel name in the pill at the top right (the official
        // vector:info spot: the middle of the pill)
        string world = MapUi.Data.Channel.World;
        string name = world.Length == 0 ? "" : $"{char.ToUpperInvariant(world[0])}{world[1..]}-Channel{ChannelBar.CurrentChannel + 1}";
        var text = gfx.Ui.GetSmall(name);
        gfx.Sb.Draw(text, new Vector2(r.X + _lay.Get("chInfoX", 894) - text.Width / 2, r.Y + _lay.Get("chInfoY", 16)), Color.White);
    }
}

public partial class MushTownGame
{
    // The MENU window (the HUD menu button opens it).
    private MenuWindow _menuWindow = null!;

    // The menu rows open the panels the game has.
    internal void OpenStatWindow(int tab)
    {
        SetStatTab(tab);
        _showStat = true;
        _dragStat = null;
        _topPanel = 1;
    }

    internal void OpenInventoryWindow()
    {
        _showInv = true;
        _dragInv = null;
        _topPanel = 0;
    }

    internal void OpenSkillWindow()
    {
        _showSkill = true;
        _dragSkill = null;
        _topPanel = 2;
    }

    internal void OpenQuestWindow()
    {
        _showQuest = true;
        _dragQuest = null;
        _topPanel = 3;
    }

    // The key settings window (the options window opens it too).
    internal void OpenKeyWindow() => KeyWindow!.Open();

    // The exit: back to the character select, or out of the game.
    internal void ExitGame()
    {
        if (_save != null) ToChars();
        else Exit();
    }
}
