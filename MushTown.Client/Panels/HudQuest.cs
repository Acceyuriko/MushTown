using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The quest window (official UI/Quest.img/Main art) and the talk panel (modal).
public partial class MushTownGame
{
    // The official quest art: the list panel (left) and the info panel (right).
    private const int QuestListW = 420;  // Quest.img/Main/questList/backgrnd
    private const int QuestInfoW = 322;  // Quest.img/Main/questInfo/backgrnd
    private const int QuestW = QuestListW + QuestInfoW;
    private const int QuestH = 550;      // both panels are 550 tall
    private const int TalkW = 519;       // the dialogue box art is 519 wide
    private const int TalkTopH = 28;     // the top piece of the box
    private const int TalkBottomH = 44;  // the bottom piece (the button bar)
    private const int TalkBtnH = 16;     // the button art is 16 tall

    // Every number of this window lives in layouts/panel-quest.json.
    private static readonly PanelLayout _questLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-quest.json"));

    // The dialogue box numbers live in layouts/panel-talk.json.
    private static readonly PanelLayout _talkLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-talk.json"));

    // The quest art (the exporter writes these names).
    private static readonly string[] QuestFiles = BuildQuestFiles();

    private static string[] BuildQuestFiles()
    {
        var files = new List<string>
        {
            "quest_list.png", "quest_info.png", "quest_empty.png", "quest_selected.png", "quest_row_sel.png",
            "quest_sec_info.png", "quest_sec_obj.png", "quest_box.png", "quest_reward.png",
            "quest_exp.png", "quest_meso.png", "quest_npc_shadow.png",
            "quest_close_n.png", "quest_close_h.png", "quest_close_p.png",
            "quest_x_n.png", "quest_x_h.png", "quest_x_p.png",
            "quest_start_n.png", "quest_start_h.png", "quest_start_p.png",
            "quest_end_n.png", "quest_end_h.png", "quest_end_p.png",
            "quest_giveup_n.png", "quest_giveup_h.png", "quest_giveup_p.png",
            "qhelper_add.png", "qhelper_check.png",
            "wm_pop_top.png", "wm_pop_mid.png", "wm_pop_bottom.png",
            "wm_pop_yes_n.png", "wm_pop_yes_h.png", "wm_pop_yes_p.png",
            "wm_pop_no_n.png", "wm_pop_no_h.png", "wm_pop_no_p.png",
        };
        for (int i = 0; i < 3; i++)
            foreach (string state in new[] { "open", "close" })
                foreach (string suffix in new[] { "n", "h", "p" })
                    files.Add($"quest_grp{i}_{state}_{suffix}.png");
        return files.ToArray();
    }

    // The dialogue box art (UI/UIWindow2.img/UtilDlgEx) and its buttons.
    private static readonly string[] TalkFiles = new[]
    {
        "talk_dlg_top.png", "talk_dlg_mid.png", "talk_dlg_bottom.png",
        "talk_dlg_btok_n.png", "talk_dlg_btok_h.png", "talk_dlg_btok_p.png",
        "talk_dlg_btclose_n.png", "talk_dlg_btclose_h.png", "talk_dlg_btclose_p.png",
        "talk_dlg_btyes_n.png", "talk_dlg_btyes_h.png", "talk_dlg_btyes_p.png",
        "talk_dlg_btno_n.png", "talk_dlg_btno_h.png", "talk_dlg_btno_p.png",
    };

    private bool _questDragged; // the player dragged the window: the file spot does not win
    private readonly bool[] _questGroupOpen = { true, true, true }; // state index: 0 Available, 1 In Progress, 2 Completed
    private static readonly int[] QuestGroupOrder = { 1, 0, 2 };    // what the list shows, top to bottom
    private QuestInfoData? _questDetail; // the built detail (name, text, objectives, rewards)

    private Rectangle QuestRect() => new(0, 0, QuestW, QuestH);
    private Rectangle QuestInfoRect() => new(QuestListW, 0, QuestInfoW, QuestH);

    // The title strips drag the window.
    private Rectangle QuestTitleRect() => new(0, 0, QuestListW, _questLay.Get("titleH", 27));
    private Rectangle QuestInfoTitleRect() => new(QuestListW, 0, QuestInfoW, _questLay.Get("titleH", 27));
    private Rectangle QuestXRect() => new(_questLay.Get("closeListX", 396), _questLay.Get("closeListY", 12),
        Tex("quest_close_n.png").Width, Tex("quest_close_n.png").Height);
    private Rectangle QuestInfoXRect() => new(QuestListW + _questLay.Get("closeInfoX", 293), _questLay.Get("closeInfoY", 2),
        Tex("quest_x_n.png").Width, Tex("quest_x_n.png").Height);

    private void BuildQuestScene()
    {
        PlaceQuestWindow();
        foreach (string f in QuestFiles) LoadTexture("ui/" + f);
        _questHost.Clear();
        var body = new GameObject();
        body.Add(new Rect(QuestRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new Picture("ui/quest_list.png", 0, 0));
        body.Add(new Picture("ui/quest_info.png", QuestListW, 0));

        AddQuestGroups(body);
        AddQuestInfo(body);

        body.AddChild(PanelButton(QuestTitleRect(), "", () => { _questDragged = true; _dragQuest = _uiScene.LastClick - _questPos; }));
        body.AddChild(PanelButton(QuestInfoTitleRect(), "", () => { _questDragged = true; _dragQuest = _uiScene.LastClick - _questPos; }));

        var x = PanelButton(QuestXRect(), "x3", () => _showQuest = false, ButtonKind.Click);
        x.Add(new Image("ui/quest_close"));
        body.AddChild(x);
        var xi = PanelButton(QuestInfoXRect(), "x4", () => _showQuest = false, ButtonKind.Click);
        xi.Add(new Image("ui/quest_x"));
        body.AddChild(xi);
        if (_questConfirm != QuestConfirmKind.None) body.AddChild(BuildQuestConfirm());
        _questHost.AddChild(body);
    }

    // The panel file owns the window spot until the player drags it.
    private void PlaceQuestWindow()
    {
        if (!_questDragged) _questPos = new Point(_questLay.Get("x", 312), _questLay.Get("y", 109));
    }

    // The three collapsible groups: In Progress, Available, Completed. The bar
    // art carries the title and the up/down chevron.
    private void AddQuestGroups(GameObject body)
    {
        int y = _questLay.Get("groupY", 70);
        int barH = _questLay.Get("groupH", 27);
        int rowH = _questLay.Get("rowH", 26);
        var groups = QuestGroups();
        foreach (int g in QuestGroupOrder)
        {
            int group = g; // the closure reads the state index
            bool open = _questGroupOpen[group];
            var bar = PanelButton(new Rectangle(_questLay.Get("groupX", 19), y, _questLay.Get("rowW", 377), barH),
                "qgrp" + group, () => ToggleQuestGroup(group), ButtonKind.Click);
            bar.Add(new Image($"ui/quest_grp{group}_{(open ? "open" : "close")}"));
            body.AddChild(bar);
            y += barH;
            if (!open) continue;
            foreach (int qid in groups[group])
            {
                AddQuestRow(body, qid, y);
                y += rowH;
            }
        }
    }

    private void AddQuestRow(GameObject body, int qid, int y)
    {
        var rb = new GameObject();
        rb.Add(new Rect(new Rectangle(_questLay.Get("rowX", 19), y, _questLay.Get("rowW", 377), _questLay.Get("rowH", 26))));
        rb.Add(new Button { LegacyId = "questrow" + qid, Kind = ButtonKind.Click, OnClick = () => _questSel = qid });
        rb.Add(new QuestRowArt(() => _questSel == qid, _questLay.Get("rowSelX", 5), _questLay.Get("rowSelY", 1)));
        rb.Add(new QuestRowText(() => QuestRowTextData(qid), _questLay.Get("rowNameX", 10),
            _questLay.Get("rowTextPad", 70), _questLay.Get("rowLevelRight", 8), _questLay.Get("rowLevelDy", 6)));
        body.AddChild(rb);
    }

    // The right panel: the empty message, or the selected quest.
    private void AddQuestInfo(GameObject body)
    {
        var info = new GameObject();
        info.Add(new Rect(QuestInfoRect()));
        var d = QuestDetail();
        if (d == null)
        {
            info.Add(new Picture("ui/quest_empty.png", _questLay.Get("emptyX", 8), _questLay.Get("emptyY", 30)));
        }
        else
        {
            info.Add(new Picture("ui/quest_selected.png", _questLay.Get("selX", 8), _questLay.Get("selY", 30)));
            info.Add(new Picture("ui/quest_npc_shadow.png", _questLay.Get("npcShadowX", 242), _questLay.Get("npcShadowY", 103)));
            var stand = new LifeStand { Game = this, Kind = 0, Id = d.NpcId };
            info.Add(new QuestNpcPortrait(stand, _questLay.Get("npcX", 263), _questLay.Get("npcY", 108)));
            info.AddChild(MakeLine(d.Name, _questLay.Get("nameX", 29), _questLay.Get("nameY", 70), _questLay.Get("nameW", 200), Color.White));
            info.AddChild(MakeLine(d.Level, _questLay.Get("lvX", 29), _questLay.Get("lvY", 88), _questLay.Get("lvW", 120), new Color(0xAB, 0xB5, 0xBB)));
            info.Add(new QuestInfoBody(() => QuestDetail(), _questLay));
            AddQuestAction(info);
        }
        body.AddChild(info);
    }

    // A fixed line of small text at an offset of its object.
    private static GameObject MakeLine(string value, int x, int y, int w, Color ink)
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(new Rectangle(x, y, w, 16)));
        o.Add(new Text { Value = () => value, Ink = ink, Line = TextLine.Top });
        return o;
    }

    // The wide button under the info panel: Start for an available quest,
    // Complete for a ready turn-in, and Forfeit for a taken quest. Forfeit
    // uses the official giveUp art at its WZ spot (225,519).
    private void AddQuestAction(GameObject info)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        int quest = _questSel;
        string stem = "";
        Action? action = null;
        if (_questLog.CanAccept(chara, quest))
        {
            stem = "quest_start";
            action = () => AskQuestStart(quest); // teleport to the giver first
        }
        else if (chara.Quests.Contains(quest))
        {
            if (_questLog.State(quest).IsComplete(chara))
            {
                stem = "quest_end";
                action = () => CompleteQuest(quest); // turn in from here
            }
            AddGiveUp(info, quest);
            AddQuestHelperButton(info, quest);
        }
        if (action == null) return;
        var art = Tex(stem + "_n.png");
        var b = PanelButton(new Rectangle(_questLay.Get("startX", 15), _questLay.Get("startY", 471), art.Width, art.Height),
            "quest" + stem, action, ButtonKind.Click);
        b.Add(new Image("ui/" + stem));
        info.AddChild(b);
    }

    private void AddGiveUp(GameObject info, int quest)
    {
        var art = Tex("quest_giveup_n.png");
        var b = PanelButton(new Rectangle(_questLay.Get("giveupX", 225), _questLay.Get("giveupY", 519), art.Width, art.Height),
            "questgiveup", () => AskQuestForfeit(quest), ButtonKind.Click);
        b.Add(new Image("ui/quest_giveup"));
        info.AddChild(b);
    }

    // The official Add to Notifier checkbox: a taken quest can join or leave
    // the helper. The checked art is the small blue tick over the empty box.
    private void AddQuestHelperButton(GameObject info, int quest)
    {
        var art = Tex("qhelper_add.png");
        var b = PanelButton(new Rectangle(_questLay.Get("helperX", 17), _questLay.Get("helperY", 524), art.Width, art.Height),
            "questhelper", () => ToggleQuestHelper(quest), ButtonKind.Click);
        b.Add(new Picture("ui/qhelper_add.png", 0, 0));
        if (QuestTracked(quest)) b.Add(new Picture("ui/qhelper_check.png", 0, 0));
        info.AddChild(b);
    }

    // The system popup over the quest window: teleport-to-giver for Start,
    // and a second ask before Forfeit. The two buttons are the official yes
    // and no art.
    private GameObject BuildQuestConfirm()
    {
        int w = _questLay.Get("confirmW", 318), h = _questLay.Get("confirmH", 143);
        var box = new Rectangle((QuestW - w) / 2, (QuestH - h) / 2, w, h);
        var q = _quests[_questConfirmQuest];
        var lines = _questConfirm == QuestConfirmKind.Start
            ? new List<string> { $"Will you teleport to {NpcName(q.Npc0)}?", q.Name }
            : new List<string> { "Forfeit this quest?", q.Name };
        var o = SysPopUi.Build(box, lines, ConfirmQuest, CloseQuestConfirm, _questLay);
        o.Modal = true; // the rest of the window waits behind the question
        return o;
    }

    // One of the three groups, by state index: 0 Available, 1 In Progress,
    // 2 Completed. Unknown quest ids stay hidden, like the tracker.
    private List<int>[] QuestGroups()
    {
        var groups = new[] { new List<int>(), new List<int>(), new List<int>() };
        var chara = _save?.Slots[_slot];
        if (chara == null) return groups;
        groups[0] = _quests.Values.Where(q => _questLog.CanAccept(chara, q.QuestId)).Select(q => q.QuestId).OrderBy(id => id).ToList();
        groups[1] = chara.Quests.Where(id => _quests.ContainsKey(id)).OrderBy(id => id).ToList();
        groups[2] = chara.QuestsDone.Where(id => _quests.ContainsKey(id)).OrderBy(id => id).ToList();
        return groups;
    }

    // The tree rebuild key: the visible rows, the open groups and the pick.
    private string QuestWindowKey()
    {
        var groups = QuestGroups();
        return $"{_questSel}:{QuestReady(_questSel)}:{QuestTracked(_questSel)}:{_questConfirm}:{_questConfirmQuest}|" +
               string.Join("", _questGroupOpen.Select(b => b ? "1" : "0")) + "|" +
               string.Join("/", groups.Select(g => string.Join(",", g)));
    }

    private void ToggleQuestGroup(int group)
    {
        _questGroupOpen[group] = !_questGroupOpen[group];
        if (!_questGroupOpen[group] && QuestGroups()[group].Contains(_questSel)) _questSel = 0;
    }

    // The quest is taken and ready to turn in (every kill done). An available
    // or completed quest has no live state: the take check comes first.
    private bool QuestReady(int qid)
    {
        var c = _save?.Slots[_slot];
        return c != null && c.Quests.Contains(qid) && _questLog.State(qid).IsComplete(c);
    }

    // The quest row: the name (green when ready) and the level.
    private (string Name, string Level, Color Ink) QuestRowTextData(int qid)
    {
        bool done = _save?.Slots[_slot]?.QuestsDone.Contains(qid) == true;
        bool ready = !done && QuestReady(qid);
        var ink = ready ? new Color(0, 130, 0) : done ? new Color(120, 120, 120) : new Color(45, 50, 60);
        return ((ready ? "? " : "") + _quests[qid].Name, QuestLevelText(qid), ink);
    }

    private string QuestLevelText(int qid)
    {
        return _quests.TryGetValue(qid, out var q)
            ? q.LvMax > 0 ? $"Lv.{q.LvMin}-{q.LvMax}" : $"Lv.{q.LvMin}+"
            : "";
    }

    // The selected quest's detail (null when nothing is selected or the pick
    // is not in any group). It is built once per change; kill counters ride the
    // key, so an open panel shows the live numbers.
    private QuestInfoData? QuestDetail()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !_quests.TryGetValue(_questSel, out var sel)) return null;
        bool taken = chara.Quests.Contains(_questSel);
        bool done = chara.QuestsDone.Contains(_questSel);
        if (!taken && !done && !_questLog.CanAccept(chara, _questSel)) return null;
        string kills = taken ? string.Join(",", sel.Kills.Select((_, i) => _questLog.KillCount(chara, _questSel, i))) : "";
        string key = $"{_questSel}:{taken}:{done}:{kills}";
        if (key != _questKey)
        {
            _questKey = key;
            string raw = done ? sel.Done : taken ? sel.Progress : sel.Offer;
            _questLines = _ui.Wrap(QText(raw,
                done ? (qid, idx) => _quests.TryGetValue(qid, out var q) && idx >= 0 && idx < q.Kills.Count ? q.Kills[idx].Count : 0 : null),
                _questLay.Get("infoTextW", 250));
            var objectives = new List<(string, bool)>();
            for (int i = 0; i < sel.Kills.Count; i++)
            {
                int n = done ? sel.Kills[i].Count : taken ? _questLog.KillCount(chara, _questSel, i) : 0;
                objectives.Add(($"{MobName(sel.Kills[i].Mob)} {n}/{sel.Kills[i].Count}", n >= sel.Kills[i].Count));
            }
            var rewards = new List<QuestRewardIcon>();
            if (sel.RewardExp > 0) rewards.Add(new("ui/quest_exp.png", "x" + sel.RewardExp, _questLay.Get("rewardExpDy", 6))); // png has 11 empty rows at the bottom
            if (sel.RewardMeso > 0) rewards.Add(new("ui/quest_meso.png", "x" + sel.RewardMeso));
            foreach (var r in sel.RewardItems)
                rewards.Add(new(_items[r.Item].Image, "x" + r.Count));
            string footer = done || taken ? "" : $"Talk to {NpcName(sel.Npc0)}";
            _questDetail = new QuestInfoData(sel.Name, QuestLevelText(_questSel), done ? sel.Npc1 : sel.Npc0,
                _questLines, objectives, footer, rewards);
        }
        return _questDetail;
    }

    // The talk panel (modal): the official dialogue box, the NPC name on
    // the top tab, one page of text, the quest entries under it and the
    // buttons in the blue bar at the bottom. The height follows the text.
    private void BuildTalkScene()
    {
        foreach (string f in TalkFiles) LoadTexture("ui/" + f);
        _talkHost.Clear();
        // The dialogue spot: the body under it is local. The rect is reused, so
        // a rebuild for another NPC moves the modal box instead of stacking one.
        var box = TalkBox();
        PlaceHost(_talkHost, box.Location, box.Size.X, box.Size.Y);
        var body = new GameObject();
        body.Add(new Rect(TalkRect())); // no Button: talk swallows every click, like the legacy guard
        body.Add(new TalkFrame());
        body.Add(new Label(_npcs[_talkNpc].Name, _talkLay.Get("nameDx", 14), _talkLay.Get("nameDy", 5)) { Tint = new Color(60, 60, 60) });
        body.Add(new TalkBody(() => TalkLines(), _talkLay.Get("textDx", 295), _talkLay.Get("textDy", 28), _talkLay.Get("lineStep", 13)));
        if (_talkQuest == 0)
        {
            var entries = TalkEntries(_talkNpc);
            for (int i = 0; i < entries.Count; i++)
            {
                int r = i; // for-loop variables capture shared: copy per row
                int q = entries[r].quest;
                bool offer = entries[r].offer;
                var eb = PanelButton(TalkEntryRect(r), "talkentry" + r, () => { _talkQuest = entries[r].quest; _talkPage = 0; }, ButtonKind.Click);
                eb.Add(new LiveTint(() =>
                {
                    var c = _save?.Slots[_slot];
                    bool ready = !offer && c != null && _questLog.State(q).IsComplete(c);
                    var ink = offer || ready ? new Color(150, 90, 0) : new Color(60, 60, 60);
                    string mark = offer ? "! " : ready ? "? " : "";
                    return (mark + _quests[q].Name, ink);
                }, 0, 0));
                body.AddChild(eb);
            }
            if (NpcHasShop(_talkNpc))
            {
                var sb = PanelButton(TalkEntryRect(entries.Count), "talkshop", () => OpenShop(_talkNpc), ButtonKind.Click);
                sb.Add(new Label("Shop", 0, 0) { Tint = new Color(150, 90, 0) });
                body.AddChild(sb);
            }
        }
        var buttons = TalkButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            var bb = PanelButton(TalkBtnRect(r, buttons.Count), "talkbtn" + r, () => TalkAction(buttons[r]), ButtonKind.Click);
            bb.Add(new TalkButton(TalkBtnArt(buttons[r]), buttons[r], "talkbtn" + r, () => _hoverId, _talkLay.Get("btnTextDy", 2)));
            body.AddChild(bb);
        }
        _talkHost.AddChild(body);
    }

    // The official dialogue buttons: the art is blank, the label is ours.
    private static string TalkBtnArt(string label) => label switch
    {
        "Accept" => "talk_dlg_btyes",
        "Complete" => "talk_dlg_btclose",
        "Next" => "talk_dlg_btok",
        _ => "talk_dlg_btno",
    };

    // The button width follows the official art (40, 91 and 57 wide).
    private static int TalkBtnWidth(string label) => label switch
    {
        "Complete" => 91,
        "Next" => 40,
        _ => 57,
    };

    // The dialogue box on the screen: the top piece, the text rows and the
    // bottom piece. The body inside it is local.
    private Rectangle TalkBox()
    {
        int rows = TalkLines().Count + (_talkQuest == 0 ? TalkEntries(_talkNpc).Count : 0)
            + (_talkQuest == 0 && NpcHasShop(_talkNpc) ? 1 : 0); // the Shop row in menu mode
        int h = TalkTopH + rows * _talkLay.Get("lineStep", 13) + TalkBottomH;
        return new Rectangle((MushTownGame.GameWidth - TalkW) / 2, _talkLay.Get("centerY", 340) - h / 2, TalkW, h);
    }

    // The dialogue box inside the talk root.
    private Rectangle TalkRect() => new(0, 0, TalkW, TalkBox().Height);

    private List<string> TalkLines()
    {
        // Counters ride the key: a kill under the open modal re-renders #a.
        var chara = _save?.Slots[_slot];
        var quest = _quests.GetValueOrDefault(_talkQuest);
        string kills = quest == null || chara == null ? ""
            : string.Join(",", quest.Kills.Select((_, i) => _questLog.KillCount(chara, _talkQuest, i)));
        string key = $"{_talkNpc}:{_talkQuest}:{_talkPage}:{kills}";
        if (key != _talkKey)
        {
            _talkKey = key;
            _talkLines = _ui.Wrap(QText(TalkPages()[_talkPage]), _talkLay.Get("textW", 208));
        }
        return _talkLines;
    }

    private List<string> TalkButtons()
    {
        var q = _quests.GetValueOrDefault(_talkQuest);
        if (q == null) return new List<string> { "Bye" };
        var chara = _save?.Slots[_slot]!; // quest mode needs an entry click, which needs a chara
        bool last = _talkPage >= TalkPages().Count - 1;
        bool offer = !chara.Quests.Contains(q.QuestId);
        if (offer) return last ? new List<string> { "Accept", "Bye" } : new List<string> { "Next", "Bye" };
        if (_questLog.State(q.QuestId).IsComplete(chara)) return last ? new List<string> { "Complete", "Bye" } : new List<string> { "Next", "Bye" };
        return new List<string> { "Bye" };
    }

    private Rectangle TalkBtnRect(int i, int n)
    {
        var r = TalkRect();
        var buttons = TalkButtons();
        int gap = _talkLay.Get("btnGap", 8);
        int total = 0;
        for (int k = 0; k < n; k++) total += TalkBtnWidth(buttons[k]) + gap;
        total -= gap;
        int x = r.X + (r.Width - total) / 2;
        for (int k = 0; k < i; k++) x += TalkBtnWidth(buttons[k]) + gap;
        return new Rectangle(x, r.Bottom - TalkBtnH - _talkLay.Get("btnY", 10), TalkBtnWidth(buttons[i]), TalkBtnH);
    }

    private Rectangle TalkEntryRect(int i)
    {
        var r = TalkRect();
        int step = _talkLay.Get("lineStep", 13);
        int y = r.Y + _talkLay.Get("textDy", 28) + TalkLines().Count * step + i * step;
        return new Rectangle(r.X + _talkLay.Get("entryDx", 16), y, _talkLay.Get("entryW", 240), step);
    }
}
