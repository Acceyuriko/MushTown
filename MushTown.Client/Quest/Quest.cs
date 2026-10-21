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
    private int _questSel; // selected quest in the window (0 = none)
    private string _talkKey = "", _questKey = "", _trackKey = "";
    private List<string> _talkLines = new(), _questLines = new(), _trackLines = new();

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
            if (q.Pre.Any(p => p == 0))
                throw new InvalidDataException($"custom quest {f} has a zero pre id");
            if (q.Kills.Any(k => k.Mob == 0 || k.Count == 0))
                throw new InvalidDataException($"custom quest {f} has a zero kill");
            if (q.Kills.Count > 9)
                throw new InvalidDataException($"custom quest {f} has {q.Kills.Count} kills (v1 takes 9)");
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

    // Quest content pass: NPC stand textures (json already loaded in Initialize).
    private void LoadNpcTextures()
    {
        foreach (int id in _npcs.Keys)
            LoadTexture($"npc/{id}.png");
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

    private string NpcName(int id) => _npcs.TryGetValue(id, out var n) ? n.Name : $"NPC {id}";

    private string MobName(int id)
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

    private string QText(string raw)
    {
        var chara = _save?.Slots[_slot] ?? new CharaSlot { Name = "hero" };
        return QuestText.Render(raw, chara, (qid, idx) => _questLog.KillCount(chara, qid, idx), MobName, NpcName);
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

    // Gold "!" over quest-relevant NPCs (an offer, or a ready turn-in).
    private bool NpcHasWork(int npc)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false;
        return _quests.Values.Any(q =>
            (q.Npc0 == npc && _questLog.CanAccept(chara, q.QuestId))
            || (q.Npc1 == npc && _questLog.IsComplete(chara, q.QuestId)));
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
            return new List<string> { npc.Greet != "" ? npc.Greet : npc.Name };
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
    // kills (window + tracker show live state), talk stands by the NPC and
    // opens the menu. Missing quest/NPC/ground fails loud, like other poses.
    private void SetupShotQuests()
    {
        if (_shotScreen is not ("quest" or "talk" or "shop")) return;
        _save ??= new SaveData();
        var chara = _save.Slots[_slot] ??= DemoChara();
        if (_shotScreen == "quest")
        {
            if (!_quests.TryGetValue(90001, out var q) || !_questLog.Accept(chara, 90001))
                throw new InvalidDataException("quest shot needs custom quest 90001");
            if (q.Kills.Count == 0)
                throw new InvalidDataException("quest shot needs custom quest 90001 with kills");
            for (int i = 0; i < 2; i++)
                _questLog.BumpKills(chara, q.Kills[0].Mob); // 2 kills through the real path
            _questSel = 90001;
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
        _topPanel = 5;
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
        var chara = _save?.Slots[_slot]!;
        if (button == "Accept" && _questLog.CanAccept(chara, _talkQuest))
        {
            if (QuestServer()) _net!.SendQuestAccept(_talkQuest);
            else if (_questLog.Accept(chara, _talkQuest)) ApplyQuestAccepted(_talkQuest);
        }
        if (button == "Complete" && _questLog.IsComplete(chara, _talkQuest))
        {
            if (QuestServer()) _net!.SendQuestComplete(_talkQuest);
            else if (_questLog.Complete(chara, _talkQuest)) ApplyQuestCompleted(chara, _talkQuest);
        }
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
        if (q.Completed)
        {
            if (_questLog.Complete(chara, q.Quest)) ApplyQuestCompleted(chara, q.Quest);
        }
        else if (_questLog.Accept(chara, q.Quest)) ApplyQuestAccepted(q.Quest);
    }

    // Tracker lines (cached: quest state changes rarely, frames do not).
    private List<string> TrackerLines()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || chara.Quests.Count == 0) return new List<string>();
        string key = string.Join(";", chara.Quests.OrderBy(id => id)
            .Select(id => _quests.TryGetValue(id, out var q)
                ? id + ":" + string.Join(",", q.Kills.Select((_, i) => _questLog.KillCount(chara, id, i)))
                : id + "?"));
        if (key == _trackKey) return _trackLines;
        _trackKey = key;
        var lines = new List<string>();
        foreach (int id in chara.Quests.OrderBy(x => x))
        {
            if (!_quests.TryGetValue(id, out var q)) continue;
            lines.Add(q.Name);
            var st = _questLog.State(id); // taken + known: the state exists
            if (q.NeedItemsToComplete)
                lines.Add("  Need items"); // v1 has no bag: say so instead of looping Talk-to
            else if (st.IsComplete(chara))
                lines.Add($"  Done - talk to {NpcName(q.Npc1)}");
            else if (q.Kills.Count > 0)
            {
                int i = 0; // first unmet kill (some unmet: complete/item cases returned above)
                while (i < q.Kills.Count && st.KillCount(chara, i) >= q.Kills[i].Count) i++;
                lines.Add($"  {MobName(q.Kills[i].Mob)} {st.KillCount(chara, i)}/{q.Kills[i].Count}");
            }
            else
                lines.Add($"  Talk to {NpcName(q.Npc1)}");
        }
        _trackLines = lines;
        return lines;
    }
}
