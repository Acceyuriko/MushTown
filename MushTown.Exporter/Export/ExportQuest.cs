using System.Text.Json;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Quest exports: the quest list, checks, rewards, say pages, and kill goals.
public static partial class Export
{
    // Exported quests: 1016 proves full Say pages (its pre is unexported,
    // so it never offers), 1027 is the W15 chain finale (Mai to Lucas talk),
    // 36301 is scripted with empty Say (the client falls back to summaries).
    private static readonly int[] QuestIds = new[] { 1016, 1027, 36301 };

    private static bool ExportQuests(WzRoot baseRoot, string outDir)
    {
        foreach (int questId in QuestIds)
        {
            var img = baseRoot.Find($@"Quest\QuestData\{questId}.img");
            if (img == null)
            {
                Console.WriteLine($"quest {questId} not found");
                return false;
            }
            var info = img.Nodes["QuestInfo"];
            string name = info?.Nodes["name"]?.GetValueEx<string>("") ?? "";
            if (name == "")
            {
                Console.WriteLine($"quest {questId} has no name");
                return false;
            }
            var check0 = img.Nodes["Check"]?.Nodes["0"];
            var check1 = img.Nodes["Check"]?.Nodes["1"];
            int npc0 = QNum(check0?.Nodes["npc"]), npc1 = QNum(check1?.Nodes["npc"]);
            if (npc0 == 0 || npc1 == 0)
            {
                Console.WriteLine($"quest {questId} has no giver or turn-in (v1 needs both)");
                return false;
            }
            var data = new QuestData
            {
                QuestId = questId,
                Name = name,
                Npc0 = npc0,
                Npc1 = npc1,
                LvMin = QNum(check0?.Nodes["lvmin"]),
                LvMax = QNum(check0?.Nodes["lvmax"]),
                Offer = QStr(info?.Nodes["0"]),
                Progress = QStr(info?.Nodes["1"]),
                Done = QStr(info?.Nodes["2"]),
                NeedItemsToComplete = check1?.Nodes["item"]?.Nodes.Count > 0,
                NeedItemsToAccept = check0?.Nodes["item"]?.Nodes.Count > 0,
                RewardExp = QNum(img.Nodes["Act"]?.Nodes["1"]?.Nodes["exp"]),
                RewardMeso = QNum(img.Nodes["Act"]?.Nodes["1"]?.Nodes["meso"]),
            };
            if (data.RewardExp < 0)
            {
                Console.WriteLine($"quest {questId} has a negative exp reward");
                return false;
            }
            if (data.RewardMeso < 0)
            {
                Console.WriteLine($"quest {questId} has a negative meso reward");
                return false;
            }
            foreach (Wz_Node it in img.Nodes["Act"]?.Nodes["1"]?.Nodes["item"]?.Nodes ?? Enumerable.Empty<Wz_Node>())
            {
                int item = QNum(it.Nodes["id"]), count = QNum(it.Nodes["count"]);
                string defPath = Path.Combine(outDir, "item", $"{item}.json");
                if (!File.Exists(defPath))
                {
                    Console.WriteLine($"quest {questId} rewards an unexported item {item}");
                    return false;
                }
                var def = JsonSerializer.Deserialize<ItemData>(File.ReadAllText(defPath));
                if (def == null || def.Slot != null || count <= 0) // rewards must be consumables (no ETC bag yet)
                {
                    Console.WriteLine($"quest {questId} has a bad item reward (item {item} x{count})");
                    return false;
                }
                data.RewardItems.Add(new QuestReward { Item = item, Count = count });
            }
            foreach (var side in new[] { check0, check1 })
                foreach (Wz_Node pre in side?.Nodes["quest"]?.Nodes ?? Enumerable.Empty<Wz_Node>())
                    if (QNum(pre.Nodes["state"]) == 2)
                    {
                        int preId = QNum(pre.Nodes["id"]);
                        if (preId == 0)
                        {
                            Console.WriteLine($"quest {questId} has a zero pre id");
                            return false;
                        }
                        if (!data.Pre.Contains(preId)) data.Pre.Add(preId); // both sides may list it
                    }
            var say0 = img.Nodes["Say"]?.Nodes["0"];
            var say1 = img.Nodes["Say"]?.Nodes["1"];
            string first0 = QStr(say0?.Nodes["0"]);
            if (first0 != "") data.Say0.Add(first0);
            data.Say0.AddRange(QPages(say0?.Nodes["yes"]));
            string first1 = QStr(say1?.Nodes["0"]);
            if (first1 != "") data.Say1.Add(first1);
            data.Say1.AddRange(QPages(say1?.Nodes["yes"]));
            data.Stop = QStr(say1?.Nodes["stop"]?.Nodes["0"]);
            if (data.Stop == "") data.Stop = QStr(say1?.Nodes["stop"]?.Nodes["mob"]?.Nodes["0"]);
            // Mob keys ("0","1",...) sort numeric like QPages: #a order-1 is the index.
            var mobs = (check1?.Nodes["mob"]?.Nodes ?? Enumerable.Empty<Wz_Node>())
                .OrderBy(m => int.TryParse(m.Text, out int key) ? key : int.MaxValue);
            foreach (Wz_Node m in mobs)
            {
                int mob = QNum(m.Nodes["id"]), count = QNum(m.Nodes["count"]);
                if (mob == 0 || count == 0)
                {
                    Console.WriteLine($"quest {questId} has a zero kill (mob {mob} x{count})");
                    return false;
                }
                data.Kills.Add(new QuestKill { Mob = mob, Count = count });
            }
            if (data.Kills.Count > 9)
            {
                Console.WriteLine($"quest {questId} has {data.Kills.Count} kills (v1 takes 9)");
                return false;
            }
            Console.WriteLine($"quest {questId}: {name} kills={data.Kills.Count} say0={data.Say0.Count} say1={data.Say1.Count}");
            WriteJson(Path.Combine(outDir, "quest", $"{questId}.json"), data);
        }
        return true;
    }

    // Quest numbers read as int or padded string (missing = 0, e.g. no lvmin).
    private static int QNum(Wz_Node? node) => node?.Value switch
    {
        int i => i,
        string s when int.TryParse(s, out int n) => n,
        _ => 0
    };

    private static string QStr(Wz_Node? node) => node?.Value as string ?? "";

    // Numbered string pages in key order (yes/0, yes/1, ...).
    private static List<string> QPages(Wz_Node? node)
    {
        var pages = new List<(int key, string text)>();
        if (node != null)
            foreach (Wz_Node p in node.Nodes)
                if (int.TryParse(p.Text, out int key) && p.Value is string s && s != "")
                    pages.Add((key, s));
        return pages.OrderBy(t => t.key).Select(t => t.text).ToList();
    }
}
