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
    // Open the feature. The caller closes its own window first (the MENU
    // window does, the quick menu row on the HUD does not).
    internal void OpenFeature(Feature feature)
    {
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
            case Feature.Guide: ShowWindow(HotkeyAction.Guide); break;
            case Feature.Quit: ExitGame(); break;
        }
    }
}
