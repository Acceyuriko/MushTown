using System.Text.RegularExpressions;

namespace MushTown.Shared;

// Exported NPC file: assets/npc/{id}.png + assets/npc/{id}.json. The art is
// the stand frame (Npc/_Canvas via _outlink; the loose node is a 1x1 stub),
// Origin anchors it like mob art (feet). Greet is the String d0 line.
public sealed class NpcData
{
    public int NpcId { get; set; }
    public string Name { get; set; } = "";
    public string Greet { get; set; } = "";
    public int OriginX { get; set; }
    public int OriginY { get; set; }
}

// Exported quest file: assets/quest/{id}.json (official, by id list) or
// quests/{id}.json (custom, same shape, wins on clash). Kill objectives only
// in v1 (no ETC bag yet, so collect quests cannot complete); scripts and
// options (#L) are out too. Rewards pay EXP, mesos, and USE potions.
public sealed class QuestData
{
    public int QuestId { get; set; }
    public string Name { get; set; } = "";
    public int Npc0 { get; set; } // giver (Check/0/npc; v1 needs one, 0 fails validation)
    public int Npc1 { get; set; } // turn-in (Check/1/npc; v1 needs one, 0 fails validation)
    public int LvMin { get; set; }
    public int LvMax { get; set; } // 0 = no cap
    public List<int> Pre { get; set; } = new(); // required done quests (Check quest ids)
    public string Offer { get; set; } = ""; // QuestInfo/0
    public string Progress { get; set; } = ""; // QuestInfo/1
    public string Done { get; set; } = ""; // QuestInfo/2
    public List<string> Say0 { get; set; } = new(); // accept pages (Say/0/0 + yes/*)
    public List<string> Say1 { get; set; } = new(); // turn-in pages (Say/1/0 + yes/*)
    public string Stop { get; set; } = ""; // Say/1/stop/0 or stop/mob/0 (not done yet)
    public List<QuestKill> Kills { get; set; } = new(); // Check/1/mob, key order; v1 takes at most 9 (detail block + single-digit #a order)
    public bool NeedItemsToComplete { get; set; } // Check/1/item non-empty: v1 cannot verify, never completes
    public bool NeedItemsToAccept { get; set; } // Check/0/item non-empty: v1 cannot accept
    public int RewardExp { get; set; } // Act/1/exp (TalkAction grants it through GainExp, like kills)
    public int RewardMeso { get; set; } // Act/1/meso (GrantRewards drops it in the purse)
    public List<QuestReward> RewardItems { get; set; } = new(); // Act/1/item (potions only in v1: no ETC bag yet)
    // Turn-in preview + log receipt: one short line per reward, no prefix
    // (the talk page heads them with "Reward:", the log wraps them in parens).
    public List<string> RewardLines()
    {
        var lines = new List<string>();
        if (RewardExp > 0) lines.Add($"{RewardExp} EXP");
        if (RewardMeso > 0) lines.Add($"{RewardMeso} mesos");
        foreach (var r in RewardItems) lines.Add($"{ItemData.Name(r.Item)} x{r.Count}");
        return lines;
    }
}

public sealed class QuestReward
{
    public int Item { get; set; }
    public int Count { get; set; }
}

public sealed class QuestKill
{
    public int Mob { get; set; }
    public int Count { get; set; }
}

// Client-side quest state machine (LAN trusts the client, like MP/CD: no
// protocol). Progress key "qid:index" counts capped kills per objective.
public static class QuestLog
{
    // Talk reach: clicking an NPC past this box does nothing.
    public const int TalkDx = 90;
    public const int TalkDy = 90;

    public static bool CanAccept(CharaSlot chara, QuestData q) =>
        !q.NeedItemsToAccept
        && chara.Level >= q.LvMin
        && (q.LvMax == 0 || chara.Level <= q.LvMax)
        && !chara.Quests.Contains(q.QuestId)
        && !chara.QuestsDone.Contains(q.QuestId)
        && q.Pre.All(p => chara.QuestsDone.Contains(p));

    public static bool Accept(CharaSlot chara, QuestData q)
    {
        if (!CanAccept(chara, q)) return false;
        chara.Quests.Add(q.QuestId);
        return true;
    }

    // One of our kills: every accepted quest hunting this mob ticks up (capped).
    public static void BumpKills(CharaSlot chara, IEnumerable<QuestData> quests, int mob)
    {
        foreach (var q in quests)
        {
            if (!chara.Quests.Contains(q.QuestId)) continue;
            for (int i = 0; i < q.Kills.Count; i++)
            {
                if (q.Kills[i].Mob != mob) continue;
                string key = $"{q.QuestId}:{i}";
                chara.QuestKills.TryGetValue(key, out int n);
                chara.QuestKills[key] = Math.Min(q.Kills[i].Count, n + 1);
            }
        }
    }

    public static int KillCount(CharaSlot chara, int quest, int index) =>
        chara.QuestKills.TryGetValue($"{quest}:{index}", out int n) ? n : 0;

    public static bool IsComplete(CharaSlot chara, QuestData q)
    {
        if (!chara.Quests.Contains(q.QuestId) || q.NeedItemsToComplete) return false;
        for (int i = 0; i < q.Kills.Count; i++)
            if (KillCount(chara, q.QuestId, i) < q.Kills[i].Count) return false;
        return true;
    }

    // Turn-in (moves the state only; TalkAction pays out after this passes).
    // Counters go with the hunt: nothing reads them once the quest leaves
    // the log.
    public static bool Complete(CharaSlot chara, QuestData q)
    {
        if (!IsComplete(chara, q)) return false;
        chara.Quests.Remove(q.QuestId);
        chara.QuestsDone.Add(q.QuestId);
        for (int i = 0; i < q.Kills.Count; i++)
            chara.QuestKills.Remove($"{q.QuestId}:{i}");
        return true;
    }

    // Turn-in payout: mesos drop in the purse, potions in the USE bag
    // (pre-W10 saves have no bag yet: build it). EXP is not paid here:
    // TalkAction routes it through GainExp like kills, so level-ups log and
    // push the build on the same path.
    public static void GrantRewards(CharaSlot chara, QuestData q)
    {
        chara.Mesos += q.RewardMeso;
        if (q.RewardItems.Count == 0) return;
        chara.Use ??= new Dictionary<int, int>();
        foreach (var r in q.RewardItems)
        {
            chara.Use.TryGetValue(r.Item, out int n);
            chara.Use[r.Item] = n + r.Count;
        }
    }
}

// Quest text renderer: official strings carry #markup#. Formats are bare
// (#b/#e/#r open, #n/#k reset); h/p/m/t/o/i/c take #x<arg>:# form. Codes:
// h (hero name), p (npc name), m/t (map/item id stubs: no name tables in
// v1), o (mob name), i (item icon, dropped), a<qid><ord> (live kill counter), L (option, dropped, v1 shows no
// option buttons), c (item count, 0: no bag yet), questorder (dropped).
// Passes are disjoint by code letter, so their order is free.
public static class QuestText
{
    public static string Render(string raw, CharaSlot chara, Func<int, string> mobName, Func<int, string> npcName)
    {
        string s = raw.Replace("\r\n", "\n");
        s = Regex.Replace(s, @"#L\d+#", "");
        s = s.Replace("#l#", "").Replace("#l", ""); // option close, with or without #
        s = Regex.Replace(s, @"#questorder\d*#", "");
        s = Regex.Replace(s, @"#a(\d+)(\d)#", m =>
        {
            // greedy qid takes all but the last digit (order): #a10161# = quest
            // 1016 order 1. Orders run 1..n in list order, so order-1 is the index.
            // A malformed qid counts as quest 0 (no such key: renders 0, never throws).
            int qid = int.TryParse(m.Groups[1].Value, out int q) ? q : 0;
            int ord = int.Parse(m.Groups[2].Value);
            return $"{QuestLog.KillCount(chara, qid, ord - 1)}";
        });
        s = Regex.Replace(s, @"#[bernk]", "");
        s = Regex.Replace(s, @"#([hpmtioc])([^#:]*):?#", m =>
        {
            char code = m.Groups[1].Value[0];
            string arg = m.Groups[2].Value;
            return code switch
            {
                'h' => chara.Name,
                'p' => int.TryParse(arg, out int npc) ? npcName(npc) : $"NPC {arg}",
                'm' => $"map {arg}",
                't' => $"item {arg}",
                'i' => "",
                'o' => int.TryParse(arg, out int mob) ? mobName(mob) : $"mob {arg}",
                'c' => "0",
                _ => "",
            };
        });
        return s;
    }
}
