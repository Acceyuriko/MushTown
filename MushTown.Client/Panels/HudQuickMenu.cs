using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The quick menu row at the bottom left of the HUD: the official 30x30
// icons of UI/UITotalMenu.img/quickMenu/menu. Only the features the game
// has get an icon (the other 46 would be dead buttons); a click opens
// that window.
public partial class MushTownGame
{
    // Every number of the row lives in layouts/panel-quickmenu.json.
    private static readonly PanelLayout _qmLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-quickmenu.json"));

    // The icons the game has: the official icon number and what it opens.
    private static readonly (int Icon, Feature Action)[] QuickIcons =
    {
        (0, Feature.Stat),      // Character Info
        (4, Feature.Equip),     // Equipment
        (6, Feature.Inventory), // Inventory
        (11, Feature.Skill),    // Skills
        (17, Feature.Quest),    // Quests
        (19, Feature.WorldMap), // World Map
        (52, Feature.Keys),     // Key Bindings
        (53, Feature.Settings), // Settings
        (54, Feature.Quit),     // Exit Game
    };

    // The three states of one icon art.
    private static readonly string[] IconStates = { "_n.png", "_h.png", "_p.png" };

    // The row: one object per icon (the hit rect, the button, the official
    // art). The Image component picks the hover and press art by itself.
    private GameObject BuildQuickMenu()
    {
        int x = _qmLay.Get("x", 341);
        int y = _qmLay.Get("y", 729);
        int size = _qmLay.Get("size", 25);
        int gap = _qmLay.Get("gap", 2);
        var row = new GameObject();
        for (int i = 0; i < QuickIcons.Length; i++)
        {
            var (icon, action) = QuickIcons[i];
            foreach (string state in IconStates)
                LoadTexture($"ui/quickmenu_{icon}{state}");
            var o = new GameObject();
            o.Add(new Rect(new Rectangle(x + i * (size + gap), y, size, size)));
            o.Add(new Button { LegacyId = $"quickmenu{icon}", Kind = ButtonKind.Click, OnClick = () => OpenFeature(action) });
            o.Add(new Image($"ui/quickmenu_{icon}"));
            row.AddChild(o);
        }
        return row;
    }
}
