using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The party art (UI/UIParty.img/mainUI): the party window with its member
// list and the search window with the invite list. The invite popup uses the
// official dialogue box (UI/UIWindow2.img/UtilDlgEx), so that art is
// exported too. The chat target button of the chat input row belongs to the
// chat panel. The art is saved as it is: no recolouring, no baking.
public static partial class Export
{
    // The file names must match the client (MushTown.Client/Panels/HudParty.cs).
    public static bool ExportPartyUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);
        string party = "UIParty.img/mainUI";

        // the party window: the frame and the close button
        if (!UiPng.Save(ui, $"{party}/backgrnd", Path.Combine(dir, "party_frame.png"))) return false;
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, $"{party}/button:close/{state}/0", Path.Combine(dir, $"party_close_{suffix}.png"))) return false;

        // the member list: the list frame, the "no party" layer, the member
        // row, the leader mark and the online mark
        if (!UiPng.Save(ui, $"{party}/partyWnd/backgrnd", Path.Combine(dir, "party_list.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/partyWnd/layer:noParty", Path.Combine(dir, "party_none.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/partyWnd/entity/back/normal", Path.Combine(dir, "party_row.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/partyWnd/entity/leader/0", Path.Combine(dir, "party_leader.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/partyWnd/entity/stateIcon/online", Path.Combine(dir, "party_online.png"))) return false;

        // the party window buttons: make a party and leave
        foreach (var (node, name) in new[]
        {
            ("button:partyMake", "party_btn_make"),
            ("button:partyOut", "party_btn_out"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{party}/partyWnd/{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the invite button: only the leader can invite, so the other
        // members get the official disabled art
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p"), ("disabled", "d") })
            if (!UiPng.Save(ui, $"{party}/partyWnd/button:partyInvite/{state}/0", Path.Combine(dir, $"party_btn_invite_{suffix}.png"))) return false;

        // the search window: the frame, the tab of the page we show (the
        // players of the map), the list panel, the row and the invite
        // button of a row
        if (!UiPng.Save(ui, $"{party}/searchWnd/backgrnd", Path.Combine(dir, "party_search.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/searchWnd/tab:partySearch/selected/0", Path.Combine(dir, "party_search_tab.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/searchWnd/partyMemberWnd/backgrnd", Path.Combine(dir, "party_search_list.png"))) return false;
        if (!UiPng.Save(ui, $"{party}/searchWnd/partyMemberWnd/entity/backgrnd", Path.Combine(dir, "party_search_row.png"))) return false;
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
            if (!UiPng.Save(ui, $"{party}/searchWnd/partyMemberWnd/entity/btInvite/{state}/0", Path.Combine(dir, $"party_btn_invite_row_{suffix}.png"))) return false;

        // the invite popup uses the official dialogue box
        if (!ExportDialogUi(ui, outDir)) return false;

        Console.WriteLine("party ui: window, list, row, buttons, search window, dialogue box");
        return true;
    }
}
