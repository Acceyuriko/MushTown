namespace MushTown.Shared;

// One default passive that is a real skill: the skill window lists it on the
// beginner page. Label is the same text the stat hover shows as the source
// (the Source of default-passives.json, or "Maple Warrior"), so the window and
// the stat panel always name the same thing. WzId is the skill id in the WZ
// (0 = not in the WZ). Desc and Effect are the text of the skill: null takes
// it from String.wz (desc and h, with the level values put in for #x style
// marks). A text is written here when the WZ one tells about an effect the game
// does not have (cooldown, party, active use). IconId is the skill whose icon
// is used when the skill has none (0 = its own). ItemIconId is the item whose icon is used (0 = none).
// HasStat is false for a passive whose effect is not a stat record (Empirical Knowledge marks mobs on hit): it still shows
// in the window but has no source in the stat panel.
public sealed record DefaultPassiveSkill(string Label, int WzId, string? Desc = null, string? Effect = null, int IconId = 0, int ItemIconId = 0, bool HasStat = true);

public static class DefaultPassiveSkills
{
    public static IReadOnlyList<DefaultPassiveSkill> All { get; } = new DefaultPassiveSkill[]
    {
        // Heroic is not in the WZ: its text is written here and Hero's Echo lends the icon.
        new("Heroic (Reboot, Lv.200-249)", 0,
            "Bonus of a Reboot character between Lv.200 and Lv.249.",
            "Final Damage: +35%\nMeso Obtained: x6\nMax HP: +200, Max MP: +100\nAttack Power and Magic ATT: +5",
            IconId: 1005),
        new("Maple Warrior", 2121000,
            "The Goddess of Maple World's grace increases your stats.",
            "All stats with a direct AP investment increase by #basicStatUp%."),
        new("Will of the Alliance", 190),
        new("Hero's Echo", 1005,
            "Increases the Attack Power and Magic ATT.",
            "Attack Power and Magic ATT: +#x%"),
        // The three Monster Park potions (red 2023125, green 2023126, blue 2023127; WZ Consume/0202.img spec) as one
        // permanent passive, with the green potion's icon. Its text is written here: the numbers are the
        // Monster Park Potion records of default-passives.json.
        new("Monster Park Potion", 0,
            "Potions from Monster Park. Permanently increases Weapon Speed, Max HP, Max MP, Attack Power and Magic ATT.",
            "Attack Speed: +1\nMax HP: +2000, Max MP: +2000\nAttack Power: +30, Magic ATT: +30",
            ItemIconId: 2023126),
        new("Link: Adventurer's Curiosity (Bowman)", 80002766,
            "Your curiosity and attention to detail grants you a bonus to Critical Rate.",
            "Critical Rate: +#cr%"),
        new("Link: Pirate Blessing", 80002774,
            "Your piratical tenacity instills you with improved stats.",
            "STR: +#strX, DEX: +#dexX, INT: +#intX, LUK: +#lukX, Max HP: +#mhpX, Max MP: +#mmpX"),
        new("Link: Cygnus Blessing", 80000055),
        new("Link: Light Wash (Luminous)", 80000005),
        new("Link: Elven Blessing (Mercedes)", 80001040,
            "Blesses you with a permanent increase in EXP gains through the power of Elven lore.",
            "EXP Obtained: +#expR%"),
        new("Link: Phantom Instinct (Phantom)", 80000002),
        new("Link: Hybrid Logic (Xenon)", 80000047),
        new("Link: Fury Unleashed (Demon Slayer)", 80000001),
        new("Link: Wild Rage (Demon Avenger)", 80000050),
        new("Link: Iron Will (Kaiser)", 80000006),
        new("Link: Rhinne's Blessing (Zero)", 80000110,
            "Receive the Goddess Rhinne's blessings, reducing the damage taken from enemies while ignoring a portion of their defense."),
        new("Link: Grounded Body (Ren)", 80003877),
        new("Link: Judgment (Kinesis)", 80000188),
        new("Link: Noble Fire (Adele)", 80002857,
            null,
            "Boss Damage: +#bdR%\nDamage: +#x%"),
        new("Link: Innate Gift (Khali)", 80003224,
            "Receive the blessing of the desert. Permanently increases Damage.",
            "Damage: +#damR%"),
        new("Link: Nature's Friend (Lara)", 80003058,
            "Receive help by becoming friends with the surrounding nature.",
            "Damage: +#damR%"),
        new("Link: Bravado (Hoyoung)", 80000609,
            "You're confident even in the face of the strongest enemy. Ignore a portion of your enemy's Defense.",
            "Ignore Defense: +#ignoreMobpdpR%"),
        new("Link: Focus Spirit (Lynn)", 80010006),
        new("Link: Qi Cultivation (Mo Xuan)", 80011964,
            "Cultivates your qi using techniques first developed in Jianghu.",
            "Boss Damage: +#bdR%"),
        new("Link: Guiding Stars (Shine)", 80010343),
        // The Explorer Magician Link: a permanent passive that marks the mobs we hit (a chance to add stacks that raise our
        // damage and DEF-ignored % against them). No stat record: the hit judge reads its numbers.
        new("Link: Empirical Knowledge (Magician)", 80002762, HasStat: false),
    };
}
