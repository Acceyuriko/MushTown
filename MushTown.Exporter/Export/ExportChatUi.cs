using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The chat window art (UI/StatusBar3.img/chat): the log frame (a nine
// piece slice), the filter tabs and the input line with its buttons.
// The art is saved as it is: no recolouring, no baking.
public static partial class Export
{
    // The file names must match the client (MushTown.Client/Panels/HudChat.cs).
    public static bool ExportChatUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);
        string chat = "StatusBar3.img/chat/ingame";

        // the log frame: nine pieces, the corners stay and the edges stretch
        foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
            if (!UiPng.Save(ui, $"{chat}/view/backgrnd/{piece}", Path.Combine(dir, $"chat_frame_{piece}.png"))) return false;

        // the filter tabs: All and Etc. carry their text in the art
        foreach (var (node, name) in new[]
        {
            ("view/tabDefaultAll", "chat_taball"),
            ("view/tabDefaultEtc", "chat_tabetc"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{chat}/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the input line: the left cap, the middle and the right cap
        foreach (string piece in new[] { "w", "c", "e" })
            if (!UiPng.Save(ui, $"{chat}/input/backgrnd/{piece}", Path.Combine(dir, $"chat_input_{piece}.png"))) return false;

        // the input button: settings. The other official buttons (whisper,
        // link, chat command, emoticon) have no function in this game, so
        // the client does not draw them.
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, $"{chat}/input/button:setting/{state}/0", Path.Combine(dir, $"chat_btn_set_{suffix}.png"))) return false;

        // the speech balloon (UI/ChatBalloon.img/0): nine pieces for the bubble
        // and the tail under it
        foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
            if (!UiPng.Save(ui, $"ChatBalloon.img/0/{piece}", Path.Combine(dir, $"balloon_{piece}.png"))) return false;
        if (!UiPng.Save(ui, "ChatBalloon.img/0/arrow", Path.Combine(dir, "balloon_arrow.png"))) return false;

        Console.WriteLine("chat ui: frame, 2 tabs, input bar, 1 button, speech balloon");
        return true;
    }
}
