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

        // the other official chat frames: the plain one (no border) and the
        // two small ones for the minimised chat
        foreach (var (node, name) in new[]
        {
            ("view/backgrnd1", "chat_frame2"),
            ("view/backgrnd_min", "chat_frame_min"),
            ("view/backgrnd_min1", "chat_frame_min1"),
        })
            foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
                if (!UiPng.Save(ui, $"{chat}/{node}/{piece}", Path.Combine(dir, $"{name}_{piece}.png"))) return false;

        // the plain tab that sits next to the All and Etc. tabs
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, $"{chat}/view/tab/{state}/0", Path.Combine(dir, $"chat_tab_{suffix}.png"))) return false;

        // the window buttons: add tab, minimise, maximise, pop out chat
        foreach (var (node, name) in new[]
        {
            ("view/btAddTab", "chat_btaddtab"),
            ("view/btMin", "chat_btmin"),
            ("view/btMax", "chat_btmax"),
            ("view/btOutChat", "chat_btoutchat"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{chat}/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the scroll bar of the chat log
        foreach (string piece in new[] { "base_t", "base_c", "base_b", "thumb0", "thumb1", "thumb2", "prev0", "prev1", "prev2", "next0", "next1", "next2" })
            if (!UiPng.Save(ui, $"{chat}/view/scroll/enabled/{piece}", Path.Combine(dir, $"chat_scroll_{piece}.png"))) return false;
        foreach (string piece in new[] { "base_t", "base_c", "base_b", "prev", "next" })
            if (!UiPng.Save(ui, $"{chat}/view/scroll/disabled/{piece}", Path.Combine(dir, $"chat_scroll_off_{piece}.png"))) return false;

        // the chat target button of the input row: the official switch
        // between the world and the party. The button shows the target the
        // line goes to, so both words are needed.
        foreach (var (node, name) in new[] { ("all", "chat_target_all"), ("party", "chat_target_party") })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{chat}/input/chatTarget/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the NPC talk art ships with the chat art
        if (!ExportTalkUi(ui, outDir)) return false;

        Console.WriteLine("chat ui: frame, 2 tabs, input bar, 1 button, speech balloon");
        return true;
    }

    // The NPC talk art: the quest helper list window (UI/Quest.img/Helper),
    // the dialogue frame the client uses for its text boxes
    // (UI/UIWindow2.img/UtilDlgEx), the blue system popup
    // (UI/UIWindow2.img/Notice) and the small monologue frame
    // (UI/Basic.img/Monologue). The art is saved as it is.
    public static bool ExportTalkUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        // the helper window frame: the list that sits under the NPC talk
        foreach (var (node, name) in new[]
        {
            ("canvas:topClosed", "talk_helper_top_closed"),
            ("canvas:topOpened", "talk_helper_top_open"),
            ("canvas:mid", "talk_helper_mid"),
            ("canvas:bottom", "talk_helper_bottom"),
        })
            if (!UiPng.Save(ui, $"Quest.img/Helper/{node}", Path.Combine(dir, $"{name}.png"))) return false;

        // the quest numbers of the helper window
        for (int i = 0; i < 10; i++)
            if (!UiPng.Save(ui, $"Quest.img/Helper/qNum/{i}", Path.Combine(dir, $"talk_helper_qnum_{i}.png"))) return false;

        // the quest title bar of the helper window
        foreach (var (node, name) in new[]
        {
            ("canvas:top", "talk_helper_title_top"),
            ("canvas:mid", "talk_helper_title_mid"),
            ("canvas:bottom", "talk_helper_title_bottom"),
        })
            if (!UiPng.Save(ui, $"Quest.img/Helper/questTitle/{node}", Path.Combine(dir, $"{name}.png"))) return false;

        // the helper window buttons
        foreach (var (node, name) in new[]
        {
            ("button:close", "talk_helper_close"),
            ("button:openList", "talk_helper_openlist"),
            ("button:closeList", "talk_helper_closelist"),
            ("questTitle/button:close", "talk_helper_titleclose"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"Quest.img/Helper/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the scroll bar of the helper list
        foreach (string piece in new[] { "prev0", "prev1", "prev2", "base", "next0", "next1", "next2", "thumb0", "thumb1", "thumb2" })
            if (!UiPng.Save(ui, $"Quest.img/Helper/scroll:list/enabled/{piece}", Path.Combine(dir, $"talk_helper_scroll_{piece}.png"))) return false;

        // the dialogue frame and its buttons: the NPC talk and the party
        // invite popup use the same box
        if (!ExportDialogUi(ui, outDir)) return false;

        // the blue system popup: the "congratulations" window and its family
        for (int i = 0; i <= 18; i++)
            if (!UiPng.Save(ui, $"UIWindow2.img/Notice/{i}", Path.Combine(dir, $"notice_popup_{i}.png"))) return false;
        foreach (var (node, name) in new[]
        {
            ("btOK", "notice_btok"),
            ("btYes", "notice_btyes"),
            ("btNo", "notice_btno"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"UIWindow2.img/Notice/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the small frame the NPC uses for a short line of talk
        for (int i = 0; i < 4; i++)
            if (!UiPng.Save(ui, $"Basic.img/Monologue/0/{i}", Path.Combine(dir, $"talk_monologue_{i}.png"))) return false;

        Console.WriteLine("talk ui: helper window, dialogue frame, system popup, monologue");
        return true;
    }

    // The dialogue box (UI/UIWindow2.img/UtilDlgEx): the frame with the
    // name tab, the text area and the buttons. The NPC talk and the party
    // invite popup both use it.
    public static bool ExportDialogUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        // the dialogue frame: top, middle and bottom. The i* ones are the
        // same frame with the icon column on the left.
        foreach (var (node, name) in new[]
        {
            ("t", "talk_dlg_top"),
            ("c", "talk_dlg_mid"),
            ("s", "talk_dlg_bottom"),
            ("it", "talk_dlgi_top"),
            ("ic", "talk_dlgi_mid"),
            ("is", "talk_dlgi_bottom"),
        })
            if (!UiPng.Save(ui, $"UIWindow2.img/UtilDlgEx/{node}", Path.Combine(dir, $"{name}.png"))) return false;

        // the text area of the dialogue and its scroll bar
        if (!UiPng.Save(ui, "UIWindow2.img/UtilDlgEx/notice", Path.Combine(dir, "talk_dlg_textarea.png"))) return false;
        if (!UiPng.Save(ui, "UIWindow2.img/UtilDlgEx/line", Path.Combine(dir, "talk_dlg_scrollline.png"))) return false;
        if (!UiPng.Save(ui, "UIWindow2.img/UtilDlgEx/bar", Path.Combine(dir, "talk_dlg_scrollbar.png"))) return false;
        foreach (var (node, name) in new[] { ("dot0", "talk_dlg_dot0"), ("dot1", "talk_dlg_dot1") })
            if (!UiPng.Save(ui, $"UIWindow2.img/UtilDlgEx/{node}", Path.Combine(dir, $"{name}.png"))) return false;

        // the dialogue buttons
        foreach (var (node, name) in new[]
        {
            ("BtOK", "talk_dlg_btok"),
            ("BtClose", "talk_dlg_btclose"),
            ("BtYes", "talk_dlg_btyes"),
            ("BtNo", "talk_dlg_btno"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"UIWindow2.img/UtilDlgEx/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        return true;
    }
}
