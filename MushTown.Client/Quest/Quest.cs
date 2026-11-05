using MushTown.Shared;

namespace MushTown.Client;

// Quest state and talk flow (solo runs the acts on the save; in net play
// the server runs the same acts on its copy and confirms intents, so a
// client can only take what it may and turn in what it finished). Draw
// code lives in Hud.cs.
public partial class MushTownGame
{
    private readonly Dictionary<int, NpcData> _npcs = new();
    private readonly Dictionary<int, QuestData> _quests = new();
    private QuestLog _questLog = null!; // live states over _quests (same dict ref: customs show up live)
    private readonly Dictionary<int, string> _mobNames = new();
    private int _talkNpc; // talking to (0 = none)
    private int _talkQuest; // quest in offer/turn-in flow (0 = menu)
    private int _talkPage; // page index within the current page list
    private bool _talkByMouse; // opened by a click: no distance rule, walking away keeps it open
    private int _questSel; // selected quest in the window (0 = none)
    private QuestConfirmKind _questConfirm; // the open confirm popup
    private int _questConfirmQuest; // the quest the popup asks about
    private string _talkKey = "", _questKey = "";
    private List<string> _talkLines = new(), _questLines = new();

    // The popup under the quest window: the start travel and the forfeit ask.
    private enum QuestConfirmKind { None, Start, Forfeit }

    // NPC + quest json (Initialize loads this before maps: art checks need
    // npc data; png textures need the device, so content loads them later).
    private void LoadQuestData()
    {
        string npcDir = Path.Combine(_assetsDir, "npc"), questDir = Path.Combine(_assetsDir, "quest");
        foreach (string dir in new[] { npcDir, questDir })
            RequireExportDir(dir);
        foreach (string f in Directory.GetFiles(npcDir, "*.json"))
        {
            var npc = LoadJson<NpcData>(f);
            if (npc.StandDelays.Count == 0)
                throw new InvalidDataException($"npc {npc.NpcId} has no stand frames");
            if (npc.StandDelays.Any(d => d <= 0))
                throw new InvalidDataException($"npc {npc.NpcId} has a bad stand delay");
            if (npc.StandOrigins.Count != npc.StandDelays.Count)
                throw new InvalidDataException($"npc {npc.NpcId}: stand origins are stale, re-run export");
            _npcs[npc.NpcId] = npc;
        }
        foreach (string f in Directory.GetFiles(questDir, "*.json"))
        {
            var q = LoadJson<QuestData>(f);
            _quests[q.QuestId] = q;
        }
        _questLog = new QuestLog(_quests);
    }

    // Custom quests/ (same shape, wins on clash, so data fixes need no
    // re-export). Runs after LoadItemDefs: reward validation reads the defs.
    private void ValidateCustomQuests()
    {
        string custom = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "quests");
        if (!Directory.Exists(custom))
            return;
        foreach (string f in Directory.GetFiles(custom, "*.json"))
        {
            var q = LoadJson<QuestData>(f);
            if (q.QuestId == 0)
                throw new InvalidDataException($"custom quest {f} has id 0");
            if (q.Name == "")
                throw new InvalidDataException($"custom quest {f} has no name");
            if (q.Npc0 == 0 || q.Npc1 == 0)
                throw new InvalidDataException($"custom quest {f} has no giver or turn-in");
            if (!_npcs.ContainsKey(q.Npc0) || !_npcs.ContainsKey(q.Npc1))
                throw new InvalidDataException($"custom quest {f} refers to an unexported npc");
            if (WorldMapUi.Holds(0, q.Npc0).Count == 0)
                throw new InvalidDataException($"custom quest {f} giver npc {q.Npc0} is not on any exported map");
            if (q.Pre.Any(p => p == 0))
                throw new InvalidDataException($"custom quest {f} has a zero pre id");
            if (q.Kills.Any(k => k.Mob == 0 || k.Count == 0))
                throw new InvalidDataException($"custom quest {f} has a zero kill");
            if (q.Kills.Count > 4)
                throw new InvalidDataException($"custom quest {f} has {q.Kills.Count} kills (the detail panel shows 4)");
            if (q.RewardExp < 0)
                throw new InvalidDataException($"custom quest {f} has a negative exp reward");
            if (q.RewardMeso < 0)
                throw new InvalidDataException($"custom quest {f} has a negative meso reward");
            var bad = q.RewardItems.FirstOrDefault(r => !_items.TryGetValue(r.Item, out var d) || d.Slot != null || r.Count <= 0);
            if (bad != null)
                throw new InvalidDataException($"custom quest {f} has a bad item reward (item {bad.Item} x{bad.Count})");
            _quests[q.QuestId] = q;
        }
    }

    // Quest content pass: every stand frame of every NPC (json already loaded
    // in Initialize). The world map tip plays them.
    private void LoadNpcTextures()
    {
        foreach (int id in _npcs.Keys)
            foreach (string image in LifeIdle(0, id).Images)
                LoadTexture(image);
    }

    // Static per map: draw/click read the NPC views (refs into _map.Npcs,
    // never copies). Talk ends with the map; missing art fails loud, not at draw.
    private void EndTalkAndCheckNpcs()
    {
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
        _showShop = false; // the shop is NPC-bound too: a new map closes it
        foreach (var s in _map.Npcs)
            if (!_npcs.ContainsKey(s.NpcId))
                throw new InvalidDataException($"map {_mapId}: npc art {s.NpcId} not exported");
    }

    internal string NpcName(int id) => _npcs.TryGetValue(id, out var n) ? n.Name : $"NPC {id}";
    internal List<string> NpcLines(int id) => _npcs[id].Says; // LoadMapData proved it exists

    internal string MobName(int id)
    {
        // Names cache apart from _mobData: a json-only entry there would make
        // LoadMob skip the textures, and names must never break mob art.
        // Unexported mobs (official quests hunt beyond our set) show raw.
        if (!_mobNames.TryGetValue(id, out string? name))
        {
            string path = Path.Combine(_assetsDir, "mob", $"{id}.json");
            name = File.Exists(path) ? LoadJson<MobData>(path).Name : "";
            if (name == "") name = $"mob {id}"; // unexported or stale file: show the id, never blank
            _mobNames[id] = name;
        }
        return name;
    }

    // The WZ text macros (#b, #p2001#, ...) turned into what the player reads.
    // Done text passes its own counters: turn-in already dropped the live ones.
    internal string QText(string raw, Func<int, int, int>? kills = null)
    {
        var chara = _save?.Slots[_slot] ?? new CharaSlot { Name = "hero" };
        return QuestText.Render(raw, chara, kills ?? ((qid, idx) => _questLog.KillCount(chara, qid, idx)), MobName, NpcName);
    }

    // One of our kills: quest counters tick up (capped per objective).
    private void BumpQuestKills(int mob)
    {
        var chara = _save?.Slots[_slot];
        if (chara != null) _questLog.BumpKills(chara, mob);
    }

    // Talk menu entries: (quest id, offer?) for this NPC, offers first.
    private List<(int quest, bool offer)> TalkEntries(int npc)
    {
        var chara = _save?.Slots[_slot];
        var entries = new List<(int, bool)>();
        if (chara == null) return entries;
        var all = _quests.Values.OrderBy(q => q.QuestId).ToList();
        foreach (var q in all)
            if (q.Npc0 == npc && _questLog.CanAccept(chara, q.QuestId)) entries.Add((q.QuestId, true));
        foreach (var q in all)
            if (q.Npc1 == npc && chara.Quests.Contains(q.QuestId)) entries.Add((q.QuestId, false));
        return entries;
    }

    // The balloon an NPC wears instead of talking: 0 = a quest to take,
    // 1 = a quest under way, 2 = a quest to hand in, -1 = none.
    internal int NpcQuestIcon(int npc)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return -1;
        bool take = false, under = false;
        foreach (var q in _quests.Values)
        {
            if (q.Npc1 == npc && _questLog.IsComplete(chara, q.QuestId)) return 2; // hand in wins
            if (q.Npc0 == npc && _questLog.CanAccept(chara, q.QuestId)) take = true;
            else if ((q.Npc0 == npc || q.Npc1 == npc) && chara.Quests.Contains(q.QuestId)) under = true;
        }
        return take ? 0 : under ? 1 : -1;
    }
    // Current talk pages: menu shows the greeting, offer/turn-in show Say
    // (scripted quests export empty Say: summaries fill in).
    private List<string> TalkPages()
    {
        var chara = _save?.Slots[_slot];
        var q = _quests.GetValueOrDefault(_talkQuest);
        if (q == null || chara == null)
        {
            var npc = _npcs[_talkNpc];
            return new List<string> { npc.Says.Count > 0 ? npc.Says[0] : npc.Name };
        }
        bool offer = !chara.Quests.Contains(q.QuestId);
        if (offer) return q.Say0.Count > 0 ? q.Say0 : new List<string> { q.Offer };
        if (_questLog.State(q.QuestId).IsComplete(chara)) // turn-in path: taken, so the state exists
        {
            // Turn-in ends with the payout preview (a copy: Say1 is live data).
            var pages = new List<string>(q.Say1.Count > 0 ? q.Say1 : new List<string> { q.Done });
            var rewards = q.RewardLines(id => _items[id].Name);
            if (rewards.Count > 0) pages.Add("Reward:\n" + string.Join("\n", rewards));
            return pages;
        }
        string stop = q.Stop != "" ? q.Stop : "You are not done yet.";
        return new List<string> { stop };
    }

    // Shot poses through the real path: quest accepts custom 90001 with 2/5
    // kills (three helper rows, the info panel stays empty), questpick selects
    // 90001, questask/questforfeit pose the confirms, talk stands by the NPC
    // and opens the menu. Missing quest/NPC/ground fails loud, like other poses.
    private void SetupShotQuests()
    {
        if (_shotScreen is not ("quest" or "questask" or "questforfeit" or "questpick" or "talk" or "shop")) return;
        _save ??= new SaveData();
        var chara = _save.Slots[_slot] ??= DemoChara();
        if (_shotScreen is "quest" or "questask" or "questforfeit" or "questpick")
        {
            if (_shotScreen == "questask")
            {
                if (!_questLog.CanAccept(chara, 36301))
                    throw new InvalidDataException("questask shot needs quest 36301 to be available");
                _questSel = 36301;
                AskQuestStart(36301);
                return;
            }
            if (!_quests.TryGetValue(90001, out var q) || !_questLog.Accept(chara, 90001))
                throw new InvalidDataException("quest shot needs custom quest 90001");
            if (q.Kills.Count == 0)
                throw new InvalidDataException("quest shot needs custom quest 90001 with kills");
            for (int i = 0; i < 2; i++)
                _questLog.BumpKills(chara, q.Kills[0].Mob); // 2 kills through the real path
            _questLog.Accept(chara, 90002); // a second helper row
            _questLog.Accept(chara, 1027);  // a third helper row
            chara.QuestHelper.AddRange(new[] { 90001, 90002, 1027 });
            _questSel = _shotScreen is "questforfeit" or "questpick" ? 90001 : 0; // quest shows the empty info panel
            if (_shotScreen == "questforfeit") AskQuestForfeit(90001);
            return;
        }
        var npc = _npcArts.FirstOrDefault(a => a.Spawn.NpcId == _shotTalk)?.Spawn
            ?? throw new InvalidDataException($"--talk needs NPC {_shotTalk} on map {_mapId}");
        _mover.X = npc.X - 30;
        _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, npc.Cy)
            ?? throw new InvalidDataException($"--talk found no ground near NPC {_shotTalk}");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
        ResetMotion();
        _mover.FacingRight = true;
        _mover.Action = "stand1";
        _frame = 0;
        _talkNpc = npc.NpcId;
        _talkQuest = 0;
        _talkPage = 0;
        if (_shotScreen == "shop") // the same call the talk entry makes (non-keepers fail loud)
        {
            if (!NpcHasShop(npc.NpcId))
                throw new InvalidDataException($"NPC {_shotTalk} keeps no shop");
            OpenShop(npc.NpcId);
        }
    }

    // A keeper is an NPC with a stock file (shops/{npc}.json).
    private bool NpcHasShop(int npc) => _shops.ContainsKey(npc);

    // Talk entry -> shop panel: talk closes, the shop opens on top. The
    // stock snapshots here: talk state clears, so the panel cannot re-ask.
    private void OpenShop(int npc)
    {
        _shopStock = _shops[npc].Stock;
        _shopTab = 0; // the shop opens on the buy tab
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
        _showShop = true;
        RaisePanel(5);
    }

    // Last-page buttons: Accept/Complete close with a log line, Next flips.
    // The buttons exist only with a quest + chara (TalkButtons), so both are
    // set; a failed accept/complete keeps the dialog open (solo: cannot
    // happen, the modal freezes quest state; net: the server confirms, and
    // silence leaves the dialog for Bye). In net play the click only sends
    // the intent: the drain applies it once the server confirms.
    private void TalkAction(string button)
    {
        if (button == "Bye") { _talkNpc = 0; _talkQuest = 0; _talkPage = 0; return; }
        if (button == "Next") { _talkPage++; return; }
        if (button == "Accept") AcceptQuest(_talkQuest);
        if (button == "Complete") CompleteQuest(_talkQuest);
    }

    // The accept act, shared by the talk dialog and the quest window's Start
    // button. The callers check the same gate; net play only sends the intent.
    private void AcceptQuest(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !_questLog.CanAccept(chara, quest)) return;
        if (QuestServer()) _net!.SendQuestAccept(quest);
        else if (_questLog.Accept(chara, quest)) ApplyQuestAccepted(quest);
    }

    // The turn-in act, shared by the talk dialog and the quest window's
    // Complete button.
    private void CompleteQuest(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !_questLog.IsComplete(chara, quest)) return;
        if (QuestServer()) _net!.SendQuestComplete(quest);
        else if (_questLog.Complete(chara, quest)) ApplyQuestCompleted(chara, quest);
    }

    // The Forfeit act: a taken quest leaves the log and its kill counters.
    // Net play only sends the intent; the server confirms with QuestGone.
    private void AbandonQuest(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !chara.Quests.Contains(quest)) return;
        if (QuestServer()) _net!.SendQuestAbandon(quest);
        else if (_questLog.Abandon(chara, quest)) ApplyQuestAbandoned(quest);
    }

    private void ApplyQuestAbandoned(int quest)
    {
        LogEvent($"abandoned: {_quests[quest].Name}");
        UntrackQuest(quest);
        if (_questSel == quest) _questSel = 0;
    }

    // A fresh accept lands: log line, dialog closes.
    private void ApplyQuestAccepted(int quest)
    {
        LogEvent($"accepted: {_quests[quest].Name}");
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
    }

    // A turn-in lands: receipt log, payout, EXP, sfx, dialog closes.
    private void ApplyQuestCompleted(CharaSlot chara, int quest)
    {
        var q = _quests[quest];
        var rewards = q.RewardLines(id => _items[id].Name);
        LogEvent(rewards.Count > 0 ? $"completed: {q.Name} ({string.Join(", ", rewards)})" : $"completed: {q.Name}");
        GainExp(_questLog.TurnIn(chara, quest)); // payout lands before the EXP, like before
        PlaySfx("questclear");
        UntrackQuest(quest);
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
        if (_questSel == quest) _questSel = 0; // turned in: drop the stale selection
    }

    // A server-pushed kill counter: set, never bump (the server owns the count).
    private void ApplyQuestKill(QuestEvent q)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions have no one to count for
        chara.QuestKills[$"{q.Quest}:{q.Index}"] = q.Count;
    }

    // A confirmed intent: run the act the server already passed (a local
    // failure means desync: the dialog stays open like a failed click).
    private void ApplyQuestOk(QuestEvent q)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        if (q.Abandoned)
        {
            if (_questLog.Abandon(chara, q.Quest)) ApplyQuestAbandoned(q.Quest);
        }
        else if (q.Completed)
        {
            if (_questLog.Complete(chara, q.Quest)) ApplyQuestCompleted(chara, q.Quest);
        }
        else if (_questLog.Accept(chara, q.Quest)) ApplyQuestAccepted(q.Quest);
    }

    // The quests the player registered, in registration order. Only accepted,
    // known quests are listed: a completed or abandoned row drops out even if
    // an old save still holds its id.
    private List<QuestHelperRow> QuestHelperRows()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return new List<QuestHelperRow>();
        var rows = new List<QuestHelperRow>();
        foreach (int id in chara.QuestHelper)
        {
            if (!chara.Quests.Contains(id) || !_quests.TryGetValue(id, out var q)) continue;
            var st = _questLog.State(id); // taken + known: the state exists
            string detail;
            if (q.NeedItemsToComplete)
                detail = "Need items"; // v1 has no bag: say so instead of looping Talk-to
            else if (st.IsComplete(chara))
                detail = "Ready to complete";
            else
            {
                int i = 0; // first unmet kill (zero kills without items is complete above)
                while (i < q.Kills.Count && st.KillCount(chara, i) >= q.Kills[i].Count) i++;
                detail = $"{MobName(q.Kills[i].Mob)} {st.KillCount(chara, i)}/{q.Kills[i].Count}";
            }
            rows.Add(new QuestHelperRow(id, q.Name, detail));
        }
        return rows;
    }

    // Is this accepted quest shown in the helper?
    private bool QuestTracked(int quest)
    {
        var chara = _save?.Slots[_slot];
        return chara != null && chara.Quests.Contains(quest) && chara.QuestHelper.Contains(quest);
    }

    // The Add to Notifier checkbox: add an accepted quest or take it out.
    private void ToggleQuestHelper(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !chara.Quests.Contains(quest)) return;
        if (chara.QuestHelper.Remove(quest)) return;
        chara.QuestHelper.Add(quest);
        _qhHidden = false; // the player asked for a row: bring a hidden bar back
    }

    // A completed or abandoned quest has nothing left to watch.
    private void UntrackQuest(int quest)
    {
        _save?.Slots[_slot]?.QuestHelper.Remove(quest);
    }

    // The quest window Start button: ask before the teleport. The player then
    // walks up to the NPC in the talk window and takes the quest there.
    private void AskQuestStart(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !_questLog.CanAccept(chara, quest)) return;
        _questConfirmQuest = quest;
        _questConfirm = QuestConfirmKind.Start;
    }

    // The quest window Forfeit button: ask again. The yes runs the same
    // abandon path as the old button.
    private void AskQuestForfeit(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !chara.Quests.Contains(quest)) return;
        _questConfirmQuest = quest;
        _questConfirm = QuestConfirmKind.Forfeit;
    }

    private void CloseQuestConfirm()
    {
        _questConfirm = QuestConfirmKind.None;
        _questConfirmQuest = 0;
    }

    // Yes on the popup: the start travels next to the giver and opens its
    // offer; the forfeit runs the abandon act and closes.
    private void ConfirmQuest()
    {
        int quest = _questConfirmQuest;
        var kind = _questConfirm;
        CloseQuestConfirm();
        if (kind == QuestConfirmKind.Forfeit) AbandonQuest(quest);
        else if (kind == QuestConfirmKind.Start) TravelToQuestNpc(quest);
    }

    // Travel to the quest giver's map, stand beside it, and open the offer in
    // the NPC talk window. The player still has to click Accept there.
    private void TravelToQuestNpc(int quest)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !_quests.TryGetValue(quest, out var q) || !_questLog.CanAccept(chara, quest)) return;
        foreach (var map in MapUi.Data.Maps)
        {
            var file = WorldMapUi.MapFile(map.Id);
            var spawn = file.Npcs.FirstOrDefault(n => n.NpcId == q.Npc0);
            if (spawn == null) continue;
            PlaySfx("portal");
            _showQuest = false; // the talk modal takes the screen
            _dragQuest = null;
            if (map.Id != _mapId) EnterMap(map.Id); // same map: only the player moves
            int x = spawn.X - 30;
            _mover.X = x;
            _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, x, spawn.Cy);
            if (_mover.Ground == null)
            {
                x = spawn.X + 30;
                _mover.X = x;
                _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, x, spawn.Cy);
            }
            if (_mover.Ground == null) throw new InvalidDataException($"quest {quest} has no ground beside NPC {q.Npc0}");
            _mover.Y = SceneMath.FootholdY(_mover.Ground, x);
            _mover.OnTravel();
            _mover.FacingRight = spawn.X > x;
            _mover.Action = "stand1";
            _frame = 0; _timerMs = 0; _shownAction = "stand1";
            _fighter.AttackLock = 0;
            ReseedPortalState();
            SnapCamera();
            OpenTalk(q.Npc0);
            _talkQuest = quest;
            _talkPage = 0;
            return;
        }
        throw new InvalidDataException($"quest {quest} giver {q.Npc0} is not on any exported map");
    }
}
