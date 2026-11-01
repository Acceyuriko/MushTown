using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The chat window art (UI/StatusBar3.img/chat): the log frame (a nine
// piece slice), the two filter tabs, the input line, the chevron that folds
// the chat and the small frame of the folded chat.
// The art is saved as it is: no recolouring, no baking.
public static partial class Export
{
    // The balloon styles written to assets/ui: the plain one and the ones the
    // chat rings in ExportItem.Gears name. One list, checked at item export.
    public static readonly int[] BalloonStyles = { 0, 1, 7 };

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

        // The speech balloons: the plain one (0) and the styles the chat rings
        // name (info/chatBalloon). A style is ten pictures (ui/balloon_<n>_<piece>.png)
        // plus the numbers the client needs (ui/balloon_<n>.json): the corner
        // size, which is the border of the bubble, where the tail hangs and the
        // color of the text.
        foreach (int style in BalloonStyles)
        {
            foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se", "arrow" })
                if (!UiPng.Save(ui, $"ChatBalloon.img/{style}/{piece}", Path.Combine(dir, $"balloon_{style}_{piece}.png"))) return false;
            if (!UiPng.TryRead(ui, "UI", $"ChatBalloon.img/{style}/nw", out var corner, out _)) return false;
            if (!UiPng.TryRead(ui, "UI", $"ChatBalloon.img/{style}/arrow", out var arrowPng, out _)) return false;
            if (!UiPng.TryRead(ui, "UI", $"ChatBalloon.img/{style}/s", out var bottom, out _)) return false;
            // The tail art is centred under the bubble: its own middle lands on
            // the middle of the bubble. It starts inside the bottom border, so
            // the frame's dark outline and the tail's dark outline join into one
            // line (the tail's top row carries no outline: it blends into the
            // fill). (The WZ origin is not the tail's middle, so it is not used.)
            WriteJson(Path.Combine(dir, $"balloon_{style}.json"), new BalloonStyleData
            {
                Style = style,
                Border = corner.Width,
                ArrowX = -(arrowPng.Width / 2),
                ArrowY = -bottom.Height, // the bottom border is how far the tail goes in
                Color = ui.Find($@"ChatBalloon.img\{style}\clr")?.GetValueEx<int>(0) ?? 0,
            });
        }

        // the small frame of the folded chat
        foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
            if (!UiPng.Save(ui, $"{chat}/view/backgrnd_min/{piece}", Path.Combine(dir, $"chat_frame_min_{piece}.png"))) return false;

        // the one window button that is left: the chevron that folds the chat.
        // The other official buttons (add tab, maximise, pop out chat) have no
        // function in this game, so the client does not draw them.
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, $"{chat}/view/btMin/{state}/0", Path.Combine(dir, $"chat_btmin_{suffix}.png"))) return false;

        // the scroll bar of the chat log
        foreach (string piece in new[] { "base_t", "base_c", "base_b", "thumb0", "thumb1", "thumb2" })
            if (!UiPng.Save(ui, $"{chat}/view/scroll/enabled/{piece}", Path.Combine(dir, $"chat_scroll_{piece}.png"))) return false;
        foreach (string piece in new[] { "base_t", "base_c", "base_b" })
            if (!UiPng.Save(ui, $"{chat}/view/scroll/disabled/{piece}", Path.Combine(dir, $"chat_scroll_off_{piece}.png"))) return false;

        // the chat target button of the input row: the official switch
        // between the world and the party. The button shows the target the
        // line goes to, so both words are needed.
        foreach (var (node, name) in new[] { ("all", "chat_target_all"), ("party", "chat_target_party") })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{chat}/input/chatTarget/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the NPC talk art ships with the chat art
        if (!ExportTalkUi(ui, outDir)) return false;

        // The balloons an NPC wears instead of talking: 0 = a quest to take,
        // 1 = a quest under way, 2 = a quest to hand in (UIWindow2.img/QuestIcon).
        for (int i = 0; i < 3; i++)
        {
            if (!UiPng.Save(ui, $"UIWindow2.img/QuestIcon/{i}/0", Path.Combine(dir, $"npc_quest_{i}.png"))) return false;
            if (!UiPng.TryRead(ui, "UI", $"UIWindow2.img/QuestIcon/{i}/0", out var icon, out _) || icon == null) return false;
            WriteJson(Path.Combine(dir, $"npc_quest_{i}.json"), MeasureTail(icon));

        }

        Console.WriteLine($"chat ui: frame, folded frame, 2 tabs, input bar, chevron, {BalloonStyles.Length} speech balloons, 3 NPC quest balloons");
        return true;
    }

    // Where the tail tip of a quest balloon sits in its own picture: the middle
    // of its lowest drawn row. The WZ origin is the picture centre, not the tail.
    private static QuestMarkData MeasureTail(Wz_Png png)
    {
        using var bmp = png.ExtractPng();
        int bottom = -1, left = int.MaxValue, right = -1;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                if (bmp.GetPixel(x, y).A > 40) bottom = y;
        for (int x = 0; x < bmp.Width; x++)
            if (bmp.GetPixel(x, bottom).A > 40) { left = Math.Min(left, x); right = Math.Max(right, x); }
        return new QuestMarkData { TailX = (left + right) / 2, TailY = bottom };
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
