using System.Diagnostics;
using WzComparerR2.WzLib;

// W1 smoke test: can WzLib open this KMST 1205+ client at all?
// Entry files are stubs (Base.wz is 392 bytes); real data lives in Base_000.wz.. chunks.
// Usage: dotnet run --project MushTown.Exporter -- [DataDir]
const string defaultDataDir = @"E:\游戏\maplestory\appdata\Data";
string dataDir = args.Length > 0 ? args[0] : defaultDataDir;

// Sound is skipped: 69 chunk files, 8.2 GB, not needed until the audio milestone.
string[] folders =
{
    "Base", "Character", "Map", "Mob", "Npc", "Skill", "Item",
    "Quest", "String", "UI", "Effect", "Etc", "Reactor", "Morph", "TamingMob",
};

Console.WriteLine($"dataDir = {dataDir}");
Console.WriteLine();

var loaded = new Dictionary<string, Wz_Structure>();

foreach (string name in folders)
{
    string entry = Path.Combine(dataDir, name, name + ".wz");
    if (!File.Exists(entry))
    {
        Console.WriteLine($"{name,-12} MISSING");
        continue;
    }

    var sw = Stopwatch.StartNew();
    int dirs = 0, imgs = 0;
    void Count(Wz_Node node)
    {
        foreach (Wz_Node child in node.Nodes)
        {
            if (child.Value is Wz_Image) imgs++;
            else dirs++;
            Count(child);
        }
    }

    try
    {
        bool kmst = new Wz_Structure().IsKMST1125WzFormat(entry);
        var structure = new Wz_Structure();
        structure.LoadKMST1125DataWz(entry);

        Count(structure.WzNode);
        loaded[name] = structure;

        Console.WriteLine($"{name,-12} OK   kmst={kmst,-5} top={structure.WzNode.Nodes.Count,4} dirs={dirs,6} imgs={imgs,6} {sw.ElapsedMilliseconds,6} ms");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{name,-12} FAIL {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine();

// Dump top-level names so we can fix probe paths.
if (loaded.TryGetValue("Character", out var chara2))
    Console.WriteLine("[Character] top: " + string.Join(", ", chara2.WzNode.Nodes.Take(20).Select(n => n.Text)));
if (loaded.TryGetValue("Map", out var map2))
    Console.WriteLine("[Map] top: " + string.Join(", ", map2.WzNode.Nodes.Take(20).Select(n => n.Text)));
if (loaded.TryGetValue("Mob", out var mob2))
    Console.WriteLine("[Mob] top: " + string.Join(", ", mob2.WzNode.Nodes.Select(n => n.Text)));

// Locate key files wherever the KMST category folders put them.
Console.WriteLine();
foreach (string target in new[] { "00002000.img", "100000000.img", "0100100.img", "0100000.img" })
{
    foreach (var (folder, st) in loaded)
    {
        var hit = FindByName(st.WzNode, target);
        if (hit != null)
        {
            Console.WriteLine($"[locate] {target,-16} -> {folder}:{hit}");
            break;
        }
    }
}

static string? FindByName(Wz_Node node, string name)
{
    foreach (Wz_Node child in node.Nodes)
    {
        if (child.Text == name) return child.Text;
        var sub = FindByName(child, name);
        if (sub != null) return child.Text + "\\" + sub;
    }
    return null;
}

static Wz_Node? FindNodeByName(Wz_Node node, string name)
{
    foreach (Wz_Node child in node.Nodes)
    {
        if (child.Text == name) return child;
        var sub = FindNodeByName(child, name);
        if (sub != null) return sub;
    }
    return null;
}

static IEnumerable<Wz_Node> FindAllByName(Wz_Node node, string name)
{
    foreach (Wz_Node child in node.Nodes)
    {
        if (child.Text == name) yield return child;
        foreach (var sub in FindAllByName(child, name)) yield return sub;
    }
}

Console.WriteLine();

// Probe 1: character body -> action list + one real png (proves image pixel decryption).
if (loaded.TryGetValue("Base", out var baseWz))
{
    var zmap = baseWz.WzNode.FindNodeByPath("zmap.img", true);
    if (zmap != null)
    {
        string[] z = zmap.Nodes.Select(n => n.Text).ToArray();
        Console.WriteLine($"[Base] zmap.img entries = {z.Length}: {string.Join(", ", z.Take(10))} ...");
    }
    else Console.WriteLine("[Base] zmap.img NOT FOUND");

    var body = FindNodeByName(baseWz.WzNode, "00002000.img");
    if (body?.Value is Wz_Image bodyImg && bodyImg.TryExtract())
    {
        string[] actions = bodyImg.Node.Nodes.Where(n => n.Text != "info").Select(n => n.Text).ToArray();
        Console.WriteLine($"[Base] 00002000.img actions = {actions.Length}: {string.Join(", ", actions.Take(15))} ...");

        var pngNode = FindFirstPng(bodyImg.Node);
        if (pngNode?.Value is Wz_Png png)
        {
            byte[] raw = png.GetRawData();
            Console.WriteLine($"[Base] first png node={pngNode.Text} size={png.Width}x{png.Height} format={png.Format} scale={png.Scale} pages={png.Pages} rawBytes={raw.Length}");
        }
        else Console.WriteLine("[Base] no png found under body image");
    }
    else Console.WriteLine("[Base] 00002000.img extract FAILED");

    // Probe 2: a real map image (several nodes share a map id: _Canvas ones are minimaps).
    var mapCandidates = FindAllByName(baseWz.WzNode, "100000000.img").ToList();
    Console.WriteLine($"[Base] 100000000.img candidates = {mapCandidates.Count}");
    Wz_Image? realMap = null;
    foreach (var cand in mapCandidates)
    {
        if (cand.Value is Wz_Image ci && ci.TryExtract())
        {
            string[] keys = ci.Node.Nodes.Select(n => n.Text).ToArray();
            Console.WriteLine($"   candidate path={cand.FullPath} sections=[{string.Join(",", keys.Take(20))}]");
            if (keys.Contains("info") || keys.Contains("foothold")) realMap = ci;
        }
    }
    if (realMap != null)
    {
        Console.WriteLine($"[Base] REAL map 100000000 sections = {realMap.Node.Nodes.Count}: {string.Join(", ", realMap.Node.Nodes.Select(n => n.Text))}");
        Console.WriteLine($"[Base] REAL map info/VRLeft exists = {realMap.Node.FindNodeByPath(@"info\VRLeft", false) != null}");
    }

    // Probe 3: one mob image.
    var mob = FindNodeByName(baseWz.WzNode, "0100100.img");
    if (mob?.Value is Wz_Image mobImg && mobImg.TryExtract())
    {
        Console.WriteLine($"[Base] 0100100.img children = {mobImg.Node.Nodes.Count}: {string.Join(", ", mobImg.Node.Nodes.Select(n => n.Text).Take(12))}");
    }
    else Console.WriteLine("[Base] 0100100.img NOT FOUND or extract failed");
}

static Wz_Node? FindFirstPng(Wz_Node node)
{
    foreach (Wz_Node child in node.Nodes)
    {
        if (child.Value is Wz_Png) return child;
        var found = FindFirstPng(child);
        if (found != null) return found;
    }
    return null;
}
