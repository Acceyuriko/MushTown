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

    // What a menu row opens. The rows without an action show the disabled art.
    private enum RowAction { None, Stat, Equip, Inventory, Skill, Quest, WorldMap }

    // What a bottom button opens.
    private enum BottomAction { None, Keys, Settings, Quit }

    // The rows the game has: column, row, what it opens.
    private static readonly (int Col, int Row, RowAction Action)[] LiveRows =
    {
        (0, 0, RowAction.Stat),      // Character Info
        (1, 0, RowAction.Equip),     // Equipment
        (1, 2, RowAction.Inventory), // Inventory
        (2, 0, RowAction.Skill),     // Skills
        (3, 1, RowAction.Quest),     // Quests
        (3, 3, RowAction.WorldMap),  // World Map
    };

    // The bottom row: the button art stem and what it opens.
    private static readonly (string Stem, BottomAction Action)[] BottomButtons =
    {
        ("menu_cashshop", BottomAction.None),       // Cash Shop
        ("menu_auction", BottomAction.None),        // Auction House
        ("menu_channel", BottomAction.None),        // CH. Change Channel
        ("menu_switchchar", BottomAction.None),     // Switch Character
        ("menu_keys", BottomAction.Keys),           // Key Bindings
        ("menu_option", BottomAction.Settings),     // Settings
        ("menu_quit", BottomAction.Quit),           // Exit Game
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
        root.Add(new Picture("ui/menu_bg.png")); // the white panel, the tabs and the quick menu strip

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
                if (action == RowAction.None)
                    item.Add(new Picture(stem + "_d.png")); // the official disabled art
                else
                {
                    item.Add(new Button { LegacyId = $"menu{c}r{r}", Kind = ButtonKind.Click, OnClick = () => Open(action) });
                    item.Add(new Image(stem));
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
            if (action == BottomAction.None)
                o.Add(new Picture($"ui/{stem}_d.png")); // the official disabled art
            else
            {
                o.Add(new Button { LegacyId = $"menub{i}", Kind = ButtonKind.Click, OnClick = () => OpenBottom(action) });
                o.Add(new Image($"ui/{stem}"));
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
        close.Add(new Image("ui/menu_close")); // the exporter bakes a dark X: our panel is solid white
        root.AddChild(close);

        Scene.Add(root);
    }

    // How many menu items the column has (the layout file wins).
    private int RowsOf(int col) => _lay.Get($"col{col}Rows", ColumnRows[col]);

    // Load the four states of one button art.
    private void LoadItemArt(string stem)
    {
        foreach (string state in new[] { "_n.png", "_h.png", "_p.png", "_d.png" })
            _game.LoadTexture(stem + state);
    }

    // What the menu row opens (the rows the game has).
    private static RowAction ActionOf(int col, int row)
    {
        foreach (var (c, r, action) in LiveRows)
            if (c == col && r == row) return action;
        return RowAction.None;
    }

    // A menu row opens the panel the game has and closes the window.
    private void Open(RowAction action)
    {
        Close();
        switch (action)
        {
            case RowAction.Stat: _game.OpenStatWindow(0); break;
            case RowAction.Equip: _game.OpenStatWindow(1); break;
            case RowAction.Inventory: _game.OpenInventoryWindow(); break;
            case RowAction.Skill: _game.OpenSkillWindow(); break;
            case RowAction.Quest: _game.OpenQuestWindow(); break;
            case RowAction.WorldMap: _game.ShowWindow(HotkeyAction.WorldMap); break;
        }
    }

    // A bottom button: the key window, the options window or the exit.
    private void OpenBottom(BottomAction action)
    {
        Close();
        switch (action)
        {
            case BottomAction.Keys: _game.OpenKeyWindow(); break;
            case BottomAction.Settings: _game.OpenSettings(); break;
            case BottomAction.Quit: _game.ExitGame(); break;
        }
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
