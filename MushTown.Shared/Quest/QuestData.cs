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
    public List<string> RewardLines(Func<int, string> itemName)
    {
        var lines = new List<string>();
        if (RewardExp > 0) lines.Add($"{RewardExp} EXP");
        if (RewardMeso > 0) lines.Add($"{RewardMeso} mesos");
        foreach (var r in RewardItems) lines.Add($"{itemName(r.Item)} x{r.Count}");
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

// Quest text renderer: official strings carry #markup#. Formats are bare
// (#b/#e/#r open, #n/#k reset); h/p/m/t/o/i/c take #x<arg>:# form. Codes:
// h (hero name), p (npc name), m/t (map/item id stubs: no name tables in
// v1), o (mob name), i (item icon, dropped), a<qid><ord> (live kill counter), L (option, dropped, v1 shows no
// option buttons), c (item count, 0: no bag yet), questorder (dropped).
// Passes are disjoint by code letter, so their order is free.
public static class QuestText
{
    public static string Render(string raw, CharaSlot chara, Func<int, int, int> kills, Func<int, string> mobName, Func<int, string> npcName)
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
            return $"{kills(qid, ord - 1)}";
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
