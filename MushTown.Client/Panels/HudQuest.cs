using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The quest window (official UIWindow.img/Quest art) and the talk panel (modal).
public partial class MushTownGame
{
    // The official quest art: the list panel (left) and the detail panel (right).
    private const int QuestListW = 245;   // Quest/backgrnd
    private const int QuestDetailW = 305; // Quest/backgrnd2
    private const int QuestW = QuestListW + QuestDetailW;
    private const int QuestBodyH = 396;   // both panels are 396 tall
    private const int TalkW = 519;       // the dialogue box art is 519 wide
    private const int TalkTopH = 28;     // the top piece of the box
    private const int TalkBottomH = 44;  // the bottom piece (the button bar)
    private const int TalkBtnH = 16;     // the button art is 16 tall

    // Every number of this window lives in layouts/panel-quest.json.
    private static readonly PanelLayout _questLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-quest.json"));

    // The dialogue box numbers live in layouts/panel-talk.json.
    private static readonly PanelLayout _talkLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-talk.json"));

    // The quest art (the exporter writes these names).
    private static readonly string[] QuestFiles = new[]
    {
        "quest_list.png", "quest_detail.png", "quest_x.png",
        "quest_tab0.png", "quest_tab1.png", "quest_tab2.png",
        "quest_icon0.png", "quest_icon1.png",
        "quest_basic.png", "quest_prob.png", "quest_reward.png",
        "quest_gauge.png", "quest_gauge_fill.png",
    };

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

    private Rectangle QuestRect() => new(_questPos.X, _questPos.Y, QuestW, QuestBodyH);
    private Rectangle QuestListRect() => new(_questPos.X, _questPos.Y, QuestListW, QuestBodyH);
    private Rectangle QuestDetailRect() => new(_questPos.X + QuestListW, _questPos.Y, QuestDetailW, QuestBodyH);

    // The QUEST strip on the list panel drags the window.
    private Rectangle QuestTitleRect() => new(_questPos.X, _questPos.Y, QuestListW, _questLay.Get("titleH", 20));
    private Rectangle QuestXRect()
    {
        var r = QuestDetailRect();
        return new Rectangle(r.X + _questLay.Get("closeX", 288), r.Y + _questLay.Get("closeY", 2), 11, 11);
    }

    // The selected quest, live (the kill counters change under the open panel).
    private sealed record QuestDetailData(
        string Name, string Level, List<string> Lines, List<(string Text, bool Done)> Kills,
        List<string> Rewards, string TurnIn, int Percent);

    private QuestDetailData? _questDetail; // the built detail (the header, the gauge and the body share it)

    private void BuildQuestScene(List<int> accepted)
    {
        PlaceQuestWindow();
        // The window opens on the first quest: without a pick the whole right
        // panel (name, gauge, description) would stay empty.
        if (_questSel == 0 && accepted.Count > 0) _questSel = accepted[0];
        foreach (string f in QuestFiles) LoadTexture("ui/" + f);
        _questScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(QuestRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new Picture("ui/quest_list.png", 0, 0));
        body.Add(new Picture("ui/quest_detail.png", QuestListW, 0));

        // The official tab labels (Available / In Progress / Complete). The WZ
        // has no dimmer art for the other tabs, so all three draw the same.
        int tx = _questLay.Get("tabX", 8), ty = _questLay.Get("tabY", 27), gap = _questLay.Get("tabGap", 10);
        int w0 = Tex("quest_tab0.png").Width, w1 = Tex("quest_tab1.png").Width;
        body.Add(new Picture("ui/quest_tab0.png", tx, ty));
        body.Add(new Picture("ui/quest_tab1.png", tx + w0 + gap, ty));
        body.Add(new Picture("ui/quest_tab2.png", tx + w0 + w1 + gap * 2, ty));

        int maxRows = _questLay.Get("maxRows", 5);
        for (int row = 0; row < accepted.Count && row < maxRows; row++) // draw shows the same rows the clicks take
        {
            int i = row; // for-loop variables capture shared: copy per row
            int qid = accepted[i];
            var rr = QuestRowRect(i);
            var rb = new GameObject();
            rb.Add(new Rect(rr));
            var btn = new Button { LegacyId = "questrow" + i, Kind = ButtonKind.Click, OnClick = () => _questSel = qid };
            rb.Add(btn);
            // The row lights up under the mouse (the list panel is white, so
            // the light is a blue fill drawn here).
            rb.Add(new Invoke(gfx =>
            {
                if (gfx.HoverButton != btn) return;
                gfx.Sb.Draw(gfx.Pixel, rr, new Color(90, 140, 200, 70));
            }));
            rb.Add(new Invoke(gfx => DrawQuestIcon(gfx, rr, qid)));
            rb.Add(new LiveTint(() => QuestRowInk(qid), _questLay.Get("nameX", 22), _questLay.Get("nameY", 2)));
            rb.Add(new RightLabel((ui, t) => ui.GetSmall(t), () => QuestLevelText(qid), rr.Width - _questLay.Get("levelX", 8), _questLay.Get("levelY", 3), new Color(120, 130, 145)));
            rb.Add(new LiveLabel(() => QuestRowProgress(qid), _questLay.Get("nameX", 22), _questLay.Get("progY", 17), new Color(130, 140, 155)));
            body.AddChild(rb);
        }

        // The detail panel (right): the quest name, the level, the gauge.
        var dr = QuestDetailRect();
        var head = new GameObject { NoHit = true };
        head.Add(new Rect(dr));
        head.Add(new LiveLabel(() => QuestDetail()?.Name ?? "", _questLay.Get("headX", 30), _questLay.Get("headY", 32), Color.White, LabelFont.Mid));
        head.Add(new LiveLabel(() => QuestDetail()?.Level ?? "", _questLay.Get("headX", 30), _questLay.Get("headLevelY", 58), new Color(195, 215, 235)));
        head.Add(new Invoke(gfx => DrawQuestGauge(gfx, dr)));
        body.AddChild(head);

        var text = new GameObject { NoHit = true };
        text.Add(new Rect(QuestRect()));
        text.Add(new Invoke(gfx => DrawQuestBody(gfx)));
        body.AddChild(text);

        body.AddChild(PanelButton(QuestTitleRect(), "", () => { _questDragged = true; _dragQuest = _questScene.LastClick - _questPos; })); // silent, like legacy
        var x = PanelButton(QuestXRect(), "x3", () => _showQuest = false, ButtonKind.Click);
        x.Add(new Picture("ui/quest_x.png"));
        body.AddChild(x);
        _questScene.Add(body);
    }

    // The panel file owns the window spot until the player drags it.
    private void PlaceQuestWindow()
    {
        if (!_questDragged) _questPos = new Point(_questLay.Get("x", 400), _questLay.Get("y", 110));
    }

    private Rectangle QuestRowRect(int i)
    {
        int x = _questLay.Get("rowX", 8), y = _questLay.Get("rowY", 52), h = _questLay.Get("rowH", 30);
        var r = QuestListRect();
        return new Rectangle(r.X + x, r.Y + y + i * h, QuestListW - x * 2, h);
    }

    // The quest is ready to turn in (every kill done).
    private bool QuestReady(int qid)
    {
        var c = _save?.Slots[_slot];
        return c != null && _questLog.State(qid).IsComplete(c);
    }

    // The list icon: the yellow "!" when the quest is ready to turn in.
    private void DrawQuestIcon(UiGfx gfx, Rectangle rr, int qid)
    {
        gfx.Sb.Draw(gfx.Tex(QuestReady(qid) ? "ui/quest_icon0.png" : "ui/quest_icon1.png"),
            new Vector2(rr.X + _questLay.Get("iconX", 4), rr.Y + _questLay.Get("iconY", 2)), Color.White);
    }

    // The quest row in the list: the name (gold when selected) and the state.
    private (string Text, Color Ink) QuestRowInk(int qid)
    {
        bool done = QuestReady(qid);
        // The list panel is white: the name needs a dark ink, not white.
        var ink = qid == _questSel ? new Color(255, 220, 0) : done ? new Color(0, 150, 0) : new Color(45, 50, 60);
        return ((done ? "? " : "") + _quests[qid].Name, ink);
    }

    // The row's second line: the first unmet kill, or the turn-in hint.
    private string QuestRowProgress(int qid)
    {
        var c = _save?.Slots[_slot];
        if (c == null || !_quests.TryGetValue(qid, out var q)) return "";
        if (QuestReady(qid)) return "Ready to turn in";
        for (int i = 0; i < q.Kills.Count; i++)
        {
            int n = _questLog.KillCount(c, qid, i);
            if (n < q.Kills[i].Count) return $"{MobName(q.Kills[i].Mob)} {n}/{q.Kills[i].Count}";
        }
        return "";
    }

    private string QuestLevelText(int qid)
    {
        return _quests.TryGetValue(qid, out var q)
            ? q.LvMax > 0 ? $"Lv.{q.LvMin}-{q.LvMax}" : $"Lv.{q.LvMin}+"
            : "";
    }

    // The selected quest's detail (null when nothing is selected). It is
    // built once per change and reused by the header, the gauge and the body.
    private QuestDetailData? QuestDetail()
    {
        var c = _save?.Slots[_slot];
        if (c == null || !_quests.TryGetValue(_questSel, out var sel) || !c.Quests.Contains(_questSel)) return null;
        // Counters ride the key: a kill under the open panel rebuilds it.
        string key = _questSel + ":" + string.Join(",", sel.Kills.Select((_, i) => _questLog.KillCount(c, _questSel, i)));
        if (key != _questKey)
        {
            _questKey = key;
            _questLines = _ui.Wrap(QText(sel.Progress), _questLay.Get("bodyW", 268));
            var kills = new List<(string, bool)>();
            int have = 0, need = 0;
            for (int i = 0; i < sel.Kills.Count; i++)
            {
                int n = _questLog.KillCount(c, _questSel, i);
                have += Math.Min(n, sel.Kills[i].Count);
                need += sel.Kills[i].Count;
                kills.Add(($"{MobName(sel.Kills[i].Mob)} {n}/{sel.Kills[i].Count}", n >= sel.Kills[i].Count));
            }
            _questDetail = new QuestDetailData(sel.Name, QuestLevelText(_questSel), _questLines, kills,
                sel.RewardLines(id => _items[id].Name),
                sel.NeedItemsToComplete ? "Need items" : $"Turn in: {NpcName(sel.Npc1)}",
                need > 0 ? have * 100 / need : 0);
        }
        return _questDetail;
    }

    // The header's progress bar (the official Gauge2 art).
    private void DrawQuestGauge(UiGfx gfx, Rectangle dr)
    {
        var d = QuestDetail();
        if (d == null) return;
        int x = dr.X + _questLay.Get("gaugeX", 200), y = dr.Y + _questLay.Get("gaugeY", 64);
        gfx.Sb.Draw(gfx.Tex("ui/quest_gauge.png"), new Vector2(x, y), Color.White);
        int w = _questLay.Get("gaugeW", 75) * d.Percent / 100;
        if (w > 0)
            gfx.Sb.Draw(gfx.Tex("ui/quest_gauge_fill.png"), new Rectangle(x + _questLay.Get("gaugeInX", 2),
                y + _questLay.Get("gaugeInY", 2), w, _questLay.Get("gaugeFillH", 4)), Color.White);
    }

    // The body of the detail panel: the official section titles with the
    // description, the kills, the reward and the turn-in line. It stops at
    // the bottom bar, so a long quest does not draw over the buttons.
    private void DrawQuestBody(UiGfx gfx)
    {
        var d = QuestDetail();
        if (d == null) return;
        int x = _questPos.X + QuestListW + _questLay.Get("bodyX", 14);
        int y = _questPos.Y + _questLay.Get("bodyY", 124);
        int lh = _questLay.Get("lineH", 16);
        int bottom = y + _questLay.Get("bodyH", 240);
        var ink = new Color(45, 50, 60);
        bool full = false; // the body is full: nothing more is drawn
        void Art(string key, int gap)
        {
            if (full) return;
            var t = gfx.Tex("ui/" + key);
            if (y + t.Height > bottom) { full = true; return; }
            gfx.Sb.Draw(t, new Vector2(x, y), Color.White);
            y += gap;
        }
        void Line(string text, Color color)
        {
            if (full) return;
            if (y + lh > bottom) { full = true; return; }
            gfx.Sb.Draw(gfx.Ui.GetSmall(text), new Vector2(x, y), color);
            y += lh;
        }
        Art("quest_basic.png", _questLay.Get("secGap", 14));
        foreach (string line in d.Lines) Line(line, ink);
        y += _questLay.Get("secSpace", 4);
        Art("quest_prob.png", _questLay.Get("secGap", 14));
        foreach (var k in d.Kills) Line(k.Text, k.Done ? new Color(0, 130, 0) : ink);
        y += _questLay.Get("secSpace", 4);
        Art("quest_reward.png", _questLay.Get("rewardGap", 20));
        foreach (string line in d.Rewards) Line(line, ink);
        y += _questLay.Get("secSpace", 4);
        Line(d.TurnIn, new Color(190, 130, 0));
    }

    // Quest window: accepted quests on top (click to select), the selected
    // quest's progress below (objectives with live counts + turn-in).
    private List<int> QuestRows()
    {
        var chara = _save?.Slots[_slot];
        // Unknown ids (removed custom file, stale save) hide everywhere: the
        // tracker skips them too, so draw, click, and tracker agree.
        return chara == null ? new List<int>()
            : chara.Quests.Where(id => _quests.ContainsKey(id)).OrderBy(id => id).ToList();
    }

    // The talk panel (modal): the official dialogue box, the NPC name on
    // the top tab, one page of text, the quest entries under it and the
    // buttons in the blue bar at the bottom. The height follows the text.
    private void BuildTalkScene()
    {
        foreach (string f in TalkFiles) LoadTexture("ui/" + f);
        _talkScene = new Scene();
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
        _talkScene.Add(body);
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

    // The dialogue box: the top piece, the text rows and the bottom piece.
    private Rectangle TalkRect()
    {
        int rows = TalkLines().Count + (_talkQuest == 0 ? TalkEntries(_talkNpc).Count : 0)
            + (_talkQuest == 0 && NpcHasShop(_talkNpc) ? 1 : 0); // the Shop row in menu mode
        int h = TalkTopH + rows * _talkLay.Get("lineStep", 13) + TalkBottomH;
        return new Rectangle((MushTownGame.GameWidth - TalkW) / 2, _talkLay.Get("centerY", 340) - h / 2, TalkW, h);
    }

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
