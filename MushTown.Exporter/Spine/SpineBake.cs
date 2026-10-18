using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Bakes ClassSelect Spine rigs to plain per-frame JSON plus atlas pages:
// assets/spine/{id}/{id}.json and assets/spine/{id}/{page}.png. Rigs the reader
// does not cover yet are skipped with a message; the client keeps static
// art for those classes.
public static class SpineBake
{
    public static bool Run(string id)
    {
        var ui = WzRoot.LoadCategory(ExporterPaths.DataDir, "UI");
        if (id == "all")
        {
            bool any = false;
            var back = ui.Find("Login.img/ClassSelect/back/1");
            if (back == null) { Console.WriteLine("node not found: ClassSelect back/1"); return false; }
            foreach (var child in back.Nodes)
                if (TryBakeOne(ui, child.Text, ExporterPaths.OutDir)) any = true;
            return any;
        }
        return TryBakeOne(ui, id, ExporterPaths.OutDir);
    }

    // Full export bakes every parseable rig; unreadable ones stay static
    // art in the client. Each class logs one line, same as spinebake all.
    public static bool BakeAll(WzRoot ui, string outDir)
    {
        var back = ui.Find("Login.img/ClassSelect/back/1");
        if (back == null) { Console.WriteLine("node not found: ClassSelect back/1"); return false; }
        int baked = 0, skipped = 0;
        foreach (var child in back.Nodes)
            if (TryBakeOne(ui, child.Text, outDir)) baked++;
            else skipped++;
        Console.WriteLine($"spine: {baked} classes baked, {skipped} skipped (static art)");
        return true;
    }

    // One class, guarded: rig failures print as skips so the client keeps
    // static art. Disk failures abort like every other export step.
    private static bool TryBakeOne(WzRoot ui, string id, string outDir)
    {
        try { return BakeOne(ui, id, outDir); }
        catch (Exception e) when (e is not (IOException or UnauthorizedAccessException))
        { Console.WriteLine($"{id}: skip ({e.GetType().Name}: {e.Message})"); return false; }
    }

    private static bool BakeOne(WzRoot ui, string id, string rootOutDir)
    {
        if (!int.TryParse(id, out _)) { Console.WriteLine($"{id}: bad id"); return false; }
        string dir = $"Login.img/ClassSelect/back/1/{id}";
        string outDir = Path.Combine(rootOutDir, "spine", id);
        string jsonPath = Path.Combine(outDir, $"{id}.json");
        if (File.Exists(jsonPath)) File.Delete(jsonPath); // skip means static: no stale clip survives a failed bake
        string rig = ui.Find($"{dir}/spine")?.Value as string ?? "skeleton";
        var skelNode = ui.Find($"{dir}/{rig}.skel");
        if (skelNode?.Value is not Wz_RawData raw)
        {
            var jsonNode = ui.Find($"{dir}/{rig}.json");
            Console.WriteLine(jsonNode?.Value is string
                ? $"{id}: {rig}.json rigs are not baked yet"
                : $"{id}: no skeleton");
            return false;
        }
        var atlasNode = ui.Find($"{dir}/atlas");
        if (atlasNode?.Value is not string atlasText) { Console.WriteLine($"{id}: no atlas"); return false; }
        var buf = new byte[raw.Length];
        raw.CopyTo(buf);
        var atlas = Atlas.Parse(atlasText);
        var baked = RigBaker.Bake(buf, atlas);
        Directory.CreateDirectory(outDir);
        foreach (var page in atlas.Pages)
        {
            if (!UiPng.TryRead(ui, "UI", $"{dir}/{page.Name}", out var png, out _))
            {
                Console.WriteLine($"{id}: page {page.Name} missing");
                return false;
            }
            WzNodeUtil.SavePng(png, Path.Combine(outDir, page.Name));
        }
        File.WriteAllText(Path.Combine(outDir, $"{id}.json"), RigBaker.ToJson(baked));
        Console.WriteLine($"{id}: {baked.Slots.Count} slots, {baked.Slots[0].Frames.Count} frames, {atlas.Pages.Count} pages");
        return true;
    }
}
