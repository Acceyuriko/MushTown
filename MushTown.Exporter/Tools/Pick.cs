using System.Drawing;
using System.Text.Json;
using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Character part picker (dev tool): lists the WZ ids of a part folder with
// their names, and renders a contact sheet of the doll with each hair so the
// right one can be picked before it is baked into the game.
// Usage: pick item <folder> <prefix>          ids + names + art actions
//        pick hair <out.png> <id,id,...>       contact sheet of the doll
public static class Pick
{
    // Ids of every img in a Character folder whose id starts with the prefix,
    // with the String.wz name and the action art the img carries.
    public static bool Items(string folder, string prefix)
    {
        var chara = WzRoot.LoadCategory(ExporterPaths.DataDir, "Character");
        var str = WzRoot.LoadCategory(ExporterPaths.DataDir, "String");
        var node = chara.Find(folder);
        if (node == null)
        {
            Console.WriteLine($"no folder: {folder}");
            return false;
        }
        foreach (Wz_Node img in node.Nodes)
        {
            if (!img.Text.EndsWith(".img")) continue;
            string id = img.Text[..^4];
            if (!id.StartsWith(prefix)) continue;
            string name = str.Find($@"Eqp.img\Eqp\{folder}\{int.Parse(id)}\name")?.GetValueEx<string>("") ?? "";
            Console.WriteLine($"{id} {name} [{string.Join(",", Actions(img))}]");
        }
        return true;
    }

    // The action art an img carries (info and other nodes are not actions).
    private static List<string> Actions(Wz_Node img)
    {
        if (img.Value is Wz_Image image)
        {
            image.TryExtract();
            img = image.Node;
        }
        return img.Nodes.Select(n => n.Text)
            .Where(n => n.StartsWith("stand") || n.StartsWith("walk") || n.StartsWith("swing") || n.StartsWith("stab"))
            .ToList();
    }

    // Contact sheet of the doll: one cell per hair, the id under it.
    public static bool Hair(string outPath, string idList)
    {
        var baseRoot = WzRoot.LoadBase(ExporterPaths.DataDir);
        var chara = WzRoot.LoadCategory(ExporterPaths.DataDir, "Character");
        string tmp = Path.Combine(Path.GetTempPath(), "mush-pick-" + Guid.NewGuid().ToString("N"));
        const int cellW = 150, cellH = 190, cols = 8;
        var cells = new List<Bitmap>();
        try
        {
            var ids = idList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0)
            {
                Console.WriteLine("no hair ids");
                return false;
            }
            foreach (string id in ids)
            {
                string hair = $@"Hair\{id}.img";
                if (!Export.BakeCharacter(baseRoot, chara, tmp, "stand1", hair))
                {
                    Console.WriteLine($"hair {id}: bake failed");
                    return false;
                }
                cells.Add(Cell(tmp, cellW, cellH));
                Console.WriteLine($"hair {id} ok");
            }
            int rows = (cells.Count + cols - 1) / cols;
            using var sheet = new Bitmap(cellW * cols, cellH * rows);
            using var g = Graphics.FromImage(sheet);
            g.Clear(Color.FromArgb(255, 40, 40, 48));
            using var font = new Font("Consolas", 11);
            using var brush = new SolidBrush(Color.White);
            for (int i = 0; i < cells.Count; i++)
            {
                int x = (i % cols) * cellW, y = (i / cols) * cellH;
                g.DrawImage(cells[i], x, y);
                g.DrawString(ids[i], font, brush, x + 4, y + cellH - 20);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            sheet.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"wrote {outPath} ({cells.Count} cells)");
            return true;
        }
        finally
        {
            foreach (var c in cells) c.Dispose();
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        }
    }

    // One doll cell: the baked frame, feet on the cell's bottom line.
    private static Bitmap Cell(string tmp, int cellW, int cellH)
    {
        string json = Path.Combine(tmp, "chara", "body", "stand1.json");
        var data = JsonSerializer.Deserialize<CharaData>(File.ReadAllText(json))!;
        var frame = data.Frames[0];
        var cell = new Bitmap(cellW, cellH);
        using var g = Graphics.FromImage(cell);
        g.Clear(Color.Transparent);
        int feetX = cellW / 2, feetY = cellH - 22;
        foreach (var part in frame.Parts)
        {
            using var art = new Bitmap(Path.Combine(tmp, part.Image));
            g.DrawImage(art, feetX + part.X, feetY + part.Y);
        }
        return cell;
    }
}
