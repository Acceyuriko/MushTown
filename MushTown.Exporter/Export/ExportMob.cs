using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Mob exports: the twin-art table, boss pin, and one mob's art + stats.
public static partial class Export
{
    // Mob table: our life mobs have no own art img, so each borrows its
    // same-named twin (String names verified identical). The numbers are v1
    // feel numbers: the modern client keeps them server-side, no maxHP node
    // exists anywhere in the data (verified in W8 recon). EXP pays for
    // levels 1-10 (the curve needs 1035 in total).
    // Boss pin: the WZ carries no boss life, so v1 pins Mushmom to map
    // 2000000 (main ground y=527 runs -212..1440; the pin stands east of the
    // NPCs). Its attack1 art ships but never plays: v1 bosses touch like
    // every other mob.
    private const int BossMushmom = 6130101;
    private const int BossMap = 2000000, BossX = 1300, BossY = 527;

    // Touch/speed/aggro (W13 feel): touch is flat contact damage, speed is
    // walk px/s (all slower than the player's 140, so everything outruns),
    // aggro is chase range in x px. No attack actions play in v1 (the twins
    // carry none; Mushmom ships one but contact is the attack).
    // The art name is the old (classic) mob id padded to 7 digits: the
    // modern ids 100000-100007 have no art img of their own, the old twins
    // do (Snail 100100, Blue Snail 100101, Spore 120100, Stump 130100,
    // Red Snail 130101, Slime 210100, Pig 1210100, Orange Mushroom 1210102).
    // The hp, exp and def numbers are a ladder by strength, not by id: the
    // Slime (100006) is weaker than the Orange Mushroom (100004). Touch,
    // speed and aggro are feel numbers, not part of that ladder. The official
    // levels live on the server, the WZ has none, so the order is ours.
    // The official training centers are no ladder either (center 1 holds the
    // Stump, center 2 the weaker Red Snail).
    private static readonly Dictionary<int, (string art, int hp, double def, int exp, int touch, int speed, int aggro)> Mobs = new()
    {
        [100000] = ("0100100", 40, 0, 35, 4, 40, 180),      // Snail
        [100001] = ("0100101", 55, 0, 50, 6, 45, 220),      // Blue Snail
        [100002] = ("0130101", 65, 0, 55, 5, 42, 200),      // Red Snail
        [100003] = ("0120100", 70, 0, 65, 8, 35, 200),      // Spore
        [100004] = ("1210102", 110, 0.05, 95, 12, 55, 280), // Orange Mushroom
        [100005] = ("0130100", 120, 0.07, 105, 13, 38, 260), // Stump
        [100006] = ("0210100", 85, 0.04, 75, 9, 40, 240),   // Slime
        [100007] = ("1210100", 150, 0.09, 130, 16, 50, 300), // Pig
        [BossMushmom] = ("6130101", 3000, 0.5, 300, 30, 50, 600), // Mushmom: duo Lv10s chew ~17s through the DEF
    };

    // One mob's art: every action's frames in numeric order (die1 starts at 1,
    // not 0, hence the sort). Frames are bare pngs with no origin or delay,
    // so each gets delay 100 and the client anchors center-bottom.
    private static bool ExportMob(WzRoot mob, WzRoot str, int mobId, string outDir, HashSet<string> saved)
    {
        var img = mob.Find($@"_Canvas\{Mobs[mobId].art}.img");
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
        var data = new MobData
        {
            MobId = mobId,
            Name = mobName,
            MaxHp = Mobs[mobId].hp,
            Def = Mobs[mobId].def,
            Exp = Mobs[mobId].exp,
            Touch = Mobs[mobId].touch,
            Speed = Mobs[mobId].speed,
            Aggro = Mobs[mobId].aggro,
            Boss = mobId == BossMushmom
        };
        foreach (Wz_Node action in img.Nodes)
        {
            if (action.Text == "info") continue;
            var outAction = new MobAction { Name = action.Text };
            var frames = new List<(int key, Wz_Node node)>();
            foreach (Wz_Node f in action.Nodes)
            {
                if (f.Text == "info") continue; // attack metadata (effect), not a frame
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
                if (f.Value is not Wz_Png png)
                {
                    Console.WriteLine($"mob {mobId} {action.Text} frame {f.Text}: not a png");
                    return false;
                }
                string path = $"mob/{mobId}/{action.Text}/{i}.png";
                SaveOnce(png, Path.Combine(outDir, path), saved);
                outAction.Frames.Add(new MobFrame { Image = path, Delay = 100 });
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
        if (!data.Actions.Any(a => a.Name is "stand" or "move"))
        {
            Console.WriteLine($"mob {mobId}: no stand or move action");
            return false;
        }
        Console.WriteLine($"mob {mobId}: " +
            string.Join(", ", data.Actions.Select(a => $"{a.Name} x{a.Frames.Count}")));
        WriteJson(Path.Combine(outDir, "mob", $"{mobId}.json"), data);
        return true;
    }
}
