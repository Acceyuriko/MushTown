using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Mob exports: the twin-art table, boss pin, and one mob's art + stats.
public static partial class Export
{
    // Mob table: our life mobs have no own art img, so each borrows its
    // same-named twin (String names verified identical). The art name is the
    // old (classic) mob id padded to 7 digits: the modern ids 100000-100007 have
    // no art img of their own, the old twins do (Snail 100100, Blue Snail 100101,
    // Spore 120100, Stump 130100, Red Snail 130101, Slime 210100, Pig 1210100,
    // Orange Mushroom 1210102).
    // Every stat except Aggro comes from the mob's info node in the WZ: MaxHp =
    // maxHP, Exp = exp, Def = PDRate/100, Touch = PADamage, Speed = 100 + speed
    // px/s (the WZ speed is a percent; the player walks 140), Boss = boss,
    // Level = level, ElemAttr = elemAttr. Aggro (chase range in x px) has no WZ
    // field, so it stays a v1 feel number.
    // Boss pin: the WZ carries no boss life, so v1 pins Mushmom to map
    // 2000000 (main ground y=527 runs -212..1440; the pin stands east of the
    // NPCs). Its attack1 art ships but never plays: v1 bosses touch like
    // every other mob.
    private const int BossMushmom = 6130101;
    private const int BossMap = 2000000, BossX = 1300, BossY = 527;

    private static readonly Dictionary<int, (string art, int aggro)> Mobs = new()
    {
        [100000] = ("0100100", 180), // Snail
        [100001] = ("0100101", 220), // Blue Snail
        [100002] = ("0130101", 200), // Red Snail
        [100003] = ("0120100", 200), // Spore
        [100004] = ("1210102", 280), // Orange Mushroom
        [100005] = ("0130100", 260), // Stump
        [100006] = ("0210100", 240), // Slime
        [100007] = ("1210100", 300), // Pig
        [BossMushmom] = ("6130101", 600), // Mushmom
    };

    // One mob's art + stats. The structure tree is not in Data/Mob: it lives in
    // Data/Packs/Mob_*.ms, where Mob/<art>.img holds one 1x1 stub per frame with
    // origin and delay, and _outlink points at the real png in Mob/_Canvas.
    // Frames sort by number (die1 starts at 1, not 0). Each frame carries its
    // own origin: the client puts that pixel on the mob's feet (the frames of
    // one action are cropped differently).
    private static bool ExportMob(WzRoot mob, WzRoot str, int mobId, string outDir, HashSet<string> saved)
    {
        var img = mob.Find($@"Mob\{Mobs[mobId].art}.img");
        if (img == null)
        {
            Console.WriteLine($"mob {mobId}: art {Mobs[mobId].art}.img not found");
            return false;
        }
        string mobName = str.Find($@"Mob.img\{mobId}\name")?.GetValueEx<string>("") ?? "";
        if (mobName == "")
        {
            Console.WriteLine($"mob {mobId} has no String name");
            return false;
        }
        var info = img.Nodes["info"];
        if (info == null)
        {
            Console.WriteLine($"mob {mobId}: no info node");
            return false;
        }
        int maxHp = Int(info, "maxHP", 0);
        if (maxHp <= 0)
        {
            Console.WriteLine($"mob {mobId}: bad maxHP");
            return false;
        }
        var data = new MobData
        {
            MobId = mobId,
            Name = mobName,
            Level = Int(info, "level", 0),
            ElemAttr = info.Nodes["elemAttr"]?.GetValueEx<string>("") ?? "",
            MaxHp = maxHp,
            Def = Int(info, "PDRate", 0) / 100.0,
            Exp = Int(info, "exp", 0),
            Touch = Int(info, "PADamage", 0),
            Speed = 100 + Int(info, "speed", 0),
            Aggro = Mobs[mobId].aggro,
            Boss = Int(info, "boss", 0) != 0
        };
        foreach (Wz_Node action in img.Nodes)
        {
            if (action.Text == "info") continue;
            var outAction = new MobAction { Name = action.Text };
            var frames = new List<(int key, Wz_Node node)>();
            foreach (Wz_Node f in action.Nodes)
            {
                if (f.Text is "info" or "zigzag") continue; // action metadata, not a frame
                if (!int.TryParse(f.Text, out int key))
                {
                    Console.WriteLine($"mob {mobId} {action.Text}: non-frame child {f.Text}");
                    return false;
                }
                frames.Add((key, f));
            }
            int i = 0;
            foreach ((int _, Wz_Node f) in frames.OrderBy(t => t.key))
            {
                // A stub carries origin and delay; a uol frame points at one.
                var frameNode = WzNodeUtil.ResolveUol(f);
                var (png, origin) = WzNodeUtil.ReadCanvas(mob, f);
                if (png == null || origin == null)
                {
                    Console.WriteLine($"mob {mobId} {action.Text} frame {f.Text}: no png or origin");
                    return false;
                }
                string path = $"mob/{mobId}/{action.Text}/{i}.png";
                SaveOnce(png, Path.Combine(outDir, path), saved);
                outAction.Frames.Add(new MobFrame
                {
                    Image = path,
                    Delay = Int(frameNode, "delay", 180),
                    OriginX = origin.X,
                    OriginY = origin.Y,
                });
                i++;
            }
            if (outAction.Frames.Count == 0)
            {
                Console.WriteLine($"mob {mobId} {action.Text}: no frames");
                return false;
            }
            data.Actions.Add(outAction);
        }
        // W8 plays the idle (stand, or move when the mob has no stand, like
        // the blue snail), hit1 (flinch), die1 (death); W12 AI drives move.
        foreach (string need in new[] { "hit1", "die1" })
            if (!data.Actions.Any(a => a.Name == need))
            {
                Console.WriteLine($"mob {mobId}: no {need} action");
                return false;
            }
        if (!data.Actions.Any(a => a.Name == data.Idle))
        {
            Console.WriteLine($"mob {mobId}: no {data.Idle} action");
            return false;
        }
        Console.WriteLine($"mob {mobId}: " +
            string.Join(", ", data.Actions.Select(a => $"{a.Name} x{a.Frames.Count}")));
        WriteJson(Path.Combine(outDir, "mob", $"{mobId}.json"), data);
        return true;
    }
}
