namespace MushTown.Exporter;

// The sounds on their own (fast loop): the map list gives the tracks, the
// login theme is fixed.
public static partial class Export
{
    public static bool ExportSounds(WzRoot root, string outDir)
    {
        var bgms = new List<string>();
        foreach (int mapId in MapIds)
        {
            if (!MapNodes(root, mapId, out _, out var info)) return false;
            string bgm = MapBgm(info, mapId);
            if (bgm == "") return false;
            bgms.Add(bgm);
        }
        return SoundExport.Run(root, bgms, outDir);
    }
}
