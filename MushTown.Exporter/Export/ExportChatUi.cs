using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The chat window art (UI/StatusBar3.img/chat): the log frame (a nine
// piece slice), the filter tabs and the input line with its five buttons.
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

        // the filter tabs: All and Etc. carry their text, the blank tab gets
        // the Party and Friend labels from the client
        foreach (var (node, name) in new[]
        {
            ("view/tabDefaultAll", "chat_taball"),
            ("view/tabDefaultEtc", "chat_tabetc"),
            ("view/tab", "chat_tab"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{chat}/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the input line: the left cap, the middle and the right cap
        foreach (string piece in new[] { "w", "c", "e" })
            if (!UiPng.Save(ui, $"{chat}/input/backgrnd/{piece}", Path.Combine(dir, $"chat_input_{piece}.png"))) return false;

        // the five input buttons: whisper, link, chat command, emoticon, settings
        foreach (var (node, name) in new[]
        {
            ("button:chat", "chat_btn_whisper"),
            ("button:link", "chat_btn_link"),
            ("button:chatCommand", "chat_btn_cmd"),
            ("button:chatEmoticon", "chat_btn_emoji"),
            ("button:setting", "chat_btn_set"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{chat}/input/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        Console.WriteLine("chat ui: frame, 3 tabs, input bar, 5 buttons");
        return true;
    }
}
