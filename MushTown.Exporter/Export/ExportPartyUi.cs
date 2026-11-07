using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The party art (UI/UIParty.img/mainUI and UI/UIParty.img/partyHP): the party
// window with its member list, the search window with the invite list and the
// HP panel. The HP bars use the old UIWindow.img/UserList set and the party
// gauge; the Show Member HP switch uses UI/Basic.img/CheckBox44. The invite
// popup uses the official dialogue box (UI/UIWindow2.img/UtilDlgEx), so that
// art is exported too. The chat target button of the chat input row belongs to
// the chat panel. The art is saved as it is: no recolouring, no baking.
public static partial class Export
{
    // The file names must match the client (MushTown.Client/Panels/HudParty.cs
    // and HudPartyHp.cs).
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

        // the party HP panel: the nine-slice frame and leader marks. The
        // gauge frame comes from the old UserList set: its corners are the
        // bevelled ones of reference 1.
        string hp = "UIParty.img/partyHP";
        foreach (var (node, name) in new[]
        {
            ("nw", "party_hp_nw"), ("ne", "party_hp_ne"), ("sw", "party_hp_sw"),
            ("se", "party_hp_se"), ("n", "party_hp_n"), ("s", "party_hp_s"),
            ("w", "party_hp_w"), ("e", "party_hp_e"), ("c", "party_hp_c"),
            ("leader/0", "party_hp_leader0"), ("leader/1", "party_hp_leader1"),
        })
            if (!UiPng.Save(ui, $"{hp}/{node}", Path.Combine(dir, name + ".png"))) return false;

        // the panel gauge: old UserList art, red gauge as in reference 1
        string panelGauge = "UIWindow.img/UserList/Party/PartyHP/GaugeBar";
        if (!UiPng.Save(ui, $"{panelGauge}/bar", Path.Combine(dir, "party_hp_bar.png"))) return false;
        if (!UiPng.Save(ui, $"{panelGauge}/graduation", Path.Combine(dir, "party_hp_graduation.png"))) return false;
        if (!UiPng.Save(ui, $"{panelGauge}/gauge", Path.Combine(dir, "party_hp_gauge.png"))) return false;

        // the HP bar over a party member's head: UIParty art with square
        // corners as in reference 2; the red old gauge replaces the pink one
        string head = "UIParty.img/partyHP/GaugeBar";
        if (!UiPng.Save(ui, $"{head}/bar", Path.Combine(dir, "party_head_bar.png"))) return false;
        if (!UiPng.Save(ui, $"{head}/graduation", Path.Combine(dir, "party_head_graduation.png"))) return false;
        if (!UiPng.Save(ui, $"{panelGauge}/gauge", Path.Combine(dir, "party_head_gauge.png"))) return false;

        // the Show Member HP switch (type 44 of the party window)
        if (!UiPng.Save(ui, "Basic.img/CheckBox44/0", Path.Combine(dir, "party_check_off.png"))) return false;
        if (!UiPng.Save(ui, "Basic.img/CheckBox44/1", Path.Combine(dir, "party_check_on.png"))) return false;

        // the invite popup uses the official dialogue box
        if (!ExportDialogUi(ui, outDir)) return false;

        Console.WriteLine("party ui: window, list, row, buttons, search window, HP panel, HP bar, toggle, dialogue box");
        return true;
    }
}
