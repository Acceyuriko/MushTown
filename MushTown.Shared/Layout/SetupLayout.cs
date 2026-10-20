using System.Text.Json;
using static MushTown.Shared.LayoutJson;

namespace MushTown.Shared;

// layouts/setup-layout.json: the name-your-hero screen in 1366x768 space.
// panel is the board top-left (the top card plus the lower card under it);
// every other position except avatar is an offset inside that board, so
// moving the board moves the whole panel. avatar is the preview doll's feet
// on the screen (an absolute spot, not a board offset). The *Size keys
// are the boxes: the card and button art stretches into them, the name box
// and caret use theirs, and classNameSize is the box the class-name art
// centres in. Missing keys keep their current values, so the file may list
// only what you tweak. This file also rejects a key it does not know (a typo
// would otherwise do nothing silently); the other layout files do not.
public static class SetupLayout
{
    // The keys the file may hold. A typo would silently do nothing, so an
    // unknown key is an error: the client logs it and keeps the old layout.
    private static readonly HashSet<string> Keys = new()
    {
        "panel", "avatar", "cardSize", "cardBot", "cardBotSize", "nameField", "nameFieldSize",
        "nameFont", "nameText", "caret", "caretGap", "className", "classNameSize", "classTint",
        "confirm", "confirmSize", "cancel", "cancelSize", "errorFont", "error",
    };

    public static (int X, int Y) Panel = (960, 220); // card top-left
    public static (int X, int Y) Avatar = (515, 420); // preview doll feet
    public static (int W, int H) CardSize = (366, 202); // top card box
    public static (int X, int Y) CardBot = (0, 202); // lower card top-left (in the board)
    public static (int W, int H) CardBotSize = (366, 91); // lower card box
    public static (int X, int Y) NameField = (27, 90); // name box top-left (in the top card)
    public static (int W, int H) NameFieldSize = (312, 42); // name box
    public static int NameFont = 16; // name text size in display px
    public static (int X, int Y) NameText = (10, 0); // name text offset in the box (y is added to the centered spot)
    public static (int W, int H) Caret = (2, 16); // caret box
    public static int CaretGap = -2; // gap between the text and the caret
    public static (int X, int Y) ClassName = (71, 156); // class-name box top-left (in the top card)
    public static (int W, int H) ClassNameSize = (273, 54); // the class-name art centres in this box
    public static (int R, int G, int B) ClassTint = (70, 96, 120); // class-name art tint (white art on a pale card)
    public static (int X, int Y) Confirm = (12, 231); // confirm button top-left (in the board)
    public static (int W, int H) ConfirmSize = (166, 43);
    public static (int X, int Y) Cancel = (186, 231); // cancel button top-left (in the board)
    public static (int W, int H) CancelSize = (167, 43);
    public static int ErrorFont = 22; // error line size in display px
    public static (int X, int Y) Error = (183, 300); // error line anchor (in the board), the text centres on it

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        foreach (var p in root.EnumerateObject())
            if (p.Name != "_help" && !Keys.Contains(p.Name)) throw new JsonException($"unknown key \"{p.Name}\"");
        // All or nothing: a bad value throws before any field changes.
        var panel = GetPoint(root, "panel", Panel);
        var avatar = GetPoint(root, "avatar", Avatar);
        var cardSize = GetSize(root, "cardSize", CardSize);
        var cardBot = GetPoint(root, "cardBot", CardBot);
        var cardBotSize = GetSize(root, "cardBotSize", CardBotSize);
        var nameField = GetPoint(root, "nameField", NameField);
        var nameFieldSize = GetSize(root, "nameFieldSize", NameFieldSize);
        var nameFont = GetInt(root, "nameFont", NameFont);
        var nameText = GetPoint(root, "nameText", NameText);
        var caret = GetSize(root, "caret", Caret);
        var caretGap = GetInt(root, "caretGap", CaretGap);
        var className = GetPoint(root, "className", ClassName);
        var classNameSize = GetSize(root, "classNameSize", ClassNameSize);
        var classTint = GetColor(root, "classTint", ClassTint);
        var confirm = GetPoint(root, "confirm", Confirm);
        var confirmSize = GetSize(root, "confirmSize", ConfirmSize);
        var cancel = GetPoint(root, "cancel", Cancel);
        var cancelSize = GetSize(root, "cancelSize", CancelSize);
        var errorFont = GetInt(root, "errorFont", ErrorFont);
        var error = GetPoint(root, "error", Error);
        if (nameFont <= 0) throw new JsonException("bad nameFont"); // GDI+ throws for 0/negative sizes
        if (errorFont <= 0) throw new JsonException("bad errorFont");
        Panel = panel; Avatar = avatar; CardSize = cardSize; CardBot = cardBot; CardBotSize = cardBotSize;
        NameField = nameField; NameFieldSize = nameFieldSize; NameFont = nameFont; NameText = nameText;
        Caret = caret; CaretGap = caretGap; ClassName = className; ClassNameSize = classNameSize;
        ClassTint = classTint; Confirm = confirm; ConfirmSize = confirmSize; Cancel = cancel;
        CancelSize = cancelSize; ErrorFont = errorFont; Error = error;
    }
}
