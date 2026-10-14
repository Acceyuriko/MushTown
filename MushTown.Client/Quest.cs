using MushTown.Shared;

namespace MushTown.Client;

// Quest state and talk flow (client-side: LAN trusts the client, like
// MP/CD, so quests need no protocol). Draw code lives in Hud.cs.
public partial class MushTownGame
{
    private readonly Dictionary<int, NpcData> _npcs = new();
    private readonly Dictionary<int, QuestData> _quests = new();
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
            if (!Directory.Exists(dir))
                throw new FileNotFoundException(
                    $"missing exported dir: {Path.GetFullPath(dir)} (run: dotnet run --project MushTown.Exporter -- export)", dir);
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
        // Custom quests/ (same shape, wins on clash, so W15 can fix data).
        string custom = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "quests");
        if (Directory.Exists(custom))
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
                var bad = q.RewardItems.FirstOrDefault(r => !ItemData.PotionIds.Contains(r.Item) || r.Count <= 0);
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

    // Static per map: draw/click read _map.Npcs direct (NPCs never move, so no
    // view copy). Talk ends with the map; missing art fails loud, not at draw.
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
        var chara = _account?.Slots[_slot];
        return QuestText.Render(raw, chara ?? new CharaSlot { Name = "hero" }, MobName, NpcName);
    }

    // One of our kills: quest counters tick up (capped per objective).
    private void BumpQuestKills(int mob)
    {
        var chara = _account?.Slots[_slot];
        if (chara != null) QuestLog.BumpKills(chara, _quests.Values, mob);
    }

    // Talk menu entries: (quest id, offer?) for this NPC, offers first.
    private List<(int quest, bool offer)> TalkEntries(int npc)
    {
        var chara = _account?.Slots[_slot];
        var entries = new List<(int, bool)>();
        if (chara == null) return entries;
        var all = _quests.Values.OrderBy(q => q.QuestId).ToList();
        foreach (var q in all)
            if (q.Npc0 == npc && QuestLog.CanAccept(chara, q)) entries.Add((q.QuestId, true));
        foreach (var q in all)
            if (q.Npc1 == npc && chara.Quests.Contains(q.QuestId)) entries.Add((q.QuestId, false));
        return entries;
    }

    // Gold "!" over quest-relevant NPCs (an offer, or a ready turn-in).
    private bool NpcHasWork(int npc)
    {
        var chara = _account?.Slots[_slot];
        if (chara == null) return false;
        return _quests.Values.Any(q =>
            (q.Npc0 == npc && QuestLog.CanAccept(chara, q))
            || (q.Npc1 == npc && QuestLog.IsComplete(chara, q)));
    }

    // Current talk pages: menu shows the greeting, offer/turn-in show Say
    // (scripted quests export empty Say: summaries fill in).
    private List<string> TalkPages()
    {
        var chara = _account?.Slots[_slot];
        var q = _quests.GetValueOrDefault(_talkQuest);
        if (q == null || chara == null)
        {
            var npc = _npcs[_talkNpc];
            return new List<string> { npc.Greet != "" ? npc.Greet : npc.Name };
        }
        bool offer = !chara.Quests.Contains(q.QuestId);
        if (offer) return q.Say0.Count > 0 ? q.Say0 : new List<string> { q.Offer };
        if (QuestLog.IsComplete(chara, q))
        {
            // Turn-in ends with the payout preview (a copy: Say1 is live data).
            var pages = new List<string>(q.Say1.Count > 0 ? q.Say1 : new List<string> { q.Done });
            var rewards = q.RewardLines();
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
        _account ??= new Account { User = "demo" };
        var chara = _account.Slots[_slot] ??= DemoChara();
        if (_shotScreen == "quest")
        {
            if (!_quests.TryGetValue(90001, out var q) || !QuestLog.Accept(chara, q))
                throw new InvalidDataException("quest shot needs custom quest 90001");
            if (q.Kills.Count == 0)
                throw new InvalidDataException("quest shot needs custom quest 90001 with kills");
            for (int i = 0; i < 2; i++)
                QuestLog.BumpKills(chara, new[] { q }, q.Kills[0].Mob); // 2 kills through the real path
            _questSel = 90001;
            return;
        }
        var npc = _map.Npcs.FirstOrDefault(n => n.NpcId == _shotTalk)
            ?? throw new InvalidDataException($"--talk needs NPC {_shotTalk} on map {_mapId}");
        _px = npc.X - 30;
        _ground = SceneMath.ArrivalGround(_map.Footholds, (int)_px, npc.Cy)
            ?? throw new InvalidDataException($"--talk found no ground near NPC {_shotTalk}");
        _py = SceneMath.FootholdY(_ground, (int)_px);
        ResetMotion();
        _facingRight = true;
        _action = "stand2";
        _frame = 0;
        _talkNpc = npc.NpcId;
        _talkQuest = 0;
        _talkPage = 0;
        if (_shotScreen == "shop") // the same call the talk entry makes (non-keepers fail loud)
        {
            if (!NpcHasShop(npc.NpcId))
                throw new InvalidDataException($"NPC {_shotTalk} keeps no shop");
            OpenShop();
        }
    }

    // v1's only shopkeeper: Pio the Alchemist (the 2nd shop gets a data flag).
    private static bool NpcHasShop(int npc) => npc == 10000;

    // Talk entry -> shop panel: talk closes, the shop opens on top.
    private void OpenShop()
    {
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
        _showShop = true;
        _topPanel = 5;
    }

    // Last-page buttons: Accept/Complete close with a log line, Next flips.
    // The buttons exist only with a quest + chara (TalkButtons), so both are
    // set; a failed accept/complete keeps the dialog open (cannot happen:
    // the modal freezes quest state, but closing on failure would strand it).
    private void TalkAction(string button)
    {
        if (button == "Bye") { _talkNpc = 0; _talkQuest = 0; _talkPage = 0; return; }
        if (button == "Next") { _talkPage++; return; }
        var chara = _account?.Slots[_slot]!;
        var q = _quests.GetValueOrDefault(_talkQuest)!;
        if (button == "Accept" && QuestLog.Accept(chara, q))
        {
            GameLog($"accepted: {q.Name}");
            _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
        }
        if (button == "Complete" && QuestLog.Complete(chara, q))
        {
            var rewards = q.RewardLines();
            GameLog(rewards.Count > 0 ? $"completed: {q.Name} ({string.Join(", ", rewards)})" : $"completed: {q.Name}");
            QuestLog.GrantRewards(chara, q);
            GainExp(q.RewardExp);
            PlaySfx("questclear");
            _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
            if (_questSel == q.QuestId) _questSel = 0; // turned in: drop the stale selection
        }
    }

    // Tracker lines (cached: quest state changes rarely, frames do not).
    private List<string> TrackerLines()
    {
        var chara = _account?.Slots[_slot];
        if (chara == null || chara.Quests.Count == 0) return new List<string>();
        string key = string.Join(";", chara.Quests.OrderBy(id => id)
            .Select(id => _quests.TryGetValue(id, out var q)
                ? id + ":" + string.Join(",", q.Kills.Select((_, i) => QuestLog.KillCount(chara, id, i)))
                : id + "?"));
        if (key == _trackKey) return _trackLines;
        _trackKey = key;
        var lines = new List<string>();
        foreach (int id in chara.Quests.OrderBy(x => x))
        {
            if (!_quests.TryGetValue(id, out var q)) continue;
            lines.Add(q.Name);
            if (q.NeedItemsToComplete)
                lines.Add("  Need items"); // v1 has no bag: say so instead of looping Talk-to
            else if (QuestLog.IsComplete(chara, q))
                lines.Add($"  Done - talk to {NpcName(q.Npc1)}");
            else if (q.Kills.Count > 0)
            {
                int i = 0; // first unmet kill (some unmet: complete/item cases returned above)
                while (i < q.Kills.Count && QuestLog.KillCount(chara, id, i) >= q.Kills[i].Count) i++;
                lines.Add($"  {MobName(q.Kills[i].Mob)} {QuestLog.KillCount(chara, id, i)}/{q.Kills[i].Count}");
            }
            else
                lines.Add($"  Talk to {NpcName(q.Npc1)}");
        }
        _trackLines = lines;
        return lines;
    }
}
