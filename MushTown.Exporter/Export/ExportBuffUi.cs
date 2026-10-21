using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The buff bar art (UI/StatusBar3.img/BuffSetting/favoriteBuff): the nine
// piece frame (four corners, four edges and the center) that wraps the buff
// icons. The art is saved as it is. The file names must match the client
// (MushTown.Client/Panels/HudBuff.cs).
public static partial class Export
{
    public static bool ExportBuffUi(WzRoot ui, string outDir)
    {
        Directory.CreateDirectory(Path.Combine(outDir, "ui"));
        var files = new List<(string node, string file)>
        {
            ("StatusBar3.img/BuffSetting/favoriteBuff/nw", "buff_frame_nw.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/n", "buff_frame_n.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/ne", "buff_frame_ne.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/sw", "buff_frame_sw.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/s", "buff_frame_s.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/se", "buff_frame_se.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/w", "buff_frame_w.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/c", "buff_frame_c.png"),
            ("StatusBar3.img/BuffSetting/favoriteBuff/e", "buff_frame_e.png"),
        };
        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        Console.WriteLine($"buff ui: {files.Count} files");
        return true;
    }
}
