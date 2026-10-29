using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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

    public override Rectangle Rect() => new(0, 0, _lay.Get("w", 1022), _lay.Get("h", 597));
    public override Rectangle TitleRect() => new(0, 0, Rect().Width, _lay.Get("titleH", 46));
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
        (6, 2, Feature.Guide),     // Guides
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

    // Close the window (the X button, a menu row, or the game).
    public void Close()
    {
        Show = false;
        OnClosed(); // the pick starts over next time
    }

    public override void OnClosed()
    {
        _curCol = 0;
        _curRow = 0;
    }

    // The arrows pick a cell: left and right hop a column, up and down walk
    // the rows of this one. The bottom button is the last row of a column.
    private int _curCol, _curRow;

    public void MoveCursor(int dc, int dr)
    {
        _curCol = (_curCol + ColumnRows.Length + dc) % ColumnRows.Length;
        _curRow = Math.Clamp(_curRow + dr, 0, RowsOf(_curCol));
    }

    // Enter opens the picked cell. A grey row does nothing, like a click.
    public bool OpenFocused()
    {
        Feature action = _curRow == RowsOf(_curCol) ? BottomButtons[_curCol].Action : ActionOf(_curCol, _curRow);
        if (action == Feature.None) return false;
        _game.OpenFeature(action);
        return true;
    }

    // The pick shows as a blue frame over the row, in the official hover blue.
    // The hover art itself would light the grey rows up like live ones.
    private Invoke FocusFrame(GameObject cell, int col, int row) => new(gfx =>
    {
        if (_curCol == col && _curRow == row) Border.Draw(gfx, cell.Get<Rect>()!.Screen, new Color(123, 204, 224));
    });

    public override void Build()
    {
        _game.LoadTexture("ui/menu_bg.png");
        for (int c = 0; c < ColumnRows.Length; c++)
            for (int r = 0; r < RowsOf(c); r++)
                LoadItemArt($"ui/menu_item_c{c}r{r}");
        foreach (var (stem, _) in BottomButtons) LoadItemArt($"ui/{stem}");
        LoadItemArt("ui/menu_close");
        MenuKeyBadge.LoadBadges(_game);

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it
        root.Add(new BlurBackdrop()); // the frosted glass: the screen at 1/4 size
        root.Add(new MenuArt()); // the 40% white panel, the tabs and the title

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
                item.Add(new Rect(new Rectangle(x, y, ItemW, ItemH)));
                if (action == Feature.None)
                    item.Add(new Picture(stem + "_d.png")); // the official disabled art
                else
                {
                    item.Add(new Button { LegacyId = $"menu{c}r{r}", Kind = ButtonKind.Click, OnClick = () => _game.OpenFeature(action) });
                    item.Add(new Image(stem));
                    AddBadge(item, action);
                }
                item.Add(FocusFrame(item, c, r)); // the arrows frame the pick here
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
            o.Add(new Rect(new Rectangle(x, botY, ItemW, ItemH)));
            if (action == Feature.None)
                o.Add(new Picture($"ui/{stem}_d.png")); // the official disabled art
            else
            {
                o.Add(new Button { LegacyId = $"menub{i}", Kind = ButtonKind.Click, OnClick = () => _game.OpenFeature(action) });
                o.Add(new Image($"ui/{stem}"));
                AddBadge(o, action);
            }
            o.Add(FocusFrame(o, i, RowsOf(i))); // the bottom row is the last row of its column
            root.AddChild(o);
        }

        // the title strip drags the window
        var title = new GameObject();
        title.Add(new Rect(TitleRect()));
        title.Add(new Button { OnClick = StartDrag });
        root.AddChild(title);

        // the X closes the window
        var close = new GameObject();
        close.Add(new Rect(new Rectangle(_lay.Get("closeX", 982), _lay.Get("closeY", 12), CloseW, CloseH)));
        close.Add(new Button { LegacyId = "menuClose", Kind = ButtonKind.Click, OnClick = Close });
        close.Add(new Image("ui/menu_close")); // the official white X, on the glass
        root.AddChild(close);

        Host.AddChild(root);
    }

    // How many menu items the column has (the layout file wins).
    private int RowsOf(int col) => _lay.Get($"col{col}Rows", ColumnRows[col]);

    // The key badge at the right of a row: the official cyan square with
    // the key the player bound to that action. A row without a key gets
    // no badge.
    private void AddBadge(GameObject row, Feature action)
    {
        var key = KeyOf(action);
        if (key == null) return;
        var badge = new GameObject { NoHit = true }; // the badge sits on the row: the row keeps the hover light
        badge.Add(new Rect(new Rectangle(_lay.Get("badgeX", 115), _lay.Get("badgeY", 12), 16, 16)));
        badge.Add(new MenuKeyBadge(() => _game.HotkeyKey(key.Value)));
        row.AddChild(badge);
    }

    // The hotkey of a menu row (the badge shows the bound key).
    private static HotkeyAction? KeyOf(Feature action) => action switch
    {
        Feature.Stat => HotkeyAction.Stat,
        Feature.Equip => HotkeyAction.Equip,
        Feature.Inventory => HotkeyAction.Inventory,
        Feature.Skill => HotkeyAction.Skill,
        Feature.Quest => HotkeyAction.Quest,
        Feature.WorldMap => HotkeyAction.WorldMap,
        Feature.Settings => HotkeyAction.Settings,
        Feature.Keys => HotkeyAction.KeyConfig,
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
}

// The panel art of the MENU window: the body (the tabs, the title and the
// 40% white). The art's last rows are the official quick menu strip, which we
// do not show (we cannot edit the row): the body and its shadow end at y=596,
// so the window stops there and the art is drawn from its own top-left.
public sealed class MenuArt : Component, IDrawable
{
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-menu.json"));

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        gfx.Sb.Draw(gfx.Tex("ui/menu_bg.png"), r, new Rectangle(0, 0, r.Width, r.Height), Color.White);
        // the channel name in the pill at the top right (the official
        // vector:info spot: the middle of the pill)
        string world = MapUi.Data.Channel.World;
        string name = world.Length == 0 ? "" : $"{char.ToUpperInvariant(world[0])}{world[1..]}-Channel1";
        var text = gfx.Ui.GetSmall(name);
        gfx.Sb.Draw(text, new Vector2(r.X + _lay.Get("chInfoX", 894) - text.Width / 2, r.Y + _lay.Get("chInfoY", 16)), Color.White);
    }
}

public partial class MushTownGame
{
    // The MENU window (the HUD menu button opens it).
    private MenuWindow _menuWindow = null!;

    // The keys of the open MENU window: the official says at the bottom of
    // the menu that Tab and the arrows move between the menus (the arrows
    // also walk the rows inside one column).
    private bool _menuLeftWasDown, _menuRightWasDown, _menuUpWasDown, _menuDownWasDown, _menuTabWasDown, _menuEnterWasDown;

    private void MenuKeys(KeyboardState kb)
    {
        bool left = FreshKey(kb, Keys.Left, ref _menuLeftWasDown);
        bool right = FreshKey(kb, Keys.Right, ref _menuRightWasDown);
        bool up = FreshKey(kb, Keys.Up, ref _menuUpWasDown);
        bool down = FreshKey(kb, Keys.Down, ref _menuDownWasDown);
        bool tab = FreshKey(kb, Keys.Tab, ref _menuTabWasDown);
        bool enter = FreshKey(kb, Keys.Enter, ref _menuEnterWasDown);
        if (!_menuWindow.Show || _talkNpc != 0) return; // a talk bubble covers the menu: it owns the keys
        if (left) _menuWindow.MoveCursor(-1, 0);
        if (right || tab) _menuWindow.MoveCursor(1, 0);
        if (up) _menuWindow.MoveCursor(0, -1);
        if (down) _menuWindow.MoveCursor(0, 1);
        if (enter && _menuWindow.OpenFocused()) PlaySfx("click");
    }

    // The menu rows open the panels the game has.
    internal void OpenStatWindow(int tab)
    {
        SetStatTab(tab);
        _showStat = true;
        _dragStat = null;
        RaisePanel(PanelId(Feature.Stat));
    }

    internal void OpenInventoryWindow()
    {
        _showInv = true;
        _dragInv = null;
        RaisePanel(PanelId(Feature.Inventory));
    }

    internal void OpenSkillWindow()
    {
        _showSkill = true;
        _dragSkill = null;
        RaisePanel(PanelId(Feature.Skill));
    }

    internal void OpenQuestWindow()
    {
        _showQuest = true;
        _dragQuest = null;
        RaisePanel(PanelId(Feature.Quest));
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
