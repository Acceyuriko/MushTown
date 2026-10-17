using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Quest-side panels: quest, settings, shop, talk (modal).
public partial class MushTownGame
{
    private const int QuestW = 320; // quest window: accepted list + detail
    private const int QuestRowH = 22;
    private const int QuestListH = 5 * QuestRowH; // 5 rows, no scroll in v1
    private const int QuestBodyH = QuestListH + 190; // list + detail block
    private const int SettingsW = 260; // settings window: 2 volume rows
    private const int SettingsRowH = 38;
    private const int SettingsBodyH = 10 + 2 * SettingsRowH + 10; // top pad + rows + bottom pad
    private const int ShopW = 340; // shop window: buy rows + sell rows (height follows the bag)
    private const int ShopRowH = 40;
    private const int TalkW = 440; // talk panel: body + quest entries + buttons
    private const int TalkBtnW = 90, TalkBtnH = 24;

    private Rectangle QuestRect() => new(_questPos.X, _questPos.Y, QuestW, 16 + QuestBodyH);
    private Rectangle QuestTitleRect() => new(_questPos.X, _questPos.Y, QuestW, 16);
    private Rectangle QuestXRect()
    {
        var r = QuestRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private Rectangle SettingsRect() => new(_settingsPos.X, _settingsPos.Y, SettingsW, 16 + SettingsBodyH);
    private Rectangle SettingsTitleRect() => new(_settingsPos.X, _settingsPos.Y, SettingsW, 16);
    private Rectangle SettingsXRect()
    {
        var r = SettingsRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    // Volume row buttons: [-] and [+] boxes around the value.
    private Rectangle SettingsMinusRect(int i)
    {
        var r = SettingsRect();
        return new Rectangle(r.X + 120, r.Y + 16 + 10 + i * SettingsRowH, 30, 24);
    }

    private Rectangle SettingsPlusRect(int i)
    {
        var r = SettingsRect();
        return new Rectangle(r.X + 200, r.Y + 16 + 10 + i * SettingsRowH, 30, 24);
    }

    // Sell rows: owned potions first, then owned equips (both in tab order).
    // Draw and clicks share this list, so row i is always row i.
    private List<(int id, int count)> ShopSellRows()
    {
        var rows = new List<(int, int)>();
        var chara = _save?.Slots[_slot];
        if (chara == null) return rows;
        foreach (int id in _potions)
            if (chara.Use?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        foreach (int id in _equips)
            if (chara.EquipBag?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        return rows;
    }

    // Shop rects: mesos line 20, Buy header 18 + stock rows, Sell header 18
    // + bag rows (an empty bag keeps one row for the "empty" line).
    private Rectangle ShopRect()
    {
        int sells = Math.Max(1, ShopSellRows().Count);
        int body = 20 + 18 + _shopStock.Count * ShopRowH + 18 + sells * ShopRowH + 10;
        return new Rectangle(_shopPos.X, _shopPos.Y, ShopW, 16 + body);
    }

    private Rectangle ShopTitleRect() => new(_shopPos.X, _shopPos.Y, ShopW, 16);

    private Rectangle ShopXRect()
    {
        var r = ShopRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private int ShopBuyTop() => _shopPos.Y + 16 + 20 + 18;

    private int ShopSellTop() => ShopBuyTop() + _shopStock.Count * ShopRowH + 18;

    private Rectangle ShopBuyRect(int i) =>
        new(_shopPos.X + ShopW - 80, ShopBuyTop() + i * ShopRowH + 7, 68, 26);

    private Rectangle ShopSellRect(int i) =>
        new(_shopPos.X + ShopW - 80, ShopSellTop() + i * ShopRowH + 7, 68, 26);

    // Click a stock row: buy one at WZ price (a broke wallet logs and skips).
    private void BuyFromShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no wallet
        var def = _items[id];
        int price = def.Price;
        if (Shop.Buy(chara, id, price))
        {
            GameLog($"bought {def.Name} for {price:#,##0} mesos");
            PlaySfx("shopbuy");
        }
        else GameLog($"not enough mesos for {def.Name} ({price:#,##0})");
    }

    // Click a bag row: sell one at half price (rows show owned only, so it lands).
    private void SellToShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no bag
        var def = _items[id];
        if (Shop.Sell(chara, id, def))
        {
            GameLog($"sold {def.Name} for {Shop.SellPrice(def.Price):#,##0} mesos");
            PlaySfx("click");
        }
    }

    private void BuildQuestScene(List<int> accepted)
    {
        _questScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(QuestRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("QUEST"));
        body.Add(new Picture(_questBack, 0, 16));
        for (int row = 0; row < accepted.Count && row < 5; row++) // draw shows 5: clicks match
        {
            int i = row; // for-loop variables capture shared: copy per row
            int qid = accepted[i];
            string name = _quests[qid].Name;
            var rb = PanelButton(QuestRowRect(i), "questrow" + i, () => _questSel = accepted[i], ButtonKind.Click);
            rb.Add(new LiveTint(() =>
            {
                var c = _save?.Slots[_slot];
                bool done = _questLog.State(qid).IsComplete(c!); // rows exist only with a chara
                var ink = qid == _questSel ? new Color(255, 220, 0)
                    : done ? new Color(120, 255, 120) : Color.White;
                return ((done ? "? " : "") + name, ink);
            }, 4, 3));
            body.AddChild(rb);
        }
        body.AddChild(PanelButton(QuestTitleRect(), "", () => _dragQuest = _questScene.LastClick - _questPos)); // silent, like legacy
        var x = PanelButton(QuestXRect(), "x3", () => _showQuest = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        var detail = new GameObject { NoHit = true };
        detail.Add(new Rect(QuestRect()));
        detail.Add(new QuestDetail(() =>
        {
            var c = _save?.Slots[_slot];
            if (c == null || !_quests.TryGetValue(_questSel, out var sel) || !c.Quests.Contains(_questSel)) return null;
            return new QuestLive(QuestDetailLines(c, sel), sel.Kills.Select((k, i) =>
            {
                int n = _questLog.KillCount(c, _questSel, i);
                return ($"{MobName(k.Mob)} {n}/{k.Count}", n >= k.Count);
            }).ToList(), sel.NeedItemsToComplete ? "Need items" : $"Turn in: {NpcName(sel.Npc1)}");
        }, 16 + QuestListH + 10));
        body.AddChild(detail);
        _questScene.Add(body);
    }

    // Selected-quest wrapped lines, re-wrapped when the sel or its kill
    // counts change (counters ride the key: a kill under the open panel
    // re-renders #a, same as the talk cache).
    private List<string> QuestDetailLines(CharaSlot chara, QuestData sel)
    {
        string key = _questSel + ":" + string.Join(",", sel.Kills.Select((_, i) => _questLog.KillCount(chara, _questSel, i)));
        if (key != _questKey)
        {
            _questKey = key;
            _questLines = _ui.Wrap(QText(sel.Progress), QuestW - 24);
        }
        return _questLines;
    }

    private void BuildSettingsScene()
    {
        _settingsScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(SettingsRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("SETTINGS"));
        body.Add(new Picture(_settingsBack, 0, 16));
        string[] labels = { "Music", "Sound" };
        for (int r = 0; r < 2; r++)
        {
            int i = r; // for-loop variables capture shared: copy per row
            body.Add(new Label(labels[i], 12, 16 + 10 + i * SettingsRowH + 4));
            var minus = PanelButton(SettingsMinusRect(i), "setminus" + i, () => BumpVolume(i == 0, -10), ButtonKind.Click);
            minus.Add(new StepBox("-"));
            body.AddChild(minus);
            var plus = PanelButton(SettingsPlusRect(i), "setplus" + i, () => BumpVolume(i == 0, 10), ButtonKind.Click);
            plus.Add(new StepBox("+"));
            plus.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => (i == 0 ? _bgmVol : _sfxVol).ToString(), 0, 4, Color.White));
            body.AddChild(plus);
        }
        body.AddChild(PanelButton(SettingsTitleRect(), "", () => _dragSettings = _settingsScene.LastClick - _settingsPos)); // silent, like legacy
        var x = PanelButton(SettingsXRect(), "x4", () => _showSettings = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        _settingsScene.Add(body);
    }

    private void BuildShopScene(List<(int id, int count)> sells)
    {
        _shopScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(ShopRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("SHOP"));
        body.Add(new Fill(new Color(62, 96, 118), 16));
        body.Add(new LiveLabel(() => $"Mesos: {(_save?.Slots[_slot]?.Mesos ?? 0):#,##0}", 12, 16 + 4, new Color(255, 220, 0)));
        body.Add(new Label("Buy", 12, 16 + 22));
        var r = ShopRect();
        for (int i = 0; i < _shopStock.Count; i++)
        {
            int s = i; // for-loop variables capture shared: copy per row
            int id = _shopStock[s];
            var def = _items[id];
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, ShopBuyTop() + s * ShopRowH, ShopW, ShopRowH)));
            holder.Add(new ShopRow(def.Image, def.Name, $"{def.Price:#,##0}"));
            body.AddChild(holder);
            var buy = PanelButton(ShopBuyRect(s), "buy" + s, () => BuyFromShop(_shopStock[s]));
            buy.Add(new FlatButton("Buy", "buy" + s, () => _hoverId));
            body.AddChild(buy);
        }
        body.Add(new Label("Sell", 12, ShopSellTop() - r.Y - 16));
        if (sells.Count == 0)
            body.Add(new Label("Your bag is empty.", 12, ShopSellTop() - r.Y + 12) { Tint = new Color(130, 140, 150) });
        for (int i = 0; i < sells.Count; i++)
        {
            int s = i; // for-loop variables capture shared: copy per row
            int id = sells[s].id;
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, ShopSellTop() + s * ShopRowH, ShopW, ShopRowH)));
            holder.Add(new ShopRow(_items[id].Image, $"{_items[id].Name} x{sells[s].count}", $"{Shop.SellPrice(_items[id].Price):#,##0}"));
            body.AddChild(holder);
            var sell = PanelButton(ShopSellRect(s), "sell" + s, () => SellToShop(sells[s].id));
            sell.Add(new FlatButton("Sell", "sell" + s, () => _hoverId));
            body.AddChild(sell);
        }
        body.AddChild(PanelButton(ShopTitleRect(), "", () => _dragShop = _shopScene.LastClick - _shopPos)); // silent, like legacy
        var x = PanelButton(ShopXRect(), "x5", () => _showShop = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        _shopScene.Add(body);
    }

    private void BuildTalkScene()
    {
        _talkScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(TalkRect())); // no Button: talk swallows every click, like the legacy guard
        body.Add(new TitleBar(_npcs[_talkNpc].Name));
        body.Add(new Fill(new Color(62, 96, 118), 16));
        body.Add(new TalkBody(() => TalkLines(), 16 + 8));
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
                    var ink = offer || ready ? new Color(255, 220, 0) : new Color(130, 140, 150);
                    string mark = offer ? "! " : ready ? "? " : "";
                    return (mark + _quests[q].Name, ink);
                }, 0, 0));
                body.AddChild(eb);
            }
            if (NpcHasShop(_talkNpc))
            {
                var sb = PanelButton(TalkEntryRect(entries.Count), "talkshop", () => OpenShop(_talkNpc), ButtonKind.Click);
                sb.Add(new Label("Shop", 0, 0) { Tint = new Color(255, 220, 0) });
                body.AddChild(sb);
            }
        }
        var buttons = TalkButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            var bb = PanelButton(TalkBtnRect(r, buttons.Count), "talkbtn" + r, () => TalkAction(buttons[r]), ButtonKind.Click);
            bb.Add(new FlatButton(buttons[r], "talkbtn" + r, () => _hoverId));
            body.AddChild(bb);
        }
        _talkScene.Add(body);
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

    private Rectangle QuestRowRect(int i)
    {
        var r = QuestRect();
        return new Rectangle(r.X + 8, r.Y + 16 + 6 + i * QuestRowH, QuestW - 16, QuestRowH);
    }

    // Talk panel (modal): NPC name on top, one page of body, quest entries in
    // menu mode, buttons at the bottom. Height follows the content.
    private Rectangle TalkRect()
    {
        int lines = TalkLines().Count + (_talkQuest == 0 ? TalkEntries(_talkNpc).Count : 0)
            + (_talkQuest == 0 && NpcHasShop(_talkNpc) ? 1 : 0); // the Shop row in menu mode
        int h = 16 + 8 + lines * 16 + 8 + TalkBtnH + 10;
        return new Rectangle((1366 - TalkW) / 2, 340 - h / 2, TalkW, h);
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
            _talkLines = _ui.Wrap(QText(TalkPages()[_talkPage]), TalkW - 32);
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
        int total = n * TalkBtnW + (n - 1) * 8;
        return new Rectangle(r.X + (TalkW - total) / 2 + i * (TalkBtnW + 8), r.Bottom - TalkBtnH - 8, TalkBtnW, TalkBtnH);
    }

    private Rectangle TalkEntryRect(int i)
    {
        var r = TalkRect();
        return new Rectangle(r.X + 16, r.Y + 16 + 8 + TalkLines().Count * 16 + i * 16, TalkW - 32, 16);
    }
}
