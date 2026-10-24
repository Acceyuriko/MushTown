namespace MushTown.Client;

// The features the game can open. The MENU window rows, the MENU window
// bottom buttons and the quick menu icons all use this one list.
internal enum Feature
{
    None,       // the feature the game does not have yet
    Stat,       // Character Info
    Equip,      // Equipment
    Inventory,  // Inventory
    Skill,      // Skills
    Quest,      // Quests
    WorldMap,   // World Map
    Keys,       // Key Bindings
    Settings,   // Settings
    Guide,      // Guides (the contents guide window)
    Quit,       // Exit Game
}

public partial class MushTownGame
{
    // Open the feature. The MENU window closes first: the panels draw under
    // the windows, so a panel would open unseen behind it.
    internal void OpenFeature(Feature feature)
    {
        _menuWindow.Close();
        switch (feature)
        {
            case Feature.Stat: OpenStatWindow(0); break;
            case Feature.Equip: OpenStatWindow(1); break;
            case Feature.Inventory: OpenInventoryWindow(); break;
            case Feature.Skill: OpenSkillWindow(); break;
            case Feature.Quest: OpenQuestWindow(); break;
            case Feature.WorldMap: ShowWindow(HotkeyAction.WorldMap); break;
            case Feature.Keys: OpenKeyWindow(); break;
            case Feature.Settings: OpenSettings(); break;
            case Feature.Guide: ShowWindow(HotkeyAction.GuideCard); break;
            case Feature.Quit: ExitGame(); break;
        }
    }
}
