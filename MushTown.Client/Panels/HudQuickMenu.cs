using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The quick menu row at the bottom left of the HUD: the official 30x30
// icons of UI/UITotalMenu.img/quickMenu/menu, and the hamburger of
// quickMenu/button:menu in the last slot that opens the MENU. Only the
// features the game has get an icon (the other 47 would be dead buttons);
// a click opens that window.
public partial class MushTownGame
{
    // Every number of the row lives in layouts/panel-quickmenu.json.
    private static readonly PanelLayout _qmLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-quickmenu.json"));

    // The icons the game has: the official icon number and what it opens.
    // Exit Game is not here: the slot is the hamburger, and the MENU window
    // has the button.
    private static readonly (int Icon, Feature Action, string Label)[] QuickIcons =
    {
        (0, Feature.Stat, "Character Info"),
        (4, Feature.Equip, "Equipment"),
        (6, Feature.Inventory, "Inventory"),
        (11, Feature.Skill, "Skills"),
        (17, Feature.Quest, "Quests"),
        (19, Feature.WorldMap, "World Map"),
        (52, Feature.Keys, "Key Bindings"),
        (53, Feature.Settings, "Settings"),
    };

    // The three states of one icon art.
    private static readonly string[] IconStates = { "_n.png", "_h.png", "_p.png" };

    // The row: one cell per icon, then the hamburger in the last slot.
    private GameObject BuildQuickMenu()
    {
        int x = _qmLay.Get("x", 284), y = _qmLay.Get("y", 729);
        int size = _qmLay.Get("size", 30), gap = _qmLay.Get("gap", 3);
        var row = new GameObject { Z = HudTopZ }; // the top of the HUD band: the clicks land there
        for (int i = 0; i < QuickIcons.Length; i++)
        {
            var (icon, action, label) = QuickIcons[i];
            row.AddChild(Cell(x + i * (size + gap), y, size, $"quickmenu_{icon}", $"quickmenu{icon}", label, () => OpenFeature(action)));
        }
        row.AddChild(Cell(x + QuickIcons.Length * (size + gap), y, size, "quickmenu_hamburger", "quickmenuMenu", "Menu", ToggleMenu));
        return row;
    }

    // One cell: the hit rect and the click id (PanelButton), plus the official
    // three state art. The Image component picks the hover and press art itself.
    private GameObject Cell(int x, int y, int size, string stem, string id, string label, Action onClick)
    {
        foreach (string state in IconStates)
            LoadTexture($"ui/{stem}{state}");
        var o = PanelButton(new Rectangle(x, y, size, size), id, onClick, ButtonKind.Click);
        o.Add(new Image($"ui/{stem}"));
        o.Add(new HoverTip(() => label)); // the official names the icon under the cursor
        return o;
    }
}
