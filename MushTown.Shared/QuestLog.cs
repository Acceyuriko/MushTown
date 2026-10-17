namespace MushTown.Shared;

// Live quest runtime (probe P1): defs stay data rows, live progress runs
// here. One QuestState per taken quest, spawned by QuestStateFactory.
// Kill counters still live on the save (CharaSlot.QuestKills): states read
// them live, so no save change and no stale copies.
// Authority: LAN still trusts the client (no quest protocol yet); every act
// takes the chara it mutates, so the server can later run the same acts on
// its own copy.

// One taken quest: live view over its def row plus the save. Dumb ctor:
// the factory wires (id, log), all reads go live.
public sealed class QuestState : Component
{
    public int QuestId { get; }
    private readonly QuestLog _log;
    public QuestState(int quest, QuestLog log) { QuestId = quest; _log = log; }
    public QuestData Def => _log.Defs[QuestId];
    public int KillCount(CharaSlot chara, int index) =>
        chara.QuestKills.TryGetValue($"{QuestId}:{index}", out int n) ? n : 0;
    public bool IsComplete(CharaSlot chara)
    {
        var q = Def;
        if (!chara.Quests.Contains(QuestId) || q.NeedItemsToComplete) return false;
        for (int i = 0; i < q.Kills.Count; i++)
            if (KillCount(chara, i) < q.Kills[i].Count) return false;
        return true;
    }
}

// First factory with a registry (probe P1): per-quest builders register by
// id, unknown ids get the default state. Loud fail lives in QuestLog.State.
public sealed class QuestStateFactory
{
    private readonly Dictionary<int, Func<int, QuestLog, QuestState>> _builders = new();
    public void Register(int quest, Func<int, QuestLog, QuestState> build) => _builders[quest] = build;
    public QuestState Build(int quest, QuestLog log) =>
        _builders.TryGetValue(quest, out var b) ? b(quest, log) : new QuestState(quest, log);
}

public sealed class QuestLog : Component
{
    // Talk reach: clicking an NPC past this box does nothing.
    public const int TalkDx = 90;
    public const int TalkDy = 90;

    public IReadOnlyDictionary<int, QuestData> Defs { get; }
    private readonly QuestStateFactory _factory;
    private readonly Dictionary<int, QuestState> _states = new();
    public QuestLog(IReadOnlyDictionary<int, QuestData> defs, QuestStateFactory? factory = null) // dumb ctor
    { Defs = defs; _factory = factory ?? new QuestStateFactory(); }

    // The live state for a taken quest. Missing = desync (Sync not called
    // on chara switch): fail loud with the id, never null down the line.
    public QuestState State(int quest) =>
        _states.TryGetValue(quest, out var s) ? s
        : throw new InvalidDataException($"quest state {quest} missing: Sync the log on chara switch");

    // Rebuilds states from the save: loads and slot switches swap the chara
    // under the global log. During play Accept/Complete keep it in sync.
    // Unknown ids (removed custom file, stale save) spawn nothing, like the
    // quest window filtering them out.
    public void Sync(CharaSlot? chara)
    {
        _states.Clear();
        if (chara == null) return;
        foreach (int q in chara.Quests)
            if (Defs.ContainsKey(q)) _states[q] = _factory.Build(q, this);
    }

    public bool CanAccept(CharaSlot chara, int quest)
    {
        var q = Defs[quest];
        return !q.NeedItemsToAccept
            && chara.Level >= q.LvMin
            && (q.LvMax == 0 || chara.Level <= q.LvMax)
            && !chara.Quests.Contains(q.QuestId)
            && !chara.QuestsDone.Contains(q.QuestId)
            && q.Pre.All(p => chara.QuestsDone.Contains(p));
    }

    public bool Accept(CharaSlot chara, int quest)
    {
        if (!CanAccept(chara, quest)) return false;
        chara.Quests.Add(quest);
        _states[quest] = _factory.Build(quest, this);
        return true;
    }

    // One of our kills: every taken quest hunting this mob ticks up (capped).
    // Reads the save list, not the states: counting stays right even if a
    // read path already failed loud on a desync.
    public void BumpKills(CharaSlot chara, int mob)
    {
        foreach (int qid in chara.Quests)
        {
            if (!Defs.TryGetValue(qid, out var q)) continue;
            for (int i = 0; i < q.Kills.Count; i++)
            {
                if (q.Kills[i].Mob != mob) continue;
                string key = $"{qid}:{i}";
                chara.QuestKills.TryGetValue(key, out int n);
                chara.QuestKills[key] = Math.Min(q.Kills[i].Count, n + 1);
            }
        }
    }

    public int KillCount(CharaSlot chara, int quest, int index) =>
        chara.QuestKills.TryGetValue($"{quest}:{index}", out int n) ? n : 0;

    // False for untaken quests (offer paths check NPCs they never took).
    public bool IsComplete(CharaSlot chara, int quest) =>
        chara.Quests.Contains(quest) && State(quest).IsComplete(chara);

    // Turn-in (moves the state only; the caller pays out after this passes).
    // Counters go with the hunt: nothing reads them once the quest leaves
    // the log.
    public bool Complete(CharaSlot chara, int quest)
    {
        if (!IsComplete(chara, quest)) return false;
        chara.Quests.Remove(quest);
        chara.QuestsDone.Add(quest);
        for (int i = 0; i < Defs[quest].Kills.Count; i++)
            chara.QuestKills.Remove($"{quest}:{i}");
        _states.Remove(quest);
        return true;
    }

    // Turn-in payout: mesos drop in the purse, potions in the USE bag
    // (pre-W10 saves have no bag yet: build it). Returns the EXP grant: the
    // caller routes it through GainExp like kills, so level-ups log and push
    // the build on the same path.
    public int TurnIn(CharaSlot chara, int quest)
    {
        var q = Defs[quest];
        chara.Mesos += q.RewardMeso;
        if (q.RewardItems.Count > 0)
        {
            chara.Use ??= new Dictionary<int, int>();
            foreach (var r in q.RewardItems)
            {
                chara.Use.TryGetValue(r.Item, out int n);
                chara.Use[r.Item] = n + r.Count;
            }
        }
        return q.RewardExp;
    }
}
